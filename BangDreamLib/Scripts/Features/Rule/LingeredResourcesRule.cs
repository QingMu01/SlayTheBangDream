using System.Runtime.CompilerServices;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace BangDreamLib.Scripts.Features.Rule;

[RegisterSingleton]
public sealed class LingeredResourcesRule() : HookedSingletonModel(HookType.Combat), ISecondaryResourceHookListener
{
    public const int HardMaxAmount = 7;

    private readonly ConditionalWeakTable<Player, SemaphoreSlim> _overflowLocks = new();
    private readonly AsyncLocal<IReadOnlySet<Player>?> _normalizingPlayers = new();

    // 休止机制触发判定
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card is not ISubsideCard subsideCard) return;

        var payment = cardPlay.SecondaryResources();
        if (payment.HasLines && payment.Shortfall(BangDreamConst.LingeredResource) == 0)
        {
            await subsideCard.OnSubside(context, cardPlay);
            await BangDreamHook.AfterCardSubside(context, cardPlay);
        }

        await Cmd.CustomScaledWait(0.1f, 0.2f);
    }

    // 自动生成余音资源唯一渠道
    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Character is ILingeredResourceCharacter { AutoGenerateSubsideResource: false }) return;
        var canGenerateRes = true;
        if (cardPlay.Card is ISubsideCard subsideCard)
        {
            canGenerateRes = subsideCard.ShouldGenerateResources;
        }

        if (canGenerateRes && cardPlay.Resources.EnergySpent > 0)
        {
            await SecondaryResourceCmd.Gain(
                cardPlay.Card.Owner,
                BangDreamConst.LingeredResource,
                cardPlay.Resources.EnergySpent,
                cardPlay.Card
            );
        }
    }

    // 机制判定
    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (!context.Definition.Id.Equals(BangDreamConst.LingeredResource)) return;
        if (_normalizingPlayers.Value?.Contains(context.Player) == true) return;

        var overflowLock = _overflowLocks.GetValue(context.Player, _ => new SemaphoreSlim(1, 1));
        await overflowLock.WaitAsync();
        var previousNormalizingPlayers = _normalizingPlayers.Value;
        var normalizingPlayers = previousNormalizingPlayers?.ToHashSet() ?? [];
        normalizingPlayers.Add(context.Player);
        _normalizingPlayers.Value = normalizingPlayers;
        try
        {
            while (SecondaryResourceCmd.Get(context.Player, BangDreamConst.LingeredResource) >= HardMaxAmount)
            {
                var extraDraw = BangDreamConst.ExtraDraw.GetPile(context.Player);
                var topCardInPile = extraDraw.Cards.FirstOrDefault();
                if (topCardInPile == null) break;

                var result = await CardPileCmd.Add(topCardInPile, BangDreamConst.PerformPile);
                if (!result.success) break;

                await Cmd.CustomScaledWait(0.05f, 0.1f);
                await SecondaryResourceCmd.Lose(context.Player, BangDreamConst.LingeredResource, HardMaxAmount,
                    this);
            }
        }
        finally
        {
            try
            {
                var current = SecondaryResourceCmd.Get(context.Player, BangDreamConst.LingeredResource);
                var max = SecondaryResourceCmd.GetMax(context.Player, BangDreamConst.LingeredResource) ?? HardMaxAmount;
                var overflow = Math.Max(0, current - max);
                if (overflow > 0)
                {
                    await SecondaryResourceCmd.Lose(context.Player, BangDreamConst.LingeredResource, overflow, this);
                }
            }
            finally
            {
                _normalizingPlayers.Value = previousNormalizingPlayers;
                overflowLock.Release();
            }
        }
    }

    public static bool IsSufficient(CardModel card)
    {
        return SecondaryResourcePaymentResolver.Plan(card).Lines
            .Where(line => line.ResourceId.Equals(BangDreamConst.LingeredResource))
            .All(line => line.IsAffordable);
    }
}
