using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes.MegeScript;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib;
using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamInSpire.Scripts.Nodes;

/// <summary>展示角色皮肤、初始卡组与遗物。</summary>
public partial class BangDreamCharacterDetailsPopup : Control
{
    [Signal]
    public delegate void SkinChangedEventHandler(string skinPath);

    private static readonly Color TextPrimary = new(0.086f, 0.125f, 0.18f);
    private static readonly Color TextMuted = new(0.42f, 0.46f, 0.52f);
    private static readonly Color ScrimColor = new(0.03f, 0.05f, 0.09f, 0.34f);

    private static readonly FontVariation? NotoBold =
        ResourceLoader.Load<FontVariation>("res://themes/fonts/jpn/noto_sans_cjkjp_bold_shared.tres");

    private static readonly FontVariation? NotoMedium =
        ResourceLoader.Load<FontVariation>("res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres");

    // 标题字体与正文区分。
    private static readonly FontVariation? TitleFont =
        ResourceLoader.Load<FontVariation>("res://themes/fonts/zhs/source_han_serif_sc_bold_shared.tres");

    private const float PanelOpenScale = 0.96f;

    // 预览使用 NCard，按单元格缩放，悬停放回标准比例。
    private static readonly Vector2 CardCellBaseSize = new(110f, 155f);
    private const float CardGap = 12f;
    private const int MaxCardColumns = 6;
    private const float MinCardWidth = 96f;
    // 限制卡牌区高度，保留遗物区。
    private const float CardAreaMaxHeight = 358f;

    // 面板高度取左右栏内容最大值并居中。
    private const float PanelContentTop = 112f;
    private const float SectionLabelHeight = 24f;
    private const float SectionGap = 8f;
    private const float PanelBottomPadding = 36f;
    private const float SkinColumnTop = 166f;
    private const float SkinColumnFallbackHeight = 280f;
    private const float PanelMinHeight = 420f;
    private const float PanelMaxHeight = 720f;
    private const float EmptyHintHeight = 20f;
    private const float RelicTileHeight = 72f;
    private const float CardHoverDuration = 0.12f;
    private const int CardHoverZIndex = 80;
    private const string HoverPadName = "HoverPad";
    private const string ExtraBadgeName = "ExtraBadge";
    private const string CountBadgeName = "CountBadge";

    private ColorRect? _scrim;
    private Panel? _panel;
    private ColorRect? _topAccent;
    private ColorRect? _divider;
    private TextureRect? _emblem;
    private Label? _title;
    private Label? _roman;
    private Button? _closeButton;
    private Label? _skinLabel;
    private Color _accentColor = Colors.White;
    private Label? _cardsLabel;
    private Label? _relicsLabel;
    private BangDreamSkinSelector? _skinSelector;
    private VBoxContainer? _cardsRow;
    private VBoxContainer? _relicsBox;

    // 重建时计算卡牌布局。
    private readonly List<Control> _cardCells = new();
    private Vector2 _cardCellSize = CardCellBaseSize;
    private Vector2 _cardPreviewScale = Vector2.One * (CardCellBaseSize.X / NCard.defaultSize.X);

    // 记录两栏内容高度。
    private float _cardsAreaHeight = CardCellBaseSize.Y;
    private float _relicsAreaHeight = RelicTileHeight;

    private Tween? _tween;
    private readonly Dictionary<Control, Tween> _cardHoverTweens = new();
    private CharacterModel? _character;
    private bool _suppressSkinRefresh;

    public bool IsOpen => Visible;

    public override void _EnterTree()
    {
        BangDreamTools.ConfigureMegaText(GetNodeOrNull<Control>("%ClassLabel"), maxFontSize: 15, minFontSize: 9);
    }

