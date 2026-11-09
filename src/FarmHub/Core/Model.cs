using System;
using System.Collections.Generic;

namespace FarmHub.Core;

/// <summary>What is wrong with a pot, as the game flags it (PlantAnomaly, without the ripe and withered bits).</summary>
[Flags]
public enum Problems
{
    None = 0,
    Pests = 1,
    Weeds = 2,
    Dry = 4,
    TooDark = 8,
    TooBright = 16,
    TooCold = 32,
    NeedsFeed = 256,
}

public enum PotState
{
    Empty,
    Growing,
    Ripe,
    Withered,
    Poor,
}

/// <summary>What the player can ask a worker to do at a pot, each one of the game's plant chores. Clear removes a withered crop, Uproot a poor one.</summary>
public enum TendAction
{
    Harvest,
    Clear,
    Uproot,
    Pests,
    Weeds,
    Water,
}

/// <summary>A kind of planter, from Config_FurniturePlant and Config_Furniture: how many seed slots it has and what it adds.</summary>
public sealed record PotKind(int Id, string Name, string? Icon, int Capacity, float Light, float Heat, bool NeedsPower, float Faster);

/// <summary>A crop, from Config_Plant. Size is the slots one seed takes; GrowHours is the base time before fertilizer and talents.</summary>
public sealed record Crop(int PlantId, string Name, string? Icon, int Size, float GrowHours, float HarvestHours, bool NeedsDailyFeed);

/// <summary>A seed item (Config_Item category 10) and the crop it grows.</summary>
public sealed record SeedItem(int ItemId, string Name, string? Icon, int PlantId);

/// <summary>A fertilizer item (category 11). Speed is its Plant_Fast: 0.5 makes a full pot grow 1.5 times as fast.</summary>
public sealed record Fertilizer(int ItemId, string Name, string? Icon, float Speed);

/// <summary>Everything that does not change while a save is played.</summary>
public sealed record FarmCatalog(
    IReadOnlyDictionary<int, PotKind> Kinds,
    IReadOnlyDictionary<int, Crop> Crops,
    IReadOnlyDictionary<int, SeedItem> Seeds,
    IReadOnlyDictionary<int, Fertilizer> Fertilizers)
{
    public string KindName(int kindId) => Kinds.TryGetValue(kindId, out var kind) ? kind.Name : $"Pot {kindId}";

    public string CropName(int plantId) => Crops.TryGetValue(plantId, out var crop) ? crop.Name : $"Crop {plantId}";

    public string ItemName(int itemId) =>
        Seeds.TryGetValue(itemId, out var seed) ? seed.Name : Fertilizers.TryGetValue(itemId, out var fert) ? fert.Name : $"Item {itemId}";

    /// <summary>The crop a seed grows, when the seed and its crop are both known.</summary>
    public Crop? CropOf(int seedItemId) => Seeds.TryGetValue(seedItemId, out var seed) && Crops.TryGetValue(seed.PlantId, out var crop) ? crop : null;
}

/// <summary>
/// A planter as the game holds it right now. HoursToRipe is set while it grows, HoursToRot while it is ripe and can
/// rot (null for a crop that never does). RobotId is the robot that has claimed it, 0 for none. Unsuitable lists the
/// crops the pot's light or temperature cannot grow.
/// </summary>
public sealed record Pot(
    long Id,
    int KindId,
    int Floor,
    int Area,
    PotState State,
    int PlantId,
    Problems Problems,
    float Progress,
    float? HoursToRipe,
    float? HoursToRot,
    int FertId,
    bool IsPending,
    long RobotId,
    bool IsPowered,
    IReadOnlySet<int> Unsuitable);

/// <summary>One of a planter robot's three sow settings: the pot size it covers, its seed and its fertilizer (0 for none).</summary>
public sealed record RobotTier(int Capacity, int SeedId, int FertId);

public sealed record Robot(long Id, string Name, int Floor, IReadOnlyList<RobotTier> Tiers, float Battery, float BatteryMax, bool IsOn);

/// <summary>A read of the home's farm and the player's seeds and fertilizer at one moment. Owned is by item id; AreaNames names the rooms the pots stand in.</summary>
public sealed record FarmWorld(
    FarmCatalog Catalog,
    IReadOnlyList<Pot> Pots,
    IReadOnlyList<Robot> Robots,
    IReadOnlyDictionary<int, int> Owned,
    IReadOnlyDictionary<int, string> AreaNames,
    int PlayerFloor,
    string? SaveKey)
{
    /// <summary>The player's stamina, null when it could not be read.</summary>
    public float? Stamina { get; init; }

    /// <summary>The stamina one plant chore costs (GlobalSetting_PlantActionCost), 0 when unknown.</summary>
    public int ChoreCost { get; init; }

    /// <summary>Each pot's id in the game's HUD, which lists pots by their shown model, by the pot's own id.</summary>
    public IReadOnlyDictionary<long, long> ShowIds { get; init; } = new Dictionary<long, long>();

    public int Own(int itemId) => Owned.TryGetValue(itemId, out var count) ? count : 0;

    public string AreaName(int area) => AreaNames.TryGetValue(area, out var name) ? name : "";
}

/// <summary>What a read of the game found: no loaded game, a game not ready to manage the farm (with why), or the farm.</summary>
public abstract record FarmRead
{
    private FarmRead()
    {
    }

    public sealed record NoGame : FarmRead;

    public sealed record NotReady(string Reason) : FarmRead;

    public sealed record Ready(FarmWorld World) : FarmRead;
}

/// <summary>The game's side of every press. Each call fails soft and answers false when the game refused or threw.</summary>
public interface IFarmGame
{
    FarmRead Read();

    /// <summary>Lines up planting the pot as the game's plant panel does, with fertilizer 0 for none.</summary>
    bool Plant(long potId, int plantId, int seedCount, int seedItemId, int fertId);

    /// <summary>Lines up a walk to put fertilizer in a growing pot.</summary>
    bool Fertilize(long potId, int fertId);

    /// <summary>Lines up one of the game's plant chores at the pot.</summary>
    bool Tend(long potId, TendAction action);

    /// <summary>The game's tend-everything patrol, as the HUD's plant button sends it.</summary>
    bool TendAll();

    /// <summary>Sets one sow tier of a planter robot: tier 0, 1 or 2, a seed item and a fertilizer item (0 for none).</summary>
    bool SetRobotTier(long robotId, int tier, int seedId, int fertId);
}
