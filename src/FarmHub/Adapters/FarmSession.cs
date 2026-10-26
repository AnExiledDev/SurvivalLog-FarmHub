using System;
using System.Collections.Generic;
using System.Linq;
using FarmHub.Core;
using UnityEngine;

namespace FarmHub.Adapters;

/// <summary>
/// The window's game side, run once a frame: its open state, the view and catalog it sends to page.js, the HUD
/// button's badge, the presses and preset edits the page asks for, and the auto replant pass. Page messages arrive on
/// the web view's thread, so they are queued and handled here, in this frame.
/// </summary>
internal sealed class FarmSession
{
    private const float OpenRefreshSeconds = 1f;
    private const float ClosedRefreshSeconds = 2f;
    private const float AutoSeconds = 5f;
    private const float SowRetrySeconds = 0.5f;
    private const int SowTries = 6;

    private readonly IFarmGame _game;
    private readonly WebPage _page;
    private readonly WebKeyboard _keyboard = new();
    private readonly PresetFileOnDisk _file;
    private readonly Action<int> _saveRotWarn;
    private readonly Action<bool> _saveRobots;
    private readonly Action<bool> _saveFromPresets;
    private readonly Action<string> _log;
    private readonly Dictionary<long, float> _retryAt = new();
    private readonly Dictionary<long, int> _sowTries = new();

    private PresetBook? _book;
    private KeyCode _panelKey;
    private bool _isPanelKeyShifted;
    private int _rotWarnHours = 12;
    private bool _isKeepingRobotsStocked;
    private bool _isTendFromPresets;

    private bool _isOpen;
    private string _shownView = "";
    private FarmCatalog? _sentCatalog;
    private int? _sentBadge;
    private bool? _sentLive;
    private float _nextRefreshAt;
    private float _nextAutoAt;
    private float _nextSowAt;
    private string _sentHud = "";
    private FarmWorld? _lastWorld;

    public FarmSession(IFarmGame game, WebPage page, PresetFileOnDisk file, Action<int> saveRotWarn, Action<bool> saveRobots, Action<bool> saveFromPresets, Action<string> log)
    {
        _game = game;
        _page = page;
        _file = file;
        _saveRotWarn = saveRotWarn;
        _saveRobots = saveRobots;
        _saveFromPresets = saveFromPresets;
        _log = log;
        TendHooks.CanSow = CanSow;
    }

    private static bool IsChinese => GameLanguage.IsChinese();

    /// <summary>Called on the main thread at start and after each settings change; the page hears them at the next refresh.</summary>
    public void UseSettings(KeyCode panelKey, bool isPanelKeyShifted, int rotWarnHours, bool isKeepingRobotsStocked, bool isTendFromPresets)
    {
        _panelKey = panelKey;
        _isPanelKeyShifted = isPanelKeyShifted;
        InputGate.UsePanelKey(panelKey, isPanelKeyShifted);
        _rotWarnHours = rotWarnHours;
        _isKeepingRobotsStocked = isKeepingRobotsStocked;
        _isTendFromPresets = isTendFromPresets;
        TendHooks.IsOn = isTendFromPresets;
        _sentHud = "";
        _sentLive = null;
        _sentBadge = null;
        _shownView = "";
    }

    public void Update()
    {
        var now = Time.unscaledTime;

        _page.KeepInstalled(now);

        while (PageMessages.TryTake(out var command)) HandleSafely(command);

        TakeTendHooks(now);

        if (!_keyboard.IsClaimed && _panelKey != KeyCode.None && Input.GetKeyDown(_panelKey) && InputGate.IsShiftHeld() == _isPanelKeyShifted)
        {
            Toggle();

            return;
        }

        if (_isOpen && !_keyboard.IsClaimed && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();

            return;
        }

        // The farm read and the auto pass each read the whole farm, so they never share a frame.
        if (now >= _nextRefreshAt) Refresh(now);
        else if (now >= _nextAutoAt) RunAuto(now);
    }

    /// <summary>One command that throws must not cost the rest of the frame its commands or the key checks.</summary>
    private void HandleSafely(PageCommand command)
    {
        try
        {
            Handle(command);
        }
        catch (Exception ex)
        {
            _log($"page command {command} failed: {ex}");
            _page.Status(Texts.Failed.Pick(IsChinese));
        }
    }

