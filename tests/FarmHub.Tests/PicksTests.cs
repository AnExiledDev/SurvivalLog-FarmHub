using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>Which seed and fertilizer a preset picks for a pot, and when it picks nothing.</summary>
public class PicksTests
{
    private static readonly PotKind SmallPot = Catalog.Kinds[Small];
    private static readonly PotKind BigPot = Catalog.Kinds[Big];

    private static FarmWorld World(params (int Item, int Count)[] stock)
    {
        var game = new FakeGame();

        foreach (var (item, count) in stock) game.Stock[item] = count;

        return game.World;
    }

    [Fact]
    public void The_first_seed_in_the_order_that_fills_the_pot_is_planted()
    {
        var preset = Preset("p1", new() { [2] = new[] { Step(TomatoSeed), Step(WheatSeed) } });
        var choice = Picks.Seed(P(1), SmallPot, preset, World((TomatoSeed, 1), (WheatSeed, 5)), new Dictionary<int, int>());

        Assert.Equal(new PlantChoice(TomatoSeed, Tomato, 1, 0), choice);
    }

    [Fact]
    public void A_seed_is_skipped_once_planting_it_would_go_below_its_keep()
    {
        var preset = Preset("p1", new() { [2] = new[] { Step(WheatSeed, keep: 3), Step(TomatoSeed) } });

        Assert.Equal(TomatoSeed, Picks.Seed(P(1), SmallPot, preset, World((WheatSeed, 4), (TomatoSeed, 1)), new Dictionary<int, int>())?.SeedItemId);
        Assert.Equal(WheatSeed, Picks.Seed(P(1), SmallPot, preset, World((WheatSeed, 5), (TomatoSeed, 1)), new Dictionary<int, int>())?.SeedItemId);
    }

    [Fact]
    public void A_crop_too_big_for_the_pot_is_never_picked()
    {
        var preset = Preset("p1", new() { [2] = new[] { Step(MelonSeed) } });

        Assert.Null(Picks.Seed(P(1), SmallPot, preset, World((MelonSeed, 9)), new Dictionary<int, int>()));
    }

    [Fact]
    public void A_crop_the_pot_cannot_grow_is_skipped_unless_the_preset_allows_it()
    {
        var preset = Preset("p1", new() { [2] = new[] { Step(TomatoSeed), Step(WheatSeed) } });
        var pot = P(1, unsuitable: Tomato);
        var world = World((TomatoSeed, 1), (WheatSeed, 2));

        Assert.Equal(WheatSeed, Picks.Seed(pot, SmallPot, preset, world, new Dictionary<int, int>())?.SeedItemId);
        Assert.Equal(TomatoSeed, Picks.Seed(pot, SmallPot, preset with { SkipUnsuitable = false }, world, new Dictionary<int, int>())?.SeedItemId);
    }

    [Fact]
    public void One_pass_never_plans_more_seed_than_the_player_owns()
    {
        var preset = Preset("p1", new() { [2] = new[] { Step(WheatSeed), Step(TomatoSeed) } });
        var world = World((WheatSeed, 3), (TomatoSeed, 1));
        var reserved = new Dictionary<int, int>();

        Assert.Equal(WheatSeed, Picks.Seed(P(1), SmallPot, preset, world, reserved)?.SeedItemId);
        Assert.Equal(TomatoSeed, Picks.Seed(P(2), SmallPot, preset, world, reserved)?.SeedItemId);
        Assert.Null(Picks.Seed(P(3), SmallPot, preset, world, reserved));
    }

    [Fact]
    public void A_crop_with_its_own_fertilizer_order_gets_that_first_then_the_presets()
    {
        var melonFirst = new CropFertilizer(Melon, new[] { Super }, WhenOut.PresetOrder);
        var preset = Preset("p1", new() { [4] = new[] { Step(MelonSeed) } }, ferts: new[] { Basic }, perCrop: new[] { melonFirst });

        Assert.Equal(Super, Picks.Seed(P(1, Big), BigPot, preset, World((MelonSeed, 1), (Basic, 9), (Super, 9)), new Dictionary<int, int>())?.FertId);
        Assert.Equal(Basic, Picks.Seed(P(1, Big), BigPot, preset, World((MelonSeed, 1), (Basic, 9)), new Dictionary<int, int>())?.FertId);
    }

