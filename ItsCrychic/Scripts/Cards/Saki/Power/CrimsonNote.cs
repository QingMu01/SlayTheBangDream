using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Power.Buff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Power;

public class CrimsonNote() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Power;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.MusicNote];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("Notes", 5)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<CrimsonNotePower>(choiceContext, Owner.Creature,
            DynamicVars["Notes"].IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(2);
    }
}
