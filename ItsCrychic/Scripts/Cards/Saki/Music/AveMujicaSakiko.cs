using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class AveMujicaSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Rare;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.ReplayStatic)
    ];

    private readonly Dictionary<CardModel, int> _affectedCards = [];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Repeat.Create(1)];

    public override Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);

        // 只随机强化手牌中的一张基础牌。
        var basics = Owner.PlayerCombatState.Hand.Cards
            .Where(card => card.Rarity == CardRarity.Basic)
            .ToList();
        var selectedCard = Owner.RunState.Rng.CombatCardSelection.NextItem(basics);
        if (selectedCard == null) return Task.CompletedTask;

        selectedCard.BaseReplayCount += DynamicVars.Repeat.IntValue;
        _affectedCards[selectedCard] = _affectedCards.GetValueOrDefault(selectedCard) + DynamicVars.Repeat.IntValue;

        return Task.CompletedTask;
    }

    public Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this) return Task.CompletedTask;
        foreach (var (card, amount) in _affectedCards)
        {
            card.BaseReplayCount = Math.Max(0, card.BaseReplayCount - amount);
        }

        _affectedCards.Clear();
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}
