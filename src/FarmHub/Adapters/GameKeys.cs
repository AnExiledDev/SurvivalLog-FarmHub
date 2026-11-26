using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate.Battle.Show;
using UnityEngine;

namespace FarmHub.Adapters;

/// <summary>
/// The key check only Farm Hub needs; Exiled's Toolkit holds the game's hotkeys and sends every click to the web UI
/// while the window is open. The game's Tend Garden key ignores Shift, so Shift with the same key would fire both.
/// Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class GameKeys
{
    private static KeyCode _panelKey;
    private static bool _isPanelKeyShifted;

    public static void Apply(PatchKit hooks)
    {
        hooks.Patch(typeof(GameKeys), typeof(HotKeyManager), "HandleHotKeyPlantPatrol", prefix: nameof(SkipTendGardenOnPanelKey), done: "Shift with the panel key opens the window without also sending Tend Garden", consequence: "Shift with the panel key may also send the game's Tend Garden");
    }

    public static void UsePanelKey(KeyCode key, bool isShifted)
    {
        _panelKey = key;
        _isPanelKeyShifted = isShifted;
    }

    public static bool IsShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>The key press is the rare part, so it is asked first and Shift only when it is down. On a failure the game's Tend Garden key runs as usual.</summary>
    private static bool SkipTendGardenOnPanelKey()
    {
        try
        {
            return !(_isPanelKeyShifted && _panelKey != KeyCode.None && Input.GetKeyDown(_panelKey) && IsShiftHeld());
        }
        catch (Exception)
        {
            return true;
        }
    }
}
