using System;
using System.Collections.Generic;
using System.Linq;

namespace FarmHub.Core;

/// <summary>What a press did, for the status line. Names are already in the player's language.</summary>
public abstract record FarmOutcome
{
    private FarmOutcome()
    {
    }

    public sealed record NoGame : FarmOutcome;

    public sealed record NotReady(string Reason) : FarmOutcome;

    public sealed record Gone : FarmOutcome;

    /// <summary>Planting several pots: how many were lined up, how many had no seed left, how many the game refused.</summary>
    public sealed record Planted(int Count, int NoSeed, int RefusedCount) : FarmOutcome;

    public sealed record PlantedOne(string Pot, string Crop) : FarmOutcome;

    public sealed record NothingToPlant : FarmOutcome;

    public sealed record NotEmpty(string Pot) : FarmOutcome;

    public sealed record NoPreset(string Pot) : FarmOutcome;

    public sealed record NoSeedFor(string Pot, string Preset) : FarmOutcome;

    public sealed record Fertilizing(int Count, int NoFert, int RefusedCount) : FarmOutcome;

    public sealed record NothingToFertilize : FarmOutcome;

    public sealed record Tending(string Pot, TendAction Action) : FarmOutcome;

    /// <summary>Tend all: whether the game took its Tend Garden, then the empty pots planted and growing pots fertilized from presets.</summary>
    public sealed record TendedAll(bool IsTending, int PlantedCount, int FertilizedCount) : FarmOutcome;

    public sealed record NothingToTend(string Pot) : FarmOutcome;

    public sealed record Refused(string Pot) : FarmOutcome;

    public sealed record RobotGone : FarmOutcome;

    public sealed record RobotTierSet(string Robot) : FarmOutcome;

    /// <summary>The auto pass: pots replanted, and robot tiers moved to another seed.</summary>
    public sealed record AutoDone(int PlantedCount, int RobotTiers) : FarmOutcome;
}

/// <summary>The presses: plant, fertilize and tend, each read fresh from the game and each failing soft through the port.</summary>
public static class FarmRun
{
    private const float RetrySeconds = 60f;

    /// <param name="onlyPots">Null for every empty pot.</param>
    public static FarmOutcome Plant(IFarmGame game, PresetBook book, IReadOnlyCollection<long>? onlyPots) => WithWorld(game, world =>
    {
        if (onlyPots is { Count: 1 }) return PlantOne(game, book, world, onlyPots.First());

        return PlantWhere(game, book, world, pot => onlyPots == null || onlyPots.Contains(pot.Id));
    });

    /// <param name="onlyPots">Null for every growing pot.</param>
    public static FarmOutcome Fertilize(IFarmGame game, PresetBook book, IReadOnlyCollection<long>? onlyPots) => WithWorld(game, world =>
    {
        var reserved = new Dictionary<int, int>();
        int count = 0, noFert = 0, refused = 0;

        foreach (var pot in FloorFirst(world))
        {
            if (!CanFertilize(pot) || (onlyPots != null && !onlyPots.Contains(pot.Id)) || !world.Catalog.Kinds.TryGetValue(pot.KindId, out var kind)) continue;

            var preset = book.PresetFor(world.SaveKey, pot);
            var fertId = preset == null ? 0 : Picks.Fertilizer(pot.PlantId, kind.Capacity, preset, world, reserved);

            if (fertId == 0) noFert++;
            else if (game.Fertilize(pot.Id, fertId)) count++;
            else refused++;
        }

        return count + noFert + refused == 0 ? new FarmOutcome.NothingToFertilize() : new FarmOutcome.Fertilizing(count, noFert, refused);
    });

    /// <summary>The one chore the pot needs most: harvest, clearing a dead crop, then pests, weeds and water.</summary>
    public static FarmOutcome Tend(IFarmGame game, long potId) => WithWorld(game, world =>
    {
        var pot = world.Pots.FirstOrDefault(found => found.Id == potId);

        if (pot == null) return new FarmOutcome.Gone();

        var name = world.Catalog.KindName(pot.KindId);
        var action = NeededChore(pot);

        if (action == null) return new FarmOutcome.NothingToTend(name);

        return game.Tend(pot.Id, action.Value) ? new FarmOutcome.Tending(name, action.Value) : new FarmOutcome.Refused(name);
    });

    /// <summary>
    /// The game's own Tend Garden (harvest and chores, budgeted by stamina), then the empty pots planted and the growing
    /// pots fertilized from their presets. A game that refuses Tend Garden still gets the planting and fertilizer.
    /// </summary>
    public static FarmOutcome TendAll(IFarmGame game, PresetBook book) => WithWorld(game, world =>
    {
        var isTending = game.TendAll();
        var planted = PlantWhere(game, book, world, _ => true);
        var fertilized = Fertilize(game, book, null);

        return new FarmOutcome.TendedAll(isTending, planted is FarmOutcome.Planted done ? done.Count : 0, fertilized is FarmOutcome.Fertilizing fed ? fed.Count : 0);
    });

    /// <summary>Sets one of a robot's sow settings. A seed whose crop does not fit that pot size is refused before the game sees it.</summary>
    public static FarmOutcome SetRobotTier(IFarmGame game, long robotId, int tier, int seedId, int fertId) => WithWorld(game, world =>
    {
        var robot = world.Robots.FirstOrDefault(found => found.Id == robotId);

        if (robot == null) return new FarmOutcome.RobotGone();

        var fits = tier < robot.Tiers.Count && (seedId == 0 || (world.Catalog.CropOf(seedId) is { } crop && Picks.Fits(crop, robot.Tiers[tier].Capacity)));

        return fits && game.SetRobotTier(robot.Id, tier, seedId, fertId) ? new FarmOutcome.RobotTierSet(robot.Name) : new FarmOutcome.Refused(robot.Name);
    });

