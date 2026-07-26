using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Builder;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Commands;

internal sealed record MusicNoteVolleyRequest(
    Creature Dealer,
    int Count,
    int BounceCount,
    decimal BaseDamage,
    decimal CapturedDamageAdditive,
    string VfxPath,
    Creature? VisualDealer,
    Creature? Target,
    AbstractModel? Source
);

internal sealed class MusicNoteVolleyRunner(MusicNoteVolleyRequest request)
{
    private const float NoteSpawnInterval = 0.05f;
    private const int NoteGroupSize = 8;
    private const float CastLeadTime = 0.1f;

    private static readonly Lock ActiveVolleysLock = new();
    private static readonly Dictionary<ICombatState, int> ActiveVolleys =
        new(ReferenceEqualityComparer.Instance);

    private readonly VfxCreator<MusicNoteFlyingVfx> _creator = new(request.VfxPath);
    private VfxContext? _lastRootContext;

    public async Task RunAsync()
    {
        if (!CanResolveCombat() || request.Count <= 0)
            return;

        var combatState = request.Dealer.CombatState!;
        RegisterVolley(combatState);
        try
        {
            await ResolveVolleyAsync();
        }
        finally
        {
            await CompleteVolleyAsync(combatState);
        }
    }

    private async Task ResolveVolleyAsync()
    {
        var rootNotes = new List<Task>(request.Count);
        var groupDirection = Rng.Chaotic.NextInt(0, 1) == 0 ? -1f : 1f;

        for (var index = 0; index < request.Count; index++)
        {
            if (!CanResolveCombat())
                break;

            var isNewGroup = index % NoteGroupSize == 0;
            if (isNewGroup)
            {
                if (index > 0)
                    groupDirection = -groupDirection;

                if (request.VisualDealer == null || request.VisualDealer == request.Dealer)
                {
                    await CreatureCmd.TriggerAnim(request.Dealer, "Cast", 0);
                    await Cmd.Wait(CastLeadTime);
                }
            }
            else
            {
                await Cmd.Wait(NoteSpawnInterval);
            }

            var target = request.Target ?? SelectTarget();
            if (target == null)
                break;

            var vfx = CreateVfx(index, request.Count, groupDirection);
            _lastRootContext = vfx.Context;
            rootNotes.Add(RunNoteAsync(vfx, target, request.VisualDealer, request.BounceCount));
        }

        if (rootNotes.Count == 0)
            return;

        await Task.WhenAll(rootNotes);

        if (_lastRootContext != null && CanResolveCombat())
        {
            await CombatEffectQueue.Shared.Enqueue(async () =>
            {
                if (CanResolveCombat() && request.Dealer.Player != null)
                {
                    await BangDreamHook.AfterMusicNoteShot(
                        request.Dealer.CombatState!,
                        _lastRootContext,
                        request.Dealer.Player);
                }
            });
            BangDreamVfxManager.NotifyCombatStateChanged();
        }
    }

    private MusicNoteFlyingVfx CreateVfx(int index, int total, float curveDirection)
    {
        return _creator.Create(vfx =>
        {
            var laneIndex = index % NoteGroupSize;
            var groupStart = index / NoteGroupSize * NoteGroupSize;
            var laneCount = Math.Min(NoteGroupSize, total - groupStart);
            vfx.Context.Set("total", total);
            vfx.Context.Set("index", index);
            vfx.SetTrajectoryLane(laneIndex, laneCount, curveDirection);
        });
    }

