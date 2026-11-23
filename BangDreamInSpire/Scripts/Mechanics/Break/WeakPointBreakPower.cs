using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>弱点击破状态，持续目标一侧一个回合；期间不能继续施加破绽。</summary>
public sealed class WeakPointBreakPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>进入时切换破裂视觉。</summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        NWeaknessBreakVfx.Sync(Owner);
        return Task.CompletedTask;
    }

    /// <summary>目标一侧回合结束时递减。</summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner is not { } owner || !participants.Contains(owner))
        {
            return;
        }

        await PowerCmd.TickDownDuration(this);
    }

    /// <summary>退出时清空破绽。</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        // 死亡清理不再排额外移除等待。
        if (!oldOwner.IsDead)
        {
            await PowerCmd.Remove<WeaknessBreakPower>(oldOwner);
        }

        NWeaknessBreakVfx.Sync(oldOwner);
    }
}
