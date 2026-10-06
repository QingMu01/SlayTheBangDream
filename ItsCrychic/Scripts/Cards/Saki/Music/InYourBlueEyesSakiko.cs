using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class InYourBlueEyesSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget),
    IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.AnySlot;

    private CardModel? _boundCard;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
        _boundCard != null ? [HoverTipFactory.FromCard(_boundCard)] : [];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (_boundCard?.CombatState == null || _boundCard.Pile == null) return;

        await CardPileCmd.Add(_boundCard, PileType.Hand);
    }

    public async Task OnCardEnterPerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this || _boundCard != null) return;

        var handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (handCards.Count == 0) return;

        var selectedCards = await CardSelectCmd.FromSimpleGrid(choiceContext,
            handCards,
            Owner,
            CardSelectorPrompt.ToHand.GetFixedPrefs(1)
        );

        _boundCard = selectedCards.FirstOrDefault();

        if (IsUpgraded && _boundCard != null)
        {
            CardCmd.Upgrade(_boundCard);
        }
    }

    public Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this || _boundCard == null) return Task.CompletedTask;

        _boundCard = null;
        return Task.CompletedTask;
    }
}