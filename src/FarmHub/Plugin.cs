using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using FarmHub.Adapters;
using HarmonyLib;
using UnityEngine;

namespace FarmHub;

[BepInPlugin(Guid, "Farm Hub", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.farmhub";
    private const string Version = "0.4.2";
    private const string PresetFileName = "farm-hub.json";

    public override void Load()
    {
        if (!OptionalLive.IsToolkitInstalled) Log.LogInfo(OptionalLive.MissingNote);

        var panelKey = Config.Bind("General", "PanelKey", KeyCode.G,
            "Key that opens and closes the Farm Hub window, G by default, pressed with Shift while PanelKeyShift is true. None turns the key off; the plant button in the HUD's tool column still opens the window.\n" +
            "打开和关闭 Farm Hub 窗口的按键，默认 G，PanelKeyShift 为 true 时需同时按住 Shift。设为 None 则停用该按键，HUD 工具栏中的植物按钮仍可打开窗口。");
        var isPanelKeyShifted = Config.Bind("General", "PanelKeyShift", true,
            "When true, the window opens with Shift and PanelKey (Shift+G by default), and G alone stays the game's Tend Garden. When false, PanelKey alone opens the window.\n" +
            "设为 true 时，按 Shift 加 PanelKey 打开窗口（默认 Shift+G），单按 G 仍是游戏自带的「巡园」。设为 false 时，单按 PanelKey 即可打开窗口。");
        var rotWarnHours = Config.Bind("General", "RotWarnHours", 12, new ConfigDescription(
            "A ripe crop that will rot within this many hours is flagged as rotting soon, on the HUD button and in the window.\n" +
            "成熟作物在这么多小时内会腐烂时，会在 HUD 按钮和窗口中标记为「快腐烂」。",
            new AcceptableValueRange<int>(1, 96)));
        var isTendFromPresets = Config.Bind("General", "TendFromPresets", true,
            "When true, the game's Tend Garden (G, or the plant button in the HUD) replants each harvested pot from its planting preset instead of opening the plant screen, then plants the empty pots and fertilizes the growing ones from their presets. Replanting needs the game's Replant box on. A pot whose preset has no seed left opens the plant screen as usual. The \"from presets\" box in the HUD's plant panel sets this too.\n" +
            "设为 true 时，游戏的「巡园」（G 键或 HUD 中的植物按钮）收获后会按种植预设直接补种，而不是打开种植界面，然后按预设种植空盆、为生长中的作物追肥。补种需要勾选游戏的「补种」。预设中没有可用种子的种植盆照常打开种植界面。HUD 植物面板中的「按预设」也可设置此项。");
        var isKeepingRobotsStocked = Config.Bind("Robots", "RobotKeepStocked", false,
            "When true, a planter robot whose seed for a pot size has run out is switched to the next seed in the planting preset for its floor, so it keeps sowing. This changes the robot's own sow settings.\n" +
            "设为 true 时，种植机器人某种盆大小的种子用完后，会按其所在楼层的种植预设换成下一种种子，让它继续播种。这会修改机器人自己的播种设置。");
        var isLoggingToFile = Config.Bind("Diagnostics", "LogToFile", false, new ConfigDescription(
            "When true, what Farm Hub does is also written to BepInEx\\farm-hub.log, which is kept between game starts. Turn it on when reporting a problem.\n" +
            "设为 true 时，Farm Hub 的操作也会写入 BepInEx\\farm-hub.log，该文件在重启游戏后保留。报告问题时再打开。", null, "ModManager.Advanced"));
        var log = new FarmLog(Log, Path.Combine(Paths.BepInExRootPath, "farm-hub.log"));
        var harmony = new Harmony(Guid);
        var session = new FarmSession(
            new GameFarm(log.Write),
            new WebPage(log.Write),
            new PresetFileOnDisk(Path.Combine(Paths.ConfigPath, PresetFileName), log.Write),
            hours => rotWarnHours.Value = hours,
            isOn => isKeepingRobotsStocked.Value = isOn,
            isOn => isTendFromPresets.Value = isOn,
            log.Write);

        OptionalLive.Watch(isLoggingToFile, isOn => log.IsFileOn = isOn);
        log.Write($"===== Farm Hub {Version} started");
        InputGate.Apply(harmony, log.Write);
        PageMessages.Apply(harmony, log.Write);
        TendHooks.Apply(harmony, log.Write);

        OptionalLive.Watch(() =>
        {
            session.UseSettings(panelKey.Value, isPanelKeyShifted.Value, rotWarnHours.Value, isKeepingRobotsStocked.Value, isTendFromPresets.Value);
            log.Write($"key {(isPanelKeyShifted.Value ? "Shift+" : "")}{panelKey.Value}, rot warning {rotWarnHours.Value} h, robots kept stocked {isKeepingRobotsStocked.Value}, tend from presets {isTendFromPresets.Value}");
        }, panelKey, isPanelKeyShifted, rotWarnHours, isKeepingRobotsStocked, isTendFromPresets);

        FarmTicker.Session = session;
        FarmTicker.Log = log.Write;
        AddComponent<FarmTicker>();
    }
}
