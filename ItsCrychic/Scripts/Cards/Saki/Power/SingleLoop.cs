using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class SingleLoop() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.PerformArea];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        return PowerCmd.Apply<SingleLoopPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
