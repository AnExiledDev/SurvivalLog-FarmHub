using System;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Web;
using FarmHub.Core;

namespace FarmHub.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's pages sit in. It
/// draws the window and puts the Farm Hub button in the HUD's tool column. The web view can reload at any time and
/// lose the script, so Exiled's Toolkit checks for it every couple of seconds and the session puts it back when the
/// page says it is missing.
/// </summary>
internal sealed class FarmPage
{
    private const string ScriptResource = "FarmHub.page.js";
    private const string Global = "window.__slfarmhub";

    private readonly WebPage _page;

    public FarmPage(Action<string> log)
    {
        // The page's words go with the first call after a load and again whenever the game's language changes;
        // resending the whole table with every call was most of what each refresh cost the web view.
        _page = new WebPage(typeof(FarmPage).Assembly, ScriptResource, Global, PageCommand.Prefix + "noscript", log)
        {
            Words = PageWords.Script,
        };
    }

    public void KeepInstalled(float now) => _page.KeepInstalled(now);

    public void Install() => _page.Install();

    /// <summary>The page lost its script, or loaded it afresh, so its words go with the next call.</summary>
    public void ForgetWords() => _page.ForgetWords();

    public void Open(string viewJson) => _page.Call($"open({viewJson})");

    public void Show(string viewJson) => _page.Call($"setData({viewJson})");

    public void Close() => _page.Call("close()");

    /// <summary>
    /// A result line: in the window's footer while it is open, as an Exiled's Toolkit toast otherwise. The page hears it
    /// either way, as it frees the button that was pressed.
    /// </summary>
    public void Status(string text, bool isWindowOpen, ToastKind kind)
    {
        _page.Call($"status({JsText.Literal(text)})");

        if (isWindowOpen || string.IsNullOrEmpty(text)) return;

        Toasts.Show(new ToastRequest(Texts.Source.Pick(GameLanguage.IsChinese()), text) { Kind = kind });
    }

    /// <summary>Whether a game is loaded (the HUD button shows only then) and the panel key's name, "" for none.</summary>
    public bool Live(bool isLive, string keyName) => _page.Call($"live({(isLive ? "true" : "false")},{JsText.Literal(keyName)})");

    /// <summary>Names, icons, crops, seeds, fertilizers and pot kinds; the views refer to them by id only.</summary>
    public bool Catalog(string catalogJson) => _page.Call($"catalog({catalogJson})");

    /// <summary>The additions to the game's plant panel (HudView), sent whether or not the window is open.</summary>
    public bool Hud(string hudJson) => _page.Call($"hud({hudJson})");

    /// <summary>The count badge on the HUD button, sent whether or not the window is open.</summary>
    public bool Badge(int count) => _page.Call($"badge({count})");
}
