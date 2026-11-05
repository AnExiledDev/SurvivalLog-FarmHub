using ExiledToolkit.Game;
using GameCore.HotUpdate.Battle.Show;
using UnityEngine;

namespace FarmHub.Adapters;

/// <summary>
/// The two key checks only Farm Hub needs; Exiled's Toolkit holds the game's hotkeys and sends every click to the web
/// UI while the window is open. The game's Tend Garden key ignores Shift, so Shift with the same key would fire both,
/// and a key typed into one of the game's own text boxes (the item search) is text, not the panel key. Static because
/// a Harmony patch has nowhere else to look.
/// </summary>
internal static class GameKeys
{
    private static KeyCode _panelKey;
    private static bool _isPanelKeyShifted;

    public static void Apply(PatchKit hooks)
    {
        hooks.Patch(typeof(GameKeys), typeof(HotKeyManager), "Update", postfix: nameof(NoteGameTyping), done: "the panel key waits while you type in the game's own text boxes", consequence: "typing the panel key in the game's item search may also open the window");
        hooks.Patch(typeof(GameKeys), typeof(HotKeyManager), "HandleHotKeyPlantPatrol", prefix: nameof(SkipTendGardenOnPanelKey), done: "Shift with the panel key opens the window without also sending Tend Garden", consequence: "Shift with the panel key may also send the game's Tend Garden");
    }

    public static void UsePanelKey(KeyCode key, bool isShifted)
    {
        _panelKey = key;
        _isPanelKeyShifted = isShifted;
    }

    public static bool IsShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>The game is taking typed text in one of its own menus, such as the item search, so letter keys are words, not hotkeys.</summary>
    public static bool IsGameTyping { get; private set; }

    /// <summary>
    /// A postfix, so it runs on every frame, also while the Toolkit holds the game's hotkeys back. A field read and no
    /// call.
    /// </summary>
    private static void NoteGameTyping(HotKeyManager __instance) => IsGameTyping = __instance != null && __instance._isTextInputActive;

    /// <summary>The key press is the rare part, so it is asked first and Shift only when it is down.</summary>
    private static bool SkipTendGardenOnPanelKey() => !(_isPanelKeyShifted && _panelKey != KeyCode.None && Input.GetKeyDown(_panelKey) && IsShiftHeld());
}
