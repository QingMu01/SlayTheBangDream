using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Desuwa() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(2),
        QuickVar.LingeredResource.Create(1)
    ];

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!play.Card.Id.Equals(Id) && Pile?.Type == PileType.Discard)
        {
            var cardPileAddResult = await CardPileCmd.Add(this, PileType.Hand);
            if (cardPileAddResult.success)
            {
                var locString = new LocString("cards", "ITS_CRYCHIC_CARD_DESUWA.verbal_tic");
                locString.Add("CardName", play.Card.Title.Replace("+", ""));
                TalkCmd.Play(locString, Owner.Creature, VfxColor.White, VfxDuration.Short);
            }
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmd.Apply<NextTurnLingeredPower>(choiceContext, Owner.Creature,
            QuickVar.LingeredResource.GetVar(this).IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}