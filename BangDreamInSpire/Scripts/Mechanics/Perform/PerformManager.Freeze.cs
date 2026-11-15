using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
/// 歌单冻结与分组激活。<see cref="PerformManager" /> 的分部实现。
/// 由傀儡师机制在形态切换时驱动：冻结期间音乐牌不再进入歌单，一切「演奏」尝试退化为激活对应分组。
/// </summary>
public partial class PerformManager
{
    private bool _isPlaylistFrozen;
    private PerformChord _activeGroups = PerformChord.None;

    /// <summary>
    /// 歌单是否被外部机制冻结（傀儡师：傀儡形态）。
    /// 冻结期间音乐牌不再进入歌单、已入组的牌不能被移除，一切"演奏"尝试改为激活对应分组。
    /// </summary>
    public bool IsPlaylistFrozen => _isPlaylistFrozen;

    /// <summary>
    /// 设置歌单冻结状态，并同步演奏区的占位表现（冻结 = 槽位降饱和 + 冷色覆盖层）。
    /// 由傀儡师形态管理器在形态切换时调用。
    /// </summary>
    public void SetPlaylistFrozen(bool frozen)
    {
        if (_isPlaylistFrozen == frozen) return;

        _isPlaylistFrozen = frozen;
        if (!frozen)
        {
            // 解冻同时清空分组激活位：激活只在冻结期间有意义，留着会在下次槽位同步时被重新点亮。
            _activeGroups = PerformChord.None;
        }

        if (TryGetPerformArea(out var performArea))
        {
            performArea.SetFrozen(frozen);
            if (!frozen) performArea.SetActivatedGroups(PerformChord.None);
        }

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player?.Character} ({_player?.NetId}) playlist frozen : {frozen}.");
    }

    /// <summary>
    /// 写回分组激活位并刷新演奏区表现（点亮 / 熄灭分组角标）。
    /// 由傀儡师形态管理器在分组激活与复活时调用。
    /// </summary>
    public void SetActivatedGroups(PerformChord groups)
    {
        _activeGroups = groups;
        if (!TryGetPerformArea(out var performArea)) return;

        // 演奏区可能在战斗中被重建（插槽变更 / 节点重挂）：顺手补一次冻结状态，同值时是空操作。
        performArea.SetFrozen(_isPlaylistFrozen);
        performArea.SetActivatedGroups(groups);
    }

    /// <summary>
    /// 歌单冻结时把"试图演奏"转换为"激活对应分组"，接管本次演奏。
    /// 返回 <see langword="true" /> 表示调用方必须直接返回（不得再发动效果、不得再排网络动作）。
    /// </summary>
    /// <param name="groups">
    /// 本次演奏覆盖的分组：<see cref="PerformChord.None" /> 表示未指定，由傀儡师状态按方案默认分组兜底。
    /// </param>
    /// <param name="source">诊断用的来源描述。</param>
    private bool TryActivateGroupsWhileFrozen(PerformChord groups, string source)
    {
        if (!_isPlaylistFrozen) return false;
        if (!CombatManager.Instance.IsInProgress) return true;

        Player.AttachedData().PuppeteerFormManager.ActivateGroups(groups);
        BangDreamInSpireCore.Logger.Info(
            $"Player {Player.Character} ({Player.NetId}) frozen playlist, activate groups instead of perform : " +
            $"{source} → {groups}.");
        return true;
    }

    /// <summary>全局槽位所属的和弦分组；索引不在任何分组内时返回方案默认分组。</summary>
    private PerformChord ResolveGroupOfSlot(int slotIndex)
    {
        foreach (var descriptor in _scheme.Groups)
        {
            if (slotIndex >= descriptor.BaseSlotIndex &&
                slotIndex < descriptor.BaseSlotIndex + descriptor.MaxSlots)
            {
                return descriptor.Chord;
            }
        }

        return _capacity.ResolveDefaultGroup();
    }

    /// <summary>
    /// 冻结期间打出的音乐牌「想去的分组」：优先手动指定的分组，其次指定槽位所属分组，
    /// 都没有时退回方案默认分组（睦为 C 组）。
    /// </summary>
    private PerformChord ResolveGroupOfPlayedMusicCard(CardModel card)
    {
        var request = CardContexts.GetOrCreate(card).Request;
        if (request.Group is { } group && group != PerformChord.None)
        {
            return group;
        }

        if (request.SlotIndex is { } slotIndex)
        {
            return ResolveGroupOfSlot(slotIndex);
        }

        return _capacity.ResolveDefaultGroup();
    }

    /// <summary>
    /// 牌所在槽位的和弦分组；牌不在歌单里时退回方案默认分组。
    /// 卡牌和弦不参与分组判定（和弦只用于激活奏响），因此这里不看卡牌自身和弦。
    /// </summary>
    private PerformChord ResolveGroupOfCard(CardModel cardModel)
    {
        var slotIndex = CardContexts.GetOrCreate(cardModel).SlotIndex;
        return IsValidSlot(slotIndex) ? ResolveGroupOfSlot(slotIndex) : _capacity.ResolveDefaultGroup();
    }
}
