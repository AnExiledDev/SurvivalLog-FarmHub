using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>The additions to the game's plant panel in the HUD, driven through page.js in Jint on a fake of that panel.</summary>
public class HudPanelTests
{
    private static PresetBook WheatEverywhere() => Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }));

    /// <summary>Pot 2 ripe in the greenhouse, pot 4 growing dry in the cellar, and pot 1 empty; the HUD lists 2 then 4.</summary>
    private static FakeGame Home() => new()
    {
        Stock = { [WheatSeed] = 6 },
        Pots = { P(1), P(2, state: PotState.Ripe, plant: Wheat), P(4, area: Cellar, state: PotState.Growing, plant: Wheat, problems: Problems.Dry) },
    };

    private static FarmWorld Seen(FakeGame game) =>
        game.World with { ShowIds = game.Pots.ToDictionary(pot => pot.Id, pot => 900 + pot.Id), Stamina = 40f, ChoreCost = 3 };

    private static PageScript Panel(FakeGame game, bool fromPresets = true, bool isReplantOn = true, string rows = "[[902,'is-mature'],[904,'is-growing']]", bool hasReplant = true)
    {
        var page = new PageScript();

        page.Catalog(CatalogView.Write(game.World.Catalog));
        page.Live(true, "G");
        page.OpenHud();
        page.Run($"openPlantPanel({rows}, {(isReplantOn ? "true" : "false")}, {(hasReplant ? "true" : "false")})");
        page.Hud(HudView.Ready(Seen(game), WheatEverywhere(), 12, fromPresets));
        page.Poll();

        return page;
    }

    [Fact]
    public void Each_row_gets_the_line_for_its_own_pot_in_the_order_the_game_lists_them()
    {
        var page = Panel(Home());

        Assert.Equal("Greenhouse \u00b7 Small PlanterthenWheat", page.Text("rowSub(0)"));
        Assert.Contains("Cellar", page.Text("rowSub(1)"));
        Assert.Contains("Water", page.Text("rowSub(1)"));
        Assert.DoesNotContain("Cellar", page.Text("rowSub(0)"));
    }

    [Fact]
    public void A_ripe_row_promises_its_replant_only_when_the_games_Replant_box_and_ours_are_on()
    {
        Assert.Contains("Wheat", Panel(Home()).Text("rowSub(0)"));
        Assert.DoesNotContain("then", Panel(Home(), isReplantOn: false).Text("rowSub(0)"));
        Assert.DoesNotContain("then", Panel(Home(), fromPresets: false).Text("rowSub(0)"));
    }

    [Fact]
    public void The_empty_pots_sit_before_the_growing_rows_and_a_click_plants_one()
    {
        var page = Panel(Home());
        var order = page.Text("hudPage().querySelector('.mature-popover').children.map(function (n) { return n.classList.contains('fh-empties') ? 'empty' : n.classList.contains('is-growing') ? 'growing' : n.classList.contains('is-mature') ? 'ripe' : 'other'; }).join(',')");

        Assert.Equal("other,ripe,empty,growing", order);
        Assert.Contains("plant Wheat", page.Text("hudPage().querySelector('.fh-empty').textContent"));

        page.Click("hudPage().querySelector('.fh-empty')");

        Assert.Equal(new long[] { 1 }, Assert.IsType<PageCommand.Plant>(Assert.Single(page.Sent)).PotIds);
    }

    [Fact]
    public void The_from_presets_box_sits_by_the_games_Replant_box_and_sends_its_setting()
    {
        var page = Panel(Home());

        Assert.True(page.Is("hudPage().querySelector('.fh-presets-chk').querySelector('input').checked"));

        page.Run("var box = hudPage().querySelector('.fh-presets-chk').querySelector('input'); box.checked = false; fire(box, 'change')");

        Assert.Equal(new PageCommand.FromPresets(false), Assert.Single(page.Sent));
        Assert.False(Panel(Home(), hasReplant: false).Is("hudPage().querySelector('.fh-presets-chk')"));
    }

    [Fact]
    public void The_Tend_Garden_tip_lists_the_chores_and_the_stamina_they_take()
    {
        var plan = Panel(Home()).Text("hudPage().querySelector('.fh-plan').textContent");

        Assert.Contains("Harvest1", plan);
        Assert.Contains("Water1", plan);
        Assert.Contains("Plant empty pots1", plan);
        // Harvest and water, the replant's till and plant, and planting the empty pot: five chores at 3 stamina each.
        Assert.Contains("5 chores \u00b7 15 of 40 stamina", plan);
    }

    [Fact]
    public void Planting_from_the_presets_costs_stamina_only_when_Tend_Garden_does_it()
    {
        var plan = Panel(Home(), fromPresets: false).Text("hudPage().querySelector('.fh-plan').textContent");

        Assert.Contains("4 chores \u00b7 12 of 40 stamina", plan);
    }

    [Fact]
    public void Each_line_of_the_tip_shows_the_crop_its_chore_is_for()
    {
        var game = new FakeGame { Pots = { P(2, state: PotState.Ripe, plant: Melon), P(4, state: PotState.Growing, plant: Wheat, problems: Problems.Dry) } };
        var icons = Panel(game, fromPresets: false).Text("hudPage().querySelector('.fh-plan').querySelectorAll('img').map(function (i) { return i.src || 'none'; }).join(',')");

        // Harvest is for the melon, which has a picture; water is for the wheat, which has none.
        Assert.Equal("Res/melon.png,none", icons);
    }

    [Fact]
    public void Needs_you_hides_the_growing_rows_past_three_and_All_shows_them()
    {
        var page = Panel(Home(), rows: "[[902,'is-mature'],[904,'is-growing'],[905,'is-growing'],[906,'is-growing'],[907,'is-growing'],[908,'is-growing']]");

        Assert.True(page.Is("hudPage().getElementById('fh-filter-style')"));
        Assert.Contains("+ 2 more growing", page.Text("hudPage().querySelector('.fh-more').textContent"));
        Assert.Contains("Needs you (2)", page.Text("hudPage().querySelector('.fh-strip').textContent"));

        page.Click("hudPage().querySelector('.fh-more')");

        Assert.False(page.Is("hudPage().getElementById('fh-filter-style')"));
        Assert.False(page.Is("hudPage().querySelector('.fh-more')"));
    }

    [Fact]
    public void Out_of_the_home_the_panel_is_left_as_the_game_drew_it()
    {
        var page = Panel(Home());

        page.Live(false, "G");
        page.Poll();

        Assert.False(page.Is("hudPage().querySelector('.fh-hudadd')"));
        Assert.Equal(2, (int)double.Parse(page.Text("gameRowsNow().length")));
    }

    [Fact]
    public void The_tend_garden_tip_and_the_open_plant_list_draw_above_the_trap_panel_and_the_tool_column()
    {
        var page = Panel(Home());
        var css = page.Text("hudPage().getElementById('fh-panel-style').textContent");

        // The game's hover filter makes the plant row a stacking context, so the hovered panel is raised over the trap panel.
        Assert.Contains(".mature-panel:hover{z-index:1}", css);
        // The tool column (.vertical-tools) is at z-index 101; the game's top-left panel at 100.
        Assert.Contains(".top-left-panel:has(.mature-toggle-row.is-expanded),.top-left-panel:has(.mature-panel:hover){z-index:102}", css);
    }
}
