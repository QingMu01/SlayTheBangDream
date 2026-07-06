using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Powers;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

public class WorldviewPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Owner.CombatState == null) return;

        var musicCards = new List<CardModel>();
        if (Owner.Player.Character is IExtraDeckSupportCharacter character && character.ExtraCardPool.AllCards.Any())
        {
            musicCards.AddRange(character.ExtraCardPool.AllCards);
        }
        else
        {
            musicCards.AddRange(ModelDb.AllCharacters
                .OfType<IExtraDeckSupportCharacter>()
                .SelectMany(item => item.ExtraCardPool.AllCards));
        }

        var card = Owner.Player.RunState.Rng.CombatCardGeneration.NextItem(CardFactory.FilterForCombat(musicCards));
        if (card == null) return;

        var generatedCard = Owner.CombatState.CreateCard(card, Owner.Player);
        generatedCard.AddKeyword(CardKeyword.Exhaust);
        
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(generatedCard, BangDreamConst.PerformPile, Owner.Player));
    }
}