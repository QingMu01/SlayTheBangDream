using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class AveMujica() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.ReplayStatic)
    ];

    private readonly Dictionary<CardModel, int> _affectedCards = [];

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Repeat.Create(1)];

    public override Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);

        foreach (var card in Owner.PlayerCombatState.Hand.Cards.Where(card => card.Rarity == CardRarity.Basic))
        {
            card.BaseReplayCount += DynamicVars.Repeat.IntValue;
            _affectedCards[card] = _affectedCards.GetValueOrDefault(card) + DynamicVars.Repeat.IntValue;
        }

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