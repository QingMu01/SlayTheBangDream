using System.Collections.Generic;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 演奏区域的机制侧契约。机制层通过本接口驱动布局与表现，不直接依赖 <c>NPerformArea</c>。
/// </summary>
public interface IPerformArea
{
    /// <summary>槽位整体的移动与新增槽位动画尚未完成。</summary>
    bool IsSlotLayoutPlaying { get; }

    /// <summary>是否正处于手动落位选择态。</summary>
    bool IsSlotSelectionActive { get; }

    /// <summary>当前等待手动落位的卡牌；无选择时为 <see langword="null" />。</summary>
    CardModel? SlotSelectionCard { get; }

    /// <summary>当前鼠标指向的候选槽位；未指向时为 0。</summary>
    int HoveredSlotIndex { get; }

    /// <summary>演奏区域整体的全局变换。</summary>
    Transform2D GlobalTransform { get; }

    /// <summary>节点是否仍在场景树内。</summary>
    bool IsInsideTree();

    /// <summary>等待下一帧。</summary>
    Task AwaitProcessFrame();

    /// <summary>向指定槽位放入卡牌。</summary>
    void AddItem(CardModel card, PerformContext context, bool waitForCardArrival);

    /// <summary>从演奏区域移除卡牌。</summary>
    void RemoveItem(CardModel card);

    /// <summary>重新排列现有槽位。</summary>
    void RefreshItemLayout();

    /// <summary>同步总槽位数与激活槽位。</summary>
    void SyncSlots(int totalSlotCount, IReadOnlyCollection<int> activeSlots);

    /// <summary>提交角色演奏方案。</summary>
    void SubmitScheme(IPerformScheme scheme);

    /// <summary>通知方案变更并刷新。</summary>
    void SubmitChanged();

    /// <summary>播放卡牌入场弹跳。</summary>
    void PlayCardArrivalBounce(CardModel card);

    /// <summary>在卡牌槽位播放演奏扫光。</summary>
    void PlayFlashVfx(CardModel card);

    /// <summary>取卡牌所属槽位的显示中心。</summary>
    bool TryGetCardSlotCenter(CardModel card, out Vector2 center);

    /// <summary>取卡牌所属槽位的全局变换与未缩放尺寸。</summary>
    bool TryGetCardSlotTransform(CardModel card, out Transform2D transform, out Vector2 size);

    /// <summary>进入手动落位选择态。</summary>
    void BeginSlotSelection(CardModel card, PerformSlotSelectionMode mode,
        IReadOnlyList<PerformSlotCandidate> candidates);

    /// <summary>按当前鼠标位置刷新选择指向。</summary>
    void RefreshSlotSelection();

    /// <summary>结束手动落位选择态。</summary>
    void EndSlotSelection();

    /// <summary>选择态诊断文本。</summary>
    string DescribeSlotSelection();

    /// <summary>消费玩家在手动落位阶段做出的选择。</summary>
    PerformEnqueueRequest TryConsumeSlotSelection();

    /// <summary>把特效节点挂到演奏区域下。</summary>
    void AddVfx(Node vfx);

    /// <summary>设置歌单冻结表现（槽位降饱和 + 冷色覆盖层）。</summary>
    void SetFrozen(bool frozen);

    /// <summary>写回已激活的分组（位掩码），点亮对应分组的行与角标。</summary>
    void SetActivatedGroups(PerformChord groups);
}
