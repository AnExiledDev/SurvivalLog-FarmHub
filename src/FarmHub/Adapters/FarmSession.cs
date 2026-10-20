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
    private const float ClosedRefreshSeconds = 5f;
    private const float AutoSeconds = 5f;

    private readonly IFarmGame _game;
    private readonly WebPage _page;
    private readonly WebKeyboard _keyboard = new();
    private readonly PresetFileOnDisk _file;
    private readonly Action<int> _saveRotWarn;
    private readonly Action<bool> _saveRobots;
    private readonly Action<string> _log;
    private readonly Dictionary<long, float> _retryAt = new();

    private PresetBook? _book;
    private KeyCode _panelKey;
    private int _rotWarnHours = 12;
    private bool _isKeepingRobotsStocked;

    private bool _isOpen;
    private string _shownView = "";
    private FarmCatalog? _sentCatalog;
    private int? _sentBadge;
    private bool? _sentLive;
    private float _nextRefreshAt;
    private float _nextAutoAt;
    private FarmWorld? _lastWorld;

    public FarmSession(IFarmGame game, WebPage page, PresetFileOnDisk file, Action<int> saveRotWarn, Action<bool> saveRobots, Action<string> log)
    {
        _game = game;
        _page = page;
        _file = file;
        _saveRotWarn = saveRotWarn;
        _saveRobots = saveRobots;
        _log = log;
    }

    private static bool IsChinese => GameLanguage.IsChinese();

    /// <summary>Called on the main thread at start and after each settings change; the page hears them at the next refresh.</summary>
    public void UseSettings(KeyCode panelKey, int rotWarnHours, bool isKeepingRobotsStocked)
    {
        _panelKey = panelKey;
        _rotWarnHours = rotWarnHours;
        _isKeepingRobotsStocked = isKeepingRobotsStocked;
        _sentLive = null;
        _sentBadge = null;
        _shownView = "";
    }

    public void Update()
    {
        var now = Time.unscaledTime;

        _page.KeepInstalled(now);

        while (PageMessages.TryTake(out var command)) HandleSafely(command);

        if (!_keyboard.IsClaimed && _panelKey != KeyCode.None && Input.GetKeyDown(_panelKey))
        {
            Toggle();

            return;
        }

        if (_isOpen && !_keyboard.IsClaimed && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();

            return;
        }

        if (now >= _nextRefreshAt) Refresh(now);
        if (now >= _nextAutoAt) RunAuto(now);
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
                Press("tend all", () => FarmRun.TendAll(_game));
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

        if (_isOpen) Show(read, isOpening: false);
    }

    private void SendLive(bool isLive)
    {
        if (isLive == _sentLive) return;
        if (_page.Live(isLive, KeyName())) _sentLive = isLive;

        _log($"HUD button {(isLive ? "shown" : "hidden")}");
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

    private string KeyName() => _panelKey == KeyCode.None ? "" : _panelKey.ToString();
}
