using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class WonderfulWorld() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    public override bool IsInstant => IsUpgraded;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var existingHope = Owner.PlayerCombatState!.AllCards
            .Concat(BangDreamConst.ExtraDraw.GetPile(Owner).Cards)
            .Concat(BangDreamConst.PerformPile.GetPile(Owner).Cards)
            .Concat(PileType.Hand.GetPile(Owner).Cards)
            .Where(card => card is Hope)
            .Distinct()
            .FirstOrDefault();

        if (existingHope != null)
        {
            if (existingHope.Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(existingHope, PileType.Hand);
            }

            existingHope.DynamicVars.Cards.BaseValue += 1;
            return;
        }

        var generatedCard = CombatState.CreateCard(ModelDb.Card<Hope>(), Owner);
        await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
    }
}