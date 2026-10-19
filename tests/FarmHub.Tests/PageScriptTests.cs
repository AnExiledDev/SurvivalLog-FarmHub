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
    public void The_pots_are_grouped_by_floor_and_the_players_floor_is_marked()
    {
        var page = Opened();

        Assert.Equal("Floor -1|Floor 1you are here|Floor 2", page.Text("panel().querySelectorAll('.fh-sect').map(function (n) { return n.textContent; }).join('|')"));
        Assert.Equal(4, (int)double.Parse(page.Text("panelButtons('pick').length")));
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

        page.Click("panel().querySelectorAll('.fh-chk')[1]");
        page.Click("panel().querySelectorAll('.fh-chk')[3]");
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
        page.Click("panelButton('keepmore','0')");
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
