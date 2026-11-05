using System;
using System.Collections.Generic;
using ExiledToolkit.Game;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace FarmHub.Adapters;

/// <summary>
/// Tend Garden from the presets. When the game's Replant box is on, its patrol sends a plant walk after each harvest,
/// and that walk opens the game's plant screen at the pot; for a pot whose preset has a seed, the screen is skipped and
/// the pot is sown from the preset instead. After the game's own patrol (the G key or the HUD button) the session also
/// plants the empty pots and fertilizes the growing ones from their presets. Every hook fails soft, back to the game.
/// Static because a Harmony patch has nowhere else to look; the game calls all three on the main thread.
/// </summary>
internal static class TendHooks
{
    private const int PlantAction = 1610;
    private const float ReplantSeconds = 600f;

    private static readonly Dictionary<long, float> ReplantSentAt = new();
    private static readonly List<long> Sows = new();

    private static PatchKit? _hooks;
    private static bool _isPatrolSeen;

    /// <summary>The TendFromPresets setting.</summary>
    public static bool IsOn { get; set; }

    /// <summary>Set while Farm Hub's own Tend all sends the patrol, since that press plants and fertilizes by itself.</summary>
    public static bool IsOwnBatch { get; set; }

    /// <summary>Whether a pot's preset has a seed for it right now; the session answers from its last read of the farm.</summary>
    public static Func<long, bool> CanSow { get; set; } = _ => false;

    public static void Apply(PatchKit hooks)
    {
        _hooks = hooks;
        hooks.Patch(typeof(TendHooks), typeof(PlantChoreBatchManager), nameof(PlantChoreBatchManager.Dispatch), prefix: nameof(NoteReplant),
            done: "Tend Garden's replant walks are noted", consequence: "Tend Garden opens the game's plant screen to replant");
        hooks.Patch(typeof(TendHooks), typeof(AE_OpenPlantPanel), nameof(AE_OpenPlantPanel.Run), prefix: nameof(SowInstead),
            done: "Tend Garden replants from the presets", consequence: "Tend Garden opens the game's plant screen to replant");
        hooks.Patch(typeof(TendHooks), typeof(PlantChoreBatchManager), nameof(PlantChoreBatchManager.OnBatchRequest), postfix: nameof(NotePatrol),
            done: "Tend Garden also plants and fertilizes from the presets", consequence: "Tend Garden does not plant or fertilize from the presets");
    }

    /// <summary>Whether a walk has handed over a pot to sow; asked every frame, so TakeSows builds its list only when there is one.</summary>
    public static bool HasSows => Sows.Count > 0;

    /// <summary>The pots to sow from their presets since the last call, in the order the walks reached them.</summary>
    public static List<long> TakeSows()
    {
        var taken = new List<long>(Sows);

        Sows.Clear();

        return taken;
    }

    /// <summary>Whether the game's own Tend Garden ran since the last call.</summary>
    public static bool TakePatrol()
    {
        var isSeen = _isPatrolSeen;

        _isPatrolSeen = false;

        return isSeen;
    }

    /// <summary>The patrol's plant walk (1610) goes only to a pot it is replanting, so that is how a replant is told apart.</summary>
    private static void NoteReplant(long __0, int __2)
    {
        try
        {
            if (__2 != PlantAction) return;

            var now = Time.unscaledTime;

            ReplantSentAt[__0] = now;

            if (ReplantSentAt.Count > 64) Forget(now);
        }
        catch (Exception ex)
        {
            _hooks?.Fail("noting a replant walk, the game's own screen is used", ex);
        }
    }

    /// <summary>
    /// The plant walk arriving. A replant walk to a pot whose preset has a seed skips the game's screen and is sown on
    /// the next frame, once this walk is done with the pot; anything else opens the screen as usual.
    /// </summary>
    private static bool SowInstead(EffectBtArgs __0, ref bool __result)
    {
        try
        {
            if (!IsOn || ReplantSentAt.Count == 0) return true;

            var parties = EffectBtArgsExtension.GetAttackerAndTarget(__0);

            if (parties.Item3 != InstanceIdType.Furniture || parties.Item2 == null) return true;

            var potId = parties.Item2.InstanceId;

            if (!ReplantSentAt.Remove(potId, out var sentAt) || Time.unscaledTime - sentAt > ReplantSeconds) return true;
            if (ChallengeBlockRules.IsBlocked(ChallengeBlockRules.TagNoPlant) || !CanSow(potId)) return true;

            Sows.Add(potId);

            if (parties.Item4 != null) GameSingleton.Existing<BattleLogicWorld>()?._DynamicEventManager?.OnActionCompleted(parties.Item4.ID);

            __result = true;

            return false;
        }
        catch (Exception ex)
        {
            _hooks?.Fail("replanting from the preset, the game's own screen is used", ex);

            return true;
        }
    }

    private static void NotePatrol()
    {
        if (IsOn && !IsOwnBatch) _isPatrolSeen = true;
    }

    private static void Forget(float now)
    {
        var old = new List<long>();

        foreach (var pair in ReplantSentAt)
        {
            if (now - pair.Value > ReplantSeconds) old.Add(pair.Key);
        }

        foreach (var potId in old) ReplantSentAt.Remove(potId);
    }
}
