using BangDreamLib.Scripts.Extensions;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Ether() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    private int _addedCapacity;

    public override Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        var performanceManager = Owner.AttachedNode().PerformanceManager;
        _addedCapacity = 7 - performanceManager.Capacity;
        if (_addedCapacity > 0)
        {
            performanceManager.AddCapacity(_addedCapacity);
        }
        else
        {
            _addedCapacity = 0;
        }

        return Task.CompletedTask;
    }

    public override Task OnStopPerformance(PlayerChoiceContext choiceContext)
    {
        var performanceManager = Owner.AttachedNode().PerformanceManager;
        if (_addedCapacity > 0)
        {
            performanceManager.ReduceCapacity(_addedCapacity);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}