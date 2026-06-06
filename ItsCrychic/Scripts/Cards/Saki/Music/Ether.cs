using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Ether() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.Performance,
        BangDreamConst.PerformanceArea
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    private int _addedCapacity;

    public override Task OnStartPerformance(PlayerChoiceContext choiceContext)
    {
        var performanceManager = Owner.AttachedData().PerformanceManager;
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
        var performanceManager = Owner.AttachedData().PerformanceManager;
        if (_addedCapacity > 0)
        {
            ItsCrychic.Logger.Info($"Ether: Reduce capacity{_addedCapacity}");
            performanceManager.ReduceCapacity(_addedCapacity);
            _addedCapacity = 0;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}