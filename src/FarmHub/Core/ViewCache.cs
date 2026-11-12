using System.Collections.Generic;

namespace FarmHub.Core;

/// <summary>
/// The HUD's additions, the badge and the window as last built, with the print of what each was built from. A read
/// that prints the same gets the very same strings back, so nothing is planned, written or compared again. Everything
/// is dropped when the catalog changes (a new game or language) or the save does, and on Forget.
/// </summary>
public sealed class ViewCache
{
    private FarmCatalog? _catalog;
    private string? _saveKey;

    private PresetBook? _hudBook;
    private ulong _hudPrint;
    private string? _hud;
    private int _badge;

    private PresetBook? _windowBook;
    private ulong _windowPrint;
    private string? _window;

    /// <summary>How many views were built, the HUD and the window each counting one.</summary>
    public int Builds { get; private set; }

    public void Forget()
    {
        _catalog = null;
        _saveKey = null;
        _hudBook = null;
        _hud = null;
        _badge = 0;
        _windowBook = null;
        _window = null;
    }

    /// <summary>The HUD's JSON (HudView) and the badge count (FarmView.NeedsYou), built from one plan.</summary>
    public (string Hud, int Badge) Hud(FarmWorld world, PresetBook book, int rotWarnHours, bool fromPresets)
    {
        KeepFor(world);

        var print = FarmPrint.Hud(world, rotWarnHours, fromPresets);

        if (_hud != null && print == _hudPrint && ReferenceEquals(book, _hudBook)) return (_hud, _badge);

        var plan = FarmView.Plan(world, book);

        _hud = HudView.Ready(world, book, rotWarnHours, fromPresets, plan);
        _badge = FarmView.NeedsYou(world, plan);
        _hudPrint = print;
        _hudBook = book;
        Builds++;

        return (_hud, _badge);
    }

    /// <param name="filled">Who set each robot tier this session (see Picks.IsFollowing); null when Farm Hub has filled nothing.</param>
    public string Window(FarmWorld world, PresetBook book, ViewSettings settings, IReadOnlyDictionary<(long Robot, int Tier), int>? filled = null)
    {
        KeepFor(world);

        var print = FarmPrint.Window(world, settings, filled);

        if (_window != null && print == _windowPrint && ReferenceEquals(book, _windowBook)) return _window;

        _window = FarmView.Ready(world, book, settings, filled);
        _windowPrint = print;
        _windowBook = book;
        Builds++;

        return _window;
    }

    private void KeepFor(FarmWorld world)
    {
        if (ReferenceEquals(world.Catalog, _catalog) && world.SaveKey == _saveKey) return;

        Forget();
        _catalog = world.Catalog;
        _saveKey = world.SaveKey;
    }
}
