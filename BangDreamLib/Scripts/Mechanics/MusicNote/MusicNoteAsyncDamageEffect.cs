using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Builder;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Mechanics.MusicNote;

internal sealed class MusicNoteAsyncDamageEffect(
    string vfxPath,
    decimal baseDamage,
    decimal capturedDamageAdditive) : IAsyncDamageEffect
{
    private const float NoteSpawnInterval = 0.05f;
    private const int NoteGroupSize = 8;
    private const float CastLeadTime = 0.1f;

    private readonly VfxCreator<MusicNoteFlyingVfx> _creator = new(vfxPath);
    private readonly float _initialGroupDirection = RandomDirection();

    public async Task BeforeSpawnAsync(AsyncDamagePreparationContext context)
    {
        if (context.Index % NoteGroupSize == 0)
        {
            if (context.Request.InitialVisualSource == null ||
                ReferenceEquals(context.Request.InitialVisualSource, context.Request.Dealer))
            {
                await CreatureCmd.TriggerAnim(context.Request.Dealer, "Cast", 0);
                await Cmd.Wait(CastLeadTime);
            }
        }
        else
        {
            await Cmd.Wait(NoteSpawnInterval);
        }
    }

    public decimal GetLockedDamage(AsyncDamageTargetContext context)
    {
        return BangDreamHook.ModifyMusicNoteDamage(
            context.CombatState,
            context.Target,
            context.Request.Dealer,
            baseDamage + capturedDamageAdditive,
            context.Request.Source,
            ModifyDamageHookType.All);
    }

    public Task<AsyncDamageAnimationHandle> StartAnimationAsync(AsyncDamageSpawnContext context)
    {
        var vfx = CreateVfx(context);
        var sourceNode = (context.VisualSource ?? context.Request.Dealer).GetCreatureNode();
        var targetNode = context.Target.GetCreatureNode();
        var vfxManager = BangDreamVfxManager.Instance;

        if (vfxManager != null && sourceNode != null && targetNode != null)
        {
            try
            {
                var start = sourceNode.VfxSpawnPosition;
                var end = targetNode.VfxSpawnPosition;
                vfx.SetPath(start, end);
                // 音符按「进度 = Speed * delta / 直线距离」推进，故抵达耗时恰为 距离 / 速度；
                // 据此启动命中计时，使玩法结算与音符落地严格同拍。
                var travelSeconds = start.DistanceTo(end) / (float)vfx.Speed;
                var handle = vfxManager.SubmitVfx(vfx);
                return Task.FromResult(new AsyncDamageAnimationHandle(
                    MusicNoteTiming.WaitAsync(travelSeconds),
                    handle.Finished,
                    handle.Context));
            }
            catch (Exception exception)
            {
                BangDreamLibCore.Logger.Error($"Music note VFX submission error: {exception}");
                vfx.QueueFreeSafely();
            }
        }
        else
        {
            vfx.QueueFreeSafely();
        }

        return Task.FromResult(AsyncDamageAnimationHandle.Immediate(vfx.Context));
    }

    public async Task ResolveDamageAsync(AsyncDamageHitContext context)
    {
        if (!context.Target.IsHittable)
            return;

        var damage = context.LockedDamage;

#if DEBUG
        // desync 对比日志：联机双端各自记录后 diff，SequenceId/目标/数值任一不一致即为分歧点
        BangDreamLibCore.Logger.Info(
            $"MusicNote #{context.SequenceId} chain={context.IsChain} target={context.Target} " +
            $"damage={damage} (locked)");
#endif

        var results = await CreatureCmd.Damage(
            choiceContext: new BlockingPlayerChoiceContext(),
            target: context.Target,
            damageVar: new DamageVar(damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim),
            dealer: context.Request.Dealer,
            cardSource: context.Request.Source as CardModel,
            cardPlay: null);

        var damageTracker = context.Request.Dealer.Player?.AttachedData().MusicNoteDamageTracker;
        if (damageTracker == null)
            return;

        foreach (var damageResult in results)
            damageTracker.AddMusicNoteDamage(context.CombatState.RoundNumber, damageResult);
    }

    public async Task AfterBatchAsync(AsyncDamageBatchContext context)
    {
        if (context.LastRootContext == null || context.Request.Dealer.Player == null)
            return;

        await BangDreamHook.AfterMusicNoteShot(
            context.CombatState,
            context.LastRootContext,
            context.Request.Dealer.Player);
        BangDreamVfxManager.NotifyCombatStateChanged();
    }

    private MusicNoteFlyingVfx CreateVfx(AsyncDamageSpawnContext context)
    {
        return _creator.Create(vfx =>
        {
            var laneIndex = context.IsChain ? 0 : context.Index % NoteGroupSize;
            var groupStart = context.Index / NoteGroupSize * NoteGroupSize;
            var laneCount = context.IsChain
                ? 1
                : Math.Min(NoteGroupSize, context.Total - groupStart);
            var groupDirection = context.IsChain
                ? RandomDirection()
                : context.Index / NoteGroupSize % 2 == 0
                    ? _initialGroupDirection
                    : -_initialGroupDirection;

            vfx.Context.Set("total", context.Total);
            vfx.Context.Set("index", context.Index);
            vfx.Context.Set("IsPrototype", context.VisualSource == null);
            vfx.Context.Set("Target", context.Target);
            vfx.SetTrajectoryLane(laneIndex, laneCount, groupDirection);
        });
    }

    private static float RandomDirection()
    {
        return Rng.Chaotic.NextInt(0, 1) == 0 ? -1f : 1f;
    }
}
