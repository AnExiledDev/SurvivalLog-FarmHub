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

    private const string Rows = "panel().querySelectorAll('[data-act]').filter(function (n) { return n.getAttribute('data-act') === 'pick'; })";

    [Fact]
    public void The_pots_are_grouped_by_room_and_the_players_floor_is_marked()
    {
        var page = Opened();
        var marked = page.Text("panel().querySelectorAll('.fh-grp-h').filter(function (n) { return n.textContent.indexOf('your floor') >= 0; }).map(function (n) { return n.textContent; }).join('|')");

        Assert.Contains("Floor 1 (your floor)", marked);
        Assert.DoesNotContain("|", marked);
        Assert.Equal(4, (int)double.Parse(page.Text(Rows + ".length")));
    }

    [Fact]
    public void A_room_header_names_its_preset_and_opens_it_for_editing()
    {
        var page = Opened();

        page.Click("panel().querySelectorAll('.fh-grp-h')[0].querySelector('[data-act]')");

        Assert.True(page.Is("panelButton('tab','presets').classList.contains('fh-on')"));
        Assert.Equal("p1", page.Text("panel().querySelectorAll('.fh-pcard').filter(function (n) { return n.classList.contains('fh-on'); })[0].getAttribute('data-args')"));
    }

    [Fact]
    public void A_refresh_that_only_moves_the_clocks_updates_the_times_without_losing_the_picked_pot()
    {
        var game = Home();
        game.Pots[3] = P(4, floor: -1, state: PotState.Growing, plant: Tomato, ripeIn: 10f);

        var page = Opened(game);

        page.Click("panelButton('pick','4')");
        page.Run("var keptRow = panelButton('pick','4');");

        game.Pots[3] = P(4, floor: -1, state: PotState.Growing, plant: Tomato, ripeIn: 3f);
        page.SetData(FarmView.Ready(game.World, TwoPresets(), Settings));

        Assert.True(page.Is("keptRow === panelButton('pick','4') && keptRow.classList.contains('fh-sel-on') && keptRow.textContent.indexOf('3 h to ripe') >= 0"));
    }

    [Fact]
    public void The_show_buttons_list_only_the_pots_that_match()
    {
        var page = Opened();

        page.Click("panelButton('show','needs')");

        Assert.Equal("2", page.Text(Rows + ".map(function (n) { return n.getAttribute('data-args'); }).join(',')"));

        page.Click("panelButton('show','empty')");

        Assert.Equal(2, (int)double.Parse(page.Text(Rows + ".length")));

        page.Click("panelButton('show','growing')");

        Assert.Equal("4", page.Text(Rows + ".map(function (n) { return n.getAttribute('data-args'); }).join(',')"));
    }

    [Fact]
    public void Searching_narrows_the_pot_list_by_crop_name()
    {
        var page = Opened();

        page.Run("type(field('potsearch'), 'toma')");

        Assert.Equal("4", page.Text(Rows + ".map(function (n) { return n.getAttribute('data-args'); }).join(',')"));
    }

    [Fact]
    public void Back_returns_from_a_pots_detail_to_what_needs_the_player()
    {
        var page = Opened();

        page.Click("panelButton('pick','2')");

        Assert.True(page.Is("!panelButton('tendall')"));

        page.Click("panelButton('back')");

        Assert.True(page.Is("!!panelButton('tendall') && !panelButton('pick','2').classList.contains('fh-sel-on')"));
    }

    [Fact]
    public void A_crop_coming_up_in_the_next_two_days_opens_its_pot()
    {
        var game = new FakeGame { Pots = { P(1, state: PotState.Growing, plant: Melon, ripeIn: 11f), P(2) } };
        var page = Opened(game);

        Assert.Contains("in 11 h", page.Text("panelButton('pickat','1').textContent"));

        page.Click("panelButton('pickat','1')");

        Assert.True(page.Is("panelButton('pick','1').classList.contains('fh-sel-on') && !!panelButton('back')"));
    }

    [Fact]
    public void With_the_panel_key_on_G_the_window_warns_that_G_also_tends_the_garden()
    {
        var page = Opened();

        page.Live(true, "G");

        ShowTab(page, "settings");

        Assert.Contains("Tend Garden", page.Text("panel().querySelectorAll('.fh-warnbox')[0].textContent"));
    }

    [Fact]
    public void With_the_panel_key_on_Shift_G_the_footer_names_both_keys_and_nothing_warns()
    {
        var page = Opened();

        page.Live(true, "Shift+G");
        ShowTab(page, "settings");

        Assert.True(page.Is("panel().querySelectorAll('.fh-warnbox').length === 0"));
        Assert.Contains("GTend Garden (game)", page.Text("panel().querySelectorAll('.fh-wfoot')[0].textContent"));
        Assert.Contains("Shift+Gthis window", page.Text("panel().querySelectorAll('.fh-wfoot')[0].textContent"));
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

    // ===== UI size (Exiled's Toolkit) =====

    /// <summary>A stand-in for the Toolkit's ExiledUi that records what each mod hands it.</summary>
    private const string FakeUi = "var handed = []; window.ExiledUi = { size: function (el, options) { handed.push({ el: el, options: options }); } }";

    private static void OpenWithToolkit(PageScript page)
    {
        var game = Home();

        page.Run(FakeUi);
        page.Catalog(CatalogView.Write(game.World.Catalog));
        page.Open(FarmView.Ready(game.World, TwoPresets(), Settings));
    }

    /// <summary>
    /// The mask fills the screen at any zoom, so a window measured by the mask would never be shrunk to fit; the window
    /// itself is what must fit. It is handed once, however often the window opens.
    /// </summary>
    [Fact]
    public void The_window_is_what_must_fit_the_screen()
    {
        var page = new PageScript();

        OpenWithToolkit(page);
        page.Run("window.__slfarmhub.close()");
        page.Open(FarmView.Ready(Home().World, TwoPresets(), Settings));

        Assert.Equal("1", page.Text("handed.length"));
        Assert.Equal("FarmHub", page.Text("handed[0].options.mod"));
        Assert.Equal("fh-root", page.Text("handed[0].el.id"));
        Assert.True(page.Is("handed[0].options.fit === document.querySelector('.fh-win')"));
    }

    /// <summary>With the window closed a result line is the plugin's Toolkit toast, so the page draws no box of its own.</summary>
    [Fact]
    public void A_result_with_the_window_closed_draws_nothing_on_the_page()
    {
        var page = new PageScript();

        page.Run("window.__slfarmhub.status('Replanted 3 pots')");

        Assert.False(page.Is("document.getElementById('fh-toast')"));
        Assert.False(page.Is("document.body.textContent.indexOf('Replanted 3 pots') >= 0"));
    }

    /// <summary>The Toolkit drops a window taken out of its page, so the window drawn again in its place must be handed again.</summary>
    [Fact]
    public void A_window_drawn_again_after_the_page_lost_it_is_handed_again()
    {
        var page = new PageScript();

        OpenWithToolkit(page);
        page.Run("document.getElementById('fh-root').remove()");
        page.Open(FarmView.Ready(Home().World, TwoPresets(), Settings));

        Assert.Equal("2", page.Text("handed.length"));
        Assert.True(page.Is("handed[1].el !== handed[0].el && handed[1].el === document.getElementById('fh-root')"));
        Assert.True(page.Is("handed[1].options.fit === document.querySelector('.fh-win')"));
    }

    /// <summary>The Toolkit's script can load after this one; the window waits in the list it reads when it starts.</summary>
    [Fact]
    public void Before_the_toolkit_has_loaded_the_window_waits_for_it()
    {
        var page = Opened();

        Assert.Equal("1", page.Text("window.__exiledUiWait.length"));
        Assert.Equal("fh-root", page.Text("window.__exiledUiWait[0][0].id"));
        Assert.True(page.Is("window.__exiledUiWait[0][1].fit === document.querySelector('.fh-win')"));
    }
}