    private void Handle(PageCommand command)
    {
        switch (command)
        {
            case PageCommand.Toggle:
                Toggle();
                break;
            case PageCommand.Close:
                Close();
                break;
            case PageCommand.Ready:
                _log("page script ready");
                _page.ForgetWords();
                _sentLive = null;
                _sentBadge = null;
                _sentCatalog = null;
                _sentHud = "";
                _shownView = "";
                _nextRefreshAt = 0f;
                break;
            case PageCommand.NoScript:
                _page.Install();
                break;
            case PageCommand.Note note:
                _log("page: " + note.Text);
                break;
            case PageCommand.Focus focus:
                if (focus.IsTyping) _keyboard.Claim();
                else _keyboard.Release();
                break;
            case PageCommand.Plant plant:
                Press("plant", () => FarmRun.Plant(_game, Book(), plant.PotIds));
                break;
            case PageCommand.Fertilize fertilize:
                Press("fertilize", () => FarmRun.Fertilize(_game, Book(), fertilize.PotIds));
                break;
            case PageCommand.Tend tend:
                Press($"tend {tend.PotId}", () => FarmRun.Tend(_game, tend.PotId));
                break;
            case PageCommand.TendAll:
                Press("tend all", () => FarmRun.TendAll(_game, Book()));
                break;
            case PageCommand.RobotTier tier:
                Press($"robot {tier.RobotId} tier {tier.Tier}", () => FarmRun.SetRobotTier(_game, tier.RobotId, tier.Tier, tier.SeedId, tier.FertId));
                break;
            case PageCommand.Assign assign:
                Keep(BookEdits.Assign(Book(), SaveSlot.CurrentKey(), assign, IsChinese));
                break;
            case PageCommand.SavePreset save:
                Keep(BookEdits.SavePreset(Book(), save.Json, IsChinese));
                break;
            case PageCommand.DeletePreset delete:
                Keep(BookEdits.DeletePreset(Book(), delete.PresetId, IsChinese));
                break;
            case PageCommand.RotWarn rotWarn:
                _rotWarnHours = rotWarn.Hours;
                _saveRotWarn(rotWarn.Hours);
                Say(Texts.RotWarnSet.Say(IsChinese, rotWarn.Hours));
                break;
            case PageCommand.Robots robots:
                _isKeepingRobotsStocked = robots.IsKeepingStocked;
                _saveRobots(robots.IsKeepingStocked);
                Say((robots.IsKeepingStocked ? Texts.RobotsOn : Texts.RobotsOff).Pick(IsChinese));
                break;
            case PageCommand.FromPresets fromPresets:
                _isTendFromPresets = fromPresets.IsOn;
                TendHooks.IsOn = fromPresets.IsOn;
                _saveFromPresets(fromPresets.IsOn);
                Say((fromPresets.IsOn ? Texts.FromPresetsOn : Texts.FromPresetsOff).Pick(IsChinese));
                SendHud(_lastWorld);
                break;
            default:
                throw new InvalidOperationException("unhandled page command " + command);
        }
    }

    /// <summary>
    /// The presets are read from farm-hub.json the first time they are needed. A file that is there but is not a preset
    /// file is copied aside before a fresh book replaces it, so nothing anyone wrote is lost.
    /// </summary>
    private PresetBook Book()
    {
        if (_book != null) return _book;

        var text = _file.Read();
        var hasText = text is { } written && !string.IsNullOrWhiteSpace(written);
        var book = text is { } stored && hasText ? PresetJson.ReadBook(stored) : null;

        if (book == null && hasText) _file.SetAside();

        _book = book ?? PresetBook.First(Texts.FirstPresetName.Pick(IsChinese));
        _log($"presets: {_book.Presets.Count}");

        return _book;
    }

    /// <summary>A changed book is used at once and written to disk; a failed write keeps it until the game closes, and says so.</summary>
    private void Keep(BookEdit edit)
    {
        var message = edit.Message;

        if (edit.Book != null)
        {
            _book = edit.Book;

            if (!_file.Replace(PresetJson.WriteBook(edit.Book))) message = Texts.NotSaved.Pick(IsChinese);
        }

        Say(message);
    }

    private void Say(string message)
    {
        _log(message);
        _page.Status(message);
        Redraw();
    }

    /// <summary>
    /// Reads the farm once: the HUD button shows while there is a home to manage, its badge counts pots that need the
    /// player, and an open window is redrawn when its view changed. A read that throws is logged by the game adapter and
    /// read as not ready, so the window stays up with the reason instead of holding the game's input with nothing in it.
    /// </summary>
    private void Refresh(float now)
    {
        _nextRefreshAt = now + (_isOpen ? OpenRefreshSeconds : ClosedRefreshSeconds);

        var read = _game.Read();
        var world = (read as FarmRead.Ready)?.World;

        _lastWorld = world;
        SendLive(world != null);
        SendBadge(world == null ? 0 : FarmView.NeedsYou(world, Book()));
        SendHud(world);

        if (_isOpen) Show(read, isOpening: false);
    }

    private void SendLive(bool isLive)
    {
        if (isLive == _sentLive) return;
        if (_page.Live(isLive, KeyName())) _sentLive = isLive;

        _log($"HUD button {(isLive ? "shown" : "hidden")}");
    }

    /// <summary>The plant panel's additions need the catalog to name seeds, so it goes too, once per game and language.</summary>
    private void SendHud(FarmWorld? world)
    {
        if (world != null) SendCatalog(world.Catalog);

        var hud = world == null ? HudView.NotReady() : HudView.Ready(world, Book(), _rotWarnHours, _isTendFromPresets);

        if (hud == _sentHud) return;
        if (_page.Hud(hud)) _sentHud = hud;
    }

