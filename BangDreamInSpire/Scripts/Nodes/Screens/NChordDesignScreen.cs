using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using BangDreamInSpire.Scripts.Nodes.Cards;
using BangDreamInSpire.Scripts.Nodes.Controls;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Nodes.Screens;

/// <summary>
/// 和弦设计面板的完成结果：把 <see cref="CardIndex" /> 指向的卡牌设为 <see cref="Chord" />。
/// <see cref="Additive" /> 为 true 时与卡牌现有和弦取并集，否则直接覆盖。
/// </summary>
public readonly record struct ChordDesignResult(int CardIndex, PerformChord Chord, bool Additive);

/// <summary>
/// 火堆（休息处）「和弦设计」选项的操作面板，参考本体锻造升级面板（<c>NDeckUpgradeSelectScreen</c>）实现。
/// <para>
/// 对齐式布局：最上方是可点和弦（C/D/F/G），点击后整行平移、所选和弦停在中央箭头正上方；
/// 中间是卡组卡牌行，同样是一格一张牌、选中时整行平移让那张牌对齐下方箭头（所有牌常驻行内，不增删）；
/// 最下方是结果展示（预览卡 + 当前/结果和弦 + 可选「增加 / 覆盖」开关）。
/// 默认状态是 C 和弦 + 第一张牌，两者都在中轴上。左侧原版取消按钮返回，右侧原版确定按钮应用。
/// </para>
/// <para>
/// 卡牌行的横向滚动是本面板自绘的：本体滚动容器只支持纵向，这里用 <see cref="NChordScrollbar" />
/// 表现「当前停在行内哪一格」，鼠标滚轮按格步进、拖拽滚动条吸附到最近的牌、键盘/手柄焦点进入某张牌
/// 也会把整行带到它上面。
/// </para>
/// <para>
/// 联机：面板只负责返回 <see cref="ChordDesignResult" />，选择同步由 <see cref="ChordDesignOption" /> 按
/// <c>ReserveChoiceId / SyncLocalChoice / WaitForRemoteChoice</c> 完成（与 <c>ExtraPileCmd</c> 同款范式）。
/// </para>
/// </summary>
public sealed partial class NChordDesignScreen : Control, IOverlayScreen
{
    /// <summary>选项与面板文案所在的本地化表。</summary>
    public const string LocTable = "rest_site_ui";

    /// <summary>面板场景（布局在场景里编辑，代码只负责内容与交互）。</summary>
    public const string ScenePath = "res://BangDreamInSpire/scenes/screens/chord_design_screen.tscn";

    /// <summary>设计分辨率（与 project.godot 的 1920×1080 一致），实际显示时整体等比缩放居中。</summary>
    private static readonly Vector2 DesignSize = new(1920f, 1080f);

    private const float ChipGap = 24f;
    private const float CardStep = 234f; // 卡牌行视觉步进：216（0.72 倍宽）+ 18 间距
    private const float CardLayerScale = 0.9f; // 行内容统一缩放：静止 0.72 倍、悬停 0.9 倍
    private const float ResultCardScale = 0.52f;

    private static readonly Color GoldColor = new("#d8bd7a");
    private static readonly Color MutedTextColor = new("#9aa3b5");
    private static readonly Font? TitleFont =
        ResourceLoader.Load<Font>("res://themes/fonts/zhs/source_han_serif_sc_bold_shared.tres");
    private static readonly Font? BodyFont =
        ResourceLoader.Load<Font>("res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres");

    private readonly List<CardModel> _cards = new();
    // 每张卡一个 holder，全部常驻行内；选中只改整行位置，不隐藏也不释放。
    private readonly Dictionary<int, NGridCardHolder> _holders = new();
    private readonly List<NChordChip> _chips = new();
    private readonly List<PerformChord> _chordOrder = [PerformChord.C, PerformChord.D, PerformChord.F, PerformChord.G];
    private readonly TaskCompletionSource<ChordDesignResult?> _completion = new();

