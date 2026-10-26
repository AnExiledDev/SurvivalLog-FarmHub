using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FarmHub.Core;

/// <summary>The settings the window shows and changes: the panel key, the rotting-soon window and whether robots are kept stocked.</summary>
public sealed record ViewSettings(string Key, int RotWarnHours, bool IsKeepingRobotsStocked);

/// <summary>What a press on every pot would do right now: the planting for each empty pot and the fertilizer for each growing one.</summary>
public sealed record FarmPlan(IReadOnlyDictionary<long, PlantChoice?> Plant, IReadOnlyDictionary<long, int> Fertilize);

/// <summary>
/// The JSON the window draws: counts of what needs the player, every pot with its timing, problems and what a press
/// would plant in it, the planter robots, and the presets. Names of items live in the catalog, sent once (CatalogView).
/// </summary>
public static class FarmView
{
    public static string NotReady(string note, ViewSettings settings) => Write(json =>
    {
        json.WriteBoolean("ready", false);
        json.WriteString("note", note);
        WriteSettings(json, settings);
    });

    public static string Ready(FarmWorld world, PresetBook book, ViewSettings settings) => Ready(world, book, settings, Plan(world, book));

    /// <param name="plan">Plan of this world and book, when the caller has it already.</param>
    public static string Ready(FarmWorld world, PresetBook book, ViewSettings settings, FarmPlan plan) => Write(json =>
    {
        var pots = Shown(world).OrderBy(pot => pot.Floor).ThenBy(pot => pot.Area).ThenBy(pot => pot.Id).ToList();

        json.WriteBoolean("ready", true);
        json.WriteBoolean("saved", world.SaveKey != null);
        json.WriteNumber("floor", world.PlayerFloor);
        WriteCounts(json, pots, plan, settings);
        json.WriteStartArray("pots");

        foreach (var pot in pots) WritePot(json, world, book, pot, plan, settings);

        json.WriteEndArray();
        WriteRooms(json, world, pots);
        json.WriteStartArray("robots");

        foreach (var robot in world.Robots.OrderBy(robot => robot.Floor).ThenBy(robot => robot.Id)) WriteRobot(json, robot);

        json.WriteEndArray();
        json.WriteStartArray("presets");

        foreach (var preset in book.Presets) WritePreset(json, book, preset);

        json.WriteEndArray();
        json.WriteStartObject("owned");

        foreach (var (itemId, count) in world.Owned.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key)) json.WriteNumber(itemId.ToString(CultureInfo.InvariantCulture), count);

        json.WriteEndObject();
        WriteSettings(json, settings);
    });

    /// <summary>
    /// The number on the HUD button: pots with a chore waiting (ripe, dead, pests, weeds or dry) and empty pots a
    /// press of Plant would fill. An empty pot with no seed left is not counted, since nothing the player presses helps.
    /// </summary>
    public static int NeedsYou(FarmWorld world, PresetBook book) => NeedsYou(world, Plan(world, book));

    /// <param name="plan">Plan of this world and its book.</param>
    public static int NeedsYou(FarmWorld world, FarmPlan plan)
    {
        var count = 0;

        foreach (var pot in world.Pots)
        {
            if (!world.Catalog.Kinds.ContainsKey(pot.KindId)) continue;

            if (FarmRun.NeededChore(pot) != null || plan.Plant.TryGetValue(pot.Id, out var choice) && choice != null) count++;
        }

        return count;
    }

    /// <summary>
    /// What Plant and Fertilize on every pot would use, in the same order and from the same shared stock as the presses,
    /// so two pots never both show the last seed.
    /// </summary>
    public static FarmPlan Plan(FarmWorld world, PresetBook book)
    {
        var reserved = new Dictionary<int, int>();
        var plant = new Dictionary<long, PlantChoice?>();
        var fertilize = new Dictionary<long, int>();

        foreach (var pot in FarmRun.FloorFirst(world))
        {
            if (!world.Catalog.Kinds.TryGetValue(pot.KindId, out var kind)) continue;

            var preset = book.PresetFor(world.SaveKey, pot);

            if (FarmRun.CanPlant(pot)) plant[pot.Id] = preset == null ? null : Picks.Seed(pot, kind, preset, world, reserved);
            else if (FarmRun.CanFertilize(pot)) fertilize[pot.Id] = preset == null ? 0 : Picks.Fertilizer(pot.PlantId, kind.Capacity, preset, world, reserved);
        }

        return new FarmPlan(plant, fertilize);
    }

    public static bool IsRottingSoon(Pot pot, int rotWarnHours) => pot.State == PotState.Ripe && pot.HoursToRot is { } hours && hours <= rotWarnHours;

    /// <summary>A pot's growth as the window shows it, whole percent.</summary>
    internal static int PercentShown(float progress) => (int)Math.Round(Math.Clamp(progress, 0f, 1f) * 100f, MidpointRounding.AwayFromZero);

    /// <summary>Hours as the window shows them, to the tenth, never below 0.</summary>
    internal static double HoursShown(float hours) => Math.Round(Math.Max(0f, hours), 1);

    /// <summary>A robot's battery as the window shows it, whole percent.</summary>
    internal static int BatteryShown(Robot robot) => robot.BatteryMax > 0f ? (int)Math.Round(Math.Clamp(robot.Battery / robot.BatteryMax, 0f, 1f) * 100f, MidpointRounding.AwayFromZero) : 0;

    private static IEnumerable<Pot> Shown(FarmWorld world) => world.Pots.Where(pot => world.Catalog.Kinds.ContainsKey(pot.KindId));

    private static void WriteCounts(Utf8JsonWriter json, IReadOnlyList<Pot> pots, FarmPlan plan, ViewSettings settings)
    {
        json.WriteStartObject("counts");
        json.WriteNumber("total", pots.Count);
        json.WriteNumber("ripe", pots.Count(pot => pot.State == PotState.Ripe));
        json.WriteNumber("rot", pots.Count(pot => IsRottingSoon(pot, settings.RotWarnHours)));
        json.WriteNumber("pests", pots.Count(pot => (pot.Problems & (Problems.Pests | Problems.Weeds)) != 0));
        json.WriteNumber("dry", pots.Count(pot => pot.Problems.HasFlag(Problems.Dry)));
        json.WriteNumber("empty", pots.Count(pot => pot.State == PotState.Empty));
        json.WriteNumber("plant", plan.Plant.Values.Count(choice => choice != null));
        json.WriteNumber("fert", plan.Fertilize.Values.Count(fertId => fertId != 0));
        json.WriteEndObject();
    }

    private static void WritePot(Utf8JsonWriter json, FarmWorld world, PresetBook book, Pot pot, FarmPlan plan, ViewSettings settings)
    {
        var (preset, from) = PresetOf(book, world.SaveKey, pot);

        json.WriteStartObject();
        json.WriteString("id", pot.Id.ToString(CultureInfo.InvariantCulture));
        json.WriteNumber("kind", pot.KindId);
        json.WriteNumber("floor", pot.Floor);
        json.WriteNumber("area", pot.Area);
        json.WriteString("room", world.AreaName(pot.Area));
        json.WriteString("state", StateName(pot.State));
        json.WriteNumber("plant", pot.PlantId);
        WriteProblems(json, pot.Problems);
        json.WriteNumber("progress", PercentShown(pot.Progress));
        WriteHours(json, "ripeIn", pot.State == PotState.Growing ? pot.HoursToRipe : null);
        WriteHours(json, "rotIn", pot.State == PotState.Ripe ? pot.HoursToRot : null);
        json.WriteBoolean("rotSoon", IsRottingSoon(pot, settings.RotWarnHours));
        json.WriteNumber("fert", pot.FertId);
        json.WriteBoolean("pending", pot.IsPending);
        json.WriteBoolean("robot", pot.RobotId != 0);
        json.WriteBoolean("powered", pot.IsPowered);
        json.WriteString("preset", preset?.Id ?? "");
        json.WriteString("from", from);

        if (plan.Plant.TryGetValue(pot.Id, out var choice))
        {
            json.WriteBoolean("canPlant", true);

            if (choice != null)
            {
                json.WriteStartObject("next");
                json.WriteNumber("seed", choice.SeedItemId);
                json.WriteNumber("count", choice.SeedCount);
                json.WriteNumber("fert", choice.FertId);
                json.WriteEndObject();
            }
        }
        else
        {
            json.WriteBoolean("canPlant", false);
        }

        if (plan.Fertilize.TryGetValue(pot.Id, out var fertId)) json.WriteNumber("nextFert", fertId);

        json.WriteString("chore", FarmRun.NeededChore(pot) is { } chore ? ChoreName(chore) : "");
        json.WriteEndObject();
    }

    /// <summary>The preset a pot follows and where it comes from: the pot's own choice, its room, its floor, or everywhere.</summary>
    private static (FarmPreset? Preset, string From) PresetOf(PresetBook book, string? saveKey, Pot pot)
    {
        if (book.Find(book.OverrideFor(saveKey, pot.Id)) is { } own) return (own, "pot");

        foreach (var (use, from) in new[] { (PresetBook.AreaUse(pot.Area), "room"), (PresetBook.FloorUse(pot.Floor), "floor"), (PresetBook.AllUse, "all") })
        {
            if (book.Uses.TryGetValue(use, out var id) && book.Find(id) is { } preset) return (preset, from);
        }

        return (null, "");
    }

    /// <summary>The rooms that hold pots, with their floor, for the preset editor's "used for" choices.</summary>
    private static void WriteRooms(Utf8JsonWriter json, FarmWorld world, IReadOnlyList<Pot> pots)
    {
        json.WriteStartArray("rooms");

        foreach (var room in pots.Where(pot => pot.Area != 0).GroupBy(pot => pot.Area).OrderBy(group => group.Min(pot => pot.Floor)).ThenBy(group => group.Key))
        {
            json.WriteStartObject();
            json.WriteNumber("area", room.Key);
            json.WriteString("name", world.AreaName(room.Key));
            json.WriteNumber("floor", room.Min(pot => pot.Floor));
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("floors");

        foreach (var floor in pots.Select(pot => pot.Floor).Concat(world.Robots.Select(robot => robot.Floor)).Distinct().OrderBy(floor => floor)) json.WriteNumberValue(floor);

        json.WriteEndArray();
    }

    private static void WriteRobot(Utf8JsonWriter json, Robot robot)
    {
        json.WriteStartObject();
        json.WriteString("id", robot.Id.ToString(CultureInfo.InvariantCulture));
        json.WriteString("name", robot.Name);
        json.WriteNumber("floor", robot.Floor);
        json.WriteNumber("battery", BatteryShown(robot));
        json.WriteBoolean("on", robot.IsOn);
        json.WriteStartArray("tiers");

        foreach (var tier in robot.Tiers)
        {
            json.WriteStartObject();
            json.WriteNumber("cap", tier.Capacity);
            json.WriteNumber("seed", tier.SeedId);
            json.WriteNumber("fert", tier.FertId);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WritePreset(Utf8JsonWriter json, PresetBook book, FarmPreset preset)
    {
        json.WriteStartObject();
        PresetJson.WritePresetFields(json, preset);
        json.WriteStartArray("uses");

        foreach (var use in book.UsesOf(preset.Id)) json.WriteStringValue(use);

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteProblems(Utf8JsonWriter json, Problems problems)
    {
        json.WriteStartArray("problems");

        foreach (var (flag, name) in ProblemNames)
        {
            if (problems.HasFlag(flag)) json.WriteStringValue(name);
        }

        json.WriteEndArray();
    }

    private static readonly (Problems Flag, string Name)[] ProblemNames =
    {
        (Problems.Pests, "pests"), (Problems.Weeds, "weeds"), (Problems.Dry, "dry"), (Problems.TooDark, "dark"),
        (Problems.TooBright, "bright"), (Problems.TooCold, "cold"), (Problems.NeedsFeed, "feed"),
    };

    private static void WriteHours(Utf8JsonWriter json, string name, float? hours)
    {
        if (hours is { } value) json.WriteNumber(name, HoursShown(value));
        else json.WriteNull(name);
    }

    private static void WriteSettings(Utf8JsonWriter json, ViewSettings settings)
    {
        json.WriteStartObject("settings");
        json.WriteString("key", settings.Key);
        json.WriteNumber("rotWarn", settings.RotWarnHours);
        json.WriteBoolean("robots", settings.IsKeepingRobotsStocked);
        json.WriteEndObject();
    }

    private static string StateName(PotState state) => state switch
    {
        PotState.Empty => "empty",
        PotState.Growing => "growing",
        PotState.Ripe => "ripe",
        PotState.Withered => "withered",
        PotState.Poor => "poor",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "unknown pot state"),
    };

    private static string ChoreName(TendAction action) => action switch
    {
        TendAction.Harvest => "harvest",
        TendAction.Clear => "clear",
        TendAction.Uproot => "uproot",
        TendAction.Pests => "pests",
        TendAction.Weeds => "weeds",
        TendAction.Water => "water",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "unknown chore"),
    };

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            body(json);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>The catalog as the window needs it, sent once per game and language: pot kinds, crops, seeds and fertilizers.</summary>
public static class CatalogView
{
    public static string Write(FarmCatalog catalog)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteStartArray("kinds");

            foreach (var kind in catalog.Kinds.Values.OrderBy(kind => kind.Capacity).ThenBy(kind => kind.Id))
            {
                json.WriteStartObject();
                json.WriteNumber("id", kind.Id);
                json.WriteString("name", kind.Name);
                json.WriteString("icon", kind.Icon);
                json.WriteNumber("cap", kind.Capacity);
                json.WriteNumber("light", Math.Round(kind.Light, 2));
                json.WriteNumber("heat", Math.Round(kind.Heat, 2));
                json.WriteBoolean("power", kind.NeedsPower);
                json.WriteNumber("faster", Math.Round(kind.Faster, 2));
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartArray("crops");

            foreach (var crop in catalog.Crops.Values.OrderBy(crop => crop.PlantId))
            {
                json.WriteStartObject();
                json.WriteNumber("id", crop.PlantId);
                json.WriteString("name", crop.Name);
                json.WriteString("icon", crop.Icon);
                json.WriteNumber("size", crop.Size);
                json.WriteNumber("grow", Math.Round(crop.GrowHours, 1));
                json.WriteNumber("harvest", Math.Round(crop.HarvestHours, 1));
                json.WriteBoolean("feed", crop.NeedsDailyFeed);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartArray("seeds");

            foreach (var seed in catalog.Seeds.Values.OrderBy(seed => seed.ItemId))
            {
                json.WriteStartObject();
                json.WriteNumber("id", seed.ItemId);
                json.WriteString("name", seed.Name);
                json.WriteString("icon", seed.Icon);
                json.WriteNumber("plant", seed.PlantId);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartArray("ferts");

            foreach (var fert in catalog.Fertilizers.Values.OrderBy(fert => fert.ItemId))
            {
                json.WriteStartObject();
                json.WriteNumber("id", fert.ItemId);
                json.WriteString("name", fert.Name);
                json.WriteString("icon", fert.Icon);
                json.WriteNumber("speed", Math.Round(fert.Speed, 2));
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
