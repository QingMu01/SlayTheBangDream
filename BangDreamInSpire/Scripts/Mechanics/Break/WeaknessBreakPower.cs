using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 破绽（Debuff，Counter）。可附着于**任意单位**，上限 <see cref="BreakConst.MaxStacks" /> 层，
/// 达到上限时由 <see cref="BreakCmd" /> 施加 <see cref="WeakPointBreakPower" /> 进入弱点击破。
/// <para>
/// 刻意**不做 Power 图标**（<see cref="IsVisibleInternal" /> = false）：视觉表现完全交给
/// 常驻的 <see cref="NWeaknessBreakVfx" />，Hover 时由它弹出与能力同款的 tips。
/// </para>
/// </summary>
public sealed class WeaknessBreakPower : BandPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>超出上限的层数一律截断（正常路径由 <see cref="BreakCmd.Discover" /> 预先截断，这里是兜底）。</summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ClampToMax();
        NWeaknessBreakVfx.Sync(Owner);
        return Task.CompletedTask;
    }

    /// <summary>层数变化（叠加/递减）后刷新视觉。</summary>
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

    /// <summary>被移除（层数归零、被清除、所有单位死亡等）后淡出视觉。</summary>
    public override Task AfterRemoved(Creature oldOwner)
    {
        NWeaknessBreakVfx.Sync(oldOwner);
        return Task.CompletedTask;
    }

    /// <summary>在层数真正写入前把「剩余可容纳层数」算出来，保证任何入口都不会超过上限。</summary>
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
