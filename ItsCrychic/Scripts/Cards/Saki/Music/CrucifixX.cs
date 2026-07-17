using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class CrucifixX() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords => [];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(7),
        QuickVar.Damage.Create("PerCard", 3)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var count = BangDreamTools.GetPile(BangDreamConst.PerformPile, Owner).Cards.Count;
        var damage = DynamicVars.Damage.IntValue + count * DynamicVars["PerCard"].IntValue;
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await CreatureCmd.Damage(choiceContext, enemy, new DamageVar(damage, ValueProp.Unpowered),
                Owner.Creature, this, null);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PerCard"].UpgradeValueBy(2);
    }
}
