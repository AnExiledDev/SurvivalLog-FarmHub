using System.Collections.Generic;
using System.Globalization;

namespace FarmHub.Core;

/// <summary>One piece of on-screen text in the two languages the game ships. {0}, {1} are filled in by Say.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    public string Say(bool isChinese, params object[] args) => string.Format(CultureInfo.InvariantCulture, Pick(isChinese), args);
}

/// <summary>A phrase about a count: English says "1 pot" and "2 pots", Chinese has one form for both. {0} is the count.</summary>
public readonly record struct Counted(Phrase One, Phrase Many)
{
    public string Say(bool isChinese, int count, params object[] more)
    {
        var args = new object[more.Length + 1];

        args[0] = count;
        more.CopyTo(args, 1);

        return (count == 1 ? One : Many).Say(isChinese, args);
    }
}

/// <summary>The words the mod puts on screen: press results, reasons, and the panel's own labels.</summary>
public static class Texts
{
    private const string Log = "BepInEx/LogOutput.log";

    public static readonly Phrase FirstPresetName = new("My farm", "我的农场");
    public static readonly Phrase NewPresetName = new("New preset", "新预设");

    public static readonly Phrase NoGame = new("Load a game first.", "请先载入游戏。");
    public static readonly Phrase NotReady = new("Can't manage the farm right now: {0}.", "现在无法管理农场：{0}。");
    public static readonly Phrase NoWorld = new("the game world is not loaded", "游戏世界尚未载入");
    public static readonly Phrase Away = new("you are away from home", "你不在家中");
    public static readonly Phrase ReadFailed = new("the pots could not be read, see " + Log, "无法读取种植盆，详见 " + Log);
    public static readonly Phrase Gone = new("That pot is gone.", "这个种植盆已经不在了。");
    public static readonly Counted Planted = new(new("Planting 1 pot.", "正在种植 {0} 个种植盆。"), new("Planting {0} pots.", "正在种植 {0} 个种植盆。"));
    public static readonly Counted AutoPlanted = new(new("Auto replant: planting 1 pot.", "自动补种：正在种植 {0} 个种植盆。"), new("Auto replant: planting {0} pots.", "自动补种：正在种植 {0} 个种植盆。"));
    public static readonly Phrase NoSeedCount = new("{0} had no seed left in their preset.", "{0} 个的预设中没有可用的种子。");
    public static readonly Phrase GameRefused = new("The game refused {0}.", "游戏拒绝了 {0} 个。");
    public static readonly Phrase PlantedOne = new("Planting {1} in the {0}.", "正在{0}中种植{1}。");
    public static readonly Phrase NothingToPlant = new("No empty pots to plant.", "没有可种植的空种植盆。");
    public static readonly Phrase NotEmpty = new("The {0} is not empty.", "{0}不是空的。");
    public static readonly Phrase NoPreset = new("The {0} has no planting preset. Pick one in the Pots tab.", "{0}没有种植预设。请在「种植盆」页选择一个。");
    public static readonly Phrase NoSeedFor = new("No seed left for the {0} (preset {1}).", "{0}没有可用的种子（预设「{1}」）。");
    public static readonly Counted Fertilizing = new(new("Fertilizing 1 pot.", "正在为 {0} 个种植盆追肥。"), new("Fertilizing {0} pots.", "正在为 {0} 个种植盆追肥。"));
    public static readonly Phrase NoFertCount = new("{0} had no fertilizer left in their preset.", "{0} 个的预设中没有可用的肥料。");
    public static readonly Phrase NothingToFertilize = new("No growing pot is waiting for fertilizer.", "没有等待追肥的生长中种植盆。");
    public static readonly Phrase Tending = new("{0}: {1}.", "{0}：{1}。");
    public static readonly Phrase TendingAll = new("Tending every pot.", "正在照料所有种植盆。");
    public static readonly Phrase TendAllRefused = new("The game would not start tending.", "游戏没有开始照料。");
    public static readonly Phrase NothingToTend = new("The {0} needs nothing right now.", "{0}现在不需要照料。");
    public static readonly Phrase Refused = new("The game would not do that for the {0}.", "游戏拒绝了对{0}的操作。");
    public static readonly Counted RobotsSwitched = new(
        new("Robots: switched 1 sow setting to the next seed.", "机器人：已将 {0} 个播种设置换成下一种种子。"),
        new("Robots: switched {0} sow settings to the next seed.", "机器人：已将 {0} 个播种设置换成下一种种子。"));
    public static readonly Phrase RobotGone = new("That robot is gone.", "这台机器人已经不在了。");
    public static readonly Phrase RobotNumber = new("Robot {0}", "机器人 {0}");
    public static readonly Phrase RobotTierSet = new("{0}: sow setting changed.", "{0}：播种设置已更改。");
    public static readonly Phrase Failed = new("Farm Hub failed, see " + Log + ".", "Farm Hub 出错，详见 " + Log + "。");

