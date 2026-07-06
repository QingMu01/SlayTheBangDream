using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class HachimanguseiDance() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None),
    ISecondaryResourceHookListener
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Handle == null || card.Owner.Creature != Owner.Creature)
            return false;

        var type = card.Pile?.Type;
        if (type.HasValue)
        {
            switch (type.GetValueOrDefault())
            {
                case PileType.Hand:
                case PileType.Play:
                {
                    modifiedCost = 0M;
                    return true;
                }
            }
        }

        return false;
    }

    public decimal ModifySecondaryResourceCostLate(SecondaryResourceCostContext context, decimal cost)
    {
        if (Handle != null && IsUpgraded && context.Definition.Id.Equals(BangDreamConst.LingeredResource))
        {
            return 0;
        }

        return cost;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Handle != null)
        {
            FlashInArea();
            await CardPileCmd.Add(this, BangDreamConst.ExtraDraw);
        }
    }
}