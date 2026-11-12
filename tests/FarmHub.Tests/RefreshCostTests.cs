using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>
/// What keeps a refresh cheap: the read split over frames, the views kept while nothing they show changed, and Auto
/// skipped when it has nothing to do. Each cache is checked both ways: kept when nothing changed, dropped when
/// something did.
/// </summary>
public class RefreshCostTests
{
    private static readonly ViewSettings Settings = new("G", 12, false);

    private static PresetBook WheatEverywhere(bool autoReplant = false) =>
        Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }, autoReplant: autoReplant));

    /// <summary>Pot 1 empty, pot 2 ripe, pot 4 growing; a new FakeGame each time, so every world is a new object.</summary>
    private static FakeGame Home(float ripeIn = 10f) => new()
    {
        Stock = { [WheatSeed] = 4, [Basic] = 2 },
        Pots = { P(1), P(2, state: PotState.Ripe, plant: Wheat, rotIn: 20f), P(4, area: Cellar, state: PotState.Growing, plant: Wheat, ripeIn: ripeIn) },
    };

    private static FarmWorld Seen(FakeGame game) =>
        game.World with { ShowIds = game.Pots.ToDictionary(pot => pot.Id, pot => 900 + pot.Id), Stamina = 40f, ChoreCost = 3 };

    [Fact]
    public void Two_reads_of_the_same_home_print_the_same_even_with_the_stock_in_another_order()
    {
        var first = Seen(Home());
        var again = Seen(Home()) with { Owned = new Dictionary<int, int> { [Basic] = 2, [WheatSeed] = 4 } };

        Assert.Equal(FarmPrint.Hud(first, 12, true), FarmPrint.Hud(again, 12, true));
        Assert.Equal(FarmPrint.Window(first, Settings), FarmPrint.Window(again, Settings));
    }

    [Fact]
    public void A_change_either_view_shows_changes_its_print()
    {
        var world = Seen(Home());
        var game = Home();

        game.Pots[0] = P(1, state: PotState.Growing, plant: Wheat);

        var planted = Seen(game);
        var moreSeed = world with { Owned = new Dictionary<int, int> { [WheatSeed] = 5, [Basic] = 2 } };
        var tired = world with { Stamina = 30f };

        foreach (var changed in new[] { planted, moreSeed })
        {
            Assert.NotEqual(FarmPrint.Hud(world, 12, true), FarmPrint.Hud(changed, 12, true));
            Assert.NotEqual(FarmPrint.Window(world, Settings), FarmPrint.Window(changed, Settings));
        }

        Assert.NotEqual(FarmPrint.Hud(world, 12, true), FarmPrint.Hud(tired, 12, true));
        Assert.NotEqual(FarmPrint.Hud(world, 12, true), FarmPrint.Hud(world, 12, false));
        Assert.NotEqual(FarmPrint.Hud(world, 12, true), FarmPrint.Hud(world, 24, true));
        Assert.NotEqual(FarmPrint.Window(world, Settings), FarmPrint.Window(world, Settings with { Key = "Shift+G" }));
    }

    [Fact]
    public void A_robots_battery_or_switch_changes_the_windows_print()
    {
        var game = Home();

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, WheatSeed, 0) }, 50f, 100f, IsOn: true));

        var world = Seen(game);
        var robot = world.Robots[0];
        var drained = world with { Robots = new[] { robot with { Battery = 20f } } };
        var switchedOff = world with { Robots = new[] { robot with { IsOn = false } } };
        var sameShown = world with { Robots = new[] { robot with { Battery = 50.2f } } };

        Assert.NotEqual(FarmPrint.Window(world, Settings), FarmPrint.Window(drained, Settings));
        Assert.NotEqual(FarmPrint.Window(world, Settings), FarmPrint.Window(switchedOff, Settings));
        Assert.Equal(FarmPrint.Window(world, Settings), FarmPrint.Window(sameShown, Settings));
    }

    // The same seed in the same tier shows differently when the player picked it (no pot-size seeds note), so who set
    // it is part of the print.
    [Fact]
    public void Who_set_a_robot_tier_changes_the_windows_print()
    {
        var game = Home();

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, WheatSeed, 0) }, 50f, 100f, IsOn: true));

        var world = Seen(game);
        var filled = new Dictionary<(long Robot, int Tier), int> { [(7, 0)] = WheatSeed };
        var picked = new Dictionary<(long Robot, int Tier), int> { [(7, 0)] = Picks.PlayerPicked };

        Assert.NotEqual(FarmPrint.Window(world, Settings, filled), FarmPrint.Window(world, Settings, picked));
        Assert.Equal(FarmPrint.Window(world, Settings, filled), FarmPrint.Window(world, Settings, new Dictionary<(long Robot, int Tier), int>(filled)));
    }

    [Fact]
    public void A_growing_crops_clock_moves_only_the_windows_print_and_only_by_the_tenth_of_an_hour_it_shows()
    {
        var world = Seen(Home(ripeIn: 10f));
        var sameTenth = Seen(Home(ripeIn: 10.02f));
        var later = Seen(Home(ripeIn: 3f));

        Assert.Equal(FarmPrint.Window(world, Settings), FarmPrint.Window(sameTenth, Settings));
        Assert.NotEqual(FarmPrint.Window(world, Settings), FarmPrint.Window(later, Settings));
        Assert.Equal(FarmPrint.Hud(world, 12, true), FarmPrint.Hud(later, 12, true));
    }

    [Fact]
    public void A_read_that_found_nothing_new_gets_the_very_same_views_back_without_building_them()
    {
        var views = new ViewCache();
        var book = WheatEverywhere();
        var hud = views.Hud(Seen(Home()), book, 12, true);
        var window = views.Window(Seen(Home()), book, Settings);

        Assert.Equal(2, views.Builds);
        Assert.Equal(HudView.Ready(Seen(Home()), book, 12, true), hud.Hud);
        Assert.Equal(FarmView.NeedsYou(Seen(Home()), book), hud.Badge);
        Assert.Equal(FarmView.Ready(Seen(Home()), book, Settings), window);

        var hudAgain = views.Hud(Seen(Home()), book, 12, true);

        Assert.Same(hud.Hud, hudAgain.Hud);
        Assert.Equal(hud.Badge, hudAgain.Badge);
        Assert.Same(window, views.Window(Seen(Home()), book, Settings));
        Assert.Equal(2, views.Builds);
    }

    [Fact]
    public void A_real_change_is_seen_and_drawn_as_a_fresh_build_would_draw_it()
    {
        var views = new ViewCache();
        var book = WheatEverywhere();
        var before = views.Hud(Seen(Home()), book, 12, true).Hud;
        var game = Home();

        game.Pots[1] = P(2, state: PotState.Ripe, plant: Wheat, rotIn: 2f);

        var after = views.Hud(Seen(game), book, 12, true).Hud;

        Assert.NotEqual(before, after);
        Assert.Equal(HudView.Ready(Seen(game), book, 12, true), after);
        Assert.Equal(FarmView.Ready(Seen(game), book, Settings), views.Window(Seen(game), book, Settings));
    }

    [Fact]
    public void The_views_are_built_again_for_a_new_catalog_a_new_save_a_new_book_and_after_Forget()
    {
        var book = WheatEverywhere();
        var world = Seen(Home());
        // A language change brings a new catalog with the same items under new names; here the same names, a new object.
        var changes = new (string What, FarmWorld World, PresetBook Book, bool IsForgotten)[]
        {
            ("catalog", world with { Catalog = world.Catalog with { } }, book, false),
            ("save", world with { SaveKey = "slot-2" }, book, false),
            ("book", world, WheatEverywhere(), false),
            ("forget", world, book, true),
        };

        foreach (var (what, changed, changedBook, isForgotten) in changes)
        {
            var views = new ViewCache();
            var hud = views.Hud(world, book, 12, true).Hud;
            var window = views.Window(world, book, Settings);

            if (isForgotten) views.Forget();

            Assert.True(!ReferenceEquals(hud, views.Hud(changed, changedBook, 12, true).Hud), what + ": HUD kept");
            Assert.True(!ReferenceEquals(window, views.Window(changed, changedBook, Settings)), what + ": window kept");
            Assert.Equal(4, views.Builds);
        }
    }

    [Fact]
    public void Auto_is_skipped_only_when_it_would_do_nothing()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2 } };
        var retryAt = new Dictionary<long, float>();

        game.Pots.AddRange(new[] { P(1), P(2, state: PotState.Growing, plant: Wheat) });

        Assert.False(FarmRun.HasAutoReplant(WheatEverywhere()));
        Assert.True(FarmRun.HasAutoReplant(WheatEverywhere(autoReplant: true)));
        Assert.False(FarmRun.HasAutoWork(game.World, WheatEverywhere(), 0f, retryAt, isKeepingRobotsStocked: false));
        Assert.True(FarmRun.HasAutoWork(game.World, WheatEverywhere(autoReplant: true), 0f, retryAt, isKeepingRobotsStocked: false));

        // A pot that just failed waits out its minute, and is worth a try again after it.
        retryAt[1] = 60f;

        Assert.False(FarmRun.HasAutoWork(game.World, WheatEverywhere(autoReplant: true), 30f, retryAt, isKeepingRobotsStocked: false));
        Assert.True(FarmRun.HasAutoWork(game.World, WheatEverywhere(autoReplant: true), 61f, retryAt, isKeepingRobotsStocked: false));
    }

    [Fact]
    public void Auto_is_not_skipped_while_a_robot_needs_its_seed_switched()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 0, [TomatoSeed] = 3, [Basic] = 2 } };
        var book = Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed), Step(TomatoSeed) } }, ferts: new[] { Basic }));

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, WheatSeed, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        Assert.True(FarmRun.HasAutoWork(game.World, book, 0f, new Dictionary<long, float>(), isKeepingRobotsStocked: true));
        Assert.False(FarmRun.HasAutoWork(game.World, book, 0f, new Dictionary<long, float>(), isKeepingRobotsStocked: false));
    }

    [Fact]
    public void The_warm_up_runs_every_view_and_press_without_throwing()
    {
        Assert.True(Warmup.Run() > 0);
    }
}
