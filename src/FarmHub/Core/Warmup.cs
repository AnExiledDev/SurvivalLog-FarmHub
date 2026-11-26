using System.Collections.Generic;

namespace FarmHub.Core;

/// <summary>
/// Runs every view, print, plan and press once on a small made-up home, so .NET compiles that code before the player
/// needs it. Meant for a background thread at start: the first refresh after a save loads then costs the game's frame
/// only the read itself. Touches nothing outside this class; the answer is the total length of what it wrote.
/// </summary>
public static class Warmup
{
    private const int Small = 1;
    private const int Wheat = 100;
    private const int WheatSeed = 200;
    private const int Compost = 300;
    private const int Room = 10;

    public static int Run()
    {
        var world = World();
        var preset = new FarmPreset(
            PresetBook.FirstPresetId, "Warm up",
            new Dictionary<int, IReadOnlyList<SeedStep>> { [2] = new[] { new SeedStep(WheatSeed, 0) } },
            new[] { Compost }, new[] { new CropFertilizer(Wheat, new[] { Compost }, WhenOut.PresetOrder) },
            SkipUnsuitable: true, Fertilize: true, AutoReplant: true);
        var book = new PresetBook(new[] { preset }, new Dictionary<string, string> { [PresetBook.AllUse] = preset.Id }, new Dictionary<string, IReadOnlyDictionary<long, string>>());
        var game = new Home(world);
        var settings = new ViewSettings("Shift+G", 12, IsKeepingRobotsStocked: true);
        var views = new ViewCache();
        var retryAt = new Dictionary<long, double>();
        var written = 0;

        written += CatalogView.Write(world.Catalog).Length;
        written += FarmView.Ready(world, book, settings).Length;
        written += FarmView.NotReady(Texts.NotReady.Say(false, Texts.Away.Pick(false)), settings).Length;
        written += FarmView.NeedsYou(world, book);
        written += HudView.Ready(world, book, 12, fromPresets: true).Length;
        written += HudView.NotReady().Length;
        written += views.Hud(world, book, 12, fromPresets: true).Hud.Length;
        written += views.Window(world, book, settings).Length;
        written += PresetJson.ReadBook(PresetJson.WriteBook(book))?.Presets.Count ?? 0;
        written += FarmRun.HasAutoWork(world, book, 0, retryAt, isKeepingRobotsStocked: true) ? 1 : 0;
        written += FarmMessages.Describe(FarmRun.Auto(game, book, 0, retryAt, isKeepingRobotsStocked: true), false).Length;
        written += FarmMessages.Describe(FarmRun.Plant(game, book, null), false).Length;
        written += FarmMessages.Describe(FarmRun.Fertilize(game, book, null), false).Length;
        written += FarmMessages.Describe(FarmRun.TendAll(game, book), false).Length;

        return written;
    }

    private static FarmWorld World()
    {
        var catalog = new FarmCatalog(
            new Dictionary<int, PotKind> { [Small] = new(Small, "Planter", null, 2, 0f, 0f, false, 0f) },
            new Dictionary<int, Crop> { [Wheat] = new(Wheat, "Wheat", null, 1, 48f, 1f, false) },
            new Dictionary<int, SeedItem> { [WheatSeed] = new(WheatSeed, "Wheat Seed", null, Wheat) },
            new Dictionary<int, Fertilizer> { [Compost] = new(Compost, "Compost", null, 0.2f) });
        var none = new HashSet<int>();
        var pots = new List<Pot>
        {
            new(1, Small, 1, Room, PotState.Empty, 0, Problems.None, 0f, null, null, 0, false, 0, true, none),
            new(2, Small, 1, Room, PotState.Growing, Wheat, Problems.Dry, 0.5f, 10f, null, 0, false, 0, true, none),
            new(3, Small, 2, Room, PotState.Ripe, Wheat, Problems.None, 1f, null, 3f, Compost, false, 0, true, none),
        };
        var robots = new List<Robot> { new(9, "Robot", 1, new[] { new RobotTier(2, WheatSeed, 0) }, 50f, 100f, true) };

        return new FarmWorld(catalog, pots, robots, new Dictionary<int, int> { [WheatSeed] = 5, [Compost] = 5 }, new Dictionary<int, string> { [Room] = "Greenhouse" }, 1, "warm-up")
        {
            Stamina = 50f,
            ChoreCost = 2,
            ShowIds = new Dictionary<long, long> { [2] = 20, [3] = 30 },
        };
    }

    /// <summary>A home that refuses every press, so the presses run all the way through without changing anything.</summary>
    private sealed class Home : IFarmGame
    {
        private readonly FarmWorld _world;

        public Home(FarmWorld world)
        {
            _world = world;
        }

        public FarmRead Read() => new FarmRead.Ready(_world);

        public bool Plant(long potId, int plantId, int seedCount, int seedItemId, int fertId) => false;

        public bool Fertilize(long potId, int fertId) => false;

        public bool Tend(long potId, TendAction action) => false;

        public bool TendAll() => false;

        public bool SetRobotTier(long robotId, int tier, int seedId, int fertId) => false;
    }
}
