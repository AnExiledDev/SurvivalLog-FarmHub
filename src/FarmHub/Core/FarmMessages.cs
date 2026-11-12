using System;
using System.Collections.Generic;
using ExiledToolkit.Kit;

namespace FarmHub.Core;

/// <summary>The status line for a press, in the player's language.</summary>
public static class FarmMessages
{
    public static string Describe(FarmOutcome outcome, bool isChinese) => outcome switch
    {
        FarmOutcome.NoGame => Texts.NoGame.Pick(isChinese),
        FarmOutcome.NotReady notReady => Texts.NotReady.Say(isChinese, notReady.Reason),
        FarmOutcome.Gone => Texts.Gone.Pick(isChinese),
        FarmOutcome.Planted planted => Batch(Texts.Planted, planted.Count, Texts.NoSeedCount, planted.NoSeed, planted.RefusedCount, isChinese),
        FarmOutcome.PlantedOne one => Texts.PlantedOne.Say(isChinese, one.Pot, one.Crop),
        FarmOutcome.NothingToPlant => Texts.NothingToPlant.Pick(isChinese),
        FarmOutcome.NotEmpty notEmpty => Texts.NotEmpty.Say(isChinese, notEmpty.Pot),
        FarmOutcome.NoPreset none => Texts.NoPreset.Say(isChinese, none.Pot),
        FarmOutcome.NoSeedFor noSeed => Texts.NoSeedFor.Say(isChinese, noSeed.Pot, noSeed.Preset),
        FarmOutcome.Fertilizing fertilizing => Batch(Texts.Fertilizing, fertilizing.Count, Texts.NoFertCount, fertilizing.NoFert, fertilizing.RefusedCount, isChinese),
        FarmOutcome.NothingToFertilize => Texts.NothingToFertilize.Pick(isChinese),
        FarmOutcome.Tending tending => Texts.Tending.Say(isChinese, tending.Pot, Texts.ChoreName(tending.Action).Pick(isChinese)),
        FarmOutcome.TendedAll all => TendedAll(all, isChinese),
        FarmOutcome.NothingToTend nothing => Texts.NothingToTend.Say(isChinese, nothing.Pot),
        FarmOutcome.Refused refused => Texts.Refused.Say(isChinese, refused.Pot),
        FarmOutcome.RobotGone => Texts.RobotGone.Pick(isChinese),
        FarmOutcome.RobotTierSet set => Texts.RobotTierSet.Say(isChinese, set.Robot),
        FarmOutcome.AutoDone done => Auto(done, isChinese),
        FarmOutcome.FromPresets presets => FromPresets(presets, isChinese),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown farm outcome"),
    };

    /// <summary>Whether the auto pass has anything worth telling the player: pots it planted, and robot tiers it switched or found out of seeds.</summary>
    public static bool IsWorthShowing(FarmOutcome outcome) => outcome switch
    {
        FarmOutcome.AutoDone done => done.PlantedCount > 0 || done.RobotTiers > 0 || done.RobotTiersEmptied > 0,
        FarmOutcome.FromPresets presets => presets.PlantedCount > 0 || presets.FertilizedCount > 0,
        _ => false,
    };

    private static string Batch(Counted done, int count, Phrase shortfall, int shortCount, int refusedCount, bool isChinese)
    {
        var parts = new List<string> { done.Say(isChinese, count) };

        if (shortCount > 0) parts.Add(shortfall.Say(isChinese, shortCount));
        if (refusedCount > 0) parts.Add(Texts.GameRefused.Say(isChinese, refusedCount));

        return string.Join(" ", parts);
    }

    private static string TendedAll(FarmOutcome.TendedAll all, bool isChinese)
    {
        var parts = new List<string> { (all.IsTending ? Texts.TendingAll : Texts.TendAllRefused).Pick(isChinese) };

        if (all.PlantedCount > 0) parts.Add(Texts.Planted.Say(isChinese, all.PlantedCount));
        if (all.FertilizedCount > 0) parts.Add(Texts.Fertilizing.Say(isChinese, all.FertilizedCount));

        return string.Join(" ", parts);
    }

    private static string FromPresets(FarmOutcome.FromPresets presets, bool isChinese)
    {
        var parts = new List<string> { Texts.FromPresetsDone.Pick(isChinese) };

        if (presets.PlantedCount > 0) parts.Add(Texts.Planted.Say(isChinese, presets.PlantedCount));
        if (presets.FertilizedCount > 0) parts.Add(Texts.Fertilizing.Say(isChinese, presets.FertilizedCount));

        return string.Join(" ", parts);
    }

    private static string Auto(FarmOutcome.AutoDone done, bool isChinese)
    {
        var parts = new List<string>();

        if (done.PlantedCount > 0) parts.Add(Texts.AutoPlanted.Say(isChinese, done.PlantedCount));
        if (done.RobotTiers > 0) parts.Add(Texts.RobotsSwitched.Say(isChinese, done.RobotTiers));
        if (done.RobotTiersEmptied > 0) parts.Add(Texts.RobotsOutOfSeeds.Say(isChinese, done.RobotTiersEmptied));

        return string.Join(" ", parts);
    }
}
