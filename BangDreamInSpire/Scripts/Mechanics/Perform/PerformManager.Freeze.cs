using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>歌单冻结与分组激活；冻结时演奏转化为激活分组。</summary>
public partial class PerformManager
{
    private bool _isPlaylistFrozen;
    private PerformChord _activeGroups = PerformChord.None;

    public bool IsPlaylistFrozen => _isPlaylistFrozen;

    /// <summary>设置冻结状态并同步演奏区。</summary>
    public void SetPlaylistFrozen(bool frozen)
    {
        if (_isPlaylistFrozen == frozen) return;

        _isPlaylistFrozen = frozen;
        if (!frozen)
        {
            // 解冻时清空激活位。
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

    /// <summary>更新激活分组与演奏区表现。</summary>
    public void SetActivatedGroups(PerformChord groups)
    {
        _activeGroups = groups;
        if (!TryGetPerformArea(out var performArea)) return;

        // 演奏区重建后补同步冻结状态。
        performArea.SetFrozen(_isPlaylistFrozen);
        performArea.SetActivatedGroups(groups);
    }

    /// <summary>冻结时接管演奏并激活分组；true 表示调用方必须停止后续结算。</summary>
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

    /// <summary>返回槽位所属分组，无效索引用默认组。</summary>
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

    /// <summary>依次使用手动分组、目标槽位分组与默认组。</summary>
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

    /// <summary>按卡牌槽位解析分组，不在歌单时用默认组。</summary>
    private PerformChord ResolveGroupOfCard(CardModel cardModel)
    {
        var slotIndex = CardContexts.GetOrCreate(cardModel).SlotIndex;
        return IsValidSlot(slotIndex) ? ResolveGroupOfSlot(slotIndex) : _capacity.ResolveDefaultGroup();
    }
}
