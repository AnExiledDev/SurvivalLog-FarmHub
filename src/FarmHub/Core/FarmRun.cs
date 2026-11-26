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

    /// <summary>After the game's Tend Garden: the empty pots planted and growing pots fertilized from presets.</summary>
    public sealed record FromPresets(int PlantedCount, int FertilizedCount) : FarmOutcome;

    public sealed record Refused(string Pot) : FarmOutcome;

    public sealed record RobotGone : FarmOutcome;

    public sealed record RobotTierSet(string Robot) : FarmOutcome;

    /// <summary>The auto pass: pots replanted, and robot tiers moved to another seed.</summary>
    public sealed record AutoDone(int PlantedCount, int RobotTiers, int RobotTiersEmptied = 0) : FarmOutcome;
}

/// <summary>The presses: plant, fertilize and tend, each read fresh from the game and each failing soft through the port.</summary>
public static class FarmRun
{
    private const double RetrySeconds = 60;

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
    /// The game's Tend Garden (harvest and chores, budgeted by stamina), then the empty pots planted and the growing
    /// pots fertilized from their presets. A game that refuses Tend Garden still gets the planting and fertilizer.
    /// </summary>
    public static FarmOutcome TendAll(IFarmGame game, PresetBook book) => WithWorld(game, world =>
    {
        var isTending = game.TendAll();
        var planted = PlantWhere(game, book, world, _ => true);
        var fertilized = Fertilize(game, book, null);

        return new FarmOutcome.TendedAll(isTending, planted is FarmOutcome.Planted done ? done.Count : 0, fertilized is FarmOutcome.Fertilizing fed ? fed.Count : 0);
    });

    /// <summary>What Tend Garden adds when the player sent it from the game: the empty pots planted and the growing pots fertilized from their presets.</summary>
    public static FarmOutcome FromPresets(IFarmGame game, PresetBook book) => WithWorld(game, world =>
    {
        var planted = PlantWhere(game, book, world, _ => true);
        var fertilized = Fertilize(game, book, null);

        return new FarmOutcome.FromPresets(planted is FarmOutcome.Planted done ? done.Count : 0, fertilized is FarmOutcome.Fertilizing fed ? fed.Count : 0);
    });

    /// <summary>
    /// Sets one of a robot's sow settings from the window. A seed whose crop does not fit that pot size is refused
    /// before the game sees it. Seed 0 hands the tier to the pot-size seeds; any other seed is the player's pick.
    /// </summary>
    /// <param name="filled">As for Auto, told who set the tier.</param>
    public static FarmOutcome SetRobotTier(IFarmGame game, long robotId, int tier, int seedId, int fertId, Dictionary<(long Robot, int Tier), int>? filled = null) =>
        WithWorld(game, world =>
        {
            var robot = world.Robots.FirstOrDefault(found => found.Id == robotId);

            if (robot == null) return new FarmOutcome.RobotGone();

            var fits = tier < robot.Tiers.Count && (seedId == 0 || (world.Catalog.CropOf(seedId) is { } crop && Picks.Fits(crop, robot.Tiers[tier].Capacity)));

            if (!fits || !game.SetRobotTier(robot.Id, tier, seedId, fertId)) return new FarmOutcome.Refused(robot.Name);

            if (seedId == 0) filled?.Remove((robot.Id, tier));
            else if (filled != null) filled[(robot.Id, tier)] = Picks.PlayerPicked;

            return new FarmOutcome.RobotTierSet(robot.Name);
        });

    /// <summary>
    /// The auto pass: empty pots whose preset replants on its own, robot tiers that follow the pot-size seeds, and, when
    /// the player turned it on, robot tiers whose own seed ran out. A pot that had no seed or that the game refused
    /// waits a minute before it is tried again.
    /// </summary>
    /// <param name="filled">Per robot and tier, the seed Farm Hub put there this session (see Picks.IsFollowing); each fill is written in.</param>
    public static FarmOutcome Auto(IFarmGame game, PresetBook book, double now, IDictionary<long, double> retryAt, bool isKeepingRobotsStocked,
        Dictionary<(long Robot, int Tier), int>? filled = null) => WithWorld(game, world =>
    {
        filled ??= new Dictionary<(long Robot, int Tier), int>();
        Picks.NoteFollowing(world, book, filled);

        // Both lists come from the same read, before either is written, so a tier is never both filled and switched.
        var fills = Picks.RobotFills(world, book, filled);
        var switches = isKeepingRobotsStocked ? Picks.RobotSwitches(world, book, filled) : Array.Empty<TierSwitch>();
        var planted = PlantWhere(game, book, world, pot => IsAutoReplanted(world, book, pot, now, retryAt), failed => retryAt[failed] = now + RetrySeconds);
        var setFills = fills.Where(fill => SetFilledTier(game, fill, filled)).ToList();
        var emptied = setFills.Count(fill => fill.SeedId == 0);
        var robotTiers = setFills.Count - emptied + switches.Count(change => game.SetRobotTier(change.RobotId, change.Tier, change.SeedId, change.FertId));

        return new FarmOutcome.AutoDone(planted is FarmOutcome.Planted done ? done.Count : 0, robotTiers, emptied);
    });

    private static bool SetFilledTier(IFarmGame game, TierSwitch fill, Dictionary<(long Robot, int Tier), int> filled)
    {
        if (!game.SetRobotTier(fill.RobotId, fill.Tier, fill.SeedId, fill.FertId)) return false;

        if (fill.SeedId == 0) filled.Remove((fill.RobotId, fill.Tier));
        else filled[(fill.RobotId, fill.Tier)] = fill.SeedId;

        return true;
    }

    /// <summary>Whether any preset replants on its own.</summary>
    public static bool HasAutoReplant(PresetBook book)
    {
        foreach (var preset in book.Presets)
        {
            if (preset.AutoReplant) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether Auto, run now on this world, would try anything: an empty pot it would replant (or find out of seed), or
    /// a robot tier it would fill or switch. The caller skips Auto while this is false.
    /// </summary>
    /// <param name="filled">As for Auto; null when Farm Hub has filled nothing this session.</param>
    public static bool HasAutoWork(FarmWorld world, PresetBook book, double now, IDictionary<long, double> retryAt, bool isKeepingRobotsStocked,
        Dictionary<(long Robot, int Tier), int>? filled = null)
    {
        foreach (var pot in world.Pots)
        {
            if (CanPlant(pot) && world.Catalog.Kinds.ContainsKey(pot.KindId) && IsAutoReplanted(world, book, pot, now, retryAt)) return true;
        }

        filled ??= new Dictionary<(long Robot, int Tier), int>();

        return Picks.RobotFills(world, book, filled).Count > 0 || (isKeepingRobotsStocked && Picks.RobotSwitches(world, book, filled).Count > 0);
    }

    /// <summary>The auto pass's own test of a pot, before the pot's seed is picked: its preset replants and it is not waiting out a retry.</summary>
    private static bool IsAutoReplanted(FarmWorld world, PresetBook book, Pot pot, double now, IDictionary<long, double> retryAt) =>
        book.PresetFor(world.SaveKey, pot)?.AutoReplant == true && !(retryAt.TryGetValue(pot.Id, out var at) && now < at);

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
