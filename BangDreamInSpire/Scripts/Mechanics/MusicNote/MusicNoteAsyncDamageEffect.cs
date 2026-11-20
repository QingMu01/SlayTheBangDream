using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Mechanics.AsyncDamage;
using BangDreamInSpire.Scripts.Nodes.VFX;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamInSpire.Scripts.Mechanics.MusicNote;

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
                // 命中时刻由音符真实命中信号（HitTriggered → VfxHandle.Arrived）驱动，与 Speed / 缓动解耦。
                var handle = vfxManager.SubmitVfx(vfx);
                return Task.FromResult(new AsyncDamageAnimationHandle(
                    handle.Arrived,
                    handle.Finished,
                    handle.Context));
            }
            catch (Exception exception)
            {
                BangDreamInSpireCore.Logger.Error($"Music note VFX submission error: {exception}");
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
        BangDreamInSpireCore.Logger.Info(
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
        {
            // 不能静默 return：没有 tracker 意味着该玩家的音符统计整段缺失，Finale / Moonlight 会读到偏低的数字。
            BangDreamInSpireCore.Logger.Warn(
                $"Music note damage tracker missing for dealer {context.Request.Dealer}; " +
                $"note #{context.SequenceId} damage results dropped (count={results.Count()}).");
            return;
        }

        // 统计口径：只记「本音符目标实际承受」的那一条 DamageResult。
        // 本体一次 Damage 可能返回多条：目标被 Osty / Wuppy 吸收时，吸收者会各有一条
        // （CreatureCmd.Damage 在 `originalTarget != unblockedDamageTarget` 分支里先给吸收者补一条、
        // 再给原目标补一条残量结果，见 sts2unpack/0.113.0/src/Core/Commands/CreatureCmd.cs:315-357；
        // DamageResult.Receiver 是「实际承受者」、OriginalTarget 才是「原本该承受者」，
        // 见 Core/Entities/Creatures/DamageResult.cs:11-22）。因此按 Receiver == 目标过滤，排除吸收者。
        // 这是「目标实际承受的音符伤害」口径；若未来产品改口径（例如改为含被吸收的总伤害），
        // 需要同步 Finale（GetAllDamageResults().Count）与 Moonlight（Sum(TotalDamage)）的读取逻辑。
        var recorded = 0;
        foreach (var damageResult in results)
        {
            if (!ReferenceEquals(damageResult.Receiver, context.Target))
                continue;

            damageTracker.AddMusicNoteDamage(context.CombatState.RoundNumber, damageResult);
            recorded++;
        }

        if (recorded == 0)
        {
            // 理论上本体总会给原目标补一条结果（即使 0 伤害）；匹配不到说明吸收语义与预期不同，
            // 保留 Warn + 原始条数，避免统计被静默吞掉。
            BangDreamInSpireCore.Logger.Warn(
                $"Music note #{context.SequenceId} produced no DamageResult received by target {context.Target} " +
                $"(dealer={context.Request.Dealer}, results={results.Count()}); nothing recorded.");
        }
    }

    public async Task AfterBatchAsync(AsyncDamageBatchContext context)
    {
        if (context.LastRootContext == null || context.Request.Dealer.Player == null)
            return;

        await BangDreamHook.AfterMusicNoteShot(
            context.CombatState,
            context.LastRootContext,
            context.Request.Dealer.Player);
        MusicNoteCmd.NotifyCombatStateChanged();
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
