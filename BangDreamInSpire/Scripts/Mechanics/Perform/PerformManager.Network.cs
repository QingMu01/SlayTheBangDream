// 网络动作。
using System.Text.Json;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

public partial class PerformManager
{
    /// <summary>歌单生命周期 payload；和弦位值原样上线，只能追加。</summary>
    private sealed record PerformNetworkActionPayload(
        PerformNetworkActionKind Kind,
        uint? CardIndex,
        int SlotIndex,
        bool IsSubsideTriggered,
        int ChordMask,
        int RequestSlotIndex = 0,
        int RequestGroupMask = 0);

    private static readonly RitsuLibManagedNetActionDescriptor<PerformNetworkActionPayload> PerformNetworkAction = new(
        "BangDreamInSpire", "perform_lifecycle",
        static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
        static bytes => JsonSerializer.Deserialize<PerformNetworkActionPayload>(bytes) ??
                        throw new InvalidOperationException("Invalid perform lifecycle payload."),
        static context => ExecuteNetworkPerformAction(context), GameActionType.Combat);

    /// <summary>生命周期动作操作码。</summary>
    internal static ulong PerformOpcode { get; private set; }

    /// <summary>还原落位覆盖，槽位优先于分组。</summary>
    private static PerformEnqueueRequest ToEnqueueRequest(PerformNetworkActionPayload payload)
    {
        if (payload.RequestSlotIndex >= 1)
        {
            return PerformEnqueueRequest.Slot(payload.RequestSlotIndex);
        }

        return payload.RequestGroupMask != 0
            ? PerformEnqueueRequest.ForGroup((PerformChord)payload.RequestGroupMask)
            : default;
    }

    internal static void InitializeNetwork()
    {
        PerformOpcode = RitsuLibManagedNetActions.Register(PerformNetworkAction);
    }

    /// <summary>是否为歌单生命周期动作。</summary>
    internal static bool IsPerformLifecycleAction(GameAction action)
    {
        return PerformOpcode != 0 &&
               action is RitsuLibManagedGameAction managed &&
               managed.DescriptorOpcode == PerformOpcode;
    }

    private static Task ExecuteNetworkPerformAction(
        RitsuLibManagedNetActionContext<PerformNetworkActionPayload> context)
    {
        var manager = context.Player.AttachedData().PerformManager;
        return manager.ExecuteNetworkPerformAction(context.Message, context.PlayerChoiceContext);
    }

    private async Task ExecuteNetworkPerformAction(
        PerformNetworkActionPayload payload,
        PlayerChoiceContext choiceContext)
    {
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;
        if (payload.Kind == PerformNetworkActionKind.Trigger)
        {
            await PerformSlot(payload.SlotIndex, payload.IsSubsideTriggered, choiceContext);
            return;
        }

        if (payload.Kind == PerformNetworkActionKind.Chord)
        {
            await PerformChordSlots((PerformChord)payload.ChordMask, choiceContext);
            return;
        }

        if (!payload.CardIndex.HasValue) return;
        var card = NetCombatCard.ForTesting(payload.CardIndex.Value).ToCardModelOrNull();
        if (card == null) return;
        var change = new PerformAreaChange(card,
            payload.Kind == PerformNetworkActionKind.Enter
                ? PerformAreaChangeType.Added
                : PerformAreaChangeType.Removed);
        // 联机与单机回退都经 hook 包装。
        await BangDreamHook.RunPerformHookAction(
            choiceContext, card,
            context => HandlePerformAreaChange(context, change, ToEnqueueRequest(payload)));
    }

    private async Task ExecuteLocalPerformAction(PerformNetworkActionPayload payload)
    {
        var combatState = Player.Creature.CombatState;
        if (combatState == null) return;
        if (payload.Kind == PerformNetworkActionKind.Trigger)
        {
            await PerformSlot(payload.SlotIndex, payload.IsSubsideTriggered);
            return;
        }

        if (payload.Kind == PerformNetworkActionKind.Chord)
        {
            await PerformChordSlots((PerformChord)payload.ChordMask);
            return;
        }

        if (!payload.CardIndex.HasValue) return;
        var card = NetCombatCard.ForTesting(payload.CardIndex.Value).ToCardModelOrNull();
        if (card == null) return;
        var change = new PerformAreaChange(card,
            payload.Kind == PerformNetworkActionKind.Enter
                ? PerformAreaChangeType.Added
                : PerformAreaChangeType.Removed);
        await BangDreamHook.RunPerformHookAction(
            combatState, card,
            choiceContext => HandlePerformAreaChange(choiceContext, change, ToEnqueueRequest(payload)));
    }

    private void RequestPerformAction(PerformNetworkActionPayload payload)
    {
        if (!CombatManager.Instance.IsInProgress) return;
        if (!LocalContext.IsMe(Player)) return;

        if (!RitsuLibManagedNetActions.Request(null, PerformNetworkAction, payload, Player.NetId))
            TaskHelper.RunSafely(HandleRejectedNetworkAction(payload));
    }

    private Task HandleRejectedNetworkAction(PerformNetworkActionPayload payload)
    {
        if (RunManager.Instance.NetService.Type == NetGameType.Singleplayer)
            return ExecuteLocalPerformAction(payload);

        BangDreamInSpireCore.Logger.Error(
            $"Perform action was rejected by the Sidecar action queue: player={Player.NetId} kind={payload.Kind}.");
        return Task.CompletedTask;
    }
}
