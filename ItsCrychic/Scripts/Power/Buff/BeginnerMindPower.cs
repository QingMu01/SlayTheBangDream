using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>
/// 初心：回合开始时，失去1点歌单容量。
/// 容量由 <c>PerformManager.ReduceCapacity</c> 统一结算，超出新容量的演奏牌会被挤回手牌（沿用其原有语义）。
/// </summary>
public class BeginnerMindPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player is not { } owner || player != owner) return Task.CompletedTask;

        Flash();
        owner.AttachedData().PerformManager.ReduceCapacity(1);

        return Task.CompletedTask;
    }
}
