using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>The window and the HUD button as the player uses them, driven through page.js in Jint.</summary>
public class PageScriptTests
{
    private static readonly ViewSettings Settings = new("G", 12, false);

    private static FakeGame Home() => new()
    {
        Stock = { [WheatSeed] = 5, [TomatoSeed] = 2, [Basic] = 3 },
        Pots = { P(1), P(2, state: PotState.Ripe, plant: Wheat), P(3, floor: 2), P(4, floor: -1, state: PotState.Growing, plant: Tomato) },
        Robots = { new Robot(7, "Robot 1", 1, new[] { new RobotTier(2, WheatSeed, 0), new RobotTier(4, 0, 0), new RobotTier(6, 0, 0) }, 50f, 100f, IsOn: true) },
    };

    private static PresetBook TwoPresets() =>
        Book(Preset("p1", new() { [2] = new[] { Step(WheatSeed) } }, ferts: new[] { Basic }), Preset("p2", new() { [2] = new[] { Step(TomatoSeed) } }));

    private static PageScript Opened(FakeGame? game = null, PresetBook? book = null)
    {
        game ??= Home();

        var page = new PageScript();

        page.Catalog(CatalogView.Write(game.World.Catalog));
        page.Open(FarmView.Ready(game.World, book ?? TwoPresets(), Settings));

        return page;
    }

    private static void ShowTab(PageScript page, string tab) => page.Click($"panelButton('tab','{tab}')");

    private static bool SameAssign(PageCommand command, string presetId, params long[] potIds) =>
        command is PageCommand.Assign assign && assign.PresetId == presetId && assign.PotIds.SequenceEqual(potIds);

    [Fact]
    public void The_pots_are_grouped_by_room_and_the_players_floor_is_marked()
    {
        var page = Opened();
        var marked = page.Text("panel().querySelectorAll('.fh-sect').filter(function (n) { return n.querySelector('.fh-badge'); }).map(function (n) { return n.textContent; }).join('|')");

        Assert.Contains("Floor 1your floor", marked);
        Assert.DoesNotContain("|", marked);
        Assert.Equal(4, (int)double.Parse(page.Text("panel().querySelectorAll('.fh-card').length")));
    }

    [Fact]
    public void A_room_card_names_its_preset_only_when_the_room_header_does_not()
    {
        var game = Home();
        var book = TwoPresets() with
        {
            Overrides = new Dictionary<string, IReadOnlyDictionary<long, string>> { [game.SaveKey!] = new Dictionary<long, string> { [2] = "p2" } },
        };
        var page = Opened(game, book);

        Assert.Equal("Small Planter", page.Text("panelButton('pick','1').querySelector('.fh-sub').textContent"));
        Assert.Contains("Preset p2", page.Text("panelButton('pick','2').querySelector('.fh-sub').textContent"));

        page.Click("panelButton('group','crop')");

        Assert.Contains("Preset p1", page.Text("panelButton('pick','1').querySelector('.fh-sub').textContent"));
    }

    [Fact]
    public void Forecast_chips_whose_words_would_overlap_go_in_separate_lanes()
    {
        var game = new FakeGame
        {
            Pots = { P(1, state: PotState.Ripe, plant: Melon, rotIn: 2f), P(2, state: PotState.Growing, plant: Melon, ripeIn: 11f) },
        };
        var page = Opened(game);

        ShowTab(page, "forecast");

        Assert.True(page.Is("(function (chips) { return chips.length === 2 && chips[0].style.top !== chips[1].style.top; })(panel().querySelectorAll('.fh-ev'))"));
    }

    [Fact]
    public void A_refresh_that_only_moves_the_clocks_updates_the_times_without_losing_the_picked_pot()
    {
        var game = Home();
        game.Pots[3] = P(4, floor: -1, state: PotState.Growing, plant: Tomato, ripeIn: 10f);

        var page = Opened(game);

        page.Click("panelButton('pick','4')");
        page.Click("panelButton('check','1')");
        page.Run("var keptCard = panel().querySelector('.fh-sel');");

        game.Pots[3] = P(4, floor: -1, state: PotState.Growing, plant: Tomato, ripeIn: 3f);
        page.SetData(FarmView.Ready(game.World, TwoPresets(), Settings));

        Assert.True(page.Is("keptCard === panel().querySelector('.fh-sel') && keptCard.textContent.indexOf('3 h to ripe') >= 0"));
        Assert.True(page.Is("panelButton('check','1').classList.contains('fh-on')"));
    }

