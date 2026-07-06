using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Kanon() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Innate,
        CardKeyword.Exhaust,
        BangDreamConst.PerformArea,
        BangDreamConst.Music
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var manager = Owner.AttachedData().PerformManager;

        foreach (var card in manager.PerformancePile.Cards.Where(card => card is not IPerformCard).ToList())
        {
            await CardCmd.Discard(choiceContext, card);
        }

        var needAddCardCount = manager.Capacity - manager.PerformancePile.Cards.Count;

        while (needAddCardCount > 0)
        {
            var performanceCard = BangDreamTools.GetPile(BangDreamConst.ExtraDraw, Owner).Cards
                .FirstOrDefault(item => item is IPerformCard);
            if (performanceCard != null)
            {
                await CardPileCmd.Add(performanceCard, BangDreamConst.PerformPile);
            }
            else
            {
                var musicCards = new List<CardModel>();
                if (Owner.Character is IExtraDeckSupportCharacter character && character.ExtraCardPool.AllCards.Any())
                {
                    musicCards.AddRange(character.ExtraCardPool.AllCards);
                }
                else
                {
                    musicCards.AddRange(ModelDb.AllCharacters
                        .OfType<IExtraDeckSupportCharacter>()
                        .SelectMany(item => item.ExtraCardPool.AllCards));
                }

                var randomCards = CardFactory.GetForCombat(Owner, musicCards, 1,
                    Owner.RunState.Rng.CombatCardGeneration);
                var selectedCard = randomCards.FirstOrDefault();
                if (selectedCard != null)
                {
                    await CardPileCmd.Add(selectedCard, BangDreamConst.PerformPile);
                }
            }

            needAddCardCount--;
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}