    /// <summary>A replant walk's pot, sown from its preset when the walk is done with it, and the extras after the game's Tend Garden.</summary>
    private void TakeTendHooks(float now)
    {
        foreach (var potId in TendHooks.TakeSows())
        {
            _sowTries[potId] = 0;
            _nextSowAt = now;
        }

        if (TendHooks.TakePatrol())
        {
            var outcome = FarmRun.FromPresets(_game, Book());

            _log($"tend from presets: {outcome}");

            if (FarmMessages.IsWorthShowing(outcome)) Say(FarmMessages.Describe(outcome, IsChinese));
        }

        if (_sowTries.Count > 0 && now >= _nextSowAt) SowQueued(now);
    }

    /// <summary>The walk that reached the pot still holds it for a moment, so a pot the game calls busy is tried again shortly.</summary>
    private void SowQueued(float now)
    {
        _nextSowAt = now + SowRetrySeconds;

        foreach (var potId in _sowTries.Keys.ToList())
        {
            var outcome = FarmRun.Plant(_game, Book(), new[] { potId });
            var tries = _sowTries[potId] + 1;

            if (outcome is FarmOutcome.NotEmpty && tries < SowTries)
            {
                _sowTries[potId] = tries;
                continue;
            }

            _sowTries.Remove(potId);
            _log($"replant {potId} from its preset after {tries} tries: {outcome}");
            Say(FarmMessages.Describe(outcome, IsChinese));
        }
    }

    /// <summary>Tend Garden's replant walk skips the game's plant screen only for a pot its preset has a seed for.</summary>
    private bool CanSow(long potId)
    {
        var world = _lastWorld;
        var pot = world?.Pots.FirstOrDefault(found => found.Id == potId);

        if (world == null || pot == null || !world.Catalog.Kinds.TryGetValue(pot.KindId, out var kind)) return false;

        var preset = Book().PresetFor(world.SaveKey, pot);

        return preset != null && Picks.Seed(pot, kind, preset, world, new Dictionary<int, int>()) != null;
    }

    private void SendBadge(int count)
    {
        if (count == _sentBadge) return;
        if (_page.Badge(count)) _sentBadge = count;
    }

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    /// <summary>With no game loaded there is nothing to show and the window stays shut; anything else opens it.</summary>
    private void Open()
    {
        var read = _game.Read();

        if (read is FarmRead.NoGame)
        {
            _page.Status(Texts.NoGame.Pick(IsChinese));

            return;
        }

        _isOpen = true;
        InputGate.PanelOpened();
        Show(read, isOpening: true);
        _log("window opened");
    }

    private void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _keyboard.Release();
        InputGate.PanelClosed();
        _page.Close();
    }

    private void Redraw()
    {
        if (_isOpen) Show(_game.Read(), isOpening: false);
    }

    /// <summary>The catalog goes first, once per game and language, since the view names items only by id.</summary>
    private void Show(FarmRead read, bool isOpening)
    {
        var settings = new ViewSettings(KeyName(), _rotWarnHours, _isKeepingRobotsStocked);
        string view;

        switch (read)
        {
            case FarmRead.Ready ready:
                _lastWorld = ready.World;
                SendCatalog(ready.World.Catalog);
                view = FarmView.Ready(ready.World, Book(), settings);
                break;
            case FarmRead.NotReady notReady:
                view = FarmView.NotReady(Texts.NotReady.Say(IsChinese, notReady.Reason), settings);
                break;
            default:
                Close();
                return;
        }

        if (isOpening)
        {
            _shownView = view;
            _page.Open(view);

            return;
        }

        if (view == _shownView) return;

        _shownView = view;
        _page.Show(view);
    }

    private void SendCatalog(FarmCatalog catalog)
    {
        if (ReferenceEquals(catalog, _sentCatalog)) return;
        if (_page.Catalog(CatalogView.Write(catalog))) _sentCatalog = catalog;
    }

    /// <summary>
    /// Replants the empty pots whose preset asks for it, and keeps robots stocked when the player turned that on, every
    /// few seconds. Only a pass that did something says so, as a toast, so a quiet farm stays quiet.
    /// </summary>
    private void RunAuto(float now)
    {
        _nextAutoAt = now + AutoSeconds;

        if (_lastWorld == null || (!_isKeepingRobotsStocked && !Book().Presets.Any(preset => preset.AutoReplant))) return;

        var outcome = FarmRun.Auto(_game, Book(), now, _retryAt, _isKeepingRobotsStocked);

        if (!FarmMessages.IsWorthShowing(outcome)) return;

        Say(FarmMessages.Describe(outcome, IsChinese));
    }

    /// <summary>Runs one press and shows what it did, then redraws so a queued walk or new count shows at once.</summary>
    private void Press(string name, Func<FarmOutcome> press)
    {
        string message;

        try
        {
            var outcome = press();

            message = FarmMessages.Describe(outcome, IsChinese);
            _log($"{name}: {outcome} -> {message}");
        }
        catch (Exception ex)
        {
            message = Texts.Failed.Pick(IsChinese);
            _log($"{name} failed: {ex}");
        }

        _page.Status(message);
        Redraw();
    }

    private string KeyName() => _panelKey == KeyCode.None ? "" : (_isPanelKeyShifted ? "Shift+" : "") + _panelKey;
}