    /// <summary>
    /// The auto pass: empty pots whose preset replants on its own, and, when the player turned it on, robot tiers out of
    /// seed. A pot that had no seed or that the game refused waits a minute before it is tried again.
    /// </summary>
    public static FarmOutcome Auto(IFarmGame game, PresetBook book, float now, IDictionary<long, float> retryAt, bool isKeepingRobotsStocked) => WithWorld(game, world =>
    {
        var planted = PlantWhere(game, book, world, pot =>
            book.PresetFor(world.SaveKey, pot)?.AutoReplant == true && !(retryAt.TryGetValue(pot.Id, out var at) && now < at),
            failed => retryAt[failed] = now + RetrySeconds);
        var switched = isKeepingRobotsStocked ? Picks.RobotSwitches(world, book).Count(change => game.SetRobotTier(change.RobotId, change.Tier, change.SeedId, change.FertId)) : 0;

        return new FarmOutcome.AutoDone(planted is FarmOutcome.Planted done ? done.Count : 0, switched);
    });

    /// <summary>The chore a pot needs first, or null when it needs none.</summary>
    public static TendAction? NeededChore(Pot pot) => pot switch
    {
        { State: PotState.Ripe } => TendAction.Harvest,
        { State: PotState.Withered } => TendAction.Clear,
        { State: PotState.Poor } => TendAction.Uproot,
        _ when pot.Problems.HasFlag(Problems.Pests) => TendAction.Pests,
        _ when pot.Problems.HasFlag(Problems.Weeds) => TendAction.Weeds,
        _ when pot.Problems.HasFlag(Problems.Dry) => TendAction.Water,
        _ => null,
    };

    /// <summary>An empty pot that no robot has claimed and no walk is already headed to.</summary>
    public static bool CanPlant(Pot pot) => pot.State == PotState.Empty && !pot.IsPending && pot.RobotId == 0;

    public static bool CanFertilize(Pot pot) => pot.State == PotState.Growing && pot.FertId == 0 && !pot.IsPending;

    /// <summary>The player's floor first, so what they carry goes to the pots around them before the rest.</summary>
    public static IEnumerable<Pot> FloorFirst(FarmWorld world) =>
        world.Pots.OrderBy(pot => pot.Floor == world.PlayerFloor ? 0 : 1).ThenBy(pot => pot.Floor).ThenBy(pot => pot.Id);

    private static FarmOutcome PlantOne(IFarmGame game, PresetBook book, FarmWorld world, long potId)
    {
        var pot = world.Pots.FirstOrDefault(found => found.Id == potId);

        if (pot == null || !world.Catalog.Kinds.TryGetValue(pot.KindId, out var kind)) return new FarmOutcome.Gone();

        var name = kind.Name;

        if (!CanPlant(pot)) return new FarmOutcome.NotEmpty(name);

        var preset = book.PresetFor(world.SaveKey, pot);

        if (preset == null) return new FarmOutcome.NoPreset(name);

        var choice = Picks.Seed(pot, kind, preset, world, new Dictionary<int, int>());

        if (choice == null) return new FarmOutcome.NoSeedFor(name, preset.Name);

        return Plant(game, pot, choice) ? new FarmOutcome.PlantedOne(name, world.Catalog.CropName(choice.PlantId)) : new FarmOutcome.Refused(name);
    }

    private static FarmOutcome PlantWhere(IFarmGame game, PresetBook book, FarmWorld world, Func<Pot, bool> isWanted, Action<long>? onFailed = null)
    {
        var reserved = new Dictionary<int, int>();
        int planted = 0, noSeed = 0, refused = 0;

        foreach (var pot in FloorFirst(world))
        {
            if (!CanPlant(pot) || !world.Catalog.Kinds.TryGetValue(pot.KindId, out var kind) || !isWanted(pot)) continue;

            var preset = book.PresetFor(world.SaveKey, pot);
            var choice = preset == null ? null : Picks.Seed(pot, kind, preset, world, reserved);

            if (choice == null)
            {
                noSeed++;
                onFailed?.Invoke(pot.Id);
                continue;
            }

            if (Plant(game, pot, choice))
            {
                planted++;
                continue;
            }

            refused++;
            onFailed?.Invoke(pot.Id);
        }

        return planted + noSeed + refused == 0 ? new FarmOutcome.NothingToPlant() : new FarmOutcome.Planted(planted, noSeed, refused);
    }

    private static bool Plant(IFarmGame game, Pot pot, PlantChoice choice) => game.Plant(pot.Id, choice.PlantId, choice.SeedCount, choice.SeedItemId, choice.FertId);

    private static FarmOutcome WithWorld(IFarmGame game, Func<FarmWorld, FarmOutcome> press) => game.Read() switch
    {
        FarmRead.Ready ready => press(ready.World),
        FarmRead.NotReady notReady => new FarmOutcome.NotReady(notReady.Reason),
        FarmRead.NoGame => new FarmOutcome.NoGame(),
        _ => throw new InvalidOperationException("unknown farm read"),
    };
}