    public static readonly Phrase Harvest = new("Harvest", "收获");
    public static readonly Phrase Clear = new("Clear withered crop", "清除枯萎作物");
    public static readonly Phrase Uproot = new("Uproot poor crop", "拔除劣质作物");
    public static readonly Phrase Pests = new("Remove Pests", "除虫");
    public static readonly Phrase Weeds = new("Remove Weeds", "除草");
    public static readonly Phrase Water = new("Water", "浇水");

    public static readonly Phrase PresetSaved = new("Preset {0} saved.", "预设「{0}」已保存。");
    public static readonly Phrase PresetDeleted = new("Preset {0} deleted.", "预设「{0}」已删除。");
    public static readonly Phrase PresetBad = new("That preset could not be read.", "无法读取该预设。");
    public static readonly Phrase AssignedOwn = new("{0} pot(s) now use {1}.", "{0} 个种植盆现在使用「{1}」。");
    public static readonly Phrase AssignedDefault = new("{0} pot(s) now use their room's or floor's preset.", "{0} 个种植盆现在使用所在房间或楼层的预设。");
    public static readonly Phrase NoSave = new("Pot choices are kept per save, and this game has no save yet. Save the game first.", "种植盆的预设选择按存档保存，而这局游戏还没有存档。请先保存游戏。");
    public static readonly Phrase NotSaved = new("farm-hub.json could not be written, so the change lasts until the game is closed. See " + Log + ".", "无法写入 farm-hub.json，更改只在关闭游戏前有效。详见 " + Log + "。");
    public static readonly Phrase RotWarnSet = new("Rotting soon now means within {0} h.", "「快腐烂」现在表示 {0} 小时内。");
    public static readonly Phrase RobotsOn = new("Robots will be kept stocked.", "将保持机器人补给。");
    public static readonly Phrase RobotsOff = new("Robots will no longer be switched to other seeds.", "不再为机器人更换种子。");

    public static Phrase ChoreName(TendAction action) => action switch
    {
        TendAction.Harvest => Harvest,
        TendAction.Clear => Clear,
        TendAction.Uproot => Uproot,
        TendAction.Pests => Pests,
        TendAction.Weeds => Weeds,
        TendAction.Water => Water,
        _ => throw new System.ArgumentOutOfRangeException(nameof(action), action, "unknown chore"),
    };

