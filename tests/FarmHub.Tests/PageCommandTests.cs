using FarmHub.Core;
using Xunit;

namespace FarmHub.Tests;

/// <summary>What the window can ask the plugin for. Anything malformed or foreign is ignored, never half-done.</summary>
public class PageCommandTests
{
    private static PageCommand? Parse(string body) => PageCommand.Parse(PageCommand.Prefix + body);

    [Fact]
    public void Each_press_is_understood()
    {
        Assert.IsType<PageCommand.Toggle>(Parse("toggle"));
        Assert.IsType<PageCommand.TendAll>(Parse("tendall"));
        Assert.Null(Assert.IsType<PageCommand.Plant>(Parse("plantall")).PotIds);
        Assert.Equal(new PageCommand.Tend(-12), Parse("tend|-12"));
        Assert.Equal(new PageCommand.RotWarn(24), Parse("rotwarn|24"));
        Assert.Equal(new PageCommand.Robots(true), Parse("robots|1"));
        Assert.Equal(new PageCommand.FromPresets(false), Parse("frompresets|0"));
        Assert.Null(Parse("frompresets|yes"));
        Assert.Equal(new PageCommand.RobotTier(7, 2, 200, 0), Parse("robottier|7|2|200|0"));
        Assert.Equal(new PageCommand.DeletePreset("p2"), Parse("deletepreset|p2"));
    }

    [Fact]
    public void Plant_fertilize_and_assign_carry_every_pot()
    {
        var plant = Assert.IsType<PageCommand.Plant>(Parse("plant|1,-2,3"));
        var fert = Assert.IsType<PageCommand.Fertilize>(Parse("fert|4"));
        var reset = Assert.IsType<PageCommand.Assign>(Parse("assign||5,6"));

        Assert.Equal(new long[] { 1, -2, 3 }, plant.PotIds);
        Assert.Equal(new long[] { 4 }, fert.PotIds);
        Assert.Equal("", reset.PresetId);
        Assert.Equal(new long[] { 5, 6 }, reset.PotIds);
    }

    [Fact]
    public void A_preset_named_with_a_bar_still_arrives_whole()
    {
        var save = Assert.IsType<PageCommand.SavePreset>(Parse("savepreset|{\"name\":\"a|b\"}"));

        Assert.Equal("{\"name\":\"a|b\"}", save.Json);
    }

    [Theory]
    [InlineData("plant|x")]
    [InlineData("plant|")]
    [InlineData("assign|p1|")]
    [InlineData("rotwarn|0")]
    [InlineData("rotwarn|97")]
    [InlineData("robots|2")]
    [InlineData("robottier|7|3|200|0")]
    [InlineData("robottier|7|0|-1|0")]
    [InlineData("deletepreset|")]
    [InlineData("tendall|now")]
    public void A_malformed_message_is_ignored(string body)
    {
        Assert.Null(Parse(body));
    }

    [Fact]
    public void Another_mods_message_is_not_ours()
    {
        Assert.Null(PageCommand.Parse("slmod|traphub|toggle"));
        Assert.Null(PageCommand.Parse(null));
    }
}