    [Fact]
    public void A_filter_shows_only_the_pots_that_match_it()
    {
        var page = Opened();

        page.Click("panelButton('filter','ripe')");

        Assert.Equal(1, (int)double.Parse(page.Text("panel().querySelectorAll('.fh-card').length")));
        Assert.True(page.Is("panel().querySelector('.fh-card').getAttribute('data-args') === '2'"));

        page.Click("panelButton('filter','empty')");

        Assert.Equal(2, (int)double.Parse(page.Text("panel().querySelectorAll('.fh-card').length")));
    }

    [Fact]
    public void Grouping_by_state_puts_the_ripe_pots_first()
    {
        var page = Opened();

        page.Click("panelButton('group','state')");

        Assert.Equal("2", page.Text("panel().querySelectorAll('.fh-card')[0].getAttribute('data-args')"));
    }

    [Fact]
    public void Plant_all_asks_once_and_the_buttons_wait_for_the_answer()
    {
        var page = Opened();

        page.Click("panelButton('plantall')");
        page.Click("panelButton('plantall')");
        page.Click("panelButton('tendall')");

        Assert.Equal(new PageCommand[] { new PageCommand.Plant(null) }, page.Sent);

        page.Status("Planting 1 pot.");
        page.Click("panelButton('tendall')");

        Assert.IsType<PageCommand.TendAll>(page.Sent.Last());
    }

    [Fact]
    public void A_ripe_pot_offers_its_harvest_from_its_detail()
    {
        var page = Opened();

        page.Click("panelButton('pick','2')");

        Assert.Equal("Harvest", page.Text("panelButton('tend','2').textContent"));

        page.Click("panelButton('tend','2')");

        Assert.Equal(new PageCommand[] { new PageCommand.Tend(2) }, page.Sent);
    }

    [Fact]
    public void A_pot_takes_a_preset_of_its_own_from_its_detail()
    {
        var page = Opened();

        page.Click("panelButton('pick','1')");
        page.Click("panelButton('assign','p2|1')");

        Assert.True(SameAssign(page.Sent.Single(), "p2", 1));
    }

    [Fact]
    public void Several_checked_pots_take_a_preset_or_go_back_to_the_default_together()
    {
        var page = Opened();

        page.Click("panelButton('check','1')");
        page.Click("panelButton('check','3')");
        page.Click("panelButton('assign','p2|1,3')");
        page.Status("");
        page.Click("panelButton('assign','|1,3')");

        Assert.True(SameAssign(page.Sent[0], "p2", 1, 3));
        Assert.True(SameAssign(page.Sent[1], "", 1, 3));
    }

    [Fact]
    public void In_a_game_never_saved_presets_cannot_be_picked_per_pot()
    {
        var game = Home();
        game.SaveKey = null;

        var page = Opened(game);

        page.Click("panelButton('pick','1')");
        page.Click("panelButton('assign','p2|1')");

        Assert.True(page.Is("panelButton('assign','p2|1').disabled"));
        Assert.Empty(page.Sent);
    }

    [Fact]
    public void Delete_needs_a_second_press()
    {
        var page = Opened();

        ShowTab(page, "presets");
        page.Click("panelButton('preset','p2')");
        page.Click("panelButton('deletepreset','p2')");

        Assert.Empty(page.Sent);
        Assert.Equal("Sure?", page.Text("panelButton('deletepreset','p2').textContent"));

        page.Click("panelButton('deletepreset','p2')");

        Assert.Equal(new PageCommand[] { new PageCommand.DeletePreset("p2") }, page.Sent);
    }

    [Fact]
    public void An_edited_preset_is_saved_in_a_shape_the_plugin_reads_back()
    {
        var page = Opened();

        ShowTab(page, "presets");
        page.Click("panelButton('preset','p1')");
        page.Type("name", "Bread | more");
        page.Blur("name");
        page.Click("panelButton('keepmore','2|0')");
        page.Click("panelButton('autoreplant')");
        page.Click("panelButton('savepreset')");

        var save = Assert.IsType<PageCommand.SavePreset>(page.Sent.Last());
        var edit = PresetJson.ReadEdit(save.Json, "x");

        Assert.NotNull(edit);
        Assert.Equal(("p1", "Bread | more", true), (edit!.Preset.Id, edit.Preset.Name, edit.Preset.AutoReplant));
        Assert.Equal(new[] { new SeedStep(WheatSeed, 1) }, edit.Preset.SeedsFor(2));
        Assert.Equal(new[] { Basic }, edit.Preset.Fertilizers);
        Assert.Equal(new[] { PresetBook.AllUse }, edit.Uses);
    }

