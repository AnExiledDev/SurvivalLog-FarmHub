using System.Collections.Generic;
using System.Linq;

namespace FarmHub.Core;

/// <summary>What to plant in a pot: a seed, how many go in, and a fertilizer (0 for none).</summary>
public sealed record PlantChoice(int SeedItemId, int PlantId, int SeedCount, int FertId);

/// <summary>A robot sow tier to switch to another seed, because the one it has can no longer fill a pot.</summary>
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
    /// For each robot tier whose seed can no longer fill one pot, the next seed in its floor's preset that can, with that
    /// seed's crop's fertilizer. A tier with no seed set is the player's choice and is left alone.
    /// </summary>
    public static IReadOnlyList<TierSwitch> RobotSwitches(FarmWorld world, PresetBook book)
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
