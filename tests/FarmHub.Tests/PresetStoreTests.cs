using System.Collections.Generic;
using FarmHub.Core;
using Xunit;
using static FarmHub.Tests.FakeGame;

namespace FarmHub.Tests;

/// <summary>A preset file that could not be read is never written over, so a locked file can't wipe the player's presets.</summary>
public class PresetStoreTests
{
    private sealed class FakeFile : IPresetFile
    {
        public Queue<string?> Reads = new();
        public List<string> Writes = new();
        public int SetAsideCount;

        public string? Read() => Reads.Count > 0 ? Reads.Dequeue() : "";

        public bool Replace(string text)
        {
            Writes.Add(text);

            return true;
        }

        public void SetAside() => SetAsideCount++;
    }

    private static readonly PresetBook Saved = Book(Preset("p1", new()), Preset("p2", new()));

    private static PresetStore Store(FakeFile file) => new(file, () => "First", _ => { });

    [Fact]
    public void An_edit_made_while_the_file_could_not_be_read_is_not_written()
    {
        var file = new FakeFile();
        file.Reads.Enqueue(null);
        file.Reads.Enqueue(null);
        var store = Store(file);

        var shown = store.Book();

        Assert.Equal(KeepResult.NotRead, store.Keep(shown));
        Assert.Empty(file.Writes);
        Assert.Equal(0, file.SetAsideCount);
    }

    [Fact]
    public void Once_the_file_reads_again_the_real_presets_come_back_and_the_stale_edit_is_dropped()
    {
        var file = new FakeFile();
        file.Reads.Enqueue(null);
        file.Reads.Enqueue(PresetJson.WriteBook(Saved));
        var store = Store(file);

        var stale = store.Book();

        Assert.Equal(KeepResult.NotRead, store.Keep(stale));
        Assert.Empty(file.Writes);
        Assert.Equal(2, store.Book().Presets.Count);
    }

    [Fact]
    public void With_no_file_yet_an_edit_is_saved()
    {
        var file = new FakeFile();
        var store = Store(file);

        var book = store.Book();

        Assert.Single(book.Presets);
        Assert.Equal(KeepResult.Saved, store.Keep(book));
        Assert.Single(file.Writes);
    }

    [Fact]
    public void A_file_that_is_not_a_preset_file_is_copied_aside_before_it_is_replaced()
    {
        var file = new FakeFile();
        file.Reads.Enqueue("not json");
        var store = Store(file);

        var book = store.Book();

        Assert.Equal(1, file.SetAsideCount);
        Assert.Equal(KeepResult.Saved, store.Keep(book));
    }

    [Fact]
    public void A_saved_file_loads_its_presets()
    {
        var file = new FakeFile();
        file.Reads.Enqueue(PresetJson.WriteBook(Saved));

        Assert.Equal(2, Store(file).Book().Presets.Count);
    }
}
