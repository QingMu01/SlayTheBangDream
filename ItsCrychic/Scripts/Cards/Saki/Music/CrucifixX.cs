using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Power.Debuff;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class CrucifixX() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    public override bool IsInstant => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<CrucifixXPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(10)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        foreach (var target in CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<CrucifixXPower>(choiceContext, target,
                QuickVar.Buff.GetVar(this).BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(10);
    }
}