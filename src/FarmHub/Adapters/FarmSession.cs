using System;
using System.Collections.Generic;
using System.Diagnostics;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
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

    /// <summary>The name the window holds the game's hotkeys and clicks under in Exiled's Toolkit.</summary>
    private const string WindowName = "FarmHub.window";

    private static readonly long StartedAt = Stopwatch.GetTimestamp();

    private readonly GameFarm _game;
    private readonly ReadDone _readDone;
    private readonly FarmPage _page;
    private readonly PageMailbox _messages = PageInbox.Listen(PageCommand.Prefix);
    private readonly WebKeyboard _keyboard = new();
    private readonly PresetFileOnDisk _file;
    private readonly Action<int> _saveRotWarn;
    private readonly Action<bool> _saveRobots;
    private readonly Action<bool> _saveFromPresets;
    private readonly Action<string> _log;
    private readonly Dictionary<long, float> _retryAt = new();
    private readonly Dictionary<long, int> _sowTries = new();

    /// <summary>Per robot and tier, the seed Farm Hub filled in from the pot-size seeds this session, or Picks.PlayerPicked.</summary>
    private readonly Dictionary<(long Robot, int Tier), int> _filled = new();
    private string? _filledSave;
    private readonly List<long> _sowing = new();
    private readonly ViewCache _views = new();
    private readonly Func<bool> _isPanelKeyDown;

    private PresetBook? _book;
    private KeyCode _panelKey;
    private bool _isPanelKeyShifted;
    private string _keyName = "";
    private int _rotWarnHours = 12;
    private bool _isKeepingRobotsStocked;
    private bool _isTendFromPresets;
    private ViewSettings? _settings;

    private bool _isOpen;
    private string _shownView = "";
    private FarmCatalog? _sentCatalog;
    private int? _sentBadge;
    private bool? _sentLive;
    private float _nextRefreshAt;
    private float _nextAutoAt;
    private float _nextSowAt;
    private string _sentHud = "";
    private Phase _phase;
    private FarmRead? _lastRead;
    private FarmWorld? _lastWorld;

    public FarmSession(GameFarm game, FarmPage page, PresetFileOnDisk file, Action<int> saveRotWarn, Action<bool> saveRobots, Action<bool> saveFromPresets, Action<string> log)
    {
        _game = game;
        _readDone = new ReadDone(game);
        _page = page;
        _file = file;
        _saveRotWarn = saveRotWarn;
        _saveRobots = saveRobots;
        _saveFromPresets = saveFromPresets;
        _log = log;
        _isPanelKeyDown = () => Input.GetKeyDown(_panelKey) && GameKeys.IsShiftHeld() == _isPanelKeyShifted;
        TendHooks.CanSow = CanSow;
    }

    /// <summary>Whether the player is typing in a text box in any mod's window, so the window key is text, not a hotkey.</summary>
    private static readonly Func<bool> IsTypingAnywhere = () => Typing.IsActive;

    /// <summary>
    /// A refresh is spread over frames, one part per frame: the farm read (itself a slice per frame), then the HUD and
    /// badge, then the open window, then the auto pass when it is due. Idle frames only check the clock.
    /// </summary>
    private enum Phase
    {
        Idle,
        Reading,
        Hud,
        Window,
        Auto,
    }

    private static bool IsChinese => GameLanguage.IsChinese();

    /// <summary>Called on the main thread at start and after each settings change; the page hears them at the next refresh.</summary>
    public void UseSettings(KeyCode panelKey, bool isPanelKeyShifted, int rotWarnHours, bool isKeepingRobotsStocked, bool isTendFromPresets)
    {
        _panelKey = panelKey;
        _isPanelKeyShifted = isPanelKeyShifted;
        _keyName = panelKey == KeyCode.None ? "" : (isPanelKeyShifted ? "Shift+" : "") + panelKey;
        GameKeys.UsePanelKey(panelKey, isPanelKeyShifted);
        _rotWarnHours = rotWarnHours;
        _isKeepingRobotsStocked = isKeepingRobotsStocked;
        _isTendFromPresets = isTendFromPresets;
        TendHooks.IsOn = isTendFromPresets;
        _settings = null;
        _sentHud = "";
        _sentLive = null;
        _sentBadge = null;
        _shownView = "";
    }

    public void Update()
    {
        var now = Now();

        // The script check is its own small call to the web view, so it waits for a frame with no refresh work.
        if (_phase == Phase.Idle) _page.KeepInstalled(now);

        TakeMessages();

        TakeTendHooks(now);

        // One question to Unity on the frames no key went down, which is nearly all of them. A key typed into one of the
        // game's text boxes (the item search) is text, not the panel key.
        if (!_keyboard.IsClaimed && !GameKeys.IsGameTyping && Input.anyKeyDown)
        {
            if (Hotkey.Fires(_panelKey != KeyCode.None, _isPanelKeyDown, IsTypingAnywhere))
            {
                Toggle();

                return;
            }

            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();

                return;
            }
        }

        Step(now);
    }

    /// <summary>Seconds since the mod loaded, from .NET's own clock: asking Unity for the time every frame is a call into the game.</summary>
    private static float Now() => (float)((Stopwatch.GetTimestamp() - StartedAt) / (double)Stopwatch.Frequency);

    /// <summary>One part of the refresh per frame, so the read, the views and the auto pass never share a frame.</summary>
    private void Step(float now)
    {
        switch (_phase)
        {
            case Phase.Idle:
                if (now < _nextRefreshAt) return;

                _nextRefreshAt = now + (_isOpen ? OpenRefreshSeconds : ClosedRefreshSeconds);
                _phase = Phase.Reading;
                return;
            case Phase.Reading:
                var read = _game.AdvanceRead();

                if (read == null) return;

                _lastRead = read;
                _lastWorld = (read as FarmRead.Ready)?.World;
                ForgetFillsOfOtherSaves(_lastWorld);
                SendLive(_lastWorld != null);
                _phase = Phase.Hud;
                return;
            case Phase.Hud:
                SendHud(_lastWorld);
                _phase = Phase.Window;
                return;
            case Phase.Window:
                _phase = Phase.Auto;

                if (_isOpen && _lastRead != null) Show(_lastRead, isOpening: false);

                return;
            case Phase.Auto:
                _phase = Phase.Idle;

                if (now >= _nextAutoAt) RunAuto(now);

                return;
        }
    }

    /// <summary>The page's messages since the last frame, in the order sent; one the plugin cannot read is dropped.</summary>
    private void TakeMessages()
    {
        while (_messages.TryTake(out var message))
        {
            if (PageCommand.Parse(message) is { } command) HandleSafely(command);
        }
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
            Status(Texts.Failed.Pick(IsChinese));
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
                Press($"robot {tier.RobotId} tier {tier.Tier}", () => FarmRun.SetRobotTier(_game, tier.RobotId, tier.Tier, tier.SeedId, tier.FertId, _filled));
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
                _settings = null;
                _saveRotWarn(rotWarn.Hours);
                Say(Texts.RotWarnSet.Say(IsChinese, rotWarn.Hours));
                break;
            case PageCommand.Robots robots:
                _isKeepingRobotsStocked = robots.IsKeepingStocked;
                _settings = null;
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

    /// <summary>A result line, in the window while it is open and as a toast otherwise; a failure shows as an error.</summary>
    private void Status(string message) => _page.Status(message, _isOpen, message == Texts.Failed.Pick(IsChinese) ? ToastKind.Error : ToastKind.Info);

    private void Say(string message)
    {
        _log(message);
        Status(message);
        Redraw();
    }

    private void SendLive(bool isLive)
    {
        if (isLive == _sentLive) return;
        if (_page.Live(isLive, _keyName)) _sentLive = isLive;

        _log($"HUD button {(isLive ? "shown" : "hidden")}");
    }

    /// <summary>
    /// The HUD button's badge counts pots that need the player, and the plant panel's additions name seeds, so the
    /// catalog goes first, once per game and language. A read that found nothing new gets the very views it had, and
    /// nothing is sent.
    /// </summary>
    private void SendHud(FarmWorld? world)
    {
        string hud;
        int badge;

        if (world == null)
        {
            hud = HudView.NotReady();
            badge = 0;
        }
        else
        {
            SendCatalog(world.Catalog);
            (hud, badge) = _views.Hud(world, Book(), _rotWarnHours, _isTendFromPresets);
        }

        SendBadge(badge);

        if (hud == _sentHud) return;
        if (_page.Hud(hud)) _sentHud = hud;
    }

    /// <summary>A replant walk's pot, sown from its preset when the walk is done with it, and the extras after the game's Tend Garden.</summary>
    private void TakeTendHooks(float now)
    {
        if (TendHooks.HasSows)
        {
            foreach (var potId in TendHooks.TakeSows())
            {
                _sowTries[potId] = 0;
                _nextSowAt = now;
            }
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
        _sowing.Clear();
        _sowing.AddRange(_sowTries.Keys);

        foreach (var potId in _sowing)
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
        Pot? pot = null;

        for (var i = 0; world != null && i < world.Pots.Count; i++)
        {
            if (world.Pots[i].Id != potId) continue;

            pot = world.Pots[i];
            break;
        }

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

    /// <summary>
    /// With no game loaded there is nothing to show and the window stays shut; anything else opens it. The window opens
    /// on the last refresh's read, and a fresh read starts at once and redraws it a few frames later.
    /// </summary>
    private void Open()
    {
        var read = _lastRead is FarmRead.Ready or FarmRead.NotReady ? _lastRead : _game.Read();

        if (read is FarmRead.NoGame)
        {
            Status(Texts.NoGame.Pick(IsChinese));

            return;
        }

        _isOpen = true;
        InputGate.Opened(WindowName);
        Show(read, isOpening: true);
        _log("window opened");
        RefreshNow();
    }

    private void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _keyboard.Release();
        InputGate.Closed(WindowName);
        _page.Close();
    }

    /// <summary>After a press or an edit the open window is read afresh at once, so a queued walk or new count shows a few frames later.</summary>
    private void Redraw()
    {
        if (_isOpen) RefreshNow();
    }

    private void RefreshNow()
    {
        _game.RestartRead();
        _phase = Phase.Reading;
        _nextRefreshAt = Now() + OpenRefreshSeconds;
    }

    private ViewSettings Settings() => _settings ??= new ViewSettings(_keyName, _rotWarnHours, _isKeepingRobotsStocked);

    /// <summary>The catalog goes first, once per game and language, since the view names items only by id.</summary>
    private void Show(FarmRead read, bool isOpening)
    {
        var settings = Settings();
        string view;

        switch (read)
        {
            case FarmRead.Ready ready:
                SendCatalog(ready.World.Catalog);
                view = _views.Window(ready.World, Book(), settings, _filled);
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

    /// <summary>Who filled which robot tier is per save: robot ids from one save mean nothing in another.</summary>
    private void ForgetFillsOfOtherSaves(FarmWorld? world)
    {
        if (world == null || world.SaveKey == _filledSave) return;

        _filled.Clear();
        _filledSave = world.SaveKey;
    }

    /// <summary>
    /// Replants the empty pots whose preset asks for it, fills robot tiers that follow the pot-size seeds, and keeps
    /// robots stocked when the player turned that on, every few seconds. It runs on the read just finished, and only
    /// when that read shows something for it to do. Only a pass that did something says so, as a toast, so a quiet farm
    /// stays quiet.
    /// </summary>
    private void RunAuto(float now)
    {
        _nextAutoAt = now + AutoSeconds;

        var world = _lastWorld;

        if (world == null) return;

        var book = Book();

        if (world.Robots.Count == 0 && !FarmRun.HasAutoReplant(book)) return;

        Picks.NoteFollowing(world, book, _filled);

        if (!FarmRun.HasAutoWork(world, book, now, _retryAt, _isKeepingRobotsStocked, _filled)) return;

        FarmOutcome outcome;

        _readDone.Last = _lastRead;

        try
        {
            outcome = FarmRun.Auto(_readDone, book, now, _retryAt, _isKeepingRobotsStocked, _filled);
        }
        finally
        {
            _readDone.Last = null;
        }

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

        Status(message);
        Redraw();
    }

    /// <summary>
    /// The game, for presses run on a read already made: Read answers with it while it is set, and every press goes to
    /// the game. That read is a few frames old, so a pot is checked once more, alone, before it is planted.
    /// </summary>
    private sealed class ReadDone : IFarmGame
    {
        private readonly GameFarm _game;

        public ReadDone(GameFarm game)
        {
            _game = game;
        }

        public FarmRead? Last { get; set; }

        public FarmRead Read() => Last ?? _game.Read();

        public bool Plant(long potId, int plantId, int seedCount, int seedItemId, int fertId) =>
            (Last == null || _game.IsStillPlantable(potId)) && _game.Plant(potId, plantId, seedCount, seedItemId, fertId);

        public bool Fertilize(long potId, int fertId) => _game.Fertilize(potId, fertId);

        public bool Tend(long potId, TendAction action) => _game.Tend(potId, action);

        public bool TendAll() => _game.TendAll();

        public bool SetRobotTier(long robotId, int tier, int seedId, int fertId) => _game.SetRobotTier(robotId, tier, seedId, fertId);
    }
}