    /// <summary>The words page.js draws the window, the HUD button and its tip with, by the key it looks them up by.</summary>
    public static readonly IReadOnlyList<(string Key, Phrase Phrase)> Page = new (string, Phrase)[]
    {
        ("tabPots", new("Pots", "种植盆")),
        ("tabPresets", new("Planting presets", "种植预设")),
        ("tabRobots", new("Robots", "机器人")),
        ("tabForecast", new("Forecast", "预测")),
        ("tabSettings", new("Settings", "设置")),
        ("cRipe", new("{0} ripe", "{0} 个成熟")),
        ("cRot", new("{0} rotting soon", "{0} 个快腐烂")),
        ("cPests", new("{0} pests or weeds", "{0} 个有虫害或杂草")),
        ("cDry", new("{0} dry", "{0} 个缺水")),
        ("cEmpty", new("{0} empty", "{0} 个空盆")),
        ("plantAll", new("Plant empty pots ({0})", "种植空盆（{0}）")),
        ("fertAll", new("Fertilize ({0})", "追肥（{0}）")),
        ("tendAll", new("Tend everything", "全部照料")),
        ("tHarvest", Harvest),
        ("tClear", Clear),
        ("tUproot", Uproot),
        ("tPests", Pests),
        ("tWeeds", Weeds),
        ("tWater", Water),
        ("fAll", new("All", "全部")),
        ("fRipe", new("Ripe", "成熟")),
        ("fCare", new("Needs care", "需要照料")),
        ("fEmpty", new("Empty", "空盆")),
        ("floor", new("Floor {0}", "{0} 楼")),
        ("here", new("you are here", "你在这里")),
        ("otherRoom", new("Elsewhere", "其他位置")),
        ("stEmpty", new("Empty", "空")),
        ("stGrowing", new("Growing", "生长中")),
        ("stRipe", new("Ripe", "成熟")),
        ("stWithered", new("Withered", "枯萎")),
        ("stPoor", new("Poor", "劣质")),
        ("pPests", new("Pests", "虫害")),
        ("pWeeds", new("Weeds", "杂草")),
        ("pDry", new("Dry", "缺水")),
        ("pDark", new("Too dark", "光照不足")),
        ("pBright", new("Too bright", "光照过强")),
        ("pCold", new("Too cold", "温度过低")),
        ("pFeed", new("Needs fertilizer today", "今天需要追肥")),
        ("ripeIn", new("Ripe in {0}", "{0}后成熟")),
        ("rotIn", new("Rots in {0}", "{0}后腐烂")),
        ("rotSoon", new("Rotting soon", "快腐烂")),
        ("noRot", new("Does not rot", "不会腐烂")),
        ("hours", new("{0} h", "{0} 小时")),
        ("fert", new("Fertilizer: {0}", "肥料：{0}")),
        ("noFert", new("No fertilizer", "无肥料")),
        ("robotHas", new("Robot's", "机器人负责")),
        ("queued", new("Walk queued", "已排队")),
        ("unpowered", new("No power", "未通电")),
        ("nextPlant", new("Plant uses: {0} x{1}", "种植将使用：{0} x{1}")),
        ("nextFert", new("Fertilize uses: {0}", "追肥将使用：{0}")),
        ("nextNone", new("No seed left for its preset", "该预设没有可用的种子")),
        ("plant", new("Plant", "种植")),
        ("fertilize", new("Fertilize", "追肥")),
        ("preset", new("Planting preset", "种植预设")),
        ("fromPot", new("This pot's own choice", "该种植盆自选")),
        ("fromRoom", new("From its room", "来自所在房间")),
        ("fromFloor", new("From its floor", "来自所在楼层")),
        ("fromAll", new("From the preset used everywhere", "来自用于所有地方的预设")),
        ("useDefault", new("Room or floor preset", "房间或楼层预设")),
        ("none", new("None", "无")),
        ("empty", new("No pots placed at home yet.", "家里还没有放置种植盆。")),
        ("pickPot", new("Pick a pot to see its timing, problems and next planting.", "选择一个种植盆以查看时间、问题和下次种植。")),
        ("pot", new("Pot", "种植盆")),
        ("slots", new("{0} slots", "{0} 格")),
        ("light", new("Light +{0}", "光照 +{0}")),
        ("heat", new("Heat +{0}", "温度 +{0}")),
        ("power", new("Needs power", "需要通电")),
        ("faster", new("Grows {0}% faster", "生长加快 {0}%")),
        ("timing", new("Timing", "时间")),
        ("grown", new("Grown {0}%", "已生长 {0}%")),
        ("selected", new("{0} selected", "已选 {0} 个")),
        ("clear", new("Clear", "清除")),
        ("assignTo", new("Planting preset for the selected pots:", "所选种植盆的种植预设：")),
        ("noSaveYet", new("Save the game once to pick presets per pot.", "保存一次游戏后即可为每个种植盆选择预设。")),
        ("confirm", new("Sure?", "确定？")),
        ("newPreset", new("New preset", "新建预设")),
        ("name", new("Name", "名称")),
        ("save", new("Save", "保存")),
        ("delete", new("Delete", "删除")),
        ("unsaved", new("Unsaved changes", "有未保存的更改")),
        ("seedsFor", new("Seeds for {0}", "{0}的种子")),
        ("seedNote", new("The first seed that fits the pot and that you have enough of is used.", "使用第一个适合该种植盆且数量足够的种子。")),
        ("noSteps", new("No seeds yet. Add some below.", "还没有种子，请在下方添加。")),
        ("keep", new("Keep at least", "至少保留")),
        ("own", new("Own {0}", "拥有 {0}")),
        ("perPot", new("{0} per pot", "每盆 {0} 个")),
        ("tooBig", new("Too big for this pot", "对这个种植盆太大")),
        ("addSeed", new("Add a seed", "添加种子")),
        ("add", new("Add", "添加")),
        ("search", new("Search", "搜索")),
        ("remove", new("Remove", "移除")),
        ("more", new("{0} more. Search to narrow the list.", "还有 {0} 个，请搜索以缩小范围。")),
        ("ferts", new("Fertilizer order", "肥料顺序")),
        ("fertNote", new("A crop without its own list below gets the first of these you have.", "下方没有单独列表的作物，使用其中你拥有的第一种。")),
        ("noFerts", new("No fertilizer in the order, so these crops are planted without any.", "顺序中没有肥料，这些作物种植时不追肥。")),
        ("addFert", new("Add a fertilizer", "添加肥料")),
        ("perCrop", new("Fertilizer per crop", "按作物设置肥料")),
        ("perCropNote", new("A crop listed here uses its own order first. Grow times are for a full pot with no other bonus.", "列在这里的作物优先使用自己的顺序。生长时间按满盆、无其他加成计算。")),
        ("whenOut", new("When none are left", "都用完时")),
        ("woPreset", new("Use the preset order", "使用预设顺序")),
        ("woNone", new("No fertilizer", "不追肥")),
        ("never", new("Never fertilized", "从不追肥")),
        ("addCrop", new("Add a crop", "添加作物")),
        ("baseTime", new("No fertilizer: {0}", "不追肥：{0}")),
        ("skipUnsuitable", new("Skip crops the pot's light or heat cannot grow", "跳过种植盆光照或温度无法种植的作物")),
        ("fertilizeOn", new("Fertilize when planting", "种植时追肥")),
        ("autoReplant", new("Replant empty pots by themselves", "空盆自动补种")),
        ("autoNote", new("Plants an empty pot from this preset on its own, as if you pressed Plant.", "会像按下「种植」一样，自动按此预设为空盆种植。")),
        ("usedFor", new("Used for", "用于")),
        ("useAll", new("Everywhere", "所有地方")),
        ("usedNote", new("A pot uses its own choice, else its room's, else its floor's, else the one used everywhere.", "种植盆优先使用自选，其次是所在房间、所在楼层，最后是用于所有地方的预设。")),
        ("on", new("On", "开")),
        ("off", new("Off", "关")),
        ("robotsNote", new("Each planter robot sows three pot sizes, with one seed and one fertilizer for each.", "每台种植机器人负责三种盆大小，每种一种种子和一种肥料。")),
        ("noRobots", new("No planter robots at home.", "家里没有种植机器人。")),
        ("battery", new("Battery {0}%", "电量 {0}%")),
        ("robotOff", new("Switched off", "已关闭")),
        ("tierCap", new("{0}-slot pots", "{0} 格种植盆")),
        ("noSeed", new("No seed", "无种子")),
        ("change", new("Change", "更改")),
        ("done", new("Done", "完成")),
        ("seed", new("Seed", "种子")),
        ("fertWord", new("Fertilizer", "肥料")),
        ("keepStocked", new("Keep robots stocked", "保持机器人补给")),
        ("keepNote", new("When a robot runs out of the seed for a pot size, switch it to the next seed in its floor's preset.", "机器人某种盆大小的种子用完时，换成其所在楼层预设中的下一种种子。")),
        ("comingUp", new("Next 48 hours", "未来 48 小时")),
        ("rots", new("Rots", "腐烂")),
        ("nothingSoon", new("Nothing ripens or rots in the next 48 hours.", "未来 48 小时内没有作物成熟或腐烂。")),
        ("stock", new("Seeds in your presets", "预设中的种子")),
        ("fills", new("Enough for {0} pot(s) of {1} slots", "够种 {0} 个 {1} 格种植盆")),
        ("rotWarn", new("Rotting soon means within", "「快腐烂」表示在以下时间内")),
        ("keyOpen", new("{0} opens and closes this window.", "按 {0} 打开或关闭此窗口。")),
        ("keyNone", new("No key is set; use the plant button on the left of the screen.", "未设置按键；请使用屏幕左侧的植物按钮。")),
        ("presetFile", new("Presets are kept in BepInEx/config/farm-hub.json, shared by every save.", "预设保存在 BepInEx/config/farm-hub.json，所有存档共用。")),
        ("escClose", new("Esc to close", "Esc 关闭")),
        ("tip", new("Your pots: ripe crops, problems and planting", "你的种植盆：成熟作物、问题和种植")),
    };
}
