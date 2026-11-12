using System;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using FarmHub.Adapters;
using FarmHub.Core;
using HarmonyLib;
using UnityEngine;

namespace FarmHub;

[BepInPlugin(Guid, "Farm Hub", Version)]
[BepInDependency(ToolkitGuid, ToolkitVersion)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.farmhub";
    private const string Version = "0.5.8";

    /// <summary>Exiled's Toolkit: its shared helpers, toasts, live settings, UI size and typing check all come from it.</summary>
    private const string ToolkitGuid = "com.dev.survivallog.exiledtoolkit";
    private const string ToolkitVersion = ">=0.19.0";

    private const string PresetFileName = "farm-hub.json";
    private const string LogFileName = "farm-hub.log";

    /// <summary>The name the page script hands its panels to the Toolkit's UI size with.</summary>
    private const string UiSizeName = "FarmHub";

    public override void Load()
    {
        ModUiSize.Bind(Config, UiSizeName);

        var panelKey = Config.Bind("General", "PanelKey", Enum.Parse<KeyCode>(Hotkey.DefaultKey),
            "Key that opens and closes the Farm Hub window, pressed with Shift while PanelKeyShift is true. Unbound by default: the plant button in the HUD's tool column opens the window. Set a key here to open it from the keyboard too. The key does nothing while you type in a text box.\n" +
            "打开和关闭 Farm Hub 窗口的按键，PanelKeyShift 为 true 时需同时按住 Shift。默认不绑定：点击 HUD 工具栏中的植物按钮即可打开窗口。在此设置按键后也可以用键盘打开。在输入框里打字时该按键不起作用。");
        var isPanelKeyShifted = Config.Bind("General", "PanelKeyShift", true,
            "When true, the window opens with Shift and PanelKey, so PanelKey can be G and G alone stays the game's Tend Garden. When false, PanelKey alone opens the window.\n" +
            "设为 true 时，按 Shift 加 PanelKey 打开窗口，因此 PanelKey 可以设为 G，单按 G 仍是游戏的「巡园」。设为 false 时，单按 PanelKey 即可打开窗口。");
        var rotWarnHours = Config.Bind("General", "RotWarnHours", 12, new ConfigDescription(
            "A ripe crop that will rot within this many hours is marked as rotting soon on the HUD button and in the window.\n" +
            "成熟作物在这么多小时内会腐烂时，会在 HUD 按钮和窗口中标记为「快腐烂」。",
            new AcceptableValueRange<int>(1, 96)));
        var isTendFromPresets = Config.Bind("General", "TendFromPresets", true,
            "When true, the game's Tend Garden (G, or the plant button in the HUD) replants each harvested pot from its planting preset instead of opening the plant screen, then plants the empty pots and fertilizes the growing ones from their presets. Replanting needs the game's Replant box on. A pot whose preset has no seed left opens the plant screen as usual. The \"from presets\" box in the HUD's plant panel sets this too.\n" +
            "设为 true 时，游戏的「巡园」（G 键或 HUD 中的植物按钮）收获后会按种植预设直接补种，而不是打开种植界面，然后按预设种植空盆、为生长中的作物追肥。补种需要勾选游戏的「补种」。预设中没有可用种子的种植盆照常打开种植界面。HUD 植物面板中的「按预设」也可设置此项。");
        var isKeepingRobotsStocked = Config.Bind("Robots", "RobotKeepStocked", false,
            "When true and a seed you picked for a planter robot runs out, Farm Hub switches the robot to the next seed in the planting preset for its floor, so it keeps sowing. When a pot size has no seed set, the robot always plants the seeds listed for that pot size in the preset, whatever this is set to. Either way, Farm Hub changes the robot's sow settings in the game.\n" +
            "设为 true 时，你为种植机器人选的种子用完后，会按其所在楼层的种植预设换成下一种种子，让它继续播种。没有设置种子的盆大小，无论此项如何设置，都会使用预设中为该盆大小列出的种子。这两种情况下 Farm Hub 都会修改机器人在游戏里的播种设置。");
        var isLoggingToFile = Config.Bind("Diagnostics", "LogToFile", false, new ConfigDescription(
            "When true, what Farm Hub does is also written to BepInEx\\farm-hub.log, which is kept between game starts. Turn it on when reporting a problem.\n" +
            "设为 true 时，Farm Hub 的操作也会写入 BepInEx\\farm-hub.log，该文件在重启游戏后保留。报告问题时再打开。", null, "ModManager.Advanced"));

        var log = new ModLog(Log, LogFileName);

        // Before the first line, since the file copy is on until the setting says otherwise.
        LiveSettings.Watch(isLoggingToFile, isOn => log.IsFileOn = isOn);

        var hooks = new PatchKit(new Harmony(Guid), log.Write);
        var session = new FarmSession(
            new GameFarm(log.Write),
            new FarmPage(log.Write),
            new PresetFileOnDisk(Path.Combine(Paths.ConfigPath, PresetFileName), log.Write),
            hours => rotWarnHours.Value = hours,
            isOn => isKeepingRobotsStocked.Value = isOn,
            isOn => isTendFromPresets.Value = isOn,
            log.Write);

        log.Write($"===== Farm Hub {Version} started");
        GameKeys.Apply(hooks);
        TendHooks.Apply(hooks);

        LiveSettings.Watch(() =>
        {
            session.UseSettings(panelKey.Value, isPanelKeyShifted.Value, rotWarnHours.Value, isKeepingRobotsStocked.Value, isTendFromPresets.Value);
            log.Write($"key {(isPanelKeyShifted.Value ? "Shift+" : "")}{panelKey.Value}, rot warning {rotWarnHours.Value} h, robots kept stocked {isKeepingRobotsStocked.Value}, tend from presets {isTendFromPresets.Value}");
        }, panelKey, isPanelKeyShifted, rotWarnHours, isKeepingRobotsStocked, isTendFromPresets);

        FarmTicker.Session = session;
        FarmTicker.Log = log.Write;
        AddComponent<FarmTicker>();

        // The views and presses are compiled on a worker now, so the first refresh in a save costs only the read.
        Task.Run(() => Warmup.Run()).ContinueWith(task => log.Write("warm-up failed: " + task.Exception?.GetBaseException().Message), TaskContinuationOptions.OnlyOnFaulted);
    }
}
