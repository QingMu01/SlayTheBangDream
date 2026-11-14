using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 弱点击破状态（1 回合）。目标破绽满层时由 <see cref="BreakCmd.Discover" /> 施加：
/// 期间**不能再被发现破绽**（<see cref="BreakCmd.CanDiscover" /> 短路），
/// 到期（目标一侧回合结束）时清空破绽并让常驻视觉淡出。
/// <para>同样不做 Power 图标，视觉由 <see cref="NWeaknessBreakVfx" /> 的「破裂」形态承担。</para>
/// </summary>
public sealed class WeakPointBreakPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>进入弱点击破：视觉切换为破裂形态。</summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        NWeaknessBreakVfx.Sync(Owner);
        return Task.CompletedTask;
    }

    /// <summary>「1 回合」：在**目标一侧**的回合结束时递减，归零即移除。</summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner is not { } owner || !participants.Contains(owner))
        {
            return;
        }

        await PowerCmd.TickDownDuration(this);
    }

    /// <summary>退出弱点击破：清空破绽（连带视觉淡出）。</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        await PowerCmd.Remove<WeaknessBreakPower>(oldOwner);
        NWeaknessBreakVfx.Sync(oldOwner);
    }
}