    public override void _Ready()
    {
        _scrim = GetNode<ColorRect>("%Scrim");
        _panel = GetNode<Panel>("%Panel");
        _topAccent = GetNode<ColorRect>("%TopAccent");
        _divider = GetNode<ColorRect>("%Divider");
        _emblem = GetNode<TextureRect>("%Emblem");
        _title = GetNode<Label>("%Title");
        _roman = GetNode<Label>("%Roman");
        _closeButton = GetNode<Button>("%CloseButton");
        _skinLabel = GetNode<Label>("%SkinLabel");
        _cardsLabel = GetNode<Label>("%CardsLabel");
        _relicsLabel = GetNode<Label>("%RelicsLabel");
        _skinSelector = GetNode<BangDreamSkinSelector>("%SkinSelector");
        _cardsRow = GetNode<VBoxContainer>("%CardsRow");
        _relicsBox = GetNode<VBoxContainer>("%RelicsBox");

        _scrim.GuiInput += OnScrimGuiInput;
        _closeButton.Pressed += Close;
        _skinSelector.Connect(BangDreamSkinSelector.SignalName.SkinSelected,
            Callable.From((Action<string>)OnSkinSelected));

        Visible = false;
        _panel.Modulate = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>刷新角色数据，支持关闭状态下更新。</summary>
    public void SetCharacter(CharacterModel character, StartRunLobby? lobby)
    {
        _character = character;

        var group = character is IGroupableCharacter groupable ? groupable.Group : (CharacterGroup?)null;
        var bandColor = group?.GetGroupInfo().Color ?? Colors.White;
        var accentColor = bandColor.ToAccentColor();
        var memberColor = character.NameColor;

        ApplyPalette(accentColor, memberColor);

        if (_title != null)
        {
            _title.Text = new LocString("characters", character.CharacterSelectTitle).GetFormattedText();
        }

        if (character is IBangDreamMateData mateData)
        {
            if (_roman != null)
            {
                _roman.Text = mateData.MemberNameRoman.ToUpperInvariant();
            }
        }

        ApplyEmblem(character);
        AlignHeader();

        var skinSupportCharacter = character as ISkinSupportCharacter;
        var hasSkins = skinSupportCharacter != null;

        if (_skinSelector != null)
        {
            _skinSelector.Visible = hasSkins;
        }

        if (_skinLabel != null)
        {
            _skinLabel.Visible = hasSkins;
        }

        if (hasSkins && _skinSelector != null)
        {
            _suppressSkinRefresh = true;
            _skinSelector.Init(skinSupportCharacter!, lobby);
            _suppressSkinRefresh = false;
        }

        RefreshContent();
    }

    /// <summary>优先使用角色图标，缺省回退乐队 Logo。</summary>
    private void ApplyEmblem(CharacterModel character)
    {
        if (_emblem == null)
        {
            return;
        }

        ClearChildren(_emblem);
        _emblem.Texture = null;

        var iconPath = (character as IModCharacterAssetOverrides)?.CustomIconPath;
        if (!string.IsNullOrWhiteSpace(iconPath) && ResourceLoader.Exists(iconPath)
                                                 && ResourceLoader.Load<PackedScene>(iconPath) is { } iconScene)
        {
            _emblem.AddChild(iconScene.Instantiate());
            _emblem.Modulate = Colors.White;
            _emblem.Visible = true;
            return;
        }

        if (character is IBangDreamMateData mateData)
        {
            _emblem.Texture = BangDreamCharacterSelector.LoadTexture(mateData.SelectLogo);
        }

        _emblem.Visible = _emblem.Texture != null;
    }

    /// <summary>对齐标题并按班级文字调整胶囊宽度。</summary>
    private void AlignHeader()
    {
        if (_title == null)
        {
            return;
        }

        if (_roman != null)
        {
            _roman.Position = new Vector2(_title.Position.X, _roman.Position.Y);
        }
    }

    public void Open()
    {
        if (Visible)
        {
            return;
        }

        Visible = true;
        _tween?.Kill();

        if (_scrim != null)
        {
            _scrim.Color = new Color(ScrimColor.R, ScrimColor.G, ScrimColor.B, 0f);
        }

        if (_panel != null)
        {
            _panel.PivotOffset = _panel.Size * 0.5f;
            _panel.Scale = new Vector2(PanelOpenScale, PanelOpenScale);
            _panel.Modulate = new Color(1f, 1f, 1f, 0f);
        }

        _tween = CreateTween();
        _tween.SetParallel()
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        if (_scrim != null)
        {
            _tween.TweenProperty(_scrim, "color:a", ScrimColor.A, 0.18f);
        }

        if (_panel != null)
        {
            _tween.TweenProperty(_panel, "modulate:a", 1f, 0.18f);
            _tween.TweenProperty(_panel, "scale", Vector2.One, 0.22f);
        }
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        _tween?.Kill();
        ResetCardHover();
        if (_panel != null)
        {
            _panel.PivotOffset = _panel.Size * 0.5f;
        }

        _tween = CreateTween();
        _tween.SetParallel()
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        if (_scrim != null)
        {
            _tween.TweenProperty(_scrim, "color:a", 0f, 0.14f);
        }

        if (_panel != null)
        {
            _tween.TweenProperty(_panel, "modulate:a", 0f, 0.14f);
            _tween.TweenProperty(_panel, "scale", new Vector2(0.97f, 0.97f), 0.14f);
        }

        _tween.Chain().TweenCallback(Callable.From(() => Visible = false));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed("ui_cancel"))
        {
            return;
        }

        Close();
        GetViewport().SetInputAsHandled();
    }

