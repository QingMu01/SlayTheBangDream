using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIv() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new EnergyVar(2),
        new CardsVar(1)
    ];

    public override async Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    public override async Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        var extraDraw = BangDreamConst.PileExtraDraw.GetPile(Owner);
        if (extraDraw.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var simpleGridSelected = await CardSelectCmd.FromSimpleGrid(choiceContext,
                    extraDraw.Cards, Owner, CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars.Cards.IntValue));
                selectedCard = simpleGridSelected.FirstOrDefault();
            }
            else
            {
                selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(extraDraw.Cards);
            }

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }
}