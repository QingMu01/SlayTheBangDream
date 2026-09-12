using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class SoundWave() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), IPerformHookListener
{
    private const int CustomCost = 4;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    private int _performDiscount;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(21),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _performDiscount = 0;

        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }

    public Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        _performDiscount++;
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card != this)
        {
            modifiedCost = originalCost;
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - _performDiscount);
        return modifiedCost != originalCost;
    }
}