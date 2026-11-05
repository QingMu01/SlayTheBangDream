using BangDreamInSpire.Scripts.Utils;
using ItsCrychic.Scripts.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

[RegisterDustyTomeCard(typeof(TogawaSakiko))]
public class NoteTorrent() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ISecondaryResourceHookListener
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Ancient;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    private int _lingeredGainedThisTurn;

    protected override bool HasEnergyCostX => true;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(11m, ValueProp.Move),
        ModCardVars.Int("LingeredPerExtraHit", 4)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var lingeredPerExtraHit = DynamicVars["LingeredPerExtraHit"].IntValue;
        var extraHits = lingeredPerExtraHit > 0 ? _lingeredGainedThisTurn / lingeredPerExtraHit : 0;
        var hitCount = ResolveEnergyXValue() + extraHits;

        if (hitCount <= 0 || CombatState.HittableEnemies.Count == 0) return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .TargetingAllOpponents(CombatState)
            .WithHitCount(hitCount)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Player != Owner) return Task.CompletedTask;
        if (!context.Definition.Id.Equals(BangDreamConst.LingeredResource)) return Task.CompletedTask;

        var gained = context.NewAmount - context.OldAmount;
        if (gained > 0)
        {
            _lingeredGainedThisTurn += gained;
        }

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            _lingeredGainedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["LingeredPerExtraHit"].UpgradeValueBy(-1);
    }
}
