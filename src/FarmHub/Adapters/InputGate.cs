using System;
using GameCore.HotUpdate.Battle.Show;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using UnityEngine;

namespace FarmHub.Adapters;

/// <summary>
/// While the panel is open: stops the game's hotkeys, so a key pressed over the panel does not also open a menu, and
/// sends every click to the web UI. The game only passes a click to the web UI where one of its own pages reported a
/// button, which the panel is not. Static because a Harmony patch has nowhere else to look.
/// </summary>
internal static class InputGate
{
    private static bool _isPanelOpen;
    private static int _blockedThroughFrame = -1;
    private static bool _hasRoutedAClick;
    private static Action<string> _log = _ => { };
    private static KeyCode _panelKey;
    private static bool _isPanelKeyShifted;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;
        Patch(harmony, typeof(HotKeyManager), "Update", prefix: nameof(SkipWhileBlocked), done: "hotkeys are blocked while the panel is open", consequence: "keys pressed over the panel will also reach the game");
        Patch(harmony, typeof(HotKeyManager), "HandleHotKeyPlantPatrol", prefix: nameof(SkipTendGardenOnPanelKey), done: "Shift with the panel key opens the window without also sending Tend Garden", consequence: "Shift with the panel key may also send the game's Tend Garden");
        Patch(harmony, typeof(WebUILayer), "ValidateRaycast", postfix: nameof(RouteClicksToPanel), done: "clicks go to the panel while it is open", consequence: "clicks on the panel may fall through to the game");
    }

    /// <summary>The game's Tend Garden key ignores Shift, so Shift with the same key would fire both without this.</summary>
    public static void UsePanelKey(KeyCode key, bool isShifted)
    {
        _panelKey = key;
        _isPanelKeyShifted = isShifted;
    }

    public static bool IsShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    public static void PanelOpened() => _isPanelOpen = true;

    /// <summary>Keeps hotkeys blocked through the next frame so the key that closed the panel (Esc) is not seen by the game too.</summary>
    public static void PanelClosed()
    {
        _isPanelOpen = false;
        _blockedThroughFrame = Time.frameCount + 1;
    }

    private static void Patch(Harmony harmony, Type type, string method, string? prefix = null, string? postfix = null, string done = "", string consequence = "")
    {
        try
        {
            harmony.Patch(
                AccessTools.Method(type, method),
                prefix: prefix == null ? null : new HarmonyMethod(typeof(InputGate), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(InputGate), postfix));
            _log(done);
        }
        catch (Exception ex)
        {
            _log($"could not hook {type.Name}.{method}, {consequence}: {ex.Message}");
        }
    }

    /// <summary>Runs before the game's hotkeys every frame, so the frame count is asked only in the frame or two after a close.</summary>
    private static bool SkipWhileBlocked()
    {
        if (_isPanelOpen) return false;
        if (_blockedThroughFrame < 0) return true;
        if (Time.frameCount <= _blockedThroughFrame) return false;

        _blockedThroughFrame = -1;

        return true;
    }

    /// <summary>The key press is the rare part, so it is asked first and Shift only when it is down.</summary>
    private static bool SkipTendGardenOnPanelKey() => !(_isPanelKeyShifted && _panelKey != KeyCode.None && Input.GetKeyDown(_panelKey) && IsShiftHeld());

    private static void RouteClicksToPanel(ref bool __result)
    {
        if (!_isPanelOpen || __result) return;

        __result = true;

        if (!_hasRoutedAClick) _log("first click routed to the panel");

        _hasRoutedAClick = true;
    }
}
