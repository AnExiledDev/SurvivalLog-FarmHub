using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>Which preset a pot follows, how presets are saved and deleted, and that the file survives a round trip.</summary>
public class PresetBookTests
{
    private static readonly FarmPreset A = Preset("p1", new() { [2] = new[] { Step(WheatSeed, keep: 4) } }, ferts: new[] { Super, Basic },
        perCrop: new[] { new CropFertilizer(Melon, new[] { Super }, WhenOut.None) }, autoReplant: true);

    private static readonly FarmPreset B = Preset("p2", new() { [2] = new[] { Step(TomatoSeed) } });

    [Fact]
    public void A_pot_follows_its_own_choice_then_its_room_then_its_floor_then_everywhere()
    {
        var c = Preset("p3", new());
        var d = Preset("p4", new());
        var book = Book(A, B, c, d)
            .Save(B, new[] { PresetBook.FloorUse(2) })
            .Save(c, new[] { PresetBook.AreaUse(Cellar) })
            .Assign("slot-1", new long[] { 5 }, "p4");

        Assert.Equal("p4", book.PresetFor("slot-1", P(5, floor: 2, area: Cellar))?.Id);
        Assert.Equal("p3", book.PresetFor("slot-1", P(6, floor: 2, area: Cellar))?.Id);
        Assert.Equal("p2", book.PresetFor("slot-1", P(7, floor: 2))?.Id);
        Assert.Equal("p1", book.PresetFor("slot-1", P(8, floor: 1))?.Id);
        Assert.Equal("p1", book.PresetFor("slot-2", P(5, floor: 1))?.Id);
    }

    [Fact]
    public void Saving_a_preset_for_a_use_takes_that_use_from_the_preset_that_had_it()
    {
        var book = Book(A, B).Save(B, new[] { PresetBook.AllUse });

        Assert.Equal(new[] { PresetBook.AllUse }, book.UsesOf("p2"));
        Assert.Empty(book.UsesOf("p1"));
    }

    [Fact]
    public void A_new_preset_gets_an_unused_id()
    {
        var book = Book(A, B).Save(Preset("", new()), Array.Empty<string>());

        Assert.Equal(new[] { "p1", "p2", "p3" }, book.Presets.Select(p => p.Id));
    }

    [Fact]
    public void Deleting_a_preset_sends_its_pots_back_to_their_room_or_floor()
    {
        var book = Book(A, B).Assign("slot-1", new long[] { 5 }, "p2").Delete("p2");

        Assert.Equal("p1", book.PresetFor("slot-1", P(5))?.Id);
        Assert.Null(book.Find("p2"));
    }

    [Fact]
    public void The_preset_file_reads_back_exactly_what_was_written()
    {
        var book = Book(A, B).Assign("slot-1", new long[] { -3, 5 }, "p2");
        var read = PresetJson.ReadBook(PresetJson.WriteBook(book));

        Assert.NotNull(read);
        Assert.Equal(PresetJson.WriteBook(book), PresetJson.WriteBook(read!));
        Assert.Equal(WhenOut.None, read!.Find("p1")?.PerCrop.Single().WhenOut);
        Assert.Equal(4, read.Find("p1")?.SeedsFor(2).Single().KeepAtLeast);
        Assert.Equal("p2", read.OverrideFor("slot-1", -3));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Something_that_is_not_a_preset_file_reads_as_nothing(string text)
    {
        Assert.Null(PresetJson.ReadBook(text));
    }

    [Fact]
    public void An_editor_save_with_a_blank_name_takes_the_new_preset_name_and_drops_unknown_uses()
    {
        var edit = PresetJson.ReadEdit("{\"id\":\"\",\"name\":\"  \",\"seeds\":{\"2\":[{\"item\":200,\"keep\":3}]},\"uses\":[\"all\",\"f2\",\"x9\"]}", "New preset");

        Assert.NotNull(edit);
        Assert.Equal("New preset", edit!.Preset.Name);
        Assert.Equal(new[] { "all", "f2" }, edit.Uses);
        Assert.Equal(new[] { new SeedStep(WheatSeed, 3) }, edit.Preset.SeedsFor(2));
    }

    [Fact]
    public void Picking_a_preset_for_a_pot_needs_a_saved_game()
    {
        var edit = BookEdits.Assign(Book(A, B), null, new PageCommand.Assign("p2", new long[] { 1 }), isChinese: false);

        Assert.Null(edit.Book);
        Assert.Equal(Texts.NoSave.En, edit.Message);
    }
}