    [Fact]
    public void A_refresh_while_typing_waits_until_the_box_loses_focus()
    {
        var page = Opened();

        ShowTab(page, "presets");
        page.Type("name", "Half typed");
        page.SetData(FarmView.Ready(Home().World, TwoPresets(), Settings));

        Assert.Equal("Half typed", page.Text("field('name').value"));
        Assert.Contains(page.Sent, c => c is PageCommand.Focus { IsTyping: true });

        page.Blur("name");

        Assert.Equal(new PageCommand.Focus(false), page.Sent.Last());
    }

    [Fact]
    public void A_new_preset_is_selected_once_the_plugin_has_saved_it()
    {
        var game = Home();
        var book = TwoPresets();
        var page = Opened(game, book);

        ShowTab(page, "presets");
        page.Click("panelButton('newpreset')");
        page.Click("panelButton('savepreset')");

        var edit = PresetJson.ReadEdit(Assert.IsType<PageCommand.SavePreset>(page.Sent.Last()).Json, Texts.NewPresetName.En);

        Assert.NotNull(edit);

        page.Status("Saved.");
        page.SetData(FarmView.Ready(game.World, book.Save(edit!.Preset, edit.Uses), Settings));

        Assert.Equal("New preset", page.Text("field('name').value"));
        Assert.True(page.Is("panelButton('deletepreset','p3')"));
    }

    [Fact]
    public void A_robot_tier_is_changed_with_one_press_after_picking_seed_and_fertilizer()
    {
        var page = Opened();

        ShowTab(page, "robots");
        page.Click("panelButton('tieredit','7|0')");
        page.Click($"panelButton('tierseed','{TomatoSeed}')");
        page.Click($"panelButton('tierfert','{Basic}')");

        Assert.Empty(page.Sent);

        page.Click("panelButton('settier')");

        Assert.Equal(new PageCommand[] { new PageCommand.RobotTier(7, 0, TomatoSeed, Basic) }, page.Sent);
    }

    [Fact]
    public void A_robot_tier_only_offers_seeds_that_fit_its_pot_size()
    {
        var game = Home();
        game.Stock[MelonSeed] = 3;

        var page = Opened(game);

        ShowTab(page, "robots");
        page.Click("panelButton('tieredit','7|0')");

        Assert.False(page.Is($"panelButton('tierseed','{MelonSeed}')"));
        Assert.True(page.Is($"panelButton('tierseed','{WheatSeed}')"));
    }

    [Fact]
    public void The_rot_warning_and_robot_switch_are_set_from_the_window()
    {
        var page = Opened();

        ShowTab(page, "settings");
        page.Click("panelButton('rotwarn','24')");
        page.Status("");
        ShowTab(page, "robots");
        page.Click("panelButton('robots','1')");

        Assert.Equal(new PageCommand[] { new PageCommand.RotWarn(24), new PageCommand.Robots(true) }, page.Sent);
    }

    [Fact]
    public void The_hud_button_shows_in_a_loaded_game_with_a_count_and_opens_the_window()
    {
        var page = new PageScript();

        page.OpenHud();
        page.Poll();

        Assert.False(page.Is("hudButton()"));

        page.Live(true, "G");
        page.Badge(3);

        Assert.Equal("3", page.Text("hudButton().querySelector('.fh-hud-badge').textContent"));
        Assert.True(page.Is("hudPage().querySelector('.vertical-tools').firstChild === hudButton()"));
        Assert.Equal("30", page.Text("hudButton().getAttribute('data-etk-dock')"));

        page.Click("hudButton()");

        Assert.Equal(new PageCommand[] { new PageCommand.Toggle() }, page.Sent);

        page.Live(false, "G");

        Assert.False(page.Is("hudButton()"));
    }

    [Fact]
    public void The_window_speaks_the_games_language()
    {
        var page = new PageScript();

        page.Run(PageWords.Script(isChinese: true));
        page.Open(FarmView.Ready(Home().World, TwoPresets(), Settings));

        Assert.Equal(Texts.Page.Single(w => w.Key == "tabPots").Phrase.Zh, page.Text("panelButton('tab','pots').textContent"));
    }
}
