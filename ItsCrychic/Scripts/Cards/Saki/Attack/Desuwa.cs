using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Desuwa() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(1m, ValueProp.Move),
        new BlockVar(1m, ValueProp.Move)
    ];

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.Card.Id.Equals(Id) && Pile == PileType.Discard.GetPile(Owner))
        {
            var cardPileAddResult = await CardPileCmd.Add(this, PileType.Hand);
            if (cardPileAddResult.success)
            {
                var locString = new LocString("cards", "ITS_CRYCHIC_CARD_DESUWA.verbal_tic");
                locString.Add("CardName", cardPlay.Card.Title);
                TalkCmd.Play(locString, Owner.Creature, VfxColor.White, VfxDuration.Short);
            }
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (IsUpgraded)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, DynamicVars.Block.Props, play);
        }
    }
}