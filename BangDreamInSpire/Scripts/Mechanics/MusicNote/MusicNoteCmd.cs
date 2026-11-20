using System.Reflection;
using System.Text.Json;
using BangDreamInSpire.Scripts.Mechanics.AsyncDamage;
using BangDreamInSpire.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace BangDreamInSpire.Scripts.Mechanics.MusicNote;

internal static class MusicNoteCmd
{
    private const string DefaultPath = "res://BangDreamInSpire/scenes/vfx/flying_music_note_default.tscn";

    private sealed record MusicNoteRequestPayload(
        uint? DealerId,
        int Count,
        int BounceCount,
        decimal BaseDamage,
        uint? TargetId,
        uint? SourceCardIndex,
        string? SourceCategory,
        string? SourceEntry);

    private static readonly RitsuLibManagedNetActionDescriptor<MusicNoteRequestPayload> NetworkAction =
        new(
            "BangDreamInSpire",
            "music_note_async_damage",
            static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
            static bytes => JsonSerializer.Deserialize<MusicNoteRequestPayload>(bytes) ??
                            throw new InvalidOperationException("Invalid music note request payload."),
            static context => ExecuteNetworkRequest(context),
            GameActionType.Combat);

    /// <summary>本体 <c>CombatStateTracker.NotifyCombatStateChanged</c> 是私有方法，反射句柄见下。</summary>
    private static readonly MethodInfo? StateChangedNotifier =
        AccessTools.Method(typeof(CombatStateTracker), "NotifyCombatStateChanged");

    /// <summary>在模组初始化阶段注册描述符，确保接收远端 Action 前本地已具备解码信息。</summary>
    internal static void InitializeNetwork()
    {
        RitsuLibManagedNetActions.Register(NetworkAction);
    }

    /// <summary>
    /// 本体 <c>CombatStateTracker.NotifyCombatStateChanged</c> 是私有方法，这里用反射强制刷新一次状态通知。
    /// 入口属于玩法层（音符整批结算之后），避免向特效层暴露「能改写战斗状态」的通道。
    /// </summary>
    internal static void NotifyCombatStateChanged()
    {
        if (CombatManager.Instance.IsInProgress)
            StateChangedNotifier?.Invoke(CombatManager.Instance.StateTracker, ["BangDreamVfx"]);
    }

