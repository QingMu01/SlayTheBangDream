using System.Collections.Generic;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>演奏区的机制侧契约，隔离机制层与具体节点。</summary>
public interface IPerformArea
{
    bool IsSlotLayoutPlaying { get; }

    bool IsSlotSelectionActive { get; }

    CardModel? SlotSelectionCard { get; }

    /// <summary>当前候选槽位，未指向时为 0。</summary>
    int HoveredSlotIndex { get; }

    Transform2D GlobalTransform { get; }

    bool IsInsideTree();

    Task AwaitProcessFrame();

    void AddItem(CardModel card, PerformContext context, bool waitForCardArrival);

    void RemoveItem(CardModel card);

    void RefreshItemLayout();

    void SyncSlots(int totalSlotCount, IReadOnlyCollection<int> activeSlots);

    void SubmitScheme(IPerformScheme scheme);

    void SubmitChanged();

    void PlayCardArrivalBounce(CardModel card);

    void PlayFlashVfx(CardModel card);

    bool TryGetCardSlotCenter(CardModel card, out Vector2 center);

    /// <summary>返回槽位全局变换与未缩放尺寸。</summary>
    bool TryGetCardSlotTransform(CardModel card, out Transform2D transform, out Vector2 size);

    /// <summary>进入手动落位选择。</summary>
    void BeginSlotSelection(CardModel card, PerformSlotSelectionMode mode,
        IReadOnlyList<PerformSlotCandidate> candidates);

    void RefreshSlotSelection();

    void EndSlotSelection();

    string DescribeSlotSelection();

    /// <summary>消费手动选择结果。</summary>
    PerformEnqueueRequest TryConsumeSlotSelection();

    void AddVfx(Node vfx);

    /// <summary>设置冻结表现。</summary>
    void SetFrozen(bool frozen);

    /// <summary>更新激活分组。</summary>
    void SetActivatedGroups(PerformChord groups);
}

/// <summary>行布局；Mirrored 用于修正视觉倾角方向。</summary>
public readonly record struct PerformRow(Rect2 Rect, float Tilt);

/// <summary>冻结层所需的分组行布局与镜像信息。</summary>
public readonly record struct PerformRowGroup(PerformChord Group, PerformRow[] Rows, bool Mirrored);

/// <summary>激活层所需的行、角标与角色端点。</summary>
public readonly record struct PerformActivationGroup(
    PerformChord Group,
    PerformRow[] Rows,
    Vector2 BadgeCenter,
    Vector2 TargetCenter,
    bool Mirrored);

/// <summary>内容模组冻结表现契约。</summary>
public interface IPerformAreaFreezeVisual
{
    /// <summary>挂到 ItemContainer 下的节点。</summary>
    Node2D View { get; }

    /// <summary>接收分组行布局。</summary>
    void SetGroups(IReadOnlyList<PerformRowGroup> groups);

    void PlayShow();

    void PlayHide();
}

/// <summary>内容模组分组激活表现契约。</summary>
public interface IPerformAreaActivationVisual
{
    /// <summary>挂到 ItemContainer 下的节点。</summary>
    Node2D View { get; }

    /// <summary>一次性表现时长，用于串行排队。</summary>
    float OneShotLifeTime { get; }

    /// <summary>接收全部激活分组。</summary>
    void SetActive(IReadOnlyList<PerformActivationGroup> groups);

    void PlayOneShot(PerformActivationGroup group);

    void Clear();
}

/// <summary>内容模组注册表现实现；未注册时仅使用核心基础表现。</summary>
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
