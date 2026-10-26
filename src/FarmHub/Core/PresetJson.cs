using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FarmHub.Core;

/// <summary>A preset as the page's editor sends it back: the preset, and the uses ("all", floors, rooms) it should have.</summary>
public sealed record PresetEdit(FarmPreset Preset, IReadOnlyList<string> Uses);

/// <summary>
/// The preset file (farm-hub.json in BepInEx/config) and the preset editor's message, both JSON. Reading is forgiving:
/// a field that is missing or of the wrong kind takes its default, and an entry naming no item is dropped.
/// </summary>
public static class PresetJson
{
    private const int MaxNameLength = 40;
    private const int MaxKeep = 9999;

    /// <summary>The book in the file, or null when the text is not a preset file at all.</summary>
    public static PresetBook? ReadBook(string text)
    {
        if (Parse(text) is not JsonObject root || root["presets"] is not JsonArray presets) return null;

        var uses = new Dictionary<string, string>();
        var overrides = new Dictionary<string, IReadOnlyDictionary<long, string>>();

        if (root["uses"] is JsonObject useNodes)
        {
            foreach (var (key, value) in useNodes)
            {
                if (PresetBook.IsUse(key) && Text(value) is { Length: > 0 } id) uses[key] = id;
            }
        }

        if (root["saves"] is JsonObject saves)
        {
            foreach (var (save, pots) in saves)
            {
                if (pots is JsonObject potObject) overrides[save] = ReadOverrides(potObject);
            }
        }

        return new PresetBook(
            presets.OfType<JsonObject>().Select(ReadPreset).Where(preset => preset.Id.Length > 0).ToList(),
            uses,
            overrides);
    }

    public static string WriteBook(PresetBook book)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("version", 1);
            json.WriteStartArray("presets");

            foreach (var preset in book.Presets) WritePreset(json, preset);

            json.WriteEndArray();
            json.WriteStartObject("uses");

            foreach (var (use, id) in book.Uses.OrderBy(pair => pair.Key, StringComparer.Ordinal)) json.WriteString(use, id);

            json.WriteEndObject();
            json.WriteStartObject("saves");

            foreach (var (save, pots) in book.Overrides.Where(save => save.Value.Count > 0).OrderBy(save => save.Key, StringComparer.Ordinal))
            {
                json.WriteStartObject(save);

                foreach (var (pot, id) in pots.OrderBy(pair => pair.Key)) json.WriteString(pot.ToString(CultureInfo.InvariantCulture), id);

                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The editor's message, or null when it is not a preset. A blank name takes the fallback name.</summary>
    public static PresetEdit? ReadEdit(string text, string fallbackName)
    {
        if (Parse(text) is not JsonObject root) return null;

        var preset = ReadPreset(root);

        if (preset.Name.Length == 0) preset = preset with { Name = fallbackName };

        var uses = root["uses"] is JsonArray useNodes ? useNodes.Select(Text).OfType<string>().Where(PresetBook.IsUse).Distinct().ToList() : new List<string>();

        return new PresetEdit(preset, uses);
    }

    public static void WritePreset(Utf8JsonWriter json, FarmPreset preset)
    {
        json.WriteStartObject();
        WritePresetFields(json, preset);
        json.WriteEndObject();
    }

    /// <summary>A preset's fields, written into an object the caller opened, so the window can add its own after them.</summary>
    public static void WritePresetFields(Utf8JsonWriter json, FarmPreset preset)
    {
        json.WriteString("id", preset.Id);
        json.WriteString("name", preset.Name);
        json.WriteStartObject("seeds");

        foreach (var (capacity, steps) in preset.Seeds.Where(pair => pair.Value.Count > 0).OrderBy(pair => pair.Key))
        {
            json.WriteStartArray(capacity.ToString(CultureInfo.InvariantCulture));

            foreach (var step in steps)
            {
                json.WriteStartObject();
                json.WriteNumber("item", step.ItemId);
                json.WriteNumber("keep", step.KeepAtLeast);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        json.WriteEndObject();
        WriteInts(json, "ferts", preset.Fertilizers);
        json.WriteStartArray("perCrop");

        foreach (var crop in preset.PerCrop)
        {
            json.WriteStartObject();
            json.WriteNumber("plant", crop.PlantId);
            WriteInts(json, "order", crop.Order);
            json.WriteString("whenOut", crop.WhenOut == WhenOut.None ? "none" : "preset");
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteBoolean("skipUnsuitable", preset.SkipUnsuitable);
        json.WriteBoolean("fertilize", preset.Fertilize);
        json.WriteBoolean("autoReplant", preset.AutoReplant);
    }

    private static FarmPreset ReadPreset(JsonObject node)
    {
        var name = (Text(node["name"]) ?? "").Trim();
        var seeds = new Dictionary<int, IReadOnlyList<SeedStep>>();
        var perCrop = new List<CropFertilizer>();

        if (node["seeds"] is JsonObject seedNodes)
        {
            foreach (var (key, value) in seedNodes)
            {
                if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var capacity) && capacity > 0 && value is JsonArray steps) seeds[capacity] = ReadSteps(steps);
            }
        }

        if (node["perCrop"] is JsonArray cropNodes)
        {
            foreach (var crop in cropNodes.OfType<JsonObject>())
            {
                var plantId = Int(crop["plant"]) ?? 0;

                if (plantId > 0 && perCrop.All(existing => existing.PlantId != plantId))
                {
                    perCrop.Add(new CropFertilizer(plantId, Ints(crop["order"]).Distinct().ToList(), Text(crop["whenOut"]) == "none" ? WhenOut.None : WhenOut.PresetOrder));
                }
            }
        }

        return new FarmPreset(
            Text(node["id"]) ?? "",
            name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name,
            seeds,
            Ints(node["ferts"]).Distinct().ToList(),
            perCrop,
            Bool(node["skipUnsuitable"]) ?? true,
            Bool(node["fertilize"]) ?? true,
            Bool(node["autoReplant"]) ?? false);
    }

    private static IReadOnlyList<SeedStep> ReadSteps(JsonArray nodes)
    {
        var steps = new List<SeedStep>();

        foreach (var step in nodes.OfType<JsonObject>())
        {
            var itemId = Int(step["item"]) ?? 0;

            if (itemId > 0 && steps.All(existing => existing.ItemId != itemId)) steps.Add(new SeedStep(itemId, Math.Clamp(Int(step["keep"]) ?? 0, 0, MaxKeep)));
        }

        return steps;
    }

    private static IReadOnlyDictionary<long, string> ReadOverrides(JsonObject pots)
    {
        var read = new Dictionary<long, string>();

        foreach (var (key, value) in pots)
        {
            if (long.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var potId) && Text(value) is { Length: > 0 } id) read[potId] = id;
        }

        return read;
    }

    private static void WriteInts(Utf8JsonWriter json, string name, IEnumerable<int> values)
    {
        json.WriteStartArray(name);

        foreach (var value in values) json.WriteNumberValue(value);

        json.WriteEndArray();
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<int> Ints(JsonNode? node) =>
        node is JsonArray array ? array.Select(Int).Where(id => id is > 0).Select(id => id!.Value) : Enumerable.Empty<int>();

    private static int? Int(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var number) && number is >= int.MinValue and <= int.MaxValue ? (int)number : null;

    private static bool? Bool(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
