using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class Improvisation() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.Music
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var musicCards = new List<CardModel>();
        if (Owner.Character is IExtraDeckSupportCharacter character && character.ExtraCardPool.AllCards.Any())
        {
            musicCards.AddRange(character.ExtraCardPool.AllCards);
        }
        else
        {
            musicCards.AddRange(ModelDb.CardPool<SakikoMusicalCardPool>().AllCards);
            musicCards.AddRange(ModelDb.CardPool<MutsumiMusicalCardPool>().AllCards);
        }

        var cardList = CardFactory.FilterForCombat(musicCards);
        var cardModel = Owner.RunState.Rng.CombatCardGeneration.NextItem(cardList);
        if (cardModel != null)
        {
            var generatedCard = CombatState.CreateCard(cardModel, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}