    private void OnScrimGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnSkinSelected(string skinPath)
    {
        if (_suppressSkinRefresh)
        {
            return;
        }

        EmitSignal(SignalName.SkinChanged, skinPath);
        RefreshContent();
    }

    private void ApplyPalette(Color accentColor, Color memberColor)
    {
        _accentColor = accentColor;

        if (_topAccent != null)
        {
            _topAccent.Color = accentColor;
        }

        if (_divider != null)
        {
            _divider.Color = new Color(accentColor.R, accentColor.G, accentColor.B, 0.35f);
        }


        foreach (var label in new[] { _skinLabel, _cardsLabel, _relicsLabel })
        {
            label?.AddThemeColorOverride("font_color", accentColor);
        }

        if (_emblem != null)
        {
            _emblem.Modulate = new Color(memberColor.R, memberColor.G, memberColor.B, 0.95f);
        }
    }

    private void RefreshContent()
    {
        if (_character == null)
        {
            return;
        }

        var skinPath = _skinSelector?.CurrentSkinPath;
        var skinInfo = string.IsNullOrWhiteSpace(skinPath) ? null : SkinManager.GetSkinInfo(skinPath);

        var deck = skinInfo != null
            ? skinInfo.GetStartingDeck().ToList()
            : _character.StartingDeck.ToList();
        IEnumerable<RelicModel> relicSource = skinInfo != null
            ? skinInfo.GetStartingRelics()
            : _character.StartingRelics;
        // 隐藏遗物不展示。
        var relics = relicSource.Where(RitsuLibFramework.IsRelicVisible).ToList();
        var extraDeck = skinInfo?.GetStartingExtraDeck().ToList() ?? [];

        RebuildCards(deck, extraDeck);
        RebuildRelics(relics);
        FitPanelHeight();
    }

    /// <summary>按左右栏内容调整高度并居中。</summary>
    private void FitPanelHeight()
    {
        if (_panel == null)
        {
            return;
        }

        var cardsContentBottom = PanelContentTop + SectionLabelHeight + SectionGap + _cardsAreaHeight;
        var relicsContentBottom = cardsContentBottom + SectionGap + SectionLabelHeight + SectionGap + _relicsAreaHeight;

        // 左栏高度取皮肤选择器最小高度。
        var skinSelectorHeight = _skinSelector?.GetChildOrNull<Control>(0)?.GetCombinedMinimumSize().Y
            ?? SkinColumnFallbackHeight;
        var skinContentBottom = SkinColumnTop + skinSelectorHeight;

        var height = Mathf.Clamp(
            Mathf.Max(relicsContentBottom, skinContentBottom) + PanelBottomPadding,
            PanelMinHeight,
            PanelMaxHeight);

        var parentHeight = (_panel.GetParent() as Control)?.Size.Y ?? 0f;
        var top = parentHeight > 0f ? Mathf.Max(8f, (parentHeight - height) * 0.5f) : _panel.Position.Y;
        _panel.Position = new Vector2(_panel.Position.X, top);
        _panel.Size = new Vector2(_panel.Size.X, height);

        // 面板尺寸同步给纸纹着色器。
        if (_panel.GetNodeOrNull<ColorRect>("%PaperGrain")?.Material is ShaderMaterial paperMaterial)
        {
            paperMaterial.SetShaderParameter("area_size", _panel.Size);
        }
    }

    private readonly record struct CardEntry(CardModel Card, int Count, bool IsExtra);