    public static Task FromCard(CardModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
    {
        return shouldImmediate
            ? SubmitImmediate(source.Owner.Creature, baseCount, bounceCount, baseDamage, target, source)
            : Submit(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target, source);
    }

    public static Task FromPower(PowerModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
    {
        return shouldImmediate
            ? SubmitImmediate(source.Owner, baseCount, bounceCount, baseDamage, target, source)
            : Submit(source.Owner, baseCount, bounceCount, baseDamage, null, target, source);
    }

    public static Task FromRelic(RelicModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
    {
        return shouldImmediate
            ? SubmitImmediate(source.Owner.Creature, baseCount, bounceCount, baseDamage, target, source)
            : Submit(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target, source);
    }

    /// <summary>从任意生物发射音符。</summary>
    /// <remarks>
    /// <paramref name="dealer" /> 必须是玩家拥有的生物：动作队列的归属端与 <c>NetId</c> 都取自
    /// <see cref="Creature.Player" />，非玩家 dealer 没有归属端，请求只会被网络层拒绝或退化成
    /// 各端各自的本地直执行（跨端结算不一致）。因此这里明确拒绝，而不是让它在 <see cref="Submit" />
    /// 里以「RunState 为 null」的形式报一个看不懂的参数异常。
    /// </remarks>
    public static Task CustomShot(Creature dealer, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
    {
        ArgumentNullException.ThrowIfNull(dealer);
        if (dealer.Player == null)
        {
            throw new InvalidOperationException(
                $"Music note dealer '{dealer.LogName}' (combatId={dealer.CombatId?.ToString() ?? "null"}) " +
                "is not owned by a player; music notes can only be fired by player-owned creatures.");
        }

        // 立即结算必须走 SubmitImmediate，否则会静默排进动作队列。
        return shouldImmediate
            ? SubmitImmediate(dealer, baseCount, bounceCount, baseDamage, target, null)
            : Submit(dealer, baseCount, bounceCount, baseDamage, null, target);
    }

    internal static Task Submit(Creature dealer, int count, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? visualDealer = null, Creature? target = null, AbstractModel? source = null)
    {
        ArgumentNullException.ThrowIfNull(dealer);
        ArgumentNullException.ThrowIfNull(dealer.CombatState);
        ArgumentNullException.ThrowIfNull(dealer.Player?.RunState);

        var payload = new MusicNoteRequestPayload(
            dealer.CombatId,
            count,
            bounceCount,
            baseDamage,
            target?.CombatId,
            source is CardModel sourceCard ? NetCombatCard.FromModel(sourceCard).CombatCardIndex : null,
            source?.Id.Category,
            source?.Id.Entry);

        if (RunManager.Instance.NetService.Type is NetGameType.Client or NetGameType.Host &&
            dealer.Player != null && !LocalContext.IsMe(dealer.Player) ||
            RitsuLibManagedNetActions.Request(null, NetworkAction, payload, dealer.Player?.NetId))
            return Task.CompletedTask;

        if (RunManager.Instance.NetService.Type is NetGameType.Client or NetGameType.Host)
        {
            BangDreamInSpireCore.Logger.Warn("Music note request was not accepted by the network layer.");
            return Task.CompletedTask;
        }

        return Execute(dealer, payload, visualDealer, target, source);
    }

    /// <summary>
    /// 不经动作队列、直接在调用方的同步上下文内发射，并等待整批结算完成。
    /// 仅可用于所有端都会以相同顺序执行的上下文（hook / 同步 Action 内部）。
    /// </summary>
    private static async Task SubmitImmediate(Creature dealer, int count, int bounceCount, decimal baseDamage,
        Creature? target, AbstractModel? source)
    {
        ArgumentNullException.ThrowIfNull(dealer);

        var combatState = dealer.CombatState;
        if (combatState == null || dealer.Player?.RunState == null)
            return;

        var payload = new MusicNoteRequestPayload(
            dealer.CombatId,
            count,
            bounceCount,
            baseDamage,
            target?.CombatId,
            source is CardModel sourceCard ? NetCombatCard.FromModel(sourceCard).CombatCardIndex : null,
            source?.Id.Category,
            source?.Id.Entry);

        await Execute(dealer, payload, visualDealer: null, target: target, source: source);
        await CombatResolutionBarrier.WaitAsync(combatState);
    }

    private static Task ExecuteNetworkRequest(RitsuLibManagedNetActionContext<MusicNoteRequestPayload> context)
    {
        var combatState = context.Player.Creature.CombatState;
        if (combatState == null)
            return Task.CompletedTask;

#if DEBUG
        BangDreamInSpireCore.Logger.Info(
            $"MusicNote request action={context.Action.Id} owner={context.Player.NetId} " +
            $"count={context.Message.Count} bounce={context.Message.BounceCount} " +
            $"target={context.Message.TargetId?.ToString() ?? "random"}");
#endif

        var dealer = context.Message.DealerId.HasValue
            ? combatState.GetCreature(context.Message.DealerId)
            : context.Player.Creature;
        if (dealer == null)
            return Task.CompletedTask;

        var target = context.Message.TargetId.HasValue
            ? combatState.GetCreature(context.Message.TargetId)
            : null;
        var source = ResolveSource(context.Message, dealer);
        return Execute(dealer, context.Message, visualDealer: null, target: target, source: source);
    }

    private static Task Execute(
        Creature dealer,
        MusicNoteRequestPayload payload,
        Creature? visualDealer = null,
        Creature? target = null,
        AbstractModel? source = null)
    {
        if (dealer.CombatState == null || dealer.Player?.RunState == null)
            return Task.CompletedTask;

        var shot = (int)BangDreamHook.ModifyMusicNoteShotCount(dealer.CombatState, dealer, payload.Count, source);
        var bounce = (int)BangDreamHook.ModifyMusicNoteBounceCount(dealer.CombatState, dealer,
            payload.BounceCount, source);
        var capturedDamageAdditive = shot > 0
            ? BangDreamHook.CaptureMusicNoteDamageAdditive(dealer.CombatState, dealer, source)
            : 0m;
        var effect = new MusicNoteAsyncDamageEffect(
            GetMusicNoteVfxPath(dealer.Player),
            payload.BaseDamage,
            capturedDamageAdditive);
        var request = new AsyncDamageBatchRequest
        {
            Dealer = dealer,
            Count = Math.Max(0, shot),
            Effect = effect,
            ChainCount = Math.Max(0, bounce),
            InitialVisualSource = visualDealer,
            FixedTarget = target,
            Source = source
        };
        return CombatAsyncDamageManager.Shared.LaunchAsync(request);
    }

    private static AbstractModel? ResolveSource(MusicNoteRequestPayload payload, Creature dealer)
    {
        if (payload.SourceCardIndex.HasValue)
            return NetCombatCard.ForTesting(payload.SourceCardIndex.Value).ToCardModelOrNull();

        if (string.IsNullOrEmpty(payload.SourceCategory) || string.IsNullOrEmpty(payload.SourceEntry))
            return null;

        var id = new ModelId(payload.SourceCategory, payload.SourceEntry);
        return dealer.GetPowerById(id) ??
               dealer.Player?.GetRelicById(id) ??
               ModelDb.GetByIdOrNull<AbstractModel>(id);
    }

    private static string GetMusicNoteVfxPath(Player player)
    {
        return SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.MultiplayerVfx.MusicNote ??
               DefaultPath;
    }
}