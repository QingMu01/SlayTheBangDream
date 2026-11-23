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

/// <summary>失去余音后强化下一批音符，结算后清空加成。</summary>
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
