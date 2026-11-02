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

/// <summary>
/// 角色详情弹窗：皮肤样式 / 初始卡组（缩小卡）/ 初始遗物。
/// 由 <see cref="BangDreamCharacterSelector" /> 在切换角色时灌入数据，点击 DETAILS 打开。
/// </summary>
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

    // 标题级字体（中文思源宋体 Bold）：与正文的黑体拉开对比，遗物名等小标题都用它。
    private static readonly FontVariation? TitleFont =
        ResourceLoader.Load<FontVariation>("res://themes/fonts/zhs/source_han_serif_sc_bold_shared.tres");

    private const float PanelOpenScale = 0.96f;

    // 卡牌预览用游戏标准卡节点（NCard，本体尺寸 300x422）统一缩小到网格单元格大小；
    // 悬停时放大回标准比例。NCard 根节点的局部原点就是卡面中心，所以统一以 Scale 缩放即可。
    // 卡牌单元格的基准尺寸；实际尺寸与列数由张数和可用空间动态决定（见 ApplyCardLayout）。
    private static readonly Vector2 CardCellBaseSize = new(110f, 155f);
    private const float CardGap = 12f;
    private const int MaxCardColumns = 6;
    private const float MinCardWidth = 96f;
    // 卡牌区的高度预算：行数决定实际占用，缩放以此为上限，避免撑掉下方遗物区。
    private const float CardAreaMaxHeight = 358f;
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

    // 卡牌区响应式布局：列数/行数与单元格缩放每次重建时按张数/可用空间重算。
    private readonly List<Control> _cardCells = new();
    private Vector2 _cardCellSize = CardCellBaseSize;
    private Vector2 _cardPreviewScale = Vector2.One * (CardCellBaseSize.X / NCard.defaultSize.X);

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

    /// <summary>
    /// 切换角色时刷新弹窗数据（弹窗可以是关闭状态，打开时即是最新内容）。
    /// </summary>
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

    /// <summary>
    /// 名字左侧的头像：优先用角色声明的 <see cref="IModCharacterAssetOverrides.CustomIconPath" /> 图标场景，
    /// 没配时回退到乐队 logo。
    /// </summary>
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

    /// <summary>
    /// 顶部标题区对齐：罗马字与标题左对齐；班级胶囊与乐队名按标题中心线垂直居中，
    /// 胶囊宽度跟随班级文字实际宽度，避免不同角色的班级名溢出固定宽度。
    /// </summary>
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
        // 隐藏遗物（如“角色专属规则”这类只做逻辑的遗物）不出现在详情页。
        var relics = relicSource.Where(RitsuLibFramework.IsRelicVisible).ToList();
        var extraDeck = skinInfo?.GetStartingExtraDeck().ToList() ?? [];

        RebuildCards(deck, extraDeck);
        RebuildRelics(relics);
    }

    private readonly record struct CardEntry(CardModel Card, int Count, bool IsExtra);

    /// <summary>
    /// 初始卡组与额外卡组合并展示：按「卡牌 id + 是否升级」去重计数，额外卡组来源的卡在右上角打标。
    /// </summary>
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
            return;
        }

        var (columns, _) = ApplyCardLayout(entries.Count);

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

    /// <summary>
    /// 按卡牌数量与可用空间计算响应式布局：最多 <see cref="MaxCardColumns" /> 列（6 张可单行放下），
    /// 超过则按行数均衡分列（7→4+3、8→4+4、10→5+5、11→6+5），卡片再按可用宽高等比缩放；
    /// 每一行都是居中的 <c>HBoxContainer</c>，所以末行不满时也水平居中，不会靠左留空。
    /// </summary>
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

            // 悬停放大后卡牌会超出单元格；用一块透明"命中垫"接管放大期间的鼠标，
            // 否则指针一离开单元格就会立刻缩回、来回闪烁。
            var hoverPad = new Control
            {
                Name = HoverPadName,
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false
            };
            cell.AddChild(hoverPad);

            // 单元格尺寸由 GridContainer 决定，布局完成后再把卡牌中心对准单元格中心。
            cell.Resized += () => cardNode.Position = cell.Size * 0.5f;
            cell.MouseEntered += () => SetCardHovered(cell, cardNode, hoverPad, true);
            hoverPad.MouseExited += () => SetCardHovered(cell, cardNode, hoverPad, false);
            // NCard._Ready 内的 Reload 在节点未就绪时会提前返回，需要显式刷新一次标题/描述。
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
            // 卡节点池不可用（如 TestMode）时退回手绘小卡，避免空窗。
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
        // 卡片按行分装在各自 HBox 里，跨行不是兄弟节点；悬停时抬整行的层级，
        // 否则上排放大的卡会被下排的卡盖住。
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

    /// <summary>
    /// 详情页里的音乐牌预览跟随当前展示的皮肤：皮肤配置了 <c>Ui.MusicCardFrame</c> 时替换卡框；
    /// 否则保留 <c>IPerformCard.PerformCardAssets</c> 提供的默认卡框（预览卡是 canonical 卡，
    /// 走不到皮肤 patch 的 mutable/owner 判定）。
    /// </summary>
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

    /// <summary>
    /// 清空卡牌区：NCard 是池化节点，必须先交还给 NodePool 再销毁承载它的行/单元格。
    /// </summary>
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

    /// <summary>
    /// 关闭弹窗时若指针正停在卡片上，<c>MouseExited</c> 不会触发；这里统一把悬停状态复位，
    /// 避免下次打开时残留一张放大卡或一层看不见的命中垫。
    /// </summary>
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
            return;
        }

        // 初始遗物通常只有 1~2 个：并排铺满整行，避免右侧一大片空白；更多时退回纵向列表。
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
            return;
        }

        foreach (var relic in relics)
        {
            _relicsBox.AddChild(BuildRelicRow(relic));
        }
    }

    /// <summary>
    /// 横向遗物条目（1~2 个遗物时并排展示）：图标在左、名称与描述在右，条目按栏宽等分。
    /// </summary>
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

        // 遗物名属于标题层：用普通 Label 保留展示字体（MegaLabel 会在 _Ready 按语言替换成正文黑体）。
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
