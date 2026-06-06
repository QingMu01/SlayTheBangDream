using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class SoundWave() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ICardPerformanceHook
{
    private const int CustomCost = 4;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    private readonly HashSet<CardModel> _performanceCards = [];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformanceArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(21),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }

    public Task OnCardEnterPerformanceArea(CardModel cardModel)
    {
        _performanceCards.Add(cardModel);
        UpdateCost();
        return Task.CompletedTask;
    }

    public Task OnCardLeavePerformanceArea(CardModel cardModel)
    {
        _performanceCards.Remove(cardModel);
        UpdateCost();
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        InitSet();
        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        InitSet();
        return Task.CompletedTask;
    }

    private void InitSet()
    {
        _performanceCards.Clear();
        var performanceCards = BangDreamTools.GetPile(BangDreamConst.PerformanceTable, Owner).Cards.ToList();
        foreach (var card in performanceCards)
        {
            _performanceCards.Add(card);
        }

        UpdateCost();
    }

    private void UpdateCost()
    {
        EnergyCost.SetThisTurn(-_performanceCards.Count);
    }
}