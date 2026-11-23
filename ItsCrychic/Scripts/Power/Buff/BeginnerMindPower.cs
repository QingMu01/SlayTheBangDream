using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>回合开始减少歌单容量；超出容量的牌由 PerformManager 退回手牌。</summary>
public class BeginnerMindPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player is not { } owner || player != owner) return Task.CompletedTask;

        Flash();
        owner.AttachedData().Perform.ReduceCapacity(1);

        return Task.CompletedTask;
    }
}
