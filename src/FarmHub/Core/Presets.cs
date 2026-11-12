using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FarmHub.Core;

/// <summary>One seed in a preset's order. The step is skipped once planting would leave fewer than KeepAtLeast seeds.</summary>
public sealed record SeedStep(int ItemId, int KeepAtLeast);

/// <summary>What a crop with its own fertilizer list gets once none of that list is left.</summary>
public enum WhenOut
{
    PresetOrder,
    None,
}

/// <summary>A crop's own fertilizer order inside a preset. An empty order with WhenOut None means the crop is never fertilized.</summary>
public sealed record CropFertilizer(int PlantId, IReadOnlyList<int> Order, WhenOut WhenOut);

/// <summary>
/// A named planting rule set. Seeds holds an ordered seed list per pot capacity, tried in order; Fertilizers is the
/// order for any crop without its own entry in PerCrop. With Fertilize off nothing is fertilized at all.
/// </summary>
public sealed record FarmPreset(
    string Id,
    string Name,
    IReadOnlyDictionary<int, IReadOnlyList<SeedStep>> Seeds,
    IReadOnlyList<int> Fertilizers,
    IReadOnlyList<CropFertilizer> PerCrop,
    bool SkipUnsuitable,
    bool Fertilize,
    bool AutoReplant)
{
    public IReadOnlyList<SeedStep> SeedsFor(int capacity) => Seeds.TryGetValue(capacity, out var steps) ? steps : Array.Empty<SeedStep>();

    public CropFertilizer? FertilizerFor(int plantId) => PerCrop.FirstOrDefault(crop => crop.PlantId == plantId);
}

/// <summary>
/// Every preset, where each one is used, and the per-pot choices of each save. A use is "all", a floor ("f" and its
/// level) or a room ("a" and its area id). A pot takes the preset set for that pot, else its room's, else its floor's, else "all".
/// </summary>
public sealed record PresetBook(
    IReadOnlyList<FarmPreset> Presets,
    IReadOnlyDictionary<string, string> Uses,
    IReadOnlyDictionary<string, IReadOnlyDictionary<long, string>> Overrides)
{
    public const string FirstPresetId = "p1";
    public const string AllUse = "all";

    public static PresetBook First(string firstPresetName) => new(
        new[] { new FarmPreset(FirstPresetId, firstPresetName, new Dictionary<int, IReadOnlyList<SeedStep>>(), Array.Empty<int>(), Array.Empty<CropFertilizer>(), SkipUnsuitable: true, Fertilize: true, AutoReplant: false) },
        new Dictionary<string, string> { [AllUse] = FirstPresetId },
        new Dictionary<string, IReadOnlyDictionary<long, string>>());

    public static string FloorUse(int level) => "f" + level.ToString(CultureInfo.InvariantCulture);

    public static string AreaUse(int area) => "a" + area.ToString(CultureInfo.InvariantCulture);

    public static bool IsUse(string key) =>
        key == AllUse || (key.Length > 1 && key[0] is 'f' or 'a' && int.TryParse(key.Substring(1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));

    public FarmPreset? Find(string? presetId) => presetId == null ? null : Presets.FirstOrDefault(preset => preset.Id == presetId);

    public FarmPreset? PresetFor(string? saveKey, Pot pot) =>
        Find(OverrideFor(saveKey, pot.Id)) ?? Used(AreaUse(pot.Area)) ?? Used(FloorUse(pot.Floor)) ?? Used(AllUse);

    /// <summary>The preset a whole floor uses: the floor's own, else "all". Robots go by this, since they work a floor.</summary>
    public FarmPreset? PresetForFloor(int level) => Used(FloorUse(level)) ?? Used(AllUse);

    public string? OverrideFor(string? saveKey, long potId) =>
        saveKey != null && Overrides.TryGetValue(saveKey, out var pots) && pots.TryGetValue(potId, out var id) ? id : null;

    public IEnumerable<string> UsesOf(string presetId) => Uses.Where(pair => pair.Value == presetId).Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal);

    /// <summary>
    /// Adds the preset, or replaces the one with its id; a preset with no id gets a new one. It becomes the preset for
    /// exactly the uses given: a use it is given moves to it from any other preset, and a use it no longer lists is dropped.
    /// </summary>
    public PresetBook Save(FarmPreset preset, IEnumerable<string> uses)
    {
        var saved = string.IsNullOrEmpty(preset.Id) ? preset with { Id = NewId() } : preset;
        var presets = Presets.ToList();
        var at = presets.FindIndex(existing => existing.Id == saved.Id);

        if (at >= 0) presets[at] = saved;
        else presets.Add(saved);

        var wanted = uses.Where(IsUse).ToHashSet();
        var kept = Uses.Where(pair => pair.Value != saved.Id && !wanted.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

        foreach (var use in wanted) kept[use] = saved.Id;

        return this with { Presets = presets, Uses = kept };
    }

    /// <summary>Removes the preset; the uses and pots that named it fall back to the next most specific choice.</summary>
    public PresetBook Delete(string presetId) => this with
    {
        Presets = Presets.Where(preset => preset.Id != presetId).ToList(),
        Uses = Uses.Where(pair => pair.Value != presetId).ToDictionary(pair => pair.Key, pair => pair.Value),
        Overrides = Overrides.ToDictionary(
            save => save.Key,
            save => (IReadOnlyDictionary<long, string>)save.Value.Where(pair => pair.Value != presetId).ToDictionary(pair => pair.Key, pair => pair.Value)),
    };

    /// <summary>Sets the pots' own preset in this save; an empty preset id puts them back on their room's or floor's.</summary>
    public PresetBook Assign(string saveKey, IEnumerable<long> potIds, string presetId)
    {
        var overrides = Overrides.ToDictionary(save => save.Key, save => save.Value);
        var pots = overrides.TryGetValue(saveKey, out var existing) ? existing.ToDictionary(pair => pair.Key, pair => pair.Value) : new Dictionary<long, string>();

        foreach (var potId in potIds)
        {
            if (presetId.Length == 0) pots.Remove(potId);
            else pots[potId] = presetId;
        }

        overrides[saveKey] = pots;

        return this with { Overrides = overrides };
    }

    private FarmPreset? Used(string use) => Uses.TryGetValue(use, out var id) ? Find(id) : null;

    private string NewId()
    {
        var taken = Presets.Select(preset => preset.Id).ToHashSet();

        for (var n = Presets.Count + 1; ; n++)
        {
            var id = "p" + n.ToString(CultureInfo.InvariantCulture);

            if (!taken.Contains(id)) return id;
        }
    }
}
