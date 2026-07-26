using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Pointillism() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Lingered];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Energy.Create(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        var target = (Owner.PlayerCombatState?.Energy ?? 0) + (IsUpgraded ? 1 : 0);
        var current = SecondaryResourceCmd.Get(Owner, BangDreamConst.LingeredResource);
        if (current < target)
        {
            await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource, target - current, this);
        }
        else if (current > target)
        {
            await SecondaryResourceCmd.Lose(Owner, BangDreamConst.LingeredResource, current - target, this);
        }
    }
}