    [Fact]
    public void A_crop_set_to_none_when_out_gets_nothing_rather_than_a_worse_fertilizer()
    {
        var melonOnly = new CropFertilizer(Melon, new[] { Super }, WhenOut.None);
        var preset = Preset("p1", new() { [4] = new[] { Step(MelonSeed) } }, ferts: new[] { Basic }, perCrop: new[] { melonOnly });

        Assert.Equal(0, Picks.Seed(P(1, Big), BigPot, preset, World((MelonSeed, 1), (Basic, 9)), new Dictionary<int, int>())?.FertId);
    }

    [Fact]
    public void A_preset_that_does_not_fertilize_uses_none()
    {
        var preset = Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }) with { Fertilize = false };

        Assert.Equal(0, Picks.Seed(P(1), SmallPot, preset, World((WheatSeed, 2), (Basic, 9)), new Dictionary<int, int>())?.FertId);
    }

    [Fact]
    public void A_robot_out_of_seed_moves_to_the_next_seed_its_floor_can_fill_and_a_blank_tier_is_left_alone()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 0, [TomatoSeed] = 3, [Basic] = 2 } };
        var book = Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed), Step(TomatoSeed) } }, ferts: new[] { Basic }));

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, WheatSeed, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        Assert.Equal(new[] { new TierSwitch(7, 0, TomatoSeed, Basic) }, Picks.RobotSwitches(game.World, book));
    }

    private static readonly Dictionary<(long Robot, int Tier), int> NobodySet = new();

    private static FakeGame OneRobot(int seedOnSmall, params (int Item, int Count)[] stock)
    {
        var game = new FakeGame();

        foreach (var (item, count) in stock) game.Stock[item] = count;

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, seedOnSmall, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        return game;
    }

    [Fact]
    public void An_empty_robot_tier_gets_the_first_pot_size_seed_it_can_fill_and_sizes_with_no_seeds_stay_empty()
    {
        var game = OneRobot(0, (TomatoSeed, 3), (WheatSeed, 5), (Basic, 2));
        var book = Book(Preset("p1", new() { [2] = new[] { Step(TomatoSeed), Step(WheatSeed) } }, ferts: new[] { Basic }));

        Assert.Equal(new[] { new TierSwitch(7, 0, TomatoSeed, Basic) }, Picks.RobotFills(game.World, book, NobodySet));
    }

    [Fact]
    public void A_seed_Farm_Hub_filled_moves_on_when_it_runs_out_and_the_same_seed_picked_by_the_player_stays()
    {
        var game = OneRobot(WheatSeed, (WheatSeed, 0), (TomatoSeed, 3));
        var book = Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed), Step(TomatoSeed) } }));
        var filledWheat = new Dictionary<(long Robot, int Tier), int> { [(7, 0)] = WheatSeed };
        var playerWheat = new Dictionary<(long Robot, int Tier), int> { [(7, 0)] = Picks.PlayerPicked };

        Assert.Equal(new[] { new TierSwitch(7, 0, TomatoSeed, 0) }, Picks.RobotFills(game.World, book, filledWheat));
        Assert.Empty(Picks.RobotFills(game.World, book, playerWheat));
        Assert.Empty(Picks.RobotSwitches(game.World, book, filledWheat));
    }

    [Fact]
    public void A_filled_tier_is_emptied_when_its_pot_size_has_no_seed_left()
    {
        var game = OneRobot(WheatSeed, (WheatSeed, 0), (Basic, 2));
        var book = Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }));
        var filled = new Dictionary<(long Robot, int Tier), int> { [(7, 0)] = WheatSeed };

        Assert.Equal(new[] { new TierSwitch(7, 0, 0, 0) }, Picks.RobotFills(game.World, book, filled));
    }

    // After a restart nothing is written down: a tier holding the seed the pot-size seeds pick now is taken as filled,
    // so it keeps following them, and a tier holding any other seed is taken as the player's.
    [Fact]
    public void A_tier_holding_the_current_pot_size_seed_is_noted_as_following_and_another_seed_is_not()
    {
        var book = Book(Preset("p1", new() { [2] = new[] { Step(TomatoSeed) } }));
        var holdsPick = OneRobot(TomatoSeed, (TomatoSeed, 3));
        var holdsOther = OneRobot(WheatSeed, (TomatoSeed, 3), (WheatSeed, 3));
        var pickNoted = new Dictionary<(long Robot, int Tier), int>();
        var otherNoted = new Dictionary<(long Robot, int Tier), int>();

        Picks.NoteFollowing(holdsPick.World, book, pickNoted);
        Picks.NoteFollowing(holdsOther.World, book, otherNoted);

        Assert.Equal(TomatoSeed, pickNoted[(7, 0)]);
        Assert.Empty(otherNoted);
    }
}
