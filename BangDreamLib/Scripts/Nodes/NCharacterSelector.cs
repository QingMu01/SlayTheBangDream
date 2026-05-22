using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using NCharacterButton = BangDreamLib.Scripts.Nodes.SubNode.NCharacterButton;

namespace BangDreamLib.Scripts.Nodes;

public partial class NCharacterSelector : Control
{
    private List<CharacterModel> _characters = [];
    private List<NCharacterButton> _buttons = [];

    private Panel _showInfo;
    private StyleBoxFlat? _infoStyleBox;

    private Panel _showBg;

    private TextureRect _characterPoster;

    private VBoxContainer _characterButtons;

    private Label _smallIntro;
    private Label _classLabel;
    private Label _bigIntro;
    private Label _healthLabel;
    private Label _goldLabel;
    private RichTextLabel _description;

    private TextureRect _relicIcon;
    private Label _relicName;
    private RichTextLabel _relicDescription;

    private Tween? _posterTween;

    private IAggregationGroup _pageGroup;

    private NCharacterSelectButton _sourceButton;
    private ICharacterSelectButtonDelegate _delegate;

    public override void _Ready()
    {
        _characterButtons = GetNode<VBoxContainer>("%CharacterButtons");

        _showInfo = GetNode<Panel>("ShowCharacterInfo");
        _showBg = GetNode<Panel>("ShowCharacterInfo/SplitContainer/ShowBg");

        _characterPoster = GetNode<TextureRect>("%CharacterPoster");
        _smallIntro = GetNode<Label>("%SmallIntro");
        _classLabel = GetNode<Label>("%ClassLabel");
        _bigIntro = GetNode<Label>("%BigIntro");
        _healthLabel = GetNode<Label>("%HealthLabel");
        _goldLabel = GetNode<Label>("%GoldLabel");
        _description = GetNode<RichTextLabel>("%Description");
        _relicIcon = GetNode<TextureRect>("%RelicIcon");
        _relicName = GetNode<Label>("%RelicName");
        _relicDescription = GetNode<RichTextLabel>("%RelicDescription");

        // 缓存并复用 StyleBox
        if (_showInfo.GetThemeStylebox("panel") is StyleBoxFlat styleBox)
        {
            _infoStyleBox = styleBox.Duplicate() as StyleBoxFlat;
            _showInfo.AddThemeStyleboxOverride("panel", _infoStyleBox);
        }
    }

    public void SelectCharacter(NCharacterButton selectedButton)
    {
        // 同步按钮状态
        foreach (var button in _buttons)
        {
            if (button == selectedButton)
            {
                button.Select();
            }
            else
            {
                button.Deselect();
            }
        }

        _delegate.SelectCharacter(_sourceButton, selectedButton.Character);

        if (selectedButton.Character is IAggregationCharacter aggregationCharacter)
        {
            if (_infoStyleBox != null)
            {
                _infoStyleBox.BorderColor = _pageGroup?.Band.GetBandColor() ?? Colors.White;
            }

            var selectPoster = aggregationCharacter.SelectPoster;
            _characterPoster.Texture = selectPoster != null ? PreloadManager.Cache.GetTexture2D(selectPoster) : null;

            _smallIntro.Text = $"{aggregationCharacter.MemberClass} {aggregationCharacter.MemberNameRoman}";
            _smallIntro.Modulate = selectedButton.Character.NameColor;

            _classLabel.Text = aggregationCharacter.MemberClass;
            _bigIntro.Text =
                new LocString("characters", selectedButton.Character.CharacterSelectTitle).GetFormattedText();
            _description.Text = new LocString("characters", selectedButton.Character.CharacterSelectDesc)
                .GetFormattedText();

            _healthLabel.Text = selectedButton.Character.StartingHp.ToString();
            _goldLabel.Text = selectedButton.Character.StartingGold.ToString();

            var relic = selectedButton.Character.StartingRelics[0];
            _relicIcon.Texture = relic.Icon;
            _relicName.Text = relic.Title.GetFormattedText();
            _relicDescription.Text = relic.DynamicDescription.GetFormattedText();

            // 设置背景着色器颜色
            if (_showBg.Material is ShaderMaterial shader)
            {
                shader.Set("shader_parameter/bg_color", selectedButton.Character.NameColor);
            }
        }

        // 播放海报切换动画（合并透明度和缩放）
        _posterTween?.Kill();
        _characterPoster.Modulate = new Color(1, 1, 1, 0);
        _characterPoster.Scale = new Vector2(1.25f, 1.25f);

        _posterTween = CreateTween();
        _posterTween.Parallel().TweenProperty(_characterPoster, "modulate:a", 1f, 0.5f);
        _posterTween.Parallel().TweenProperty(_characterPoster, "scale", Vector2.One, 0.25f);
    }

    public void Init(IAggregationGroup pageGroup,
        NCharacterSelectButton sourceButton, ICharacterSelectButtonDelegate buttonDelegate)
    {
        _pageGroup = pageGroup;
        _sourceButton = sourceButton;
        _delegate = buttonDelegate;

        _characters = ModelDb.AllCharacters
            .Where(c => c is IAggregationCharacter ac && ac.Band == pageGroup.Band).ToList();
        _buttons.Clear();

        if (_characters.Count == 0)
        {
            throw new InvalidOperationException($"Group {pageGroup.Band.GetBandName()} doesn't have any members!");
        }

        foreach (var child in _characterButtons.GetChildren())
        {
            child.QueueFreeSafely();
        }

        var autoSelectedFirst = false;
        foreach (var character in _characters)
        {
            var button = NCharacterButton.Create(this, character);
            var control = new Control();
            control.AddChildSafely(button);
            _characterButtons.AddChildSafely(control);

            _buttons.Add(button);

            if (character is IAggregationCharacter aggregationCharacter)
            {
                button.Visible = !aggregationCharacter.IsHidden;
                button.Disabled = !aggregationCharacter.AllowSelect;
            }

            if (!autoSelectedFirst && character is IAggregationCharacter { AllowSelect: true })
            {
                button.Select();
                SelectCharacter(button);
                autoSelectedFirst = true;
            }
            else
            {
                button.Deselect();
            }
        }
    }
}