    /// <summary>合并初始与额外卡组，按 ID 与升级状态去重。</summary>
    private void RebuildCards(IReadOnlyList<CardModel> deck, IReadOnlyList<CardModel> extraDeck)
    {
        if (_cardsRow == null)
        {
            return;
        }

        ClearCardGrid();

        var entries = MergeCards(deck, extraDeck);
        if (entries.Count == 0)
        {
            var emptyRow = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            emptyRow.AddChild(BuildEmptyHint("该皮肤没有配置初始卡组"));
            _cardsRow.AddChild(emptyRow);
            _cardsAreaHeight = EmptyHintHeight;
            return;
        }

        var (columns, rows) = ApplyCardLayout(entries.Count);
        _cardsAreaHeight = rows * _cardCellSize.Y + (rows - 1) * CardGap;

        var index = 0;
        while (index < entries.Count)
        {
            var row = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddThemeConstantOverride("separation", (int)CardGap);

            var countInRow = Mathf.Min(columns, entries.Count - index);
            for (var i = 0; i < countInRow; i++)
            {
                var cell = BuildCardCell(entries[index++]);
                row.AddChild(cell);
                _cardCells.Add(cell);
            }

            _cardsRow.AddChild(row);
        }
    }

    /// <summary>按卡牌数与空间计算行列和缩放，末行居中。</summary>
    private (int Columns, int Rows) ApplyCardLayout(int count)
    {
        var columnWidth = _cardsRow?.GetParent<Control>()?.Size.X ?? 668f;
        var area = new Vector2(columnWidth, CardAreaMaxHeight);
        var maxColumns = Mathf.Clamp(
            Mathf.FloorToInt((area.X + CardGap) / (MinCardWidth + CardGap)),
            1,
            MaxCardColumns);
        maxColumns = Mathf.Min(maxColumns, count);

        var rows = Mathf.CeilToInt(count / (float)maxColumns);
        var columns = Mathf.Clamp(Mathf.CeilToInt(count / (float)rows), 1, maxColumns);

        var fitWidth = (area.X - (columns - 1) * CardGap) / (columns * CardCellBaseSize.X);
        var fitHeight = (area.Y - (rows - 1) * CardGap) / (rows * CardCellBaseSize.Y);
        var scale = Mathf.Max(0.1f, Mathf.Min(1f, Mathf.Min(fitWidth, fitHeight)));

        _cardCellSize = CardCellBaseSize * scale;
        _cardPreviewScale = Vector2.One * (_cardCellSize.X / NCard.defaultSize.X);
        return (columns, rows);
    }

    private static List<CardEntry> MergeCards(IReadOnlyList<CardModel> deck, IReadOnlyList<CardModel> extraDeck)
    {
        var entries = new List<CardEntry>();
        var indexByKey = new Dictionary<(string Id, bool Upgraded), int>();

        void Add(CardModel card, bool isExtra)
        {
            var key = (card.Id.Entry, card.IsUpgraded);
            if (indexByKey.TryGetValue(key, out var index))
            {
                var existing = entries[index];
                entries[index] = existing with
                {
                    Count = existing.Count + 1,
                    IsExtra = existing.IsExtra || isExtra
                };
                return;
            }

            indexByKey[key] = entries.Count;
            entries.Add(new CardEntry(card, 1, isExtra));
        }

        foreach (var card in deck)
        {
            Add(card, false);
        }

        foreach (var card in extraDeck)
        {
            Add(card, true);
        }

        return entries;
    }

    private Control BuildCardCell(CardEntry entry)
    {
        var cell = new Control
        {
            CustomMinimumSize = _cardCellSize,
            MouseFilter = MouseFilterEnum.Stop
        };

        var cardNode = NCard.Create(entry.Card);
        if (cardNode != null)
        {
            cardNode.MouseFilter = MouseFilterEnum.Ignore;
            cardNode.Scale = _cardPreviewScale;
            cell.AddChild(cardNode);

            // 透明命中垫避免悬停放大时来回闪烁。
            var hoverPad = new Control
            {
                Name = HoverPadName,
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false
            };
            cell.AddChild(hoverPad);

            // 布局后将卡牌对齐单元格中心。
            cell.Resized += () => cardNode.Position = cell.Size * 0.5f;
            cell.MouseEntered += () => SetCardHovered(cell, cardNode, hoverPad, true);
            hoverPad.MouseExited += () => SetCardHovered(cell, cardNode, hoverPad, false);
            // 节点未就绪时需显式刷新卡牌内容。
            Callable.From(() =>
            {
                try
                {
                    cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                    ApplyMusicCardFrame(cardNode, entry.Card);
                }
                catch (Exception exception)
                {
                    GD.PushWarning($"刷新卡片预览失败：{entry.Card.Id.Entry} - {exception.Message}");
                }
            }).CallDeferred();
        }
        else
        {
            // 节点池不可用时使用手绘小卡。
            cell.AddChild(BuildMiniCard(entry.Card));
        }

        if (entry.IsExtra)
        {
            cell.AddChild(BuildExtraBadge());
        }

        if (entry.Count > 1)
        {
            cell.AddChild(BuildCountBadge(entry.Count));
        }

        return cell;
    }

