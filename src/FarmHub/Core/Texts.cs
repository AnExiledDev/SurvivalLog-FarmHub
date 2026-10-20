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
        ("sumPots", new("{0} pots", "{0} 个种植盆")),
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
        ("show", new("Show", "显示")),
        ("fAll", new("All", "全部")),
        ("fRipe", new("Ripe", "成熟")),
        ("fRot", new("Rotting soon", "快腐烂")),
        ("fPests", new("Pests or weeds", "虫害或杂草")),
        ("fDry", new("Dry", "缺水")),
        ("fCare", new("Needs care", "需要照料")),
        ("fEmpty", new("Empty", "空盆")),
        ("fNoRobot", new("No robot", "无机器人")),
        ("crops", new("Crop", "作物")),
        ("rooms", new("Room", "房间")),
        ("groupBy", new("Group by", "分组")),
        ("gRoom", new("Room", "房间")),
        ("gCrop", new("Crop", "作物")),
        ("gState", new("State", "状态")),
        ("sortBy", new("Sort", "排序")),
        ("sRot", new("Rots first", "先看快腐烂")),
        ("sRipe", new("Ripe first", "先看成熟")),
        ("sName", new("Name", "名称")),
        ("noMatch", new("No pot matches these filters.", "没有符合筛选条件的种植盆。")),
        ("floor", new("Floor {0}", "{0} 楼")),
        ("here", new("your floor", "你所在楼层")),
        ("otherRoom", new("Elsewhere", "其他位置")),
        ("emptyPots", new("Empty pots", "空盆")),
        ("nPots", new("{0} pots", "{0} 个种植盆")),
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
        ("toRipe", new("{0} to ripe", "{0}后成熟")),
        ("beforeRot", new("{0} before it rots", "{0}后腐烂")),
        ("rotSoon", new("Rotting soon", "快腐烂")),
        ("noRot", new("Does not rot", "不会腐烂")),
        ("hours", new("{0} h", "{0} 小时")),
        ("noFert", new("No fertilizer", "无肥料")),
        ("robot", new("Robot", "机器人")),
        ("queued", new("Walk queued", "已排队")),
        ("unpowered", new("No power", "未通电")),
        ("nextShort", new("Next: {0} x{1}", "下次：{0} x{1}")),
        ("nextNone", new("No seed left for its preset", "该预设没有可用的种子")),
        ("noPreset", new("No preset", "无预设")),
        ("plant", new("Plant", "种植")),
        ("fertilize", new("Fertilize", "追肥")),
        ("preset", new("Planting preset", "种植预设")),
        ("fromPot", new("This pot's own choice", "该种植盆自选")),
        ("fromRoom", new("From its room", "来自所在房间")),
        ("fromFloor", new("From its floor", "来自所在楼层")),
        ("fromAll", new("From the preset used everywhere", "来自用于所有地方的预设")),
        ("useDefault", new("Room or floor preset", "房间或楼层预设")),
        ("empty", new("No pots placed at home yet.", "家里还没有放置种植盆。")),
        ("needsYou", new("Needs you", "需要你处理")),
        ("allFine", new("Nothing needs you right now.", "现在没有需要你处理的事。")),
        ("pickPot", new("Click a pot to see its timing, next planting and preset.", "点击一个种植盆，查看时间、下次种植和预设。")),
        ("pot", new("Pot", "种植盆")),
        ("size", new("Size", "大小")),
        ("light", new("Light +{0}", "光照 +{0}")),
        ("heat", new("Heat +{0}", "温度 +{0}")),
        ("power", new("Needs power", "需要通电")),
        ("faster", new("Grows {0}% faster", "生长加快 {0}%")),
        ("bonus", new("Bonus", "加成")),
        ("none", new("None", "无")),
        ("timing", new("Timing", "时间")),
        ("state", new("State", "状态")),
        ("ripeIn", new("Ripe in", "成熟还需")),
        ("rotIn", new("Rots in", "腐烂还需")),
        ("grown", new("Grown", "已生长")),
        ("fertWord", new("Fertilizer", "肥料")),
        ("fertNext", new("Next fertilizer", "下次追肥")),
        ("nextPlanting", new("Next planting", "下次种植")),
        ("nextBadge", new("next", "下次")),
        ("nSeeds", new("{0} seeds", "{0} 个种子")),
        ("keepN", new("keep {0}", "保留 {0}")),
        ("plantNote", new("Plants the first seed you have more of than its keep count.", "会种植第一种数量超过保留数的种子。")),
        ("selected", new("{0} selected", "已选 {0} 个")),
        ("clear", new("Clear", "清除")),
        ("assignTo", new("Preset:", "预设：")),
        ("noSaveYet", new("Save the game once to pick presets per pot.", "保存一次游戏后即可为每个种植盆选择预设。")),
        ("confirm", new("Sure?", "确定？")),
        ("newPreset", new("New preset", "新建预设")),
        ("name", new("Name", "名称")),
        ("save", new("Save", "保存")),
        ("delete", new("Delete", "删除")),
        ("unsaved", new("Unsaved changes", "有未保存的更改")),
        ("presetsHead", new("Presets", "预设")),
        ("usedNowhere", new("Not used yet", "尚未使用")),
        ("seedsBySize", new("Seeds by pot size", "按盆大小设置种子")),
        ("seedNote", new("The first seed you have more of than its keep count is planted.", "会种植第一种数量超过保留数的种子。")),
        ("szSmall", new("Small pots", "小种植盆")),
        ("szMedium", new("Medium pots", "中种植盆")),
        ("szLarge", new("Large pots", "大种植盆")),
        ("tierCap", new("{0}-slot pots", "{0} 格种植盆")),
        ("keep", new("keep", "保留")),
        ("tooBig", new("Too big for this pot", "对这个种植盆太大")),
        ("addSeed", new("+ Add seed", "+ 添加种子")),
        ("search", new("Search", "搜索")),
        ("more", new("{0} more. Search to narrow the list.", "还有 {0} 个，请搜索以缩小范围。")),
        ("nothingToAdd", new("Nothing to add. Search for a seed you do not have yet.", "没有可添加的。请搜索你还没有的种子。")),
        ("ferts", new("Fertilizer order", "肥料顺序")),
        ("fertNote", new("A crop without its own order below gets the first of these you have.", "下方没有单独顺序的作物，使用其中你拥有的第一种。")),
        ("noFerts", new("None: crops are planted without fertilizer.", "无：作物种植时不追肥。")),
        ("addFert", new("Add:", "添加：")),
        ("owned", new("{0} owned", "拥有 {0}")),
        ("perCrop", new("Fertilizer per crop", "按作物设置肥料")),
        ("perCropNote", new("A crop listed here uses its own order first.", "列在这里的作物优先使用自己的顺序。")),
        ("colCrop", new("Crop", "作物")),
        ("colOrder", new("Fertilizer, in order", "肥料（按顺序）")),
        ("colOut", new("When none are left", "都用完时")),
        ("colTime", new("Grow time", "生长时间")),
        ("woPreset", new("Preset order", "预设顺序")),
        ("woNone", new("None", "不追肥")),
        ("never", new("Never fertilize", "从不追肥")),
        ("addCrop", new("+ Add crop", "+ 添加作物")),
        ("skipUnsuitable", new("Skip crops the pot cannot grow", "跳过种植盆无法种植的作物")),
        ("skipNote", new("A seed whose crop needs more light or heat than the pot gives is passed over.", "需要的光照或温度超过种植盆所能提供的种子会被跳过。")),
        ("fertilizeOn", new("Fertilize when planting", "种植时追肥")),
        ("fertilizeNote", new("Uses the fertilizer order above.", "使用上面的肥料顺序。")),
        ("autoReplant", new("Replant empty pots by themselves", "空盆自动补种")),
        ("autoNote", new("Plants an empty pot from this preset on its own, as if you pressed Plant.", "会像按下「种植」一样，自动按此预设为空盆种植。")),
        ("usedFor", new("Used for", "用于")),
        ("useAll", new("Everywhere", "所有地方")),
        ("usedNote", new("A pot uses its own choice, else its room's, else its floor's, else the one used everywhere.", "种植盆优先使用自选，其次是所在房间、所在楼层，最后是用于所有地方的预设。")),
        ("willDo", new("What it will do", "会怎样种植")),
        ("plantingsLeft", new("{0} plantings left", "还能种 {0} 次")),
        ("ok", new("OK", "充足")),
        ("low", new("Low", "偏少")),
        ("out", new("Out", "用完")),
        ("planNote", new("Plantings left counts seeds above the keep counts. Two rounds for every pot is OK.", "「还能种」按超过保留数的种子计算。每个种植盆够种两轮即为充足。")),
        ("nextBySize", new("Next crop for each size", "各大小的下一种作物")),
        ("nothingLeft", new("No seed left", "没有种子了")),
        ("seedOut", new("{0}: not enough seed, skipped.", "{0}：种子不够，会被跳过。")),
        ("noPotsUse", new("No pot uses this preset yet.", "还没有种植盆使用此预设。")),
        ("keepStocked", new("Keep robots stocked", "保持机器人补给")),
        ("keepNote", new("When a robot runs out of the seed for a pot size, switch it to the next seed in its floor's preset.", "机器人某种盆大小的种子用完时，换成其所在楼层预设中的下一种种子。")),
        ("noRobots", new("No planter robots at home.", "家里没有种植机器人。")),
        ("battery", new("Battery {0}%", "电量 {0}%")),
        ("working", new("Working", "工作中")),
        ("robotOff", new("Switched off", "已关闭")),
        ("colSize", new("Size", "大小")),
        ("colSeed", new("Seed now", "当前种子")),
        ("noSeed", new("No seed", "无种子")),
        ("change", new("Change", "更改")),
        ("cancel", new("Cancel", "取消")),
        ("seed", new("Seed", "种子")),
        ("stockHead", new("Seeds and fertilizer", "种子和肥料")),
        ("stockNote", new("Per size: how many pots what you own would fill, after the keep counts.", "按大小：你拥有的数量（扣除保留数后）够种多少个种植盆。")),
        ("colItem", new("Item", "物品")),
        ("colHave", new("Have", "拥有")),
        ("comingUp", new("Next 48 hours", "未来 48 小时")),
        ("now", new("now", "现在")),
        ("rots", new("{0} rots", "{0} 腐烂")),
        ("nothingSoon", new("Nothing ripens or rots in the next 48 hours.", "未来 48 小时内没有作物成熟或腐烂。")),
        ("rotWarn", new("Rotting soon means within", "「快腐烂」表示在以下时间内")),
        ("rotWarnNote", new("Pots this close to rotting are marked red and counted on the plant button.", "这么快就要腐烂的种植盆会标红，并计入植物按钮上的数字。")),
        ("hotkey", new("Hotkey", "按键")),
        ("keyOpen", new("{0} opens and closes this window.", "按 {0} 打开或关闭此窗口。")),
        ("keyNone", new("No key is set; use the plant button on the left of the screen.", "未设置按键；请使用屏幕左侧的植物按钮。")),
        ("presetFile", new("Presets are kept in BepInEx/config/farm-hub.json, shared by every save.", "预设保存在 BepInEx/config/farm-hub.json，所有存档共用。")),
        ("escClose", new("Esc to close", "Esc 关闭")),
        ("tip", new("Your pots: ripe crops, problems and planting", "你的种植盆：成熟作物、问题和种植")),
    };
}
