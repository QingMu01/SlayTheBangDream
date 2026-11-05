using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Powers;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>
/// 终达繁星：每次失去余音后，下一次发射音符造成的伤害增加当前能力层数点。
/// 加成在下一次音符批次结算后清空。
/// </summary>
public class AdAstraPower : BandPowerModel, ISecondaryResourceHookListener, IMusicNoteModifyHookListener,
    IMusicNoteShotHookListener
{
    private decimal _pendingDamageBonus;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Player != Owner.Player) return Task.CompletedTask;
        if (!context.Definition.Id.Equals(BangDreamConst.LingeredResource)) return Task.CompletedTask;
        if (context.NewAmount >= context.OldAmount) return Task.CompletedTask;

        Flash();
        _pendingDamageBonus += Amount;

        return Task.CompletedTask;
    }

    public decimal CaptureMusicNoteDamageAdditive(Creature? dealer, AbstractModel? source)
    {
        if (dealer != Owner) return 0m;

        return _pendingDamageBonus;
    }

    public Task AfterShot(VfxContext context, Player player)
    {
        if (player == Owner.Player)
        {
            _pendingDamageBonus = 0m;
        }

        return Task.CompletedTask;
    }
}