    private void SetCardHovered(Control cell, NCard cardNode, Control hoverPad, bool hovered)
    {
        cell.ZIndex = hovered ? CardHoverZIndex : 0;
        // 悬停时提升整行层级，避免下排遮挡放大卡。
        if (cell.GetParent() is Control row)
        {
            row.ZIndex = hovered ? 1 : 0;
        }
        hoverPad.Visible = hovered;
        foreach (var child in cell.GetChildren())
        {
            if (child is Control badge
                && (badge.Name == ExtraBadgeName || badge.Name == CountBadgeName))
            {
                badge.Visible = !hovered;
            }
        }

        if (hovered)
        {
            hoverPad.Position = cell.Size * 0.5f - NCard.defaultSize * 0.5f;
            hoverPad.Size = NCard.defaultSize;
        }

        _cardHoverTweens.TryGetValue(cell, out var previousTween);
        previousTween?.Kill();
        var hoverTween = cardNode.CreateTween();
        hoverTween
            .TweenProperty(cardNode, "scale", hovered ? Vector2.One : _cardPreviewScale, CardHoverDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _cardHoverTweens[cell] = hoverTween;
    }

    private Control BuildExtraBadge()
    {
        var badge = CreateBadge(_accentColor, lightText: true);
        badge.Name = ExtraBadgeName;
        badge.SetAnchorsPreset(LayoutPreset.TopRight);
        badge.OffsetLeft = -52f;
        badge.OffsetTop = -6f;
        badge.OffsetRight = 3f;
        badge.OffsetBottom = 18f;

        var label = CreateBadgeLabel("EXTRA", 10);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.55f));
        label.AddThemeConstantOverride("outline_size", 3);
        badge.AddChild(label);
        return badge;
    }

    private Control BuildCountBadge(int count)
    {
        var badge = CreateBadge(new Color(0.09f, 0.13f, 0.19f), lightText: true);
        badge.Name = CountBadgeName;
        badge.SetAnchorsPreset(LayoutPreset.BottomRight);
        badge.OffsetLeft = -40f;
        badge.OffsetTop = -27f;
        badge.OffsetRight = 3f;
        badge.OffsetBottom = -3f;

        var label = CreateBadgeLabel($"×{count}", 13);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.55f));
        label.AddThemeConstantOverride("outline_size", 3);
        badge.AddChild(label);
        return badge;
    }

    private static Panel CreateBadge(Color background, bool lightText)
    {
        var badge = new Panel
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(background.R, background.G, background.B, lightText ? 0.96f : 0.95f),
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomRight = 7,
            CornerRadiusBottomLeft = 7,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = lightText ? new Color(1f, 1f, 1f, 0.5f) : new Color(0f, 0f, 0f, 0.2f),
            AntiAliasing = true,
            ShadowColor = new Color(0f, 0f, 0f, 0.4f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(0f, 2f)
        });
        return badge;
    }

    private static Label CreateBadgeLabel(string text, int fontSize)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.AddThemeFontOverride("font", NotoBold);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        return label;
    }

    /// <summary>按当前皮肤替换音乐卡框，未配置时使用默认卡框。</summary>
    private void ApplyMusicCardFrame(NCard cardNode, CardModel card)
    {
        if (!BangDreamConst.IsMusicCardType(card.Type))
        {
            return;
        }

        var skinPath = _skinSelector?.CurrentSkinPath;
        var framePath = string.IsNullOrWhiteSpace(skinPath)
            ? null
            : SkinManager.GetSkinInfo(skinPath)?.SkinTemplate.Ui.MusicCardFrame;
        if (string.IsNullOrWhiteSpace(framePath) || !ResourceLoader.Exists(framePath))
        {
            return;
        }

        if (cardNode.GetNodeOrNull<TextureRect>("%Frame") is not { } frame)
        {
            return;
        }

        frame.Texture = BangDreamPreloadManager.GetTexture2D(framePath);
        frame.Material = null;
    }

    /// <summary>先将池化卡牌归还 NodePool，再移除布局。</summary>
    private void ClearCardGrid()
    {
        foreach (var tween in _cardHoverTweens.Values)
        {
            tween.Kill();
        }

        _cardHoverTweens.Clear();

        if (_cardsRow == null)
        {
            return;
        }

        foreach (var cell in _cardCells)
        {
            if (!GodotObject.IsInstanceValid(cell))
            {
                continue;
            }

            foreach (var inner in cell.GetChildren())
            {
                if (inner is NCard cardNode)
                {
                    cardNode.QueueFreeSafely();
                }
            }
        }

        _cardCells.Clear();

        foreach (var row in _cardsRow.GetChildren())
        {
            _cardsRow.RemoveChild(row);
            row.QueueFreeSafely();
        }
    }

    /// <summary>关闭时复位悬停，避免残留放大卡与命中垫。</summary>
    private void ResetCardHover()
    {
        foreach (var tween in _cardHoverTweens.Values)
        {
            tween.Kill();
        }

        _cardHoverTweens.Clear();

        foreach (var cell in _cardCells)
        {
            if (!GodotObject.IsInstanceValid(cell))
            {
                continue;
            }

            cell.ZIndex = 0;

            if (cell.GetParent() is Control row)
            {
                row.ZIndex = 0;
            }

            foreach (var inner in cell.GetChildren())
            {
                if (inner.Name == HoverPadName && inner is Control hoverPad)
                {
                    hoverPad.Visible = false;
                }
                else if (inner is NCard cardNode && GodotObject.IsInstanceValid(cardNode))
                {
                    cardNode.Scale = _cardPreviewScale;
                }
            }
        }
    }

    private void RebuildRelics(IReadOnlyList<RelicModel> relics)
    {
        if (_relicsBox == null)
        {
            return;
        }

        ClearChildren(_relicsBox);

        if (relics.Count == 0)
        {
            _relicsBox.AddChild(BuildEmptyHint("该皮肤没有配置初始遗物"));
            _relicsAreaHeight = EmptyHintHeight;
            return;
        }

        // 少量遗物横向排列，更多时纵向列表。
        if (relics.Count <= 2)
        {
            var row = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddThemeConstantOverride("separation", 12);

            foreach (var relic in relics)
            {
                var tile = BuildRelicTile(relic);
                tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(tile);
            }

            _relicsBox.AddChild(row);
            _relicsAreaHeight = RelicTileHeight;
            return;
        }

        foreach (var relic in relics)
        {
            _relicsBox.AddChild(BuildRelicRow(relic));
        }

        _relicsAreaHeight = relics.Count * 56f + (relics.Count - 1) * 10f;
    }

    /// <summary>创建横向遗物条目。</summary>
    private Control BuildRelicTile(RelicModel relic)
    {
        var tile = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 64f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        tile.AddThemeConstantOverride("separation", 10);

        var icon = new TextureRect
        {
            Texture = relic.Icon,
            CustomMinimumSize = new Vector2(48f, 48f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            MouseFilter = MouseFilterEnum.Ignore
        };
        tile.AddChild(icon);

        var textBox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textBox.AddThemeConstantOverride("separation", 2);

        var title = new Label
        {
            Text = relic.Title.GetFormattedText(),
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontOverride("font", TitleFont);
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", TextPrimary);
        textBox.AddChild(title);

        var description = new BangDreamRichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            AutoSizeEnabled = false,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            MaxFontSize = 12,
            MinFontSize = 10
        };
        description.AddThemeFontOverride("normal_font", NotoMedium);
        description.AddThemeFontSizeOverride("normal_font_size", 12);
        description.AddThemeColorOverride("default_color", TextMuted);
        description.Text = relic.DynamicDescription.GetFormattedText();
        textBox.AddChild(description);

        tile.AddChild(textBox);
        return tile;
    }

    private Control BuildMiniCard(CardModel card)
    {
        const float width = 100f;
        const float height = 134f;

        var typeColor = GetTypeColor(card.Type);
        var frame = new Panel
        {
            CustomMinimumSize = new Vector2(width, height),
            MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.99f, 0.984f, 0.969f),
            BorderColor = typeColor,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6,
            ShadowColor = new Color(0.05f, 0.08f, 0.12f, 0.16f),
            ShadowSize = 6,
            ShadowOffset = new Vector2(0f, 2f),
            AntiAliasing = true
        });

        var portrait = new TextureRect
        {
            Texture = card.Portrait,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        portrait.OffsetLeft = 5f;
        portrait.OffsetTop = 5f;
        portrait.OffsetRight = -5f;
        portrait.OffsetBottom = -34f;
        frame.AddChild(portrait);

        var costPanel = new Panel
        {
            Position = new Vector2(5f, 5f),
            Size = new Vector2(22f, 22f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        costPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = typeColor.Darkened(0.2f),
            CornerRadiusTopLeft = 11,
            CornerRadiusTopRight = 11,
            CornerRadiusBottomRight = 11,
            CornerRadiusBottomLeft = 11,
            ShadowColor = new Color(0f, 0f, 0f, 0.3f),
            ShadowSize = 3,
            AntiAliasing = true
        });
        var costLabel = new BangDreamLabel
        {
            Text = card.EnergyCost.CostsX ? "X" : card.EnergyCost.Canonical.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            MaxFontSize = 12,
            MinFontSize = 8
        };
        costLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        costLabel.AddThemeFontOverride("font", NotoBold);
        costLabel.AddThemeFontSizeOverride("font_size", 12);
        costLabel.AddThemeColorOverride("font_color", Colors.White);
        costPanel.AddChild(costLabel);
        frame.AddChild(costPanel);

        var nameLabel = new BangDreamLabel
        {
            Text = card.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            MouseFilter = MouseFilterEnum.Ignore,
            MaxFontSize = 10,
            MinFontSize = 7
        };
        nameLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        nameLabel.OffsetLeft = 4f;
        nameLabel.OffsetTop = -32f;
        nameLabel.OffsetRight = -4f;
        nameLabel.OffsetBottom = -4f;
        nameLabel.AddThemeFontOverride("font", NotoBold);
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", TextPrimary);
        frame.AddChild(nameLabel);

        return frame;
    }

    private Control BuildRelicRow(RelicModel relic)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 56f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 12);

        var icon = new TextureRect
        {
            Texture = relic.Icon,
            CustomMinimumSize = new Vector2(52f, 52f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(icon);

        var textBox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textBox.AddThemeConstantOverride("separation", 2);

        // 普通 Label 保留标题字体。
        var title = new Label
        {
            Text = relic.Title.GetFormattedText(),
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontOverride("font", TitleFont);
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", TextPrimary);
        textBox.AddChild(title);

        var description = new BangDreamRichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            AutoSizeEnabled = false,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            MaxFontSize = 13,
            MinFontSize = 11
        };
        description.AddThemeFontOverride("normal_font", NotoMedium);
        description.AddThemeFontSizeOverride("normal_font_size", 13);
        description.AddThemeColorOverride("default_color", TextMuted);
        description.Text = relic.DynamicDescription.GetFormattedText();
        textBox.AddChild(description);

        row.AddChild(textBox);
        return row;
    }

    private static Control BuildEmptyHint(string text)
    {
        var label = new BangDreamLabel
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            MaxFontSize = 13,
            MinFontSize = 11
        };
        label.AddThemeFontOverride("font", NotoMedium);
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", TextMuted);
        return label;
    }

    private static Color GetTypeColor(CardType type)
    {
        if (BangDreamConst.IsMusicCardType(type))
        {
            return new Color(0.86f, 0.5f, 0.76f);
        }

        return type switch
        {
            CardType.Attack => new Color(0.85f, 0.31f, 0.31f),
            CardType.Skill => new Color(0.3f, 0.55f, 0.85f),
            CardType.Power => new Color(0.6f, 0.42f, 0.86f),
            CardType.Status => new Color(0.55f, 0.57f, 0.62f),
            CardType.Curse => new Color(0.45f, 0.28f, 0.5f),
            CardType.Quest => new Color(0.8f, 0.55f, 0.25f),
            _ => new Color(0.55f, 0.57f, 0.62f)
        };
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFreeSafely();
        }
    }
}
