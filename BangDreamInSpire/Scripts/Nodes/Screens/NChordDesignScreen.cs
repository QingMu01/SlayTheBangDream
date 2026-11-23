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

/// <summary>面板结果；Additive 表示与现有和弦取并集，否则覆盖。</summary>
public readonly record struct ChordDesignResult(int CardIndex, PerformChord Chord, bool Additive);

/// <summary>休息处和弦设计面板，支持对齐选择、横向滚动与结果预览。
/// 仅返回结果，联机同步由 ChordDesignOption 负责。</summary>
public sealed partial class NChordDesignScreen : Control, IOverlayScreen
{
    public const string LocTable = "rest_site_ui";

    public const string ScenePath = "res://BangDreamInSpire/scenes/screens/chord_design_screen.tscn";

    private static readonly Vector2 DesignSize = new(1920f, 1080f);

    private const float ChipGap = 24f;
    private const float CardStep = 234f; // 216 宽 + 18 间距
    private const float CardLayerScale = 0.9f; // 行内容额外缩放
    private const float ResultCardScale = 0.52f;

    private static readonly Color GoldColor = new("#d8bd7a");
    private static readonly Color MutedTextColor = new("#9aa3b5");
    private static readonly Font? TitleFont =
        ResourceLoader.Load<Font>("res://themes/fonts/zhs/source_han_serif_sc_bold_shared.tres");
    private static readonly Font? BodyFont =
        ResourceLoader.Load<Font>("res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres");

    private readonly List<CardModel> _cards = new();
    // 卡牌节点常驻，选择只移动整行。
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

    /// <summary>是否允许切换增加或覆盖模式。</summary>
    public static bool ModeToggleUnlocked { get; set; }

    public ChordDesignResult? Result { get; private set; }


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

    /// <summary>打开面板，场景缺失返回 null。</summary>
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

    /// <summary>等待确定或取消。</summary>
    public Task<ChordDesignResult?> SelectionFinished() => _completion.Task;


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
        else
        {
            BangDreamInSpireCore.Logger.Warn("NChordDesignScreen: %BackButton missing; cancel hotkey only.");
        }

        if (_confirmButton != null)
        {
            _confirmButton.Connect(NClickableControl.SignalName.Released,
                Callable.From<NClickableControl>(_ => Confirm()));
        }
        else
        {
            BangDreamInSpireCore.Logger.Warn("NChordDesignScreen: %ConfirmButton missing; confirm hotkey only.");
        }

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

    /// <summary>重新摆放和弦筹码。</summary>
    /// <summary>移动整行使选中筹码对齐中轴。</summary>
    private void LayoutChips(bool animate)
    {
        const float rowTopY = 0f;
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


    /// <summary>重建常驻卡牌行。</summary>
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

            // 清理对象池节点残留的旧书签。
            RemoveStaleChordBookmarks(cardNode);

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

    /// <summary>以卡牌中心为原点按格排列。</summary>
    private Vector2 HolderTargetPosition(int index)
    {
        // 将视觉像素换算到 CardLayer 坐标。
        var visual = new Vector2(
            _band.Size.X * 0.5f + (index - _selectedCardIndex) * CardStep,
            _band.Size.Y * 0.5f);
        return visual / CardLayerScale;
    }

    /// <summary>移动整行使选中卡牌对齐中轴。</summary>
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
        // 滚动条与整行同步补间。
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

    /// <summary>选中卡牌并对齐中轴。</summary>
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


    private void RefreshResult()
    {
        if (_resultCard != null)
        {
            // 回池前移除预览书签。
            RemoveStaleChordBookmarks(_resultCard);
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
        cardNode.Position = _resultCardHost.Size * 0.5f;
        cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);

        // 用显式和弦书签替换卡面能力书签；仅修改预览节点。
        RemoveStaleChordBookmarks(cardNode);
        var overlay = cardNode.GetNodeOrNull<Control>("%OverlayContainer");
        overlay?.GetNodeOrNull<Control>("RitsuLibCapabilityOverlays")?.Hide();

        if (finalChord != PerformChord.None && overlay != null)
        {
            overlay.AddChildSafely(NChordBookmark.Create(finalChord));
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

    /// <summary>移除池化卡牌上的自绘书签，保留 RitsuLib 覆盖层。</summary>
    private static void RemoveStaleChordBookmarks(NCard cardNode)
    {
        if (cardNode.GetNodeOrNull<Control>("%OverlayContainer") is not { } overlay)
        {
            return;
        }

        foreach (var child in overlay.GetChildren())
        {
            if (child is NChordBookmark)
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
        // 先移除覆盖层，再完成等待续体。
        NOverlayStack.Instance?.Remove(this);
        _completion.TrySetResult(result);
    }


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
