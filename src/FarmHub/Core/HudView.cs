using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FarmHub.Core;

/// <summary>
/// The JSON for the additions to the game's own plant panel in the HUD: a second line for each of its rows (keyed by
/// the pot's HUD id), the empty pots it does not list, and the Tend Garden plan for the patrol button's tip. Items are
/// ids the page names from the catalog (CatalogView).
/// </summary>
public static class HudView
{
    public static string NotReady() => "{\"ready\":false}";

    /// <param name="fromPresets">Whether Tend Garden also replants, plants and fertilizes from the presets.</param>
    public static string Ready(FarmWorld world, PresetBook book, int rotWarnHours, bool fromPresets)
    {
        var plan = FarmView.Plan(world, book);
        var shown = world.Pots.Where(pot => world.Catalog.Kinds.ContainsKey(pot.KindId)).ToList();
        var replants = Replants(world, book, plan, shown);

        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteBoolean("ready", true);
            json.WriteBoolean("fromPresets", fromPresets);
            json.WriteNumber("stamina", world.Stamina is { } stamina ? (int)Math.Floor(stamina) : -1);
            json.WriteNumber("cost", world.ChoreCost);
            json.WriteStartArray("pots");

            foreach (var pot in shown.Where(pot => pot.State != PotState.Empty && world.ShowIds.ContainsKey(pot.Id))) WritePot(json, world, pot, rotWarnHours, replants);

            json.WriteEndArray();
            json.WriteStartArray("empty");

            foreach (var pot in FarmRun.FloorFirst(world).Where(pot => plan.Plant.ContainsKey(pot.Id))) WriteEmpty(json, world, pot, plan.Plant[pot.Id]);

            json.WriteEndArray();
            WriteChores(json, shown);
            json.WriteNumber("replant", shown.Count(pot => pot.State == PotState.Ripe && !pot.IsPending && pot.RobotId == 0));
            json.WriteNumber("plant", plan.Plant.Values.Count(choice => choice != null));
            json.WriteNumber("fert", plan.Fertilize.Values.Count(fertId => fertId != 0));

            var noSeed = FarmRun.FloorFirst(world).Where(pot => plan.Plant.TryGetValue(pot.Id, out var choice) && choice == null).ToList();

            json.WriteNumber("noSeed", noSeed.Count);
            json.WriteString("noSeedName", noSeed.Count == 0 ? "" : PotName(world, noSeed[0]));
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// The seed each ripe pot would be replanted with from its preset, after the empty pots have claimed theirs, so a
    /// row never promises seed another pot takes first.
    /// </summary>
    public static IReadOnlyDictionary<long, int> Replants(FarmWorld world, PresetBook book, FarmPlan plan, IEnumerable<Pot> pots)
    {
        var reserved = new Dictionary<int, int>();

        foreach (var choice in plan.Plant.Values.Where(choice => choice != null)) reserved[choice!.SeedItemId] = (reserved.TryGetValue(choice.SeedItemId, out var taken) ? taken : 0) + choice.SeedCount;

        var seeds = new Dictionary<long, int>();

        foreach (var pot in pots.Where(pot => pot.State == PotState.Ripe).OrderBy(pot => pot.Floor == world.PlayerFloor ? 0 : 1).ThenBy(pot => pot.Floor).ThenBy(pot => pot.Id))
        {
            var preset = book.PresetFor(world.SaveKey, pot);

            if (preset == null || !world.Catalog.Kinds.TryGetValue(pot.KindId, out var kind)) continue;

            if (Picks.Seed(pot, kind, preset, world, reserved) is { } choice) seeds[pot.Id] = choice.SeedItemId;
        }

        return seeds;
    }

    /// <summary>The one word for what a pot needs first: one of the game's chores, or fertilizer, or "" for nothing.</summary>
    public static string Need(Pot pot) => FarmRun.NeededChore(pot) switch
    {
        TendAction.Harvest => "harvest",
        TendAction.Clear => "clear",
        TendAction.Uproot => "uproot",
        TendAction.Pests => "pests",
        TendAction.Weeds => "weeds",
        TendAction.Water => "water",
        _ => pot.Problems.HasFlag(Problems.NeedsFeed) ? "feed" : "",
    };

    private static void WritePot(Utf8JsonWriter json, FarmWorld world, Pot pot, int rotWarnHours, IReadOnlyDictionary<long, int> replants)
    {
        json.WriteStartObject();
        json.WriteString("show", world.ShowIds[pot.Id].ToString(CultureInfo.InvariantCulture));
        json.WriteString("area", world.AreaName(pot.Area));
        json.WriteString("kind", world.Catalog.KindName(pot.KindId));

        if (pot.State == PotState.Ripe && pot.HoursToRot is { } hours) json.WriteNumber("rot", Math.Round(hours, 1));
        else json.WriteNull("rot");

        json.WriteBoolean("warn", FarmView.IsRottingSoon(pot, rotWarnHours));
        json.WriteString("need", Need(pot));
        json.WriteNumber("then", replants.TryGetValue(pot.Id, out var seedId) ? seedId : 0);
        json.WriteEndObject();
    }

    private static void WriteEmpty(Utf8JsonWriter json, FarmWorld world, Pot pot, PlantChoice? choice)
    {
        json.WriteStartObject();
        json.WriteString("id", pot.Id.ToString(CultureInfo.InvariantCulture));
        json.WriteString("area", world.AreaName(pot.Area));
        json.WriteString("kind", world.Catalog.KindName(pot.KindId));
        json.WriteNumber("kindId", pot.KindId);
        json.WriteNumber("seed", choice?.SeedItemId ?? 0);
        json.WriteNumber("count", choice == null ? 0 : world.Own(choice.SeedItemId));
        json.WriteEndObject();
    }

    /// <summary>The chores Tend Garden would walk to: one per pot, skipping pots a walk or a robot already has.</summary>
    private static void WriteChores(Utf8JsonWriter json, IReadOnlyList<Pot> pots)
    {
        var chores = pots.Where(pot => !pot.IsPending && pot.RobotId == 0).Select(FarmRun.NeededChore).Where(chore => chore != null).ToList();

        json.WriteStartObject("chores");
        json.WriteNumber("harvest", chores.Count(chore => chore == TendAction.Harvest));
        json.WriteNumber("water", chores.Count(chore => chore == TendAction.Water));
        json.WriteNumber("pests", chores.Count(chore => chore == TendAction.Pests));
        json.WriteNumber("weeds", chores.Count(chore => chore == TendAction.Weeds));
        json.WriteNumber("clean", chores.Count(chore => chore is TendAction.Clear or TendAction.Uproot));
        json.WriteEndObject();
    }

    private static string PotName(FarmWorld world, Pot pot)
    {
        var area = world.AreaName(pot.Area);
        var kind = world.Catalog.KindName(pot.KindId);

        return area.Length == 0 ? kind : kind + ", " + area;
    }
}