    private async Task RunNoteAsync(MusicNoteFlyingVfx vfx, Creature target, Creature? visualDealer,
        int bounceCount)
    {
        var sourceNode = (visualDealer ?? request.Dealer).GetCreatureNode();
        var targetNode = target.GetCreatureNode();
        var manager = BangDreamVfxManager.Instance;
        vfx.Context.Set("IsPrototype", visualDealer == null);
        vfx.Context.Set("Target", target);

        if (manager != null && sourceNode != null && targetNode != null)
        {
            try
            {
                vfx.SetPath(sourceNode.VfxSpawnPosition, targetNode.VfxSpawnPosition);
                var handle = manager.SubmitVfx(vfx);
                var arrival = await handle.Arrived;

                if (arrival != VfxResult.CombatEnded && CanResolveCombat())
                    await QueueDamageAsync(target);

                await handle.Finished;
            }
            catch (Exception e)
            {
                BangDreamLibCore.Logger.Error($"Music note VFX submission error: {e}");
                vfx.QueueFreeSafely();
                if (CanResolveCombat())
                    await QueueDamageAsync(target);
            }
        }
        else
        {
            vfx.QueueFreeSafely();
            if (CanResolveCombat())
                await QueueDamageAsync(target);
        }

        await TryBounceAsync(target, bounceCount);
    }

    private Task QueueDamageAsync(Creature target)
    {
        return CombatEffectQueue.Shared.Enqueue(async () =>
        {
            if (!CanResolveCombat() || !target.IsHittable)
                return;

            var damage = BangDreamHook.ModifyMusicNoteDamage(
                request.Dealer.CombatState!,
                target,
                request.Dealer,
                request.BaseDamage + request.CapturedDamageAdditive,
                request.Source,
                ModifyDamageHookType.All
            );

            var results = await CreatureCmd.Damage(
                choiceContext: new BlockingPlayerChoiceContext(),
                target: target,
                damageVar: new DamageVar(damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim),
                dealer: request.Dealer,
                cardSource: request.Source as CardModel,
                cardPlay: null
            );

            var damageTracker = request.Dealer.Player?.AttachedData().MusicNoteDamageTracker;
            if (damageTracker == null)
                return;

            foreach (var damageResult in results)
                damageTracker.AddMusicNoteDamage(request.Dealer.CombatState!.RoundNumber, damageResult);
        });
    }

    private async Task TryBounceAsync(Creature oldTarget, int bounceCount)
    {
        if (bounceCount <= 0 || !CanResolveCombat())
            return;

        if (CombatManager.Instance.IsEnding)
            return;

        var newTarget = SelectTarget(oldTarget);
        if (newTarget == null)
            return;

        var vfx = CreateVfx(0, 1, Rng.Chaotic.NextInt(0, 1) == 0 ? -1f : 1f);
        await RunNoteAsync(vfx, newTarget, oldTarget, bounceCount - 1);
    }

    private async Task CompleteVolleyAsync(ICombatState combatState)
    {
        if (!UnregisterVolley(combatState))
            return;

        await CombatEffectQueue.Shared.Enqueue(async () =>
        {
            if (!CanResolveCombat() ||
                HasActiveVolleys(combatState) ||
                !ReferenceEquals(request.Dealer.CombatState, combatState))
            {
                return;
            }

            await CombatManager.Instance.CheckWinCondition();
        });
    }

    private static void RegisterVolley(ICombatState combatState)
    {
        lock (ActiveVolleysLock)
        {
            ActiveVolleys.TryGetValue(combatState, out var count);
            ActiveVolleys[combatState] = count + 1;
        }
    }

    private static bool UnregisterVolley(ICombatState combatState)
    {
        lock (ActiveVolleysLock)
        {
            if (!ActiveVolleys.TryGetValue(combatState, out var count))
                return false;

            if (count > 1)
            {
                ActiveVolleys[combatState] = count - 1;
                return false;
            }

            ActiveVolleys.Remove(combatState);
            return true;
        }
    }

    private static bool HasActiveVolleys(ICombatState combatState)
    {
        lock (ActiveVolleysLock)
        {
            return ActiveVolleys.ContainsKey(combatState);
        }
    }

    private Creature? SelectTarget(Creature? excluded = null)
    {
        var targets = request.Dealer.CombatState?.HittableEnemies.Where(target => target != excluded);
        return targets == null
            ? null
            : request.Dealer.Player?.RunState.Rng.CombatTargets.NextItem(targets);
    }

    private bool CanResolveCombat()
    {
        return CombatManager.Instance.IsInProgress && request.Dealer is { CombatState: not null, Player.RunState: not null };
    }
}
