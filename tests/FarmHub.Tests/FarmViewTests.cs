using System.Text.Json;
using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>What the window and the HUD count show about the farm: what needs the player and what a press would use.</summary>
public class FarmViewTests
{
    private static readonly ViewSettings Settings = new("G", RotWarnHours: 12, IsKeepingRobotsStocked: false);

    private static PresetBook WheatBook() => Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }));

    private static JsonElement View(FakeGame game, PresetBook book) => JsonDocument.Parse(FarmView.Ready(game.World, book, Settings)).RootElement;

    private static JsonElement Pot(JsonElement view, long id) => view.GetProperty("pots").EnumerateArray().Single(p => p.GetProperty("id").GetString() == id.ToString());

    [Fact]
    public void The_counts_say_what_needs_the_player()
    {
        var game = new FakeGame
        {
            Stock = { [WheatSeed] = 2, [Basic] = 5 },
            Pots =
            {
                P(1, state: PotState.Ripe, plant: Wheat, rotIn: 6f),
                P(2, state: PotState.Ripe, plant: Wheat, rotIn: 30f),
                P(3, state: PotState.Growing, plant: Wheat, problems: Problems.Pests | Problems.Dry),
                P(4),
                P(5),
            },
        };
        var counts = View(game, WheatBook()).GetProperty("counts");

        Assert.Equal((5, 2, 1, 1, 1, 2, 1, 1),
            (counts.GetProperty("total").GetInt32(), counts.GetProperty("ripe").GetInt32(), counts.GetProperty("rot").GetInt32(), counts.GetProperty("pests").GetInt32(),
                counts.GetProperty("dry").GetInt32(), counts.GetProperty("empty").GetInt32(), counts.GetProperty("plant").GetInt32(), counts.GetProperty("fert").GetInt32()));
    }

    [Fact]
    public void Two_empty_pots_never_both_show_the_last_seed()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2 }, Pots = { P(1), P(2) } };
        var view = View(game, WheatBook());

        Assert.Equal(WheatSeed, Pot(view, 1).GetProperty("next").GetProperty("seed").GetInt32());
        Assert.False(Pot(view, 2).TryGetProperty("next", out _));
        Assert.True(Pot(view, 2).GetProperty("canPlant").GetBoolean());
    }

    [Fact]
    public void The_hud_count_is_chores_plus_pots_a_press_would_plant()
    {
        var game = new FakeGame
        {
            Stock = { [WheatSeed] = 2 },
            Pots = { P(1, state: PotState.Ripe, plant: Wheat), P(2, state: PotState.Withered, plant: Wheat), P(3), P(4), P(5, state: PotState.Growing, plant: Wheat) },
        };

        Assert.Equal(3, FarmView.NeedsYou(game.World, WheatBook()));
    }

    [Fact]
    public void A_ripe_crop_is_rotting_soon_only_inside_the_warning_window()
    {
        Assert.True(FarmView.IsRottingSoon(P(1, state: PotState.Ripe, plant: Wheat, rotIn: 12f), 12));
        Assert.False(FarmView.IsRottingSoon(P(1, state: PotState.Ripe, plant: Wheat, rotIn: 12.5f), 12));
        Assert.False(FarmView.IsRottingSoon(P(1, state: PotState.Growing, plant: Wheat, rotIn: 1f), 12));
    }

    [Fact]
    public void A_pot_says_which_preset_it_follows_and_why()
    {
        var book = Book(Preset("p1", new()), Preset("p2", new())).Save(Preset("p2", new()), new[] { PresetBook.FloorUse(2) }).Assign("slot-1", new long[] { 3 }, "p1");
        var view = View(new FakeGame { Pots = { P(1), P(2, floor: 2), P(3, floor: 2) } }, book);

        Assert.Equal(("p1", "all"), (Pot(view, 1).GetProperty("preset").GetString(), Pot(view, 1).GetProperty("from").GetString()));
        Assert.Equal(("p2", "floor"), (Pot(view, 2).GetProperty("preset").GetString(), Pot(view, 2).GetProperty("from").GetString()));
        Assert.Equal(("p1", "pot"), (Pot(view, 3).GetProperty("preset").GetString(), Pot(view, 3).GetProperty("from").GetString()));
    }

    // Small pots hold Wheat, the seed the preset picks now, but the player chose it, so only the empty tiers say they
    // use the pot-size seeds. The robot names its floor's preset so the page can list those seeds.
    [Fact]
    public void A_robot_tier_with_no_seed_of_its_own_says_it_uses_the_pot_size_seeds()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 5 } };
        var picked = new Dictionary<(long Robot, int Tier), int> { [(7, 0)] = Picks.PlayerPicked };

        game.Robots.Add(new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, WheatSeed, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true));

        var robot = JsonDocument.Parse(FarmView.Ready(game.World, WheatBook(), Settings, picked)).RootElement.GetProperty("robots")[0];
        var follows = robot.GetProperty("tiers").EnumerateArray().Select(tier => tier.GetProperty("follow").GetBoolean());

        Assert.Equal("p1", robot.GetProperty("preset").GetString());
        Assert.Equal(new[] { false, true, true }, follows);
    }
}
