using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>破绽计数，满层进入弱点击破；隐藏 Power 图标，由常驻 VFX 表现。</summary>
public sealed class WeaknessBreakPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>层数截断到上限。</summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ClampToMax();
        NWeaknessBreakVfx.Sync(Owner);
        return Task.CompletedTask;
    }

    /// <summary>层数变化后刷新视觉。</summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        if (!ReferenceEquals(power, this))
        {
            return Task.CompletedTask;
        }

        ClampToMax();
        NWeaknessBreakVfx.Sync(Owner);
        return Task.CompletedTask;
    }

    /// <summary>移除后淡出视觉。</summary>
    public override Task AfterRemoved(Creature oldOwner)
    {
        NWeaknessBreakVfx.Sync(oldOwner);
        return Task.CompletedTask;
    }

    /// <summary>计算剩余可容纳层数。</summary>
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount,
        Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not WeaknessBreakPower || amount <= 0m)
        {
            return false;
        }

        if (Owner is not { } owner || !ReferenceEquals(target, owner))
        {
            return false;
        }

        var remaining = Math.Max(0, BreakConst.MaxStacks - Amount);
        var clamped = Math.Min(amount, remaining);
        if (clamped == amount)
        {
            return false;
        }

        modifiedAmount = clamped;
        return true;
    }

    private void ClampToMax()
    {
        if (Amount > BreakConst.MaxStacks)
        {
            SetAmount(BreakConst.MaxStacks);
        }
    }
}
