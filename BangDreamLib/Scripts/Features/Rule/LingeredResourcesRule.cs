using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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

    // 休止机制触发判定
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        var playedCard = cardPlay.Card;
        if (playedCard is ISubsideCard subsideCard)
        {
            var sumCost = SecondaryResourcePaymentResolver.Plan(playedCard).Lines
                .Where(line => line.ResourceId.Equals(BangDreamConst.LingeredResource))
                .Sum(line => line.Value);

            var spend = await SecondaryResourceCmd.Spend(playedCard.Owner, BangDreamConst.LingeredResource,
                sumCost, playedCard, this);

            if (spend)
            {
                await subsideCard.OnSubside(context, cardPlay);
            }

            await Cmd.CustomScaledWait(0.1f, 0.2f);
        }
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

        if (canGenerateRes)
        {
            await SecondaryResourceCmd.Gain(
                cardPlay.Card.Owner,
                BangDreamConst.LingeredResource,
                cardPlay.Resources.EnergySpent,
                this
            );
        }
    }

    // 机制判定
    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (!context.Definition.Id.Equals(BangDreamConst.LingeredResource)) return;
        if (context.Reason == SecondaryResourceChangeReason.Gain && context.NewAmount > context.OldAmount)
        {
            while (SecondaryResourceCmd.Get(context.Player, BangDreamConst.LingeredResource) >= HardMaxAmount)
            {
                var extraDraw = BangDreamTools.GetPile(BangDreamConst.ExtraDraw, context.Player);
                var topCardInPile = extraDraw.Cards.ToList().FirstOrDefault();
                if (topCardInPile != null)
                {
                    var result = await CardPileCmd.Add(topCardInPile, BangDreamConst.PerformPile);
                    if (result.success)
                    {
                        await Cmd.CustomScaledWait(0.1f, 0.2f);
                        await SecondaryResourceCmd.Lose(context.Player, BangDreamConst.LingeredResource, HardMaxAmount,
                            this);
                    }
                }
                else
                {
                    break;
                }
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