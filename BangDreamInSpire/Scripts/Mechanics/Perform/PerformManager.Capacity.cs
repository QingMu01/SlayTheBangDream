// PerformManager 的容量部分：演奏方案、槽位总数与容量增减。
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics.Perform.Schemes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

public partial class PerformManager
{
    private IPerformScheme _scheme = EmptyPerformScheme.Instance;

    private PerformCapacityState _capacity = new(EmptyPerformScheme.Instance);

    /// <summary>当前已激活的演奏槽位总数。</summary>
    public int Capacity => _capacity.ActiveSlotCount;

    /// <summary>全部演奏分组的容量上限之和。</summary>
    public int MaxCapacity => _capacity.TotalSlotCount;

    /// <summary>角色提供的演奏方案（不可变策略）。</summary>
    public IPerformScheme Scheme => _scheme;

    /// <summary>
    /// 增加演奏容量。未指定和弦分组时对全部分组生效。
    /// </summary>
    public void AddCapacity(int amount, PerformChord? chord = null)
    {
        if (amount <= 0) return;

        switch (_capacity.Add(amount, chord))
        {
            case PerformCapacityChangeResult.Applied:
                SyncPerformArea();
                break;
            case PerformCapacityChangeResult.RejectedAtMax:
                ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
                break;
            case PerformCapacityChangeResult.Ignored:
            default:
                break;
        }
    }

    /// <summary>
    /// 减少演奏容量。未指定和弦分组时对全部分组生效。
    /// </summary>
    public void ReduceCapacity(int amount, PerformChord? chord = null)
    {
        if (amount <= 0) return;
        _capacity.Reduce(amount, chord);
        SyncPerformArea();
        TaskHelper.RunSafely(RemoveOverflowItems());
    }

    private void SyncPerformArea()
    {
        PerformArea?.SyncSlots(_capacity.TotalSlotCount, _capacity.ActiveSlots);
    }
}
