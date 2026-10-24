using System.Text.Json;
using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>What the HUD's plant panel is told: each listed pot's place and need, the empty pots, and the Tend Garden plan.</summary>
public class HudViewTests
{
    private static PresetBook WheatEverywhere() => Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }));

    private static JsonElement Read(FarmWorld world, bool fromPresets = true, int rotWarnHours = 12) =>
        JsonDocument.Parse(HudView.Ready(world, WheatEverywhere(), rotWarnHours, fromPresets)).RootElement;

    private static Dictionary<long, long> Shown(params long[] potIds) => potIds.ToDictionary(id => id, id => 900 + id);

    [Fact]
    public void Tend_Garden_counts_one_chore_per_pot_and_skips_pots_a_walk_or_a_robot_already_has()
    {
        var game = new FakeGame();

        game.Pots.AddRange(new[]
        {
            P(1, state: PotState.Ripe, plant: Wheat),
            P(2, state: PotState.Ripe, plant: Wheat, pending: true),
            P(3, state: PotState.Growing, plant: Wheat, problems: Problems.Dry | Problems.Pests),
            P(4, state: PotState.Growing, plant: Wheat, problems: Problems.Weeds, robot: 9),
            P(5, state: PotState.Withered, plant: Wheat),
        });

        var chores = Read(game.World).GetProperty("chores");

        Assert.Equal(1, chores.GetProperty("harvest").GetInt32());
        Assert.Equal(1, chores.GetProperty("pests").GetInt32() + chores.GetProperty("water").GetInt32());
        Assert.Equal(0, chores.GetProperty("weeds").GetInt32());
        Assert.Equal(1, chores.GetProperty("clean").GetInt32());
        Assert.Equal(1, Read(game.World).GetProperty("replant").GetInt32());
    }

    [Fact]
    public void A_ripe_pot_is_promised_a_replant_seed_only_after_the_empty_pots_have_theirs()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 2 } };

        game.Pots.AddRange(new[] { P(1), P(2, state: PotState.Ripe, plant: Wheat) });

        var shortOfSeed = Read(game.World with { ShowIds = Shown(2) }).GetProperty("pots")[0];

        game.Stock[WheatSeed] = 4;

        var enough = Read(game.World with { ShowIds = Shown(2) }).GetProperty("pots")[0];

        Assert.Equal("902", shortOfSeed.GetProperty("show").GetString());
        Assert.Equal(0, shortOfSeed.GetProperty("then").GetInt32());
        Assert.Equal(WheatSeed, enough.GetProperty("then").GetInt32());
        Assert.Equal("harvest", enough.GetProperty("need").GetString());
    }

    [Fact]
    public void Only_pots_the_HUD_lists_get_a_line_and_the_empty_ones_come_with_their_seed()
    {
        var game = new FakeGame { Stock = { [WheatSeed] = 3 }, PlayerFloor = 1 };

        game.Pots.AddRange(new[] { P(1, floor: 2), P(2), P(3, state: PotState.Growing, plant: Wheat), P(4, state: PotState.Growing, plant: Wheat) });

        var hud = Read(game.World with { ShowIds = Shown(1, 2, 3) });
        var empty = hud.GetProperty("empty");

        Assert.Equal(new[] { "903" }, hud.GetProperty("pots").EnumerateArray().Select(p => p.GetProperty("show").GetString()));
        Assert.Equal(new[] { "2", "1" }, empty.EnumerateArray().Select(e => e.GetProperty("id").GetString()));
        Assert.Equal(WheatSeed, empty[0].GetProperty("seed").GetInt32());
        Assert.Equal(3, empty[0].GetProperty("count").GetInt32());
        Assert.Equal(0, empty[1].GetProperty("seed").GetInt32());
        Assert.Equal(1, hud.GetProperty("noSeed").GetInt32());
        Assert.Equal("Small Planter, Greenhouse", hud.GetProperty("noSeedName").GetString());
        Assert.Equal(1, hud.GetProperty("plant").GetInt32());
    }

    [Fact]
    public void A_pot_close_to_rotting_is_flagged_and_unknown_stamina_is_sent_as_minus_one()
    {
        var game = new FakeGame();

        game.Pots.AddRange(new[] { P(1, state: PotState.Ripe, plant: Wheat, rotIn: 5.04f), P(2, state: PotState.Ripe, plant: Wheat, rotIn: 30f) });

        var hud = Read(game.World with { ShowIds = Shown(1, 2), ChoreCost = 3 });
        var pots = hud.GetProperty("pots");

        Assert.True(pots[0].GetProperty("warn").GetBoolean());
        Assert.Equal(5.0, pots[0].GetProperty("rot").GetDouble());
        Assert.False(pots[1].GetProperty("warn").GetBoolean());
        Assert.Equal(-1, hud.GetProperty("stamina").GetInt32());
        Assert.Equal(3, hud.GetProperty("cost").GetInt32());
        Assert.Equal(41, Read(game.World with { Stamina = 41.7f }).GetProperty("stamina").GetInt32());
    }

    [Fact]
    public void The_need_is_the_first_chore_and_then_fertilizer()
    {
        Assert.Equal("harvest", HudView.Need(P(1, state: PotState.Ripe, plant: Wheat, problems: Problems.Pests)));
        Assert.Equal("feed", HudView.Need(P(2, state: PotState.Growing, plant: Tomato, problems: Problems.NeedsFeed)));
        Assert.Equal("", HudView.Need(P(3, state: PotState.Growing, plant: Wheat)));
    }
}
