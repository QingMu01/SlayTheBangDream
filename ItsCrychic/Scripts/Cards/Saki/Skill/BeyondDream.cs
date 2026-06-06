using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class BeyondDream() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.Self;

    private bool _canExtraTurn;

    protected override bool IsPlayable => Owner.Creature.CurrentHp <= DynamicVars["HpThreshold"].IntValue;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("HpThreshold", 15)
    ];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _canExtraTurn = true;

        return Task.CompletedTask;
    }

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner && _canExtraTurn;
    }

    public override Task AfterTakingExtraTurn(Player player)
    {
        _canExtraTurn = false;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HpThreshold"].UpgradeValueBy(5);
    }
}