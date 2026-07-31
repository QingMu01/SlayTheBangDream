using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class BeyondDream() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<IHoverTip> CardHoverTips
    {
        get
        {
            if (IsMutable)
            {
                var candidates = GetCandidates();
                if (candidates != null)
                {
                    foreach (var cardModel in candidates)
                    {
                        yield return HoverTipFactory.FromCard(cardModel, cardModel.IsUpgraded);
                    }
                }
            }
        }
    }

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var candidates = GetCandidates()?.Select(card => CombatState.CreateCard(card.CanonicalInstance, Owner))
            .ToList();

        if (candidates == null || candidates.Count == 0)
        {
            return;
        }

        CardModel? selectedCard;
        if (candidates.Count < 3)
        {
            selectedCard = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates, Owner, true);
        }
        else
        {
            selectedCard = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, Owner,
                CardSelectorPrompt.ToHand.GetFixedPrefs(1, false, true))).FirstOrDefault();
        }

        if (selectedCard != null)
        {
            selectedCard.EnergyCost.SetThisCombat(0, true);
            await CardPileCmd.AddGeneratedCardToCombat(selectedCard, PileType.Hand, Owner);
        }
    }


    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    private IEnumerable<CardModel>? GetCandidates()
    {
        if (IsMutable && Owner is { RunState: not null })
        {
            return Owner.RunState.MapPointHistory
                .Reverse()
                .SelectMany(act => act.Reverse())
                .Where(mapPoint => mapPoint.HasRoomOfType(RoomType.Monster) || mapPoint.HasRoomOfType(RoomType.Elite) ||
                                   mapPoint.HasRoomOfType(RoomType.Boss))
                .Select(entries => entries.GetEntry(Owner.NetId).CardChoices)
                .FirstOrDefault(choices => choices.Count > 0)?
                .Where(entry => !entry.wasPicked && entry.Card.Id != null)
                .Select(entry => FromSerializable(entry.Card));
        }

        return null;
    }
}