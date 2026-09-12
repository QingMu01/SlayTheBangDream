using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class FullMoonBall() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    public int LingeredResourceCost => 3;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<FullMoonBallPower>(choiceContext, Owner.Creature, QuickVar.Buff.GetVar(this).IntValue,
            Owner.Creature, this);
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(CreateClone(),
            PileType.Draw, Owner, CardPilePosition.Random));
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(1);
    }
}
