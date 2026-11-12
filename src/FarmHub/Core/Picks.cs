using System.Collections.Generic;
using System.Linq;

namespace FarmHub.Core;

/// <summary>What to plant in a pot: a seed, how many go in, and a fertilizer (0 for none).</summary>
public sealed record PlantChoice(int SeedItemId, int PlantId, int SeedCount, int FertId);

/// <summary>A robot sow tier to set to another seed and fertilizer: from the pot-size seeds, or the next seed when its own ran out.</summary>
public sealed record TierSwitch(long RobotId, int Tier, int SeedId, int FertId);

/// <summary>
/// The choices a preset makes. Reserved counts what earlier picks in the same pass have already claimed, so one
/// pass never plans more seeds or fertilizer than the player owns.
/// </summary>
public static class Picks
{
    /// <summary>
    /// The first seed in the pot size's order that fits the pot, can grow there (when the preset skips unsuitable crops)
    /// and still leaves its keep-at-least after filling the pot; null when none does.
    /// </summary>
    public static PlantChoice? Seed(Pot pot, PotKind kind, FarmPreset preset, FarmWorld world, IDictionary<int, int> reserved)
    {
        foreach (var step in preset.SeedsFor(kind.Capacity))
        {
            var crop = world.Catalog.CropOf(step.ItemId);

            if (crop == null || !Fits(crop, kind.Capacity)) continue;
            if (preset.SkipUnsuitable && pot.Unsuitable.Contains(crop.PlantId)) continue;

            var count = kind.Capacity / crop.Size;

            if (Available(world, reserved, step.ItemId) - count < step.KeepAtLeast) continue;

            var fertId = Fertilizer(crop.PlantId, kind.Capacity, preset, world, reserved);

            Reserve(reserved, step.ItemId, count);

            return new PlantChoice(step.ItemId, crop.PlantId, count, fertId);
        }

        return null;
    }

    /// <summary>
    /// The first fertilizer in the crop's order the player has any of, reserving what the pot takes (one per slot, or
    /// all that is left); 0 when the preset does not fertilize or none is left.
    /// </summary>
    public static int Fertilizer(int plantId, int capacity, FarmPreset preset, FarmWorld world, IDictionary<int, int> reserved)
    {
        foreach (var fertId in FertilizerOrder(preset, plantId))
        {
            var available = Available(world, reserved, fertId);

            if (available <= 0) continue;

            Reserve(reserved, fertId, System.Math.Min(capacity, available));

            return fertId;
        }

        return 0;
    }

    /// <summary>The fertilizers a crop is given, in order: its own list, then the preset's unless the crop says none.</summary>
    public static IReadOnlyList<int> FertilizerOrder(FarmPreset preset, int plantId)
    {
        if (!preset.Fertilize) return new int[0];

        var own = preset.FertilizerFor(plantId);

        if (own == null) return preset.Fertilizers;
        if (own.WhenOut == WhenOut.None) return own.Order;

        return own.Order.Concat(preset.Fertilizers.Where(fertId => !own.Order.Contains(fertId))).ToList();
    }

    /// <summary>
    /// What Farm Hub writes for a robot tier the player set to a seed in the window: theirs for the rest of the session,
    /// even when it matches the pot-size seeds.
    /// </summary>
    public const int PlayerPicked = -1;

    private static readonly IReadOnlyDictionary<(long Robot, int Tier), int> NoneFilled = new Dictionary<(long Robot, int Tier), int>();

    /// <summary>
    /// The seed a robot tier gets from its floor preset's seeds for that pot size: the first one that can fill a pot of
    /// that size and still keep its keep-at-least. 0 when none can.
    /// </summary>
    public static int PotSizeSeed(FarmWorld world, FarmPreset preset, int capacity) =>
        preset.SeedsFor(capacity).FirstOrDefault(step => CanFill(world, step.ItemId, capacity, step.KeepAtLeast))?.ItemId ?? 0;

    /// <summary>
    /// Whether a robot tier follows the pot-size seeds. It does when it has no seed set, when its seed is the one Farm
    /// Hub put there, or, when nobody set it this session, when its seed is the one the pot-size seeds pick right now.
    /// The last case keeps a tier Farm Hub filled before the game was restarted. A seed the player picked is theirs.
    /// </summary>
    /// <param name="filled">Per robot and tier, the seed Farm Hub put there this session, or PlayerPicked.</param>
    public static bool IsFollowing(Robot robot, int tier, int potSizeSeed, IReadOnlyDictionary<(long Robot, int Tier), int> filled)
    {
        var seedId = robot.Tiers[tier].SeedId;

        if (seedId == 0) return true;
        if (filled.TryGetValue((robot.Id, tier), out var known)) return known == seedId;

        return seedId == potSizeSeed;
    }

