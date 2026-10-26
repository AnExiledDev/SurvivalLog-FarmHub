using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>
/// What page.js costs while nothing changes: a poll that finds everything in place searches nothing and draws nothing,
/// and data that did not change keeps the nodes drawn from it. Searches are counted by the fake page, so a cache that
/// stops working shows up here as a search.
/// </summary>
public class PageCostTests
{
    private static readonly ViewSettings Settings = new("G", 12, false);

    private static PresetBook WheatEverywhere() => Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }));

    /// <summary>Pot 2 ripe, pot 4 growing dry in the cellar, pot 1 empty; the HUD lists 2 then 4.</summary>
    private static FakeGame Home() => new()
    {
        Stock = { [WheatSeed] = 6, [Basic] = 3 },
        Pots = { P(1), P(2, state: PotState.Ripe, plant: Wheat), P(4, area: Cellar, state: PotState.Growing, plant: Wheat, problems: Problems.Dry) },
    };

    private static FarmWorld Seen(FakeGame game) =>
        game.World with { ShowIds = game.Pots.ToDictionary(pot => pot.Id, pot => 900 + pot.Id), Stamina = 40f, ChoreCost = 3 };

    private static string HudOf(FakeGame game) => HudView.Ready(Seen(game), WheatEverywhere(), 12, true);

    private static PageScript Panel(FakeGame game)
    {
        var page = new PageScript();

        page.Catalog(CatalogView.Write(game.World.Catalog));
        page.Live(true, "G");
        page.OpenHud();
        page.Run("openPlantPanel([[902,'is-mature'],[904,'is-growing']], true, true)");
        page.Hud(HudOf(game));
        page.Poll();

        return page;
    }

    /// <summary>How many searches the next polls make, with nothing changed in between.</summary>
    private static int SearchesOver(PageScript page, int polls)
    {
        page.Run("searches = 0");

        for (var i = 0; i < polls; i++) page.Poll();

        return int.Parse(page.Text("searches"));
    }

    [Fact]
    public void With_the_plant_list_open_a_poll_that_finds_nothing_changed_searches_nothing_and_sends_nothing()
    {
        var page = Panel(Home());
        var notes = page.Notes.Count;

        page.Run("var kept = [hudButton(), hudPage().querySelector('.fh-strip'), hudPage().querySelector('.fh-plan'), hudPage().querySelector('.fh-empties'), rowSubNode(0), rowSubNode(1)]");

        Assert.Equal(0, SearchesOver(page, 3));
        Assert.Empty(page.Sent);
        Assert.Equal(notes, page.Notes.Count);
        Assert.True(page.Is("kept.every(function (n) { return n !== null; })"));
        Assert.True(page.Is("[hudButton(), hudPage().querySelector('.fh-strip'), hudPage().querySelector('.fh-plan'), hudPage().querySelector('.fh-empties'), rowSubNode(0), rowSubNode(1)].every(function (n, i) { return n === kept[i]; })"));
    }

    [Fact]
    public void With_the_plant_list_closed_a_poll_searches_nothing_and_the_tip_keeps_its_plan()
    {
        var page = Panel(Home());

        page.Run("closePlantList()");
        page.Poll();

        Assert.Equal(0, SearchesOver(page, 3));
        Assert.True(page.Is("hudPage().querySelector('.fh-plan')"));
        Assert.False(page.Is("hudPage().getElementById('fh-filter-style')"));
    }

    [Fact]
    public void Outside_a_game_a_poll_searches_nothing_once_what_was_placed_is_gone()
    {
        var page = new PageScript();

        page.OpenHud();

        Assert.Equal(0, SearchesOver(page, 2));

        page = Panel(Home());
        page.Live(false, "G");
        page.Poll();

        Assert.False(page.Is("hudButton()"));
        Assert.False(page.Is("hudPage().querySelector('.fh-hudadd')"));
        Assert.Equal(0, SearchesOver(page, 2));
    }

    [Fact]
    public void A_new_count_or_key_is_written_into_the_same_HUD_button()
    {
        var page = new PageScript();

        page.OpenHud();
        page.Live(true, "G");
        page.Badge(3);
        page.Run("var button = hudButton()");
        page.Badge(5);

        Assert.True(page.Is("button === hudButton()"));
        Assert.Equal("5", page.Text("button.querySelector('.fh-hud-badge').textContent"));

        page.Badge(120);

        Assert.Equal("99+", page.Text("button.querySelector('.fh-hud-badge').textContent"));

        page.Badge(0);

        Assert.False(page.Is("button.querySelector('.fh-hud-badge')"));

        page.Live(true, "Shift+G");

        Assert.True(page.Is("button === hudButton()"));
        Assert.Equal("Shift+G", page.Text("button.querySelector('.entry-tip-key').textContent"));

        page.Live(true, "");

        Assert.False(page.Is("button.querySelector('.entry-tip-key')"));
    }

    [Fact]
    public void A_new_HUD_page_gets_the_button_again()
    {
        var page = new PageScript();

        page.OpenHud();
        page.Live(true, "G");
        page.Badge(2);
        page.Run("swapHud()");
        page.Poll();

        Assert.True(page.Is("hudPage().querySelector('.vertical-tools').firstChild === hudButton()"));
        Assert.Equal("2", page.Text("hudButton().querySelector('.fh-hud-badge').textContent"));
        Assert.Equal("1", page.Text("hudPage().querySelectorAll('.fh-hud-btn').length"));
    }

    [Fact]
    public void The_same_HUD_data_again_draws_no_row_line_again()
    {
        var page = Panel(Home());

        page.Run("var first = rowSubNode(0), second = rowSubNode(1), plan = hudPage().querySelector('.fh-plan'), strip = hudPage().querySelector('.fh-strip')");
        page.Hud(HudOf(Home()));
        page.Poll();

        Assert.True(page.Is("first === rowSubNode(0) && second === rowSubNode(1)"));
        Assert.True(page.Is("plan === hudPage().querySelector('.fh-plan') && strip === hudPage().querySelector('.fh-strip')"));
    }

    [Fact]
    public void A_pot_that_changed_gets_its_line_drawn_again_and_the_others_keep_theirs()
    {
        var page = Panel(Home());
        var watered = Home();

        Assert.Contains("Water", page.Text("rowSub(1)"));

        watered.Pots[2] = P(4, area: Cellar, state: PotState.Growing, plant: Wheat);
        page.Run("var first = rowSubNode(0), second = rowSubNode(1)");
        page.Hud(HudOf(watered));
        page.Poll();

        Assert.True(page.Is("first === rowSubNode(0)"));
        Assert.False(page.Is("second === rowSubNode(1)"));
        Assert.DoesNotContain("Water", page.Text("rowSub(1)"));
        // One line under each row, the old one taken out.
        Assert.Equal("1", page.Text("gameRowsNow()[1].querySelectorAll('.fh-sub').length"));
    }

    [Fact]
    public void A_new_language_draws_the_panel_and_the_button_tip_again()
    {
        var page = Panel(Home());

        page.Run("var strip = hudPage().querySelector('.fh-strip'), first = rowSubNode(0), tip = hudButton().querySelector('.entry-tip-body').textContent");
        page.Run(PageWords.Script(isChinese: true));
        page.Poll();

        Assert.False(page.Is("strip === hudPage().querySelector('.fh-strip')"));
        Assert.False(page.Is("first === rowSubNode(0)"));
        Assert.False(page.Is("tip === hudButton().querySelector('.entry-tip-body').textContent"));
    }

    [Fact]
    public void In_the_window_a_refresh_draws_only_the_rows_of_pots_that_changed()
    {
        var game = Home();
        var page = new PageScript();

        page.Catalog(CatalogView.Write(game.World.Catalog));
        page.Open(FarmView.Ready(game.World, WheatEverywhere(), Settings));
        page.Run("var one = panelButton('pick','1'), four = panelButton('pick','4')");

        game.Pots[2] = P(4, area: Cellar, state: PotState.Growing, plant: Wheat);
        page.SetData(FarmView.Ready(game.World, WheatEverywhere(), Settings));

        Assert.True(page.Is("one === panelButton('pick','1')"));
        Assert.True(page.Is("panelButton('pick','4') && four !== panelButton('pick','4')"));
        Assert.NotEqual(page.Text("four.textContent"), page.Text("panelButton('pick','4').textContent"));
    }

    [Fact]
    public void In_the_window_a_new_catalog_or_language_draws_every_row_again()
    {
        var game = Home();
        var page = new PageScript();
        var view = FarmView.Ready(game.World, WheatEverywhere(), Settings);

        page.Catalog(CatalogView.Write(game.World.Catalog));
        page.Open(view);
        page.Run("var one = panelButton('pick','1')");
        page.Open(view);

        Assert.True(page.Is("one === panelButton('pick','1')"));

        page.Catalog(CatalogView.Write(game.World.Catalog));

        Assert.False(page.Is("one === panelButton('pick','1')"));

        page.Run("one = panelButton('pick','1')");
        page.Run(PageWords.Script(isChinese: true));
        page.Open(view);

        Assert.False(page.Is("one === panelButton('pick','1')"));
    }
}
