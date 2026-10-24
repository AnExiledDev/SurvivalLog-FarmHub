using FarmHub.Core;

namespace FarmHub.Tests;

/// <summary>
/// A home with pots, robots and a stock of seeds and fertilizer, standing in for the game behind IFarmGame. Planting
/// takes the seeds and fertilizer from the stock and lines up a walk, and any pot can be made to refuse, so a test sees
/// what the player would.
/// </summary>
internal sealed class FakeGame : IFarmGame
{
    public const int Small = 1;
    public const int Big = 2;

    public const int Wheat = 100;
    public const int Melon = 101;
    public const int Tomato = 102;

    public const int WheatSeed = 200;
    public const int MelonSeed = 201;
    public const int TomatoSeed = 202;

    public const int Basic = 300;
    public const int Super = 301;

    public const int Greenhouse = 10;
    public const int Cellar = 20;

    public static readonly FarmCatalog Catalog = new(
        new Dictionary<int, PotKind>
        {
            [Small] = new(Small, "Small Planter", "Res/small.png", Capacity: 2, Light: 0f, Heat: 0f, NeedsPower: false, Faster: 0f),
            [Big] = new(Big, "Big Planter", null, Capacity: 4, Light: 20f, Heat: 5f, NeedsPower: true, Faster: 0.2f),
        },
        new Dictionary<int, Crop>
        {
            [Wheat] = new(Wheat, "Wheat", null, Size: 1, GrowHours: 48f, HarvestHours: 1f, NeedsDailyFeed: false),
            [Melon] = new(Melon, "Watermelon", "Res/melon.png", Size: 4, GrowHours: 120f, HarvestHours: 2f, NeedsDailyFeed: false),
            [Tomato] = new(Tomato, "Tomato", null, Size: 2, GrowHours: 72f, HarvestHours: 1f, NeedsDailyFeed: true),
        },
        new Dictionary<int, SeedItem>
        {
            [WheatSeed] = new(WheatSeed, "Wheat Seed", null, Wheat),
            [MelonSeed] = new(MelonSeed, "Watermelon Seed", null, Melon),
            [TomatoSeed] = new(TomatoSeed, "Tomato Seed", null, Tomato),
        },
        new Dictionary<int, Fertilizer>
        {
            [Basic] = new(Basic, "Compost", null, Speed: 0.2f),
            [Super] = new(Super, "Super Fertilizer", null, Speed: 0.5f),
        });

    public List<Pot> Pots { get; } = new();

    public List<Robot> Robots { get; } = new();

    public Dictionary<int, int> Stock { get; } = new();

    public int PlayerFloor { get; set; } = 1;

    public string? SaveKey { get; set; } = "slot-1";

    /// <summary>Set to make every read find no game or a game not ready, with its reason.</summary>
    public FarmRead? ReadInstead { get; set; }

    public HashSet<long> Refusing { get; } = new();

    /// <summary>What went in which pot, in the order the game was asked.</summary>
    public List<(long PotId, int SeedId, int Count, int FertId)> Planted { get; } = new();

    public List<(long PotId, int FertId)> Fertilized { get; } = new();

    public List<(long PotId, TendAction Action)> Tended { get; } = new();

    public int TendAllCount { get; private set; }

    /// <summary>Set to make the game turn down Tend Garden, as it does before the player has unlocked it.</summary>
    public bool IsRefusingTendAll { get; set; }

    public static Pot P(long id, int kind = Small, int floor = 1, int area = Greenhouse, PotState state = PotState.Empty, int plant = 0,
        Problems problems = Problems.None, float? ripeIn = null, float? rotIn = null, int fert = 0, bool pending = false, long robot = 0,
        params int[] unsuitable) =>
        new(id, kind, floor, area, state, plant, problems, state == PotState.Growing ? 0.5f : 0f, ripeIn, rotIn, fert, pending, robot, IsPowered: true, unsuitable.ToHashSet());

    public static SeedStep Step(int seed, int keep = 0) => new(seed, keep);

    /// <summary>A preset with these seed orders per pot capacity and this fertilizer order.</summary>
    public static FarmPreset Preset(string id, Dictionary<int, SeedStep[]> seeds, int[]? ferts = null, CropFertilizer[]? perCrop = null, bool autoReplant = false) =>
        new(id, "Preset " + id, seeds.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<SeedStep>)pair.Value), ferts ?? Array.Empty<int>(),
            perCrop ?? Array.Empty<CropFertilizer>(), SkipUnsuitable: true, Fertilize: true, autoReplant);

    /// <summary>A book whose first preset is used everywhere.</summary>
    public static PresetBook Book(params FarmPreset[] presets) => new(
        presets,
        new Dictionary<string, string> { [PresetBook.AllUse] = presets[0].Id },
        new Dictionary<string, IReadOnlyDictionary<long, string>>());

    public FarmWorld World => new(
        Catalog, Pots.ToList(), Robots.ToList(), new Dictionary<int, int>(Stock),
        new Dictionary<int, string> { [Greenhouse] = "Greenhouse", [Cellar] = "Cellar" }, PlayerFloor, SaveKey);

    public FarmRead Read() => ReadInstead ?? new FarmRead.Ready(World);

    public bool Plant(long potId, int plantId, int seedCount, int seedItemId, int fertId)
    {
        var at = Pots.FindIndex(pot => pot.Id == potId);

        if (Refusing.Contains(potId) || at < 0 || Own(seedItemId) < seedCount) return false;

        Stock[seedItemId] -= seedCount;

        if (fertId != 0) Stock[fertId] = Math.Max(0, Own(fertId) - Catalog.Kinds[Pots[at].KindId].Capacity);

        Pots[at] = Pots[at] with { IsPending = true };
        Planted.Add((potId, seedItemId, seedCount, fertId));

        return true;
    }

    public bool Fertilize(long potId, int fertId)
    {
        if (Refusing.Contains(potId) || Own(fertId) <= 0) return false;

        Fertilized.Add((potId, fertId));

        return true;
    }

    public bool Tend(long potId, TendAction action)
    {
        if (Refusing.Contains(potId)) return false;

        Tended.Add((potId, action));

        return true;
    }

    public bool TendAll()
    {
        TendAllCount++;

        return !IsRefusingTendAll;
    }

    public bool SetRobotTier(long robotId, int tier, int seedId, int fertId)
    {
        var at = Robots.FindIndex(robot => robot.Id == robotId);

        if (at < 0) return false;

        var tiers = Robots[at].Tiers.ToList();
        tiers[tier] = tiers[tier] with { SeedId = seedId, FertId = fertId };
        Robots[at] = Robots[at] with { Tiers = tiers };

        return true;
    }

    private int Own(int itemId) => Stock.TryGetValue(itemId, out var count) ? count : 0;
}