    private Control _content = null!;
    private Control _chordRow = null!;
    private Control _band = null!;
    private Control _cardLayer = null!;
    private Control _resultCardHost = null!;
    private Control _modeToggleAnchor = null!;
    private MegaLabel _titleLabel = null!;
    private MegaLabel _subtitleLabel = null!;
    private MegaLabel _boardChordLabel = null!;
    private MegaLabel _hintLabel = null!;
    private NChordScrollbar _scrollbar = null!;
    private NChordModeToggle _modeToggle = null!;
    private NBackButton? _backButton;
    private NConfirmButton? _confirmButton;
    private NCard? _resultCard;
    private Tween? _chipLayoutTween;
    private Tween? _cardLayoutTween;

    private int _selectedCardIndex = -1;
    private PerformChord _selectedChord = PerformChord.None;
    private bool _additive;
    private bool _finished;

    /// <summary>
    /// 「增加 / 覆盖」开关是否解锁。默认 false（隐藏，只按覆盖处理）；后续由事件遗物把它置 true。
    /// </summary>
    public static bool ModeToggleUnlocked { get; set; }

    /// <summary>本次面板的完成结果；取消时保留 <see langword="null" />。</summary>
    public ChordDesignResult? Result { get; private set; }

    // ── IOverlayScreen / IScreenContext ─────────────────────────────────────────────

    public NetScreenType ScreenType => NetScreenType.Room;

    public bool UseSharedBackstop => true;

    public Control? DefaultFocusedControl => _chips.FirstOrDefault();

    public void AfterOverlayOpened()
    {
        Modulate = new Color(1f, 1f, 1f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1f, 0.18f);
        GrabInitialFocus();
    }

    public void AfterOverlayClosed()
    {
        this.QueueFreeSafely();
    }

    public void AfterOverlayShown()
    {
        GrabInitialFocus();
    }

    public void AfterOverlayHidden()
    {
    }

    /// <summary>打开面板并返回实例；完成后用 <see cref="SelectionFinished" /> 取结果。场景缺失时返回 <see langword="null" />。</summary>
    public static NChordDesignScreen? ShowScreen(IReadOnlyList<CardModel> cards)
    {
        if (!ResourceLoader.Exists(ScenePath)
            || ResourceLoader.Load<PackedScene>(ScenePath)?.Instantiate<NChordDesignScreen>() is not { } screen)
        {
            return null;
        }

        screen._cards.AddRange(cards);
        NOverlayStack.Instance!.Push(screen);
        return screen;
    }

    /// <summary>等待玩家确定或取消；取消时结果为 <see langword="null" />。</summary>
    public Task<ChordDesignResult?> SelectionFinished() => _completion.Task;

    // ── 生命周期 ────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;

        _content = GetNode<Control>("%Content");
        _titleLabel = GetNode<MegaLabel>("%Title");
        _subtitleLabel = GetNode<MegaLabel>("%Subtitle");
        _chordRow = GetNode<Control>("%ChordRow");
        _band = GetNode<Control>("%CardBand");
        // 卡牌行整体再缩 0.9：静止 0.8×0.9=0.72 倍，悬停回 1.0×0.9=0.9 倍，避免整行显得过大
        _cardLayer = new Control { Name = "CardLayer", MouseFilter = MouseFilterEnum.Pass };
        _cardLayer.Scale = new Vector2(CardLayerScale, CardLayerScale);
        _cardLayer.Size = _band.Size / CardLayerScale;
        _band.AddChild(_cardLayer);
        _scrollbar = GetNode<NChordScrollbar>("%Scrollbar");
        _resultCardHost = GetNode<Control>("%PreviewHost");
        _boardChordLabel = GetNode<MegaLabel>("%ResultChord");
        _hintLabel = GetNode<MegaLabel>("%ResultHint");
        _modeToggleAnchor = GetNode<Control>("%ModeToggleAnchor");
        _backButton = GetNodeOrNull<NBackButton>("%BackButton");
        _confirmButton = GetNodeOrNull<NConfirmButton>("%ConfirmButton");

