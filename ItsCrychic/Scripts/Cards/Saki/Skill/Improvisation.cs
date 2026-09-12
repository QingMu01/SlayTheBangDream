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

public class Improvisation() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.Music
    ];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(13)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

        var musicCards = new List<CardModel>();
        if (Owner.Character is IPerformableCharacter character && character.ExtraCardPool.AllCards.Any())
        {
            musicCards.AddRange(character.ExtraCardPool.AllCards);
        }
        else
        {
            musicCards.AddRange(ModelDb.AllCharacters
                .OfType<IPerformableCharacter>()
                .SelectMany(item => item.ExtraCardPool.AllCards));
        }

        var cardList = CardFactory.FilterForCombat(musicCards)
            .Where(card => card is IPerformCard { IsInstant: true })
            .ToList();
        var cardModel = Owner.RunState.Rng.CombatCardGeneration.NextItem(cardList);
        if (cardModel != null)
        {
            var generatedCard = CombatState.CreateCard(cardModel, Owner);
            await CardPileCmd.AddGeneratedCardToCombat(generatedCard, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
    }
}
