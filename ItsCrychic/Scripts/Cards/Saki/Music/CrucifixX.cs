using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class CrucifixX() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    public override bool IsInstant { get; set; } = true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Instant,
        BangDreamConst.Perform
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("PerCardDamage", 3),
        ComputedDynamicVarHelper.CreateBaseVar("FixedDamage", 7m, (card, _) =>
        {
            if (card != null && card.DynamicVars.TryGetValue("PerCardDamage", out var perCardDamage))
            {
                return 7m + card.Owner.AttachedData().PerformManager.Count * perCardDamage.IntValue;
            }

            return 7m;
        })
    ];

    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.ComputedValue("FixedDamage"))
            .FromCard(this, null)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PerCardDamage"].UpgradeValueBy(2);
    }
}