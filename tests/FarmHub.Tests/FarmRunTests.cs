using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>The presses as the player makes them: what the game is asked to do, and the line the status bar shows.</summary>
public class FarmRunTests
{
    private static PresetBook WheatEverywhere(bool autoReplant = false) =>
        Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) }, [4] = new[] { Step(MelonSeed) } }, ferts: new[] { Basic }, autoReplant: autoReplant));

    private static string Say(FarmOutcome outcome) => FarmMessages.Describe(outcome, isChinese: false);

    [Fact]
    public void Plant_all_fills_empty_pots_on_the_players_floor_first_and_counts_what_had_no_seed()
    {
        var game = new FakeGame { PlayerFloor = 2, Stock = { [WheatSeed] = 2 } };

        game.Pots.AddRange(new[] { P(1), P(2, floor: 2), P(3, state: PotState.Growing, plant: Wheat), P(4, robot: 9) });

        var outcome = FarmRun.Plant(game, WheatEverywhere(), null);

        Assert.Equal(new long[] { 2 }, game.Planted.Select(p => p.PotId));
        Assert.Equal("Planting 1 pot. 1 had no seed left in their preset.", Say(outcome));
    }

    [Fact]
    public void Planting_one_pot_names_the_crop_and_says_why_when_it_cannot()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2 } };

        game.Pots.AddRange(new[] { P(1), P(2, state: PotState.Ripe, plant: Wheat), P(3, Big) });

        Assert.Equal("Planting Wheat in the Small Planter.", Say(FarmRun.Plant(game, WheatEverywhere(), new long[] { 1 })));
        Assert.Equal("The Small Planter is not empty.", Say(FarmRun.Plant(game, WheatEverywhere(), new long[] { 2 })));
        Assert.Equal("No seed left for the Big Planter (preset Preset p1).", Say(FarmRun.Plant(game, WheatEverywhere(), new long[] { 3 })));
        Assert.Equal("That pot is gone.", Say(FarmRun.Plant(game, WheatEverywhere(), new long[] { 99 })));
    }

    [Fact]
    public void A_pot_the_game_refuses_is_reported_and_the_rest_still_plant()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 9 }, Refusing = { 1 } };

        game.Pots.AddRange(new[] { P(1), P(2) });

        Assert.Equal("Planting 1 pot. The game refused 1.", Say(FarmRun.Plant(game, WheatEverywhere(), null)));
        Assert.Equal(new long[] { 2 }, game.Planted.Select(p => p.PotId));
    }

    [Fact]
    public void Fertilize_all_feeds_growing_pots_that_have_none()
    {
        var game = new FakeGame { Stock = { [Basic] = 9 } };

        game.Pots.AddRange(new[] { P(1, state: PotState.Growing, plant: Wheat), P(2, state: PotState.Growing, plant: Wheat, fert: Super), P(3) });

        Assert.Equal("Fertilizing 1 pot.", Say(FarmRun.Fertilize(game, WheatEverywhere(), null)));
        Assert.Equal(new[] { (1L, Basic) }, game.Fertilized);
    }

    [Fact]
    public void Tend_all_sends_the_games_tend_garden_then_plants_and_fertilizes_from_presets()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 9, [Basic] = 9 } };

        game.Pots.AddRange(new[] { P(1), P(2, state: PotState.Growing, plant: Wheat), P(3, state: PotState.Ripe, plant: Wheat) });

        Assert.Equal("Tend Garden sent. Planting 1 pot. Fertilizing 1 pot.", Say(FarmRun.TendAll(game, WheatEverywhere())));
        Assert.Equal(1, game.TendAllCount);
        Assert.Equal(new long[] { 1 }, game.Planted.Select(p => p.PotId));
        Assert.Equal(new[] { (2L, Basic) }, game.Fertilized);
    }

    [Fact]
    public void Tend_all_still_plants_when_the_game_turns_tend_garden_down()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 9 }, IsRefusingTendAll = true };

        game.Pots.Add(P(1));

        Assert.Equal("The game would not start Tend Garden. Planting 1 pot.", Say(FarmRun.TendAll(game, WheatEverywhere())));
        Assert.Equal(new long[] { 1 }, game.Planted.Select(p => p.PotId));
    }

    [Theory]
    [InlineData(PotState.Ripe, Problems.Pests, TendAction.Harvest)]
    [InlineData(PotState.Withered, Problems.None, TendAction.Clear)]
    [InlineData(PotState.Poor, Problems.None, TendAction.Uproot)]
    [InlineData(PotState.Growing, Problems.Pests | Problems.Dry, TendAction.Pests)]
    [InlineData(PotState.Growing, Problems.Weeds | Problems.Dry, TendAction.Weeds)]
    [InlineData(PotState.Growing, Problems.Dry, TendAction.Water)]
    public void Tend_does_the_chore_that_matters_most(PotState state, Problems problems, TendAction expected)
    {
        var game = new FakeGame();

        game.Pots.Add(P(1, state: state, plant: Wheat, problems: problems));
        FarmRun.Tend(game, 1);

        Assert.Equal(new[] { (1L, expected) }, game.Tended);
    }

    [Fact]
    public void A_healthy_pot_needs_no_tending()
    {
        var game = new FakeGame();

        game.Pots.Add(P(1, state: PotState.Growing, plant: Wheat, problems: Problems.TooDark));

        Assert.Equal("The Small Planter needs nothing right now.", Say(FarmRun.Tend(game, 1)));
        Assert.Empty(game.Tended);
    }

    [Fact]
    public void A_robot_tier_refuses_a_seed_too_big_for_its_pot_size()
    {
        var game = new FakeGame();

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, 0, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        Assert.Equal("The game would not do that for the Robot 1.", Say(FarmRun.SetRobotTier(game, 7, 0, MelonSeed, 0)));
        Assert.Equal("Robot 1: seed saved.", Say(FarmRun.SetRobotTier(game, 7, 1, MelonSeed, Super)));
        Assert.Equal(new RobotTier(4, MelonSeed, Super), game.Robots[0].Tiers[1]);
    }

    // The player never set a seed for this robot and left Keep robots stocked off: the pot-size seeds still drive it,
    // first Tomato (listed first, though Wheat is also in stock), then Wheat once the Tomato seed is gone.
    [Fact]
    public void An_empty_robot_tier_sows_the_pot_size_seeds_even_with_keep_stocked_off()
    {
        var game = new FakeGame { Stock = { [TomatoSeed] = 3, [WheatSeed] = 5, [Basic] = 2 } };
        var book = Book(Preset("p1", new() { [2] = new[] { Step(TomatoSeed), Step(WheatSeed) } }, ferts: new[] { Basic }));
        var filled = new Dictionary<(long Robot, int Tier), int>();

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, 0, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        Assert.True(FarmRun.HasAutoWork(game.World, book, 0f, new Dictionary<long, float>(), isKeepingRobotsStocked: false, filled));
        Assert.Equal("Robots: set a new seed for 1 pot size.", Say(FarmRun.Auto(game, book, 0f, new Dictionary<long, float>(), isKeepingRobotsStocked: false, filled)));
        Assert.Equal(new RobotTier(2, TomatoSeed, Basic), game.Robots[0].Tiers[0]);
        Assert.Equal(new RobotTier(4, 0, 0), game.Robots[0].Tiers[1]);

        game.Stock[TomatoSeed] = 0;
        FarmRun.Auto(game, book, 1f, new Dictionary<long, float>(), isKeepingRobotsStocked: false, filled);

        Assert.Equal(new RobotTier(2, WheatSeed, Basic), game.Robots[0].Tiers[0]);

        // With every listed seed gone the tier is emptied, and the toast says so instead of claiming a new seed.
        game.Stock[WheatSeed] = 0;

        var emptied = FarmRun.Auto(game, book, 2f, new Dictionary<long, float>(), isKeepingRobotsStocked: false, filled);

        Assert.True(FarmMessages.IsWorthShowing(emptied));
        Assert.Equal("Robots: out of seeds for 1 pot size.", Say(emptied));
        Assert.Equal(0, game.Robots[0].Tiers[0].SeedId);
    }

    // With Keep robots stocked off, a seed the player picked stays even when it runs out. Choosing Seeds by pot size
    // (seed 0) in the window hands the tier back, and the next pass fills it.
    [Fact]
    public void A_seed_the_player_picked_stays_until_they_hand_the_tier_back()
    {
        var game = new FakeGame { Stock = { [TomatoSeed] = 3, [WheatSeed] = 0 } };
        var book = Book(Preset("p1", new() { [2] = new[] { Step(TomatoSeed) } }));
        var filled = new Dictionary<(long Robot, int Tier), int>();

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, 0, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        FarmRun.SetRobotTier(game, 7, 0, WheatSeed, 0, filled);
        FarmRun.Auto(game, book, 0f, new Dictionary<long, float>(), isKeepingRobotsStocked: false, filled);

        Assert.Equal(new RobotTier(2, WheatSeed, 0), game.Robots[0].Tiers[0]);

        FarmRun.SetRobotTier(game, 7, 0, 0, 0, filled);
        FarmRun.Auto(game, book, 1f, new Dictionary<long, float>(), isKeepingRobotsStocked: false, filled);

        Assert.Equal(new RobotTier(2, TomatoSeed, 0), game.Robots[0].Tiers[0]);
    }

    [Fact]
    public void Auto_replant_plants_only_pots_whose_preset_asks_and_waits_a_minute_after_a_miss()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2 } };
        var book = WheatEverywhere(autoReplant: true);
        var retryAt = new Dictionary<long, float>();

        game.Pots.AddRange(new[] { P(1), P(2, Big) });

        var first = FarmRun.Auto(game, book, now: 0f, retryAt, isKeepingRobotsStocked: false);

        game.Stock[MelonSeed] = 1;

        var soon = FarmRun.Auto(game, book, now: 30f, retryAt, isKeepingRobotsStocked: false);
        var later = FarmRun.Auto(game, book, now: 61f, retryAt, isKeepingRobotsStocked: false);

        Assert.Equal("Auto replant: planting 1 pot.", Say(first));
        Assert.False(FarmMessages.IsWorthShowing(soon));
        Assert.Equal(new long[] { 1, 2 }, game.Planted.Select(p => p.PotId));
        Assert.True(FarmMessages.IsWorthShowing(later));
    }

    [Fact]
    public void Auto_replant_leaves_pots_alone_when_their_preset_does_not_ask()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2 } };

        game.Pots.Add(P(1));

        Assert.False(FarmMessages.IsWorthShowing(FarmRun.Auto(game, WheatEverywhere(), 0f, new Dictionary<long, float>(), false)));
        Assert.Empty(game.Planted);
    }

    [Fact]
    public void After_the_games_Tend_Garden_the_empty_pots_are_planted_and_the_growing_ones_fed_without_tending_again()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2, [Basic] = 4 } };

        game.Pots.AddRange(new[] { P(1), P(2, state: PotState.Growing, plant: Wheat), P(3, state: PotState.Ripe, plant: Wheat) });

        var outcome = FarmRun.FromPresets(game, WheatEverywhere());

        Assert.Equal(0, game.TendAllCount);
        Assert.Empty(game.Tended);
        Assert.Equal(new long[] { 1 }, game.Planted.Select(p => p.PotId));
        Assert.Equal(new long[] { 2 }, game.Fertilized.Select(p => p.PotId));
        Assert.True(FarmMessages.IsWorthShowing(outcome));
        Assert.Equal("Tend Garden, from your presets: Planting 1 pot. Fertilizing 1 pot.", Say(outcome));
    }

    [Fact]
    public void A_Tend_Garden_with_nothing_to_plant_or_feed_says_nothing()
    {
        var game = new FakeGame();

        game.Pots.Add(P(1, state: PotState.Ripe, plant: Wheat));

        Assert.False(FarmMessages.IsWorthShowing(FarmRun.FromPresets(game, WheatEverywhere())));
    }

    [Fact]
    public void Away_from_home_nothing_is_asked_of_the_game()
    {
        var game = new FakeGame { ReadInstead = new FarmRead.NotReady("you are away from home") };

        game.Pots.Add(P(1));

        Assert.Equal("Can't manage the farm right now: you are away from home.", Say(FarmRun.Plant(game, WheatEverywhere(), null)));
        Assert.Empty(game.Planted);
    }
}
