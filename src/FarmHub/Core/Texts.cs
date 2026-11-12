using System.Collections.Generic;
using ExiledToolkit.Kit;

namespace FarmHub.Core;

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
    public static readonly Phrase NoSeedCount = new("Pots with no seed left in their preset: {0}.", "预设中没有可用种子的种植盆：{0} 个。");
    public static readonly Phrase GameRefused = new("Pots the game refused: {0}.", "游戏拒绝的种植盆：{0} 个。");
    public static readonly Phrase PlantedOne = new("Planting {1} in the {0}.", "正在{0}中种植{1}。");
    public static readonly Phrase NothingToPlant = new("No empty pots to plant.", "没有可种植的空种植盆。");
    public static readonly Phrase NotEmpty = new("The {0} is not empty.", "{0}不是空的。");
    public static readonly Phrase NoPreset = new("The {0} has no planting preset. Pick one in the Pots tab.", "{0}没有种植预设。请在「种植盆」页选择一个。");
    public static readonly Phrase NoSeedFor = new("No seed left for the {0} (preset {1}).", "{0}没有可用的种子（预设「{1}」）。");
    public static readonly Counted Fertilizing = new(new("Fertilizing 1 pot.", "正在为 {0} 个种植盆追肥。"), new("Fertilizing {0} pots.", "正在为 {0} 个种植盆追肥。"));
    public static readonly Phrase NoFertCount = new("Pots with no fertilizer left in their preset: {0}.", "预设中没有可用肥料的种植盆：{0} 个。");
    public static readonly Phrase NothingToFertilize = new("No growing pot is waiting for fertilizer.", "没有等待追肥的生长中种植盆。");
    public static readonly Phrase Tending = new("{0}: {1}.", "{0}：{1}。");
    public static readonly Phrase TendingAll = new("Tend Garden sent.", "已开始「巡园」。");
    public static readonly Phrase TendAllRefused = new("The game would not start Tend Garden.", "游戏没有开始「巡园」。");
    public static readonly Phrase NothingToTend = new("The {0} needs nothing right now.", "{0}现在不需要照料。");
    public static readonly Phrase Refused = new("The game would not do that for the {0}.", "游戏拒绝了对{0}的操作。");
    public static readonly Counted RobotsSwitched = new(
        new("Robots: set a new seed for 1 pot size.", "机器人：已为 {0} 种大小的种植盆换了种子。"),
        new("Robots: set a new seed for {0} pot sizes.", "机器人：已为 {0} 种大小的种植盆换了种子。"));
    public static readonly Counted RobotsOutOfSeeds = new(
        new("Robots: out of seeds for 1 pot size.", "机器人：{0} 种大小的种植盆没有种子了。"),
        new("Robots: out of seeds for {0} pot sizes.", "机器人：{0} 种大小的种植盆没有种子了。"));
    public static readonly Phrase RobotGone = new("That robot is gone.", "这台机器人已经不在了。");
    public static readonly Phrase RobotNumber = new("Robot {0}", "机器人 {0}");
    public static readonly Phrase RobotTierSet = new("{0}: seed saved.", "{0}：种子已保存。");
    /// <summary>Who a toast is from, on its first line.</summary>
    public static readonly Phrase Source = new("Farm Hub", "农场中心");

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
    public static readonly Phrase AssignedOwn = new("Pots now using {1}: {0}.", "现在使用「{1}」的种植盆：{0} 个。");
    public static readonly Phrase AssignedDefault = new("Pots now using their room's or floor's preset: {0}.", "现在使用所在房间或楼层预设的种植盆：{0} 个。");
    public static readonly Phrase NoSave = new("Pot choices are kept per save, and this game has no save yet. Save the game first.", "种植盆的预设选择按存档保存，而这局游戏还没有存档。请先保存游戏。");
    public static readonly Phrase NotSaved = new("farm-hub.json could not be written, so the change lasts until the game is closed. See " + Log + ".", "无法写入 farm-hub.json，更改只在关闭游戏前有效。详见 " + Log + "。");
    public static readonly Phrase RotWarnSet = new("Rotting soon now means within {0} h.", "「快腐烂」现在表示 {0} 小时内。");
    public static readonly Phrase RobotsOn = new("When a seed you picked for a robot runs out, Farm Hub now switches the robot to the next seed in the preset.", "你为机器人选的种子用完时，Farm Hub 现在会换成预设中的下一种种子。");
    public static readonly Phrase FromPresetsOn = new("Tend Garden will also replant, plant and fertilize from your presets.", "「巡园」也会按你的预设补种、种植和追肥。");
    public static readonly Phrase FromPresetsOff = new("Tend Garden works like it normally does again.", "「巡园」恢复正常效果。");
    public static readonly Phrase FromPresetsDone = new("Tend Garden, from your presets:", "「巡园」，按你的预设：");
    public static readonly Phrase RobotsOff = new("When a seed you picked for a robot runs out, Farm Hub no longer switches the robot to another seed.", "你为机器人选的种子用完时，Farm Hub 不再换成别的种子。");

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
        ("tabSettings", new("Settings", "设置")),
        ("sumPots", new("{0} pots", "{0} 个种植盆")),
        ("cEmpty", new("{0} empty", "{0} 个空盆")),
        ("plantAll", new("Plant empty ({0})", "种植空盆（{0}）")),
        ("fertAll", new("Fertilize ({0})", "追肥（{0}）")),
        ("tendAll", new("Tend all ({0})", "全部照料（{0}）")),
        ("tHarvest", Harvest),
        ("tClear", Clear),
        ("tUproot", Uproot),
        ("tPests", Pests),
        ("tWeeds", Weeds),
        ("tWater", Water),
        ("fAll", new("All", "全部")),
        ("fEmpty", new("Empty", "空盆")),
        ("noMatch", new("No pot matches these filters.", "没有符合筛选条件的种植盆。")),
        ("floor", new("Floor {0}", "{0} 楼")),
        ("here", new("your floor", "你所在楼层")),
        ("otherRoom", new("Elsewhere", "其他位置")),
        ("nPots", new("{0} pots", "{0} 个种植盆")),
        ("onePot", new("1 pot", "1 个种植盆")),
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
        ("queued", new("Queued", "已排队")),
        ("unpowered", new("No power", "未通电")),
        ("nextShort", new("Next: {0} x{1}", "下次：{0} x{1}")),
        ("nextNone", new("No seed left in the preset", "预设中没有可用的种子")),
        ("noPreset", new("No preset", "无预设")),
        ("plant", new("Plant", "种植")),
        ("fertilize", new("Fertilize", "追肥")),
        ("fromPot", new("Picked for this pot", "为该种植盆单独选择")),
        ("fromRoom", new("From its room", "来自所在房间")),
        ("fromFloor", new("From its floor", "来自所在楼层")),
        ("fromAll", new("From the preset used everywhere", "来自用于所有地方的预设")),
        ("useDefault", new("Room or floor preset", "房间或楼层预设")),
        ("empty", new("No pots placed at home yet.", "家里还没有放置种植盆。")),
        ("needsYou", new("Needs you", "需要你处理")),
        ("allFine", new("Nothing needs you right now.", "现在没有需要你处理的事。")),
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
        ("plantNote", new("Plants the first seed on the list that you have more of than you keep.", "按顺序种植第一种数量超过保留数的种子。")),
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
        ("seedNote", new("Plants the first seed on the list that you have more of than you keep.", "按顺序种植第一种数量超过保留数的种子。")),
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
        ("skipNote", new("Seeds whose crop needs more light or heat than the pot gives are skipped.", "需要的光照或温度超过种植盆所能提供的种子会被跳过。")),
        ("fertilizeOn", new("Fertilize when planting", "种植时追肥")),
        ("fertilizeNote", new("Uses the fertilizer order above.", "使用上面的肥料顺序。")),
        ("autoReplant", new("Auto replant empty pots", "空盆自动补种")),
        ("autoNote", new("Plants an empty pot from this preset on its own, as if you pressed Plant.", "会像按下「种植」一样，自动按此预设为空盆种植。")),
        ("usedFor", new("Used for", "用于")),
        ("useAll", new("Everywhere", "所有地方")),
        ("usedNote", new("A pot uses the preset picked for it first, then its room's, then its floor's, then the one used everywhere.", "种植盆优先使用为它单独选择的预设，其次是所在房间、所在楼层，最后是用于所有地方的预设。")),
        ("willDo", new("What this preset does", "此预设会怎样种植")),
        ("plantingsLeft", new("{0} plantings left", "还能种 {0} 次")),
        ("ok", new("OK", "充足")),
        ("low", new("Low", "偏少")),
        ("out", new("Out", "用完")),
        ("planNote", new("Plantings left counts only the seeds above the keep counts. OK means you have enough to plant every pot twice.", "「还能种」只计算超过保留数的种子。「充足」表示够每个种植盆种两轮。")),
        ("nextBySize", new("Next crop for each size", "各大小的下一种作物")),
        ("nothingLeft", new("No seed left", "没有种子了")),
        ("seedOut", new("{0}: not enough seeds, so it is skipped.", "{0}：种子不够，会被跳过。")),
        ("noPotsUse", new("No pot uses this preset yet.", "还没有种植盆使用此预设。")),
        ("keepStocked", new("Keep robots stocked", "保持机器人补给")),
        ("keepNote", new("When a seed you picked for a robot runs out, switch it to the next seed in the preset for the robot's floor.", "你为机器人选的种子用完时，换成机器人所在楼层预设中的下一种种子。")),
        ("noRobots", new("No planter robots at home.", "家里没有种植机器人。")),
        ("battery", new("Battery {0}%", "电量 {0}%")),
        ("working", new("Working", "工作中")),
        ("robotOff", new("Switched off", "已关闭")),
        ("colSize", new("Size", "大小")),
        ("colSeed", new("Seed now", "当前种子")),
        ("noSeed", new("No seed", "无种子")),
        ("sizeSeeds", new("Seeds by pot size", "按盆大小的种子")),
        ("sizeSeedsNote", new("The robot plants the seeds listed for this pot size in the preset for its floor, in that order, with the preset's fertilizer.", "机器人会按顺序种植所在楼层预设中为这种盆大小列出的种子，并使用该预设的肥料。")),
        ("followSeeds", new("Uses the seeds for this pot size from preset {0}: {1}", "使用预设「{0}」中这种盆大小的种子：{1}")),
        ("followNone", new("Preset {0} has no seeds for this pot size, so the robot plants nothing here. Add some on the Planting presets tab.", "预设「{0}」没有这种盆大小的种子，所以机器人在这里什么都不种。请在「种植预设」页添加。")),
        ("followOut", new("Preset {0} is out of seeds for this pot size: {1}", "预设「{0}」中这种盆大小的种子都用完了：{1}")),
        ("pickedNote", new("You picked this seed, so the robot doesn't use the preset's seeds for this pot size. To go back to them, press Change and pick \"Seeds by pot size\".", "这是你选的种子，所以机器人在这种盆大小不用预设的种子。要改回来，请点「更改」并选择「按盆大小的种子」。")),
        ("change", new("Change", "更改")),
        ("cancel", new("Cancel", "取消")),
        ("seed", new("Seed", "种子")),
        ("stockHead", new("Seeds and fertilizer", "种子和肥料")),
        ("stockNote", new("How many pots of each size you can fill with what you have, not counting what you keep.", "你现有的种子和肥料（不算保留的部分）够种每种大小的多少个种植盆。")),
        ("colItem", new("Item", "物品")),
        ("colHave", new("Have", "拥有")),
        ("comingUp", new("Next 48 hours", "未来 48 小时")),
        ("nothingSoon", new("Nothing ripens or rots in the next 48 hours.", "未来 48 小时内没有作物成熟或腐烂。")),
        ("rotWarn", new("Rotting soon means within", "「快腐烂」表示在以下时间内")),
        ("rotWarnNote", new("Pots this close to rotting are marked red and counted on the plant button.", "这么快就要腐烂的种植盆会标红，并计入植物按钮上的数字。")),
        ("hotkey", new("Hotkey", "按键")),
        ("keyOpen", new("{0} opens and closes this window.", "按 {0} 打开或关闭此窗口。")),
        ("keyNone", new("No key is set. Use the plant button on the left of the screen.", "未设置按键。请使用屏幕左侧的植物按钮。")),
        ("presetFile", new("Presets are kept in BepInEx/config/farm-hub.json, shared by every save.", "预设保存在 BepInEx/config/farm-hub.json，所有存档共用。")),
        ("escClose", new("Esc to close", "Esc 关闭")),
        ("fNeeds", new("Needs you", "需要你处理")),
        ("fGrowing", new("Growing", "生长中")),
        ("cNeed", new("{0} need you", "{0} 个需要你处理")),
        ("keyTend", new("Tend Garden (game)", "「巡园」（游戏）")),
        ("keyThis", new("this window", "此窗口")),
        ("nJobs", new("{0} jobs", "{0} 项")),
        ("todoCare", new("Clear, weed or water", "清理、除草或浇水")),
        ("todoSpot", new("Bad spot", "位置不佳")),
        ("todoPlant", new("Plant empty pots", "种植空盆")),
        ("inHours", new("in {0}", "{0}后")),
        ("readyPill", new("Ripens", "成熟")),
        ("rotsPill", new("Rots", "腐烂")),
        ("tendNote", new("Harvests, clears, weeds and waters like the game's Tend Garden, then plants and fertilizes from your presets.", "像游戏的「巡园」一样收获、清理、除草和浇水，然后按你的预设种植和追肥。")),
        ("backNeeds", new("Needs you", "需要你处理")),
        ("thisPot", new("This pot", "这个种植盆")),
        ("afterHarvest", new("After harvest", "收获之后")),
        ("szS", new("S", "小")),
        ("szM", new("M", "中")),
        ("szL", new("L", "大")),
        ("keysHead", new("Keys", "按键")),
        ("warnHead", new("Warnings", "提醒")),
        ("otherHead", new("Other", "其他")),
        ("gTwice", new("G opens this window and also runs the game's Tend Garden. Set PanelKeyShift to true to open it with Shift+G instead.", "G 会打开此窗口，同时也会触发游戏的「巡园」。将 PanelKeyShift 设为 true 后改用 Shift+G 打开。")),
        ("tip", new("Your pots: ripe crops, problems and planting", "你的种植盆：成熟作物、问题和种植")),
        ("fromPresets", new("from presets", "按预设")),
        ("hNextTend", new("Next Tend:", "下次巡园：")),
        ("hChores", new("{0} chores", "{0} 项")),
        ("hStamina", new("{0} of {1} stamina", "体力 {0}/{1}")),
        ("hThen", new("then plant {0}, fertilize {1}", "然后种植 {0}、追肥 {1}")),
        ("hShow", new("Show", "显示")),
        ("hNeeds", new("Needs you ({0})", "需要你处理（{0}）")),
        ("hAll", new("All ({0})", "全部（{0}）")),
        ("hMore", new("+ {0} more growing", "还有 {0} 株生长中")),
        ("hEmptyHead", new("Empty pots: plant from your presets", "空盆 \u00b7 按你的预设种植")),
        ("hGrowingHead", new("Growing: ripe soonest", "生长中 \u00b7 即将成熟")),
        ("hEmptyPot", new("Empty pot", "空盆")),
        ("hPlantSeed", new("plant {0}", "种植{0}")),
        ("hNoSeed", new("no seed", "没有种子")),
        ("hSeeds", new("{0} seeds", "{0} 颗种子")),
        ("hRotsIn", new("rots in {0}", "{0}后腐烂")),
        ("hThenCrop", new("then", "然后")),
        ("hClean", new("Clean up", "清理")),
        ("hNeedsFert", new("needs fertilizer", "需要追肥")),
        ("hThenHead", new("Then, from your presets", "然后，按你的预设")),
        ("hReplant", new("Replant", "补种")),
        ("hPlantEmpty", new("Plant empty pots", "种植空盆")),
        ("hShort", new("Not enough stamina for every chore, so some will wait.", "体力不够做完所有事，剩下的之后再做。")),
        ("hNoSeedFor", new("No seed left for {0}", "{0}没有可用的种子")),
        ("hNoSeedMore", new("No seed left for {0} and {1} more", "{0}等 {1} 个种植盆没有可用的种子")),
        ("hIdle", new("Nothing to tend right now.", "现在没有需要照料的。")),
    };
}