    /// <summary>
    /// For each robot tier that follows the pot-size seeds and does not hold the seed they pick now, that seed with its
    /// crop's first fertilizer the player has. A tier whose pot size has no seed left gets 0.
    /// </summary>
    public static IReadOnlyList<TierSwitch> RobotFills(FarmWorld world, PresetBook book, IReadOnlyDictionary<(long Robot, int Tier), int> filled)
    {
        var fills = new List<TierSwitch>();

        foreach (var robot in world.Robots)
        {
            var preset = book.PresetForFloor(robot.Floor);

            if (preset == null) continue;

            for (var tier = 0; tier < robot.Tiers.Count; tier++)
            {
                var pick = PotSizeSeed(world, preset, robot.Tiers[tier].Capacity);

                if (pick == robot.Tiers[tier].SeedId || !IsFollowing(robot, tier, pick, filled)) continue;

                var fertId = world.Catalog.CropOf(pick) is { } crop ? FertilizerOrder(preset, crop.PlantId).FirstOrDefault(id => world.Own(id) > 0) : 0;

                fills.Add(new TierSwitch(robot.Id, tier, pick, fertId));
            }
        }

        return fills;
    }

    /// <summary>
    /// Writes down each tier nobody set this session that holds the seed the pot-size seeds pick now, so it keeps
    /// following them when that seed runs out and the pick moves on.
    /// </summary>
    public static void NoteFollowing(FarmWorld world, PresetBook book, IDictionary<(long Robot, int Tier), int> filled)
    {
        foreach (var robot in world.Robots)
        {
            var preset = book.PresetForFloor(robot.Floor);

            if (preset == null) continue;

            for (var tier = 0; tier < robot.Tiers.Count; tier++)
            {
                var seedId = robot.Tiers[tier].SeedId;

                if (seedId == 0 || filled.ContainsKey((robot.Id, tier))) continue;
                if (seedId == PotSizeSeed(world, preset, robot.Tiers[tier].Capacity)) filled[(robot.Id, tier)] = seedId;
            }
        }
    }

    /// <summary>
    /// For each robot tier with a seed of the player's that can no longer fill one pot, the next seed in its floor's
    /// preset that can, with that seed's crop's fertilizer. Tiers that follow the pot-size seeds are RobotFills' job.
    /// </summary>
    /// <param name="filled">As for IsFollowing; null when Farm Hub has filled nothing this session.</param>
    public static IReadOnlyList<TierSwitch> RobotSwitches(FarmWorld world, PresetBook book, IReadOnlyDictionary<(long Robot, int Tier), int>? filled = null)
    {
        var switches = new List<TierSwitch>();

        foreach (var robot in world.Robots)
        {
            var preset = book.PresetForFloor(robot.Floor);

            if (preset == null) continue;

            for (var tier = 0; tier < robot.Tiers.Count; tier++)
            {
                var current = robot.Tiers[tier];

                if (current.SeedId == 0 || CanFill(world, current.SeedId, current.Capacity, 0)) continue;
                if (IsFollowing(robot, tier, PotSizeSeed(world, preset, current.Capacity), filled ?? NoneFilled)) continue;

                var next = preset.SeedsFor(current.Capacity).FirstOrDefault(step => step.ItemId != current.SeedId && CanFill(world, step.ItemId, current.Capacity, step.KeepAtLeast));

                if (next == null || world.Catalog.CropOf(next.ItemId) is not { } crop) continue;

                var fertId = FertilizerOrder(preset, crop.PlantId).FirstOrDefault(id => world.Own(id) > 0);

                switches.Add(new TierSwitch(robot.Id, tier, next.ItemId, fertId));
            }
        }

        return switches;
    }

    public static bool Fits(Crop crop, int capacity) => crop.Size > 0 && crop.Size <= capacity;

    private static bool CanFill(FarmWorld world, int seedId, int capacity, int keep)
    {
        var crop = world.Catalog.CropOf(seedId);

        return crop != null && Fits(crop, capacity) && world.Own(seedId) - capacity / crop.Size >= keep;
    }

    private static int Available(FarmWorld world, IDictionary<int, int> reserved, int itemId) =>
        world.Own(itemId) - (reserved.TryGetValue(itemId, out var taken) ? taken : 0);

    private static void Reserve(IDictionary<int, int> reserved, int itemId, int count) =>
        reserved[itemId] = (reserved.TryGetValue(itemId, out var taken) ? taken : 0) + count;
}
