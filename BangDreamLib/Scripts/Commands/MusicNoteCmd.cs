using System.Text.Json;
using BangDreamLib.Scripts.Features.AsyncDamage;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace BangDreamLib.Scripts.Commands;

public static class MusicNoteCmd
{
    private const string DefaultPath = "res://ItsCrychic/scenes/vfx/flying_music_note_default.tscn";

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
            "BangDreamLib",
            "music_note_async_damage",
            static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
            static bytes => JsonSerializer.Deserialize<MusicNoteRequestPayload>(bytes) ??
                            throw new InvalidOperationException("Invalid music note request payload."),
            static context => ExecuteNetworkRequest(context),
            GameActionType.Combat);

    /// <summary>在模组初始化阶段注册描述符，确保接收远端 Action 前本地已具备解码信息。</summary>
    internal static void InitializeNetwork()
    {
        RitsuLibManagedNetActions.Register(NetworkAction);
    }

    public static Task FromCard(CardModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        return Submit(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target, source);
    }

    public static Task FromPower(PowerModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        return Submit(source.Owner, baseCount, bounceCount, baseDamage, null, target, source);
    }

    public static Task FromRelic(RelicModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        return Submit(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target, source);
    }

    public static Task CustomShot(Creature dealer, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        return Submit(dealer, baseCount, bounceCount, baseDamage, null, target);
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
            BangDreamLibCore.Logger.Warn("Music note request was not accepted by the network layer.");
            return Task.CompletedTask;
        }

        return Execute(dealer, payload, visualDealer, target, source);
    }

    private static Task ExecuteNetworkRequest(RitsuLibManagedNetActionContext<MusicNoteRequestPayload> context)
    {
        var combatState = context.Player.Creature.CombatState;
        if (combatState == null)
            return Task.CompletedTask;

#if DEBUG
        BangDreamLibCore.Logger.Info(
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
        return CombatAsyncDamageManager.Shared.SubmitAsync(request);
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
        return BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVfx.MusicNote ??
               DefaultPath;
    }
}