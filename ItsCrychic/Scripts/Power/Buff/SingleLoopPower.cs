using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace ItsCrychic.Scripts.Power.Buff;

public class SingleLoopPower : BandPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStartLate(CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.Player == null)
        {
            return;
        }

        var manager = Owner.Player.AttachedData().PerformManager;
        var topMusicCard = manager.PerformPile.Cards
            .Where(card => card is IPerformCard)
            .OrderByDescending(card => manager.CardContexts.GetOrCreate(card).SlotIndex)
            .FirstOrDefault();
        if (topMusicCard == null)
        {
            return;
        }

        for (var i = 0; i < Amount; i++)
        {
            await manager.PerformCard(topMusicCard);
        }
    }
}
