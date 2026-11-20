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

/// <summary>
/// 演奏区的一行：视觉矩形（容器局部坐标）与冻结时的倾斜（弧度，槽位节点本地 rotation；
/// 左侧列为 +、右侧列视觉反向，因此特效按 <c>Mirrored</c> 取符号即可得到视觉角度）。
/// </summary>
public readonly record struct PerformRow(Rect2 Rect, float Tilt);

/// <summary>
/// 歌单冻结层的数据：一个分组的行布局（容器局部坐标）与是否右侧镜像。
/// 由核心计算、内容模组的表现实现消费，避免核心引用具体角色特效类型。
/// </summary>
public readonly record struct PerformRowGroup(PerformChord Group, PerformRow[] Rows, bool Mirrored);

/// <summary>
/// 已激活分组的布局与端点（容器局部坐标）：行、角标中心、角色命中框中心、是否镜像。
/// </summary>
public readonly record struct PerformActivationGroup(
    PerformChord Group,
    PerformRow[] Rows,
    Vector2 BadgeCenter,
    Vector2 TargetCenter,
    bool Mirrored);

/// <summary>
/// 歌单冻结层的表现契约（具体实现由内容模组提供）。核心只负责在布局变化后投喂数据。
/// </summary>
public interface IPerformAreaFreezeVisual
{
    /// <summary>实际挂到演奏区 <c>ItemContainer</c> 下的节点。</summary>
    Node2D View { get; }

    /// <summary>投喂分组行布局（幂等）。</summary>
    void SetGroups(IReadOnlyList<PerformRowGroup> groups);

    /// <summary>开始冻结表现。</summary>
    void PlayShow();

    /// <summary>结束冻结表现（实现可自行淡出并释放节点）。</summary>
    void PlayHide();
}

/// <summary>
/// 分组激活表现契约（具体实现由内容模组提供）。
/// </summary>
public interface IPerformAreaActivationVisual
{
    /// <summary>实际挂到演奏区 <c>ItemContainer</c> 下的节点。</summary>
    Node2D View { get; }

    /// <summary>一次性激活表现时长，核心据此把多组点亮串行排队。</summary>
    float OneShotLifeTime { get; }

    /// <summary>投喂全部已激活分组（幂等）。</summary>
    void SetActive(IReadOnlyList<PerformActivationGroup> groups);

    /// <summary>播放某个分组的一次性激活表现。</summary>
    void PlayOneShot(PerformActivationGroup group);

    /// <summary>清空常驻层。</summary>
    void Clear();
}

/// <summary>
/// 内容模组注册演奏区表现实现的入口。核心不持有任何具体角色特效类型；
/// 未注册时冻结/激活只有槽位降亮与角标等核心表现。
/// </summary>
public static class PerformAreaVisuals
{
    private static Func<IPerformAreaFreezeVisual>? _freezeFactory;
    private static Func<IPerformAreaActivationVisual>? _activationFactory;

    public static void RegisterFreezeVisual(Func<IPerformAreaFreezeVisual> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _freezeFactory = factory;
    }

    public static void RegisterActivationVisual(Func<IPerformAreaActivationVisual> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _activationFactory = factory;
    }

    public static IPerformAreaFreezeVisual? CreateFreezeVisual() => _freezeFactory?.Invoke();

    public static IPerformAreaActivationVisual? CreateActivationVisual() => _activationFactory?.Invoke();
}
