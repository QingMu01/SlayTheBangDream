using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIi() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IMusicNoteModifyHook
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordPerformance.GetModCardKeyword(),
        BangDreamConst.KeywordPerformanceArea.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(5),
        QuickVar.Cards.Create(1),
        ModCardVars.Int("AddedDamage", 2)
    ];

    public override async Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        await MusicNoteCmd.FromCard(choiceContext, this, baseCount: DynamicVars.Repeat.IntValue);
    }

    public override async Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        var drawPile = Owner.PlayerCombatState!.DrawPile;
        if (drawPile.Cards.Count > 0)
        {
            CardModel? selectedCard;
            if (IsUpgraded)
            {
                var simpleGridSelected = await CardSelectCmd.FromSimpleGrid(choiceContext,
                    drawPile.Cards, Owner, CardSelectorPrompt.ToHand.GetFixedPrefs(DynamicVars.Cards.IntValue));
                selectedCard = simpleGridSelected.FirstOrDefault();
            }
            else
            {
                selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(drawPile.Cards);
            }

            if (selectedCard != null)
            {
                await CardPileCmd.Add(selectedCard, PileType.Hand);
            }
        }
    }

    public decimal ModifyMusicNoteDamageAdditive(Creature? target, decimal amount, Creature? dealer,
        AbstractModel? source)
    {
        if (Pile?.Type == BangDreamConst.PilePerformance.GetPileType() && dealer == Owner.Creature)
        {
            return amount + DynamicVars["AddedDamage"].IntValue;
        }

        return amount;
    }
}