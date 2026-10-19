using System;
using System.Collections.Generic;

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
        FarmOutcome.TendingAll => Texts.TendingAll.Pick(isChinese),
        FarmOutcome.TendAllRefused => Texts.TendAllRefused.Pick(isChinese),
        FarmOutcome.NothingToTend nothing => Texts.NothingToTend.Say(isChinese, nothing.Pot),
        FarmOutcome.Refused refused => Texts.Refused.Say(isChinese, refused.Pot),
        FarmOutcome.RobotGone => Texts.RobotGone.Pick(isChinese),
        FarmOutcome.RobotTierSet set => Texts.RobotTierSet.Say(isChinese, set.Robot),
        FarmOutcome.AutoDone done => Auto(done, isChinese),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown farm outcome"),
    };

    /// <summary>Whether the auto pass has anything worth telling the player: only pots it planted or robots it switched.</summary>
    public static bool IsWorthShowing(FarmOutcome outcome) => outcome is FarmOutcome.AutoDone done && (done.PlantedCount > 0 || done.RobotTiers > 0);

    private static string Batch(Counted done, int count, Phrase shortfall, int shortCount, int refusedCount, bool isChinese)
    {
        var parts = new List<string> { done.Say(isChinese, count) };

        if (shortCount > 0) parts.Add(shortfall.Say(isChinese, shortCount));
        if (refusedCount > 0) parts.Add(Texts.GameRefused.Say(isChinese, refusedCount));

        return string.Join(" ", parts);
    }

    private static string Auto(FarmOutcome.AutoDone done, bool isChinese)
    {
        var parts = new List<string>();

        if (done.PlantedCount > 0) parts.Add(Texts.AutoPlanted.Say(isChinese, done.PlantedCount));
        if (done.RobotTiers > 0) parts.Add(Texts.RobotsSwitched.Say(isChinese, done.RobotTiers));

        return string.Join(" ", parts);
    }
}