        _titleLabel.SetTextAutoSize(Loc("CHORD_DESIGN_TITLE"));
        _subtitleLabel.SetTextAutoSize(Loc("CHORD_DESIGN_SUBTITLE"));
        _hintLabel.SetTextAutoSize(Loc("CHORD_DESIGN_HINT"));

        _scrollbar.Scrolled += OnScrollbarScrolled;
        if (_backButton != null)
        {
            _backButton.Connect(NClickableControl.SignalName.Released,
                Callable.From<NClickableControl>(_ => Cancel()));
            _backButton.Enable();
        }

        if (_confirmButton != null)
        {
            _confirmButton.Connect(NClickableControl.SignalName.Released,
                Callable.From<NClickableControl>(_ => Confirm()));
        }

        // 默认状态：C 和弦 + 第一张牌，都停在中轴上
        _selectedChord = PerformChord.C;
        _selectedCardIndex = _cards.Count > 0 ? 0 : -1;

        BuildModeToggle();
        BuildChordChips();
        RebuildCardRow();

        Resized += ApplyViewportLayout;
        ApplyViewportLayout();
        RefreshResult();
        RefreshConfirmState();
    }

    public override void _ExitTree()
    {
        _completion.TrySetResult(null);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button && IsPointerOverBand())
        {
            var direction = button.ButtonIndex switch
            {
                MouseButton.WheelUp => -1,
                MouseButton.WheelDown => 1,
                _ => 0
            };
            if (direction != 0)
            {
                StepCard(direction);
                AcceptEvent();
            }
        }
    }

    // ── 场景节点初始化 ─────────────────────────────────────────────────────────────

    private void BuildModeToggle()
    {
        if (!ModeToggleUnlocked)
        {
            return;
        }

        _modeToggle = new NChordModeToggle(Loc("CHORD_DESIGN_MODE_ADD"), Loc("CHORD_DESIGN_MODE_OVERWRITE"))
        {
            Position = Vector2.Zero
        };
        _modeToggle.Toggled += additive =>
        {
            _additive = additive;
            RefreshResult();
        };
        _modeToggleAnchor.AddChild(_modeToggle);
    }

    private void BuildChordChips()
    {
        foreach (var chord in _chordOrder)
        {
            var chip = new NChordChip(chord);
            chip.Clicked += OnChipClicked;
            _chordRow.AddChild(chip);
            _chips.Add(chip);
        }

        LayoutChips(animate: false);
    }

    // ── 视口缩放 ────────────────────────────────────────────────────────────────────

    private void ApplyViewportLayout()
    {
        if (!IsInstanceValid(_content))
        {
            return;
        }

        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0f || viewport.Y <= 0f)
        {
            return;
        }

        var scale = Mathf.Min(viewport.X / DesignSize.X, viewport.Y / DesignSize.Y);
        _content.Scale = new Vector2(scale, scale);
        _content.Position = (viewport - DesignSize * scale) * 0.5f;
    }

    // ── 和弦选择 ────────────────────────────────────────────────────────────────────

    private void OnChipClicked(NChordChip chip)
    {
        if (_finished)
        {
            return;
        }

        _selectedChord = _selectedChord == chip.Chord ? PerformChord.None : chip.Chord;
        LayoutChips(animate: true);
        RefreshResult();
        RefreshConfirmState();
    }

    /// <summary>
    /// 重新摆放和弦筹码：未选中的按顺序整体居中、挨个排开；选中的那颗滑到箭头上方的槽位。
    /// 这里只调整位置，不交换筹码彼此的顺序，视觉上就是「所有可选项整体移动」。
    /// </summary>
    /// <summary>
    /// 和弦筹码始终排在同一行：选中时整行水平平移，让选中筹码的中心正好压在中央箭头上方；
    /// 未选中时整行居中。其它筹码按原顺序左右让位，不做交换。
    /// </summary>
    private void LayoutChips(bool animate)
    {
        const float rowTopY = 0f; // chip 的 Position 是左上角，行高恰好等于 chip 高度
        _chipLayoutTween?.Kill();
        _chipLayoutTween = null;
        if (animate)
        {
            _chipLayoutTween = CreateTween();
            _chipLayoutTween.SetParallel(true);
            _chipLayoutTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        var selectedIndex = _chips.FindIndex(chip => chip.Chord == _selectedChord);
        var count = _chips.Count;
        var totalWidth = count * NChordChip.ChipSize + Mathf.Max(0, count - 1) * ChipGap;
        var centerX = DesignSize.X * 0.5f;
        // 选中时：选中筹码中心对齐中轴；未选中时：整行居中
        var startCenterX = selectedIndex >= 0
            ? centerX - selectedIndex * (NChordChip.ChipSize + ChipGap)
            : centerX - (totalWidth - NChordChip.ChipSize) * 0.5f;

        for (var i = 0; i < count; i++)
        {
            var chip = _chips[i];
            var target = new Vector2(
                startCenterX + i * (NChordChip.ChipSize + ChipGap) - NChordChip.ChipSize * 0.5f,
                rowTopY);
            MoveChip(chip, target, animate);
            chip.SetSelected(i == selectedIndex);
        }
    }

    private void MoveChip(NChordChip chip, Vector2 target, bool animate)
    {
        if (!animate || _chipLayoutTween == null)
        {
            chip.Position = target;
            return;
        }

        _chipLayoutTween.TweenProperty(chip, "position", target, 0.22f);
    }

    // ── 卡牌行（与和弦行同一套「整行平移、选中项对齐中轴」语义） ─────────────────────

    /// <summary>重建卡牌 holder：所有牌常驻行内，选中只改位置，不增删。</summary>
    private void RebuildCardRow()
    {
        foreach (var holder in _holders.Values.ToList())
        {
            holder.QueueFreeSafely();
        }

        _holders.Clear();
        for (var index = 0; index < _cards.Count; index++)
        {
            var card = _cards[index];
            var cardNode = NCard.Create(card, ModelVisibility.Visible);
            if (cardNode == null)
            {
                continue;
            }

            var holder = NGridCardHolder.Create(cardNode);
            if (holder == null)
            {
                cardNode.QueueFreeSafelyNoPool();
                continue;
            }

            // NCard 是对象池节点：上一次面板会话里残留的自绘角标要清掉，否则复用节点时会带上旧和弦
            RemoveStaleChordBadges(cardNode);

            holder.Connect(NCardHolder.SignalName.Pressed, Callable.From<NCardHolder>(OnHolderPressed));
            holder.Visible = true;
            holder.MouseFilter = MouseFilterEnum.Pass;
            holder.Scale = holder.SmallScale;
            holder.FocusMode = FocusModeEnum.All;
            _cardLayer.AddChildSafely(holder);
            cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            holder.FocusEntered += () => OnHolderFocused(index);
            _holders[index] = holder;
        }

        LayoutCardRow(animate: false);
    }

    /// <summary>holder 原点是卡牌中心；行内以卡牌格为单位排布，整体随选中项左右平移。</summary>
    private Vector2 HolderTargetPosition(int index)
    {
        // 目标位置先按「卡牌带的视觉像素」算，再换算回缩放后的 CardLayer 坐标系
        var visual = new Vector2(
            _band.Size.X * 0.5f + (index - _selectedCardIndex) * CardStep,
            _band.Size.Y * 0.5f);
        return visual / CardLayerScale;
    }

    /// <summary>整行卡牌水平平移：选中的那张落在中央箭头正下方，其余按原顺序左右让位。</summary>
    private void LayoutCardRow(bool animate)
    {
        _cardLayoutTween?.Kill();
        _cardLayoutTween = null;
        if (animate)
        {
            _cardLayoutTween = CreateTween();
            _cardLayoutTween.SetParallel(true);
            _cardLayoutTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }

        foreach (var (index, holder) in _holders)
        {
            var target = HolderTargetPosition(index);
            if (!animate || _cardLayoutTween == null)
            {
                holder.Position = target;
                continue;
            }

            _cardLayoutTween.TweenProperty(holder, "position", target, 0.22f);
        }

        UpdateScrollbar();
    }

    private void UpdateScrollbar()
    {
        var count = _cards.Count;
        if (count == 0)
        {
            _scrollbar.SetState(0f, 1f);
            return;
        }

        var ratio = count == 1 ? 0f : (float)_selectedCardIndex / (count - 1);
        _scrollbar.SetState(ratio, 1f / count);
    }

    private void OnScrollbarScrolled(float ratio)
    {
        if (_cards.Count == 0)
        {
            return;
        }

        var index = _cards.Count == 1 ? 0 : Mathf.RoundToInt(ratio * (_cards.Count - 1));
        // 拖条也走补间，避免滑块换格时整行瞬移
        SelectCard(index, animate: true);
    }

    private void OnHolderPressed(NCardHolder holder)
    {
        if (_finished)
        {
            return;
        }

        var pair = _holders.FirstOrDefault(entry => entry.Value == holder);
        if (pair.Value == null)
        {
            return;
        }

        SelectCard(pair.Key, animate: true);
    }

    private void OnHolderFocused(int index)
    {
        if (_finished)
        {
            return;
        }

        SelectCard(index, animate: true);
    }

    /// <summary>选中一张卡：整行平移让它对齐到中轴（与和弦行同样的对齐式选择）。</summary>
    private void SelectCard(int index, bool animate)
    {
        if (_finished || index < 0 || index >= _cards.Count || index == _selectedCardIndex)
        {
            return;
        }

        _selectedCardIndex = index;
        LayoutCardRow(animate);
        RefreshResult();
        RefreshConfirmState();
    }

    private void StepCard(int delta)
    {
        if (_cards.Count == 0)
        {
            return;
        }

        var next = Mathf.Clamp(_selectedCardIndex + delta, 0, _cards.Count - 1);
        if (next != _selectedCardIndex)
        {
            SelectCard(next, animate: true);
        }
    }

    // ── 结果展示 ────────────────────────────────────────────────────────────────────

    private void RefreshResult()
    {
        if (_resultCard != null)
        {
            // 预览角标是预览卡的子节点：回池前先摘掉，否则池化复用时会带着旧角标跑到别的界面上
            RemoveStaleChordBadges(_resultCard);
            _resultCard.QueueFreeSafely();
            _resultCard = null;
        }

        if (_selectedCardIndex < 0 || _selectedCardIndex >= _cards.Count)
        {
            _boardChordLabel.Visible = false;
            _hintLabel.Visible = true;
            return;
        }

        var card = _cards[_selectedCardIndex];
        var currentChord = ChordOf(card);
        var finalChord = FinalChord(card, currentChord);

        _hintLabel.Visible = false;
        _boardChordLabel.Visible = true;
        _boardChordLabel.SetTextAutoSize(
            $"{Loc("CHORD_DESIGN_CURRENT")}：{FormatChord(currentChord)}    " +
            $"{Loc("CHORD_DESIGN_RESULT")}：{FormatChord(finalChord)}");

        var cardNode = NCard.Create(card, ModelVisibility.Visible);
        if (cardNode == null)
        {
            return;
        }

        _resultCard = cardNode;
        _resultCardHost.AddChildSafely(cardNode);
        cardNode.Scale = Vector2.One * ResultCardScale;
        // NCard 的视觉内容按原点居中（CardContainer 用中心锚点），所以原点放在 host 中心即居中
        cardNode.Position = _resultCardHost.Size * 0.5f;
        cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);

        // 预览要显示「结果和弦」而不是卡牌当前和弦：隐掉卡面自带的能力角标，
        // 再挂一枚按显式和弦构建的角标——挂在卡面 %OverlayContainer 下，继承卡的缩放/变换，
        // 因此和真实卡牌一样会被标题横幅压住一角（角标只给背景着色，不抢前景）。
        // 只动这张预览卡节点，卡牌模型与卡组数据完全不变。
        RemoveStaleChordBadges(cardNode);
        var overlay = cardNode.GetNodeOrNull<Control>("%OverlayContainer");
        overlay?.GetNodeOrNull<Control>("RitsuLibCapabilityOverlays")?.Hide();

        if (finalChord != PerformChord.None && overlay != null)
        {
            overlay.AddChildSafely(NChordCornerBadge.Create(finalChord));
        }

        cardNode.Modulate = new Color(1f, 1f, 1f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(cardNode, "modulate:a", 1f, 0.12f);
    }

    private PerformChord FinalChord(CardModel card, PerformChord currentChord)
    {
        if (_selectedChord == PerformChord.None)
        {
            return currentChord;
        }

        return _additive ? currentChord | _selectedChord : _selectedChord;
    }

    private static PerformChord ChordOf(CardModel card)
    {
        return card.TryGetCapability<ChordCapability>(out var capability)
            ? capability.Chord
            : PerformChord.None;
    }

    private static string FormatChord(PerformChord chord)
    {
        var groups = chord.EnumerateGroups().ToList();
        return groups.Count == 0 ? Loc("CHORD_DESIGN_NONE") : string.Join("、", groups.Select(group => group.ToString()));
    }

    /// <summary>
    /// 清掉池化卡牌节点上残留的自绘角标（直挂 <c>%OverlayContainer</c> 的 <see cref="NChordCornerBadge" />）。
    /// 能力角标在 <c>RitsuLibCapabilityOverlays</c> 容器里、由 RitsuLib 每次重建，不在这里处理。
    /// </summary>
    private static void RemoveStaleChordBadges(NCard cardNode)
    {
        if (cardNode.GetNodeOrNull<Control>("%OverlayContainer") is not { } overlay)
        {
            return;
        }

        foreach (var child in overlay.GetChildren())
        {
            if (child is NChordCornerBadge)
            {
                child.QueueFreeSafely();
            }
        }
    }

    private void RefreshConfirmState()
    {
        var ready = _selectedCardIndex >= 0 && _selectedCardIndex < _cards.Count
                                                  && _selectedChord != PerformChord.None;
        if (ready)
        {
            _confirmButton?.Enable();
        }
        else
        {
            _confirmButton?.Disable();
        }
    }

    // ── 结束 ────────────────────────────────────────────────────────────────────────

    private void Confirm()
    {
        if (_finished || _selectedCardIndex < 0 || _selectedChord == PerformChord.None)
        {
            return;
        }

        var card = _cards[_selectedCardIndex];
        Finish(new ChordDesignResult(
            _selectedCardIndex,
            FinalChord(card, ChordOf(card)),
            _additive));
    }

    private void Cancel()
    {
        Finish(null);
    }

    private void Finish(ChordDesignResult? result)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Result = result;
        _completion.TrySetResult(result);
        NOverlayStack.Instance?.Remove(this);
    }

    // ── 小工具 ──────────────────────────────────────────────────────────────────────

    private void GrabInitialFocus()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var target = DefaultFocusedControl;
        target?.GrabFocus();
    }

    private bool IsPointerOverBand()
    {
        if (!IsInstanceValid(_band))
        {
            return false;
        }

        return _band.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
    }

    private static string Loc(string key) => new LocString(LocTable, key).GetFormattedText();

}
