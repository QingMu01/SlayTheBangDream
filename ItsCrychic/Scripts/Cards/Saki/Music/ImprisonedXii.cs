using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Power.Temporary;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class ImprisonedXii() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<ImprisonedXiiDownPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(1),
        QuickVar.Buff.Create("StrengthLoss", 10)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var targets = IsUpgraded
            ? CombatState.HittableEnemies.ToList()
            : Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies) is { } randomTarget
                ? [randomTarget]
                : [];

        foreach (var target in targets)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target, QuickVar.Buff.GetVar(this).BaseValue,
                Owner.Creature, this);

            if (perform.IsSubsideTriggered && target.IsHittable)
            {
                await PowerCmd.Apply<ImprisonedXiiDownPower>(choiceContext, target,
                    DynamicVars["StrengthLoss"].BaseValue, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthLoss"].UpgradeValueBy(5);
    }
}