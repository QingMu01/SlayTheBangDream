using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

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

    private const float PanelOpenScale = 0.96f;

    private ColorRect? _scrim;
    private Panel? _panel;
    private ColorRect? _topAccent;
    private ColorRect? _divider;
    private TextureRect? _emblem;
    private Label? _title;
    private Label? _roman;
    private Panel? _classChip;
    private StyleBoxFlat? _classChipStyleBox;
    private Label? _classLabel;
    private Label? _bandLabel;
    private Button? _closeButton;
    private Label? _skinLabel;
    private Label? _skinIndex;
    private Label? _extraLabel;
    private Label? _cardsLabel;
    private Label? _relicsLabel;
    private BangDreamSkinSelector? _skinSelector;
    private HBoxContainer? _extraRow;
    private GridContainer? _cardsGrid;
    private VBoxContainer? _relicsBox;

    private Tween? _tween;
    private CharacterModel? _character;
    private bool _suppressSkinRefresh;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        _scrim = GetNode<ColorRect>("%Scrim");
        _panel = GetNode<Panel>("%Panel");
        _topAccent = GetNode<ColorRect>("%TopAccent");
        _divider = GetNode<ColorRect>("%Divider");
        _emblem = GetNode<TextureRect>("%Emblem");
        _title = GetNode<Label>("%Title");
        _roman = GetNode<Label>("%Roman");
        _classChip = GetNode<Panel>("%ClassChip");
        _classLabel = GetNode<Label>("%ClassLabel");
        _bandLabel = GetNode<Label>("%BandLabel");
        _closeButton = GetNode<Button>("%CloseButton");
        _skinLabel = GetNode<Label>("%SkinLabel");
        _skinIndex = GetNode<Label>("%SkinIndex");
        _extraLabel = GetNode<Label>("%ExtraLabel");
        _cardsLabel = GetNode<Label>("%CardsLabel");
        _relicsLabel = GetNode<Label>("%RelicsLabel");
        _skinSelector = GetNode<BangDreamSkinSelector>("%SkinSelector");
        _extraRow = GetNode<HBoxContainer>("%ExtraRow");
        _cardsGrid = GetNode<GridContainer>("%CardsGrid");
        _relicsBox = GetNode<VBoxContainer>("%RelicsBox");

        if (_classChip.GetThemeStylebox("panel") is StyleBoxFlat chipStyleBox)
        {
            _classChipStyleBox = (StyleBoxFlat)chipStyleBox.Duplicate();
            _classChip.AddThemeStyleboxOverride("panel", _classChipStyleBox);
        }

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

        if (_bandLabel != null)
        {
            _bandLabel.Text = group?.GetGroupInfo().Name.ToUpperInvariant() ?? string.Empty;
        }

        if (character is IBangDreamMateData mateData)
        {
            if (_roman != null)
            {
                _roman.Text = mateData.MemberNameRoman.ToUpperInvariant();
            }

            if (_classLabel != null)
            {
                _classLabel.Text = mateData.MemberClass;
            }

            if (_emblem != null)
            {
                _emblem.Texture = BangDreamCharacterSelector.LoadTexture(mateData.SelectLogo);
                _emblem.Visible = _emblem.Texture != null;
            }
        }

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
        UpdateSkinIndex();

        if (_suppressSkinRefresh)
        {
            return;
        }

        EmitSignal(SignalName.SkinChanged, skinPath);
        RefreshContent();
    }

    private void ApplyPalette(Color accentColor, Color memberColor)
    {
        if (_topAccent != null)
        {
            _topAccent.Color = accentColor;
        }

        if (_divider != null)
        {
            _divider.Color = new Color(accentColor.R, accentColor.G, accentColor.B, 0.35f);
        }

        if (_classChipStyleBox != null)
        {
            _classChipStyleBox.BgColor = accentColor;
        }

        foreach (var label in new[] { _bandLabel, _skinLabel, _extraLabel, _cardsLabel, _relicsLabel })
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
        var relics = skinInfo != null
            ? skinInfo.GetStartingRelics().ToList()
            : _character.StartingRelics.ToList();
        var extraDeck = skinInfo?.GetStartingExtraDeck().ToList() ?? [];

        RebuildCards(deck);
        RebuildExtraCards(extraDeck);
        RebuildRelics(relics);
        UpdateSkinIndex();
    }

    private void UpdateSkinIndex()
    {
        if (_skinIndex == null)
        {
            return;
        }

        if (_skinSelector is { SkinCount: > 0 })
        {
            _skinIndex.Visible = _skinSelector.Visible;
            _skinIndex.Text = $"{_skinSelector.SkinIndex} / {_skinSelector.SkinCount}";
        }
        else
        {
            _skinIndex.Visible = false;
        }
    }

    private void RebuildCards(IReadOnlyList<CardModel> cards)
    {
        if (_cardsGrid == null)
        {
            return;
        }

        ClearChildren(_cardsGrid);

        if (cards.Count == 0)
        {
            _cardsGrid.AddChild(BuildEmptyHint("该皮肤没有配置初始卡组"));
            return;
        }

        foreach (var card in cards)
        {
            _cardsGrid.AddChild(BuildMiniCard(card));
        }
    }

    private void RebuildExtraCards(IReadOnlyList<CardModel> cards)
    {
        if (_extraRow == null || _extraLabel == null)
        {
            return;
        }

        ClearChildren(_extraRow);
        _extraLabel.Visible = cards.Count > 0;

        foreach (var card in cards)
        {
            _extraRow.AddChild(BuildMiniCard(card));
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

        foreach (var relic in relics)
        {
            _relicsBox.AddChild(BuildRelicRow(relic));
        }
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
            BgColor = new Color(0.994f, 0.996f, 1f),
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
        var costLabel = new Label
        {
            Text = card.EnergyCost.CostsX ? "X" : card.EnergyCost.Canonical.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        costLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        costLabel.AddThemeFontOverride("font", NotoBold);
        costLabel.AddThemeFontSizeOverride("font_size", 12);
        costLabel.AddThemeColorOverride("font_color", Colors.White);
        costPanel.AddChild(costLabel);
        frame.AddChild(costPanel);

        var nameLabel = new Label
        {
            Text = card.Title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            MouseFilter = MouseFilterEnum.Ignore
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

        var title = new Label
        {
            Text = relic.Title.GetFormattedText(),
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontOverride("font", NotoBold);
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", TextPrimary);
        textBox.AddChild(title);

        var description = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
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
        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontOverride("font", NotoMedium);
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", TextMuted);
        return label;
    }

    private static Color GetTypeColor(CardType type)
    {
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
