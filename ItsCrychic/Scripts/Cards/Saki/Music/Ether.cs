using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Ether() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<IHoverTip> CardHoverTips
    {
        get
        {
            return ModelDb.AllCards.Where(item => item.Tags.Contains(BangDreamConst.SymbolCard))
                .Select(cardModel => HoverTipFactory.FromCard(cardModel));
        }
    }

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        var symbolCards = BangDreamConst.PerformPile.GetPile(Owner).Cards
            .Where(cardModel => cardModel.Tags.Contains(BangDreamConst.SymbolCard) && cardModel is IPerformCard)
            .ToList();
        var manager = Owner.AttachedData().PerformManager;
        foreach (var symbolCard in symbolCards)
        {
            await manager.PerformCard(symbolCard);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
