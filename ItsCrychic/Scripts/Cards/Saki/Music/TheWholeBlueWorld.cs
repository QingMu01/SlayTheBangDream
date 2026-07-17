using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class TheWholeBlueWorld() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None),
    IPerformHookListener
{
    private bool _isRepeatingSubsidePerform;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new PowerVar<VulnerablePower>(1),
        new PowerVar<WeakPower>(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var targets = IsUpgraded
            ? CombatState.HittableEnemies.ToList()
            : Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies) is { } randomTarget
                ? [randomTarget]
                : [];
        foreach (var target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.IntValue,
                Owner.Creature, this);
            await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue,
                Owner.Creature, this);
        }
    }

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel)
    {
        if (cardModel != this || !ctx.IsSubsideTriggered || _isRepeatingSubsidePerform) return;

        _isRepeatingSubsidePerform = true;
        try
        {
            await Owner.AttachedData().PerformManager.PerformCard(this);
        }
        finally
        {
            _isRepeatingSubsidePerform = false;
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后对所有敌人奏效。
    }
}
