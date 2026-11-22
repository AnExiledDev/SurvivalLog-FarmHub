using System;

namespace FarmHub.Core;

/// <summary>Where the presets are kept. Read gives "" when there is no file yet and null when the file is there but could not be read.</summary>
public interface IPresetFile
{
    string? Read();
    bool Replace(string text);
    void SetAside();
}

public enum KeepResult { Saved, NotSaved, NotRead }

/// <summary>
/// The preset book, read from the file the first time it is needed. A file that is there but is not a preset file is
/// copied aside before a fresh book replaces it. A file that could not be read at all (locked by antivirus or a sync
/// tool) is never written over: the fresh book is shown but not kept, and edits are refused until a read works.
/// </summary>
public sealed class PresetStore
{
    private readonly IPresetFile _file;
    private readonly Func<string> _firstPresetName;
    private readonly Action<string> _log;
    private PresetBook? _book;

    public PresetStore(IPresetFile file, Func<string> firstPresetName, Action<string> log)
    {
        _file = file;
        _firstPresetName = firstPresetName;
        _log = log;
    }

    public PresetBook Book()
    {
        if (_book != null) return _book;

        var text = _file.Read();

        if (text == null) return PresetBook.First(_firstPresetName());

        var book = string.IsNullOrWhiteSpace(text) ? null : PresetJson.ReadBook(text);

        if (book == null && !string.IsNullOrWhiteSpace(text)) _file.SetAside();

        _book = book ?? PresetBook.First(_firstPresetName());
        _log($"presets: {_book.Presets.Count}");

        return _book;
    }

    /// <summary>
    /// Uses the changed book at once and writes it. An edit made while the file could not be read was made on the
    /// fresh book, so it is dropped rather than written over the real presets.
    /// </summary>
    public KeepResult Keep(PresetBook book)
    {
        if (_book == null)
        {
            Book();

            return KeepResult.NotRead;
        }

        _book = book;

        return _file.Replace(PresetJson.WriteBook(book)) ? KeepResult.Saved : KeepResult.NotSaved;
    }
}
