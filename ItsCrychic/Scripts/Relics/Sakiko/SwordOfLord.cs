using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;

namespace ItsCrychic.Scripts.Relics.Sakiko;

public class SwordOfLord : AbstractSakikoRelic
{
    public override RelicRarity Rarity => RelicRarity.None;

    protected override IEnumerable<IHoverTip> RelicHoverTips =>
    [
        HoverTipFactory.FromKeyword(BangDreamConst.PerformArea)
    ];


    public override async Task BeforeCombatStartLate()
    {
        var extraDraw = BangDreamConst.ExtraDraw.GetPile(Owner).Cards.ToList();
        if (extraDraw.Count > 0)
        {
            var selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(extraDraw);
            if (selectedCard != null)
            {
                Flash();
                await CardPileCmd.Add(selectedCard, BangDreamConst.PerformPile);
            }
        }
    }
}