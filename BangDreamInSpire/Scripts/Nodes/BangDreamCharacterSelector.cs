using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes.MegeScript;
using BangDreamInSpire.Scripts.Nodes.SubNode;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves;

namespace BangDreamInSpire.Scripts.Nodes;

public partial class BangDreamCharacterSelector : Control
{
    private static readonly StringName BgColorShaderParameter = "bg_color";

    private const float AccentTransitionDuration = 0.35f;
    private const float PosterFadeDuration = 0.45f;
    private const float PosterScaleDuration = 0.35f;
    private const float ContentFadeDuration = 0.26f;
    private const float ContentFadeStagger = 0.03f;
    private const float PosterFloatAmplitude = 7f;
    private const float PosterFloatDuration = 2.4f;

    private List<CharacterModel> _characters = [];
    private List<NCharacterButton> _buttons = [];
    private List<CanvasItem> _contentItems = [];

    private ColorRect? _showBg;
    private ColorRect? _posterBackdrop;
    private ColorRect? _stageSweep;
    private TextureRect? _characterPoster;
    private TextureRect? _leftGlow;
    private GpuParticles2D? _particles;
    private HBoxContainer? _characterButtons;

    private Label? _bandLabel;
    private Panel? _classChip;
    private StyleBoxFlat? _classChipStyleBox;
    private Label? _classLabel;
    private Label? _smallIntro;
    private Label? _bigIntro;
    private TextureRect? _signature;
    private ColorRect? _divider1;
    private ColorRect? _divider2;
    private RichTextLabel? _description;
    private Panel? _healthChip;
    private Label? _healthLabel;
    private Panel? _goldChip;
    private Label? _goldLabel;
    private TextureRect? _relicIcon;
    private BangDreamRichTextLabel? _relicDescription;
    private ColorRect? _accentBar;
    private Panel? _skinCard;
    private BangDreamAscensionPanel? _ascensionPanel;
    private BangDreamSkinSelector? _skinSelector;

    private Tween? _posterTween;
    private Tween? _accentTween;
    private Tween? _contentTween;
    private Tween? _floatTween;
    private Tween? _sweepTween;
    private Vector2 _posterRestPosition;

    private CharacterGroup? _buttonGroup;

    private NCharacterSelectButton? _sourceButton;
    private ICharacterSelectButtonDelegate? _delegate;

    public StartRunLobby? Lobby { get; set; }

    public BangDreamAscensionPanel AscensionPanel =>
        _ascensionPanel ?? throw new NullReferenceException("ascension panel is not init.");

    public override void _Ready()
    {
        _characterButtons = GetNode<HBoxContainer>("%CharacterButtons");

        _showBg = GetNode<ColorRect>("%ShowBg");
        _posterBackdrop = GetNode<ColorRect>("%PosterBackdrop");
        _stageSweep = GetNode<ColorRect>("%StageSweep");
        _characterPoster = GetNode<TextureRect>("%CharacterPoster");
        _leftGlow = GetNode<TextureRect>("LeftGlow");
        _particles = GetNode<GpuParticles2D>("ScreenBackground/GPUParticles2D");

        _bandLabel = GetNode<Label>("%BandLabel");
        _classChip = GetNode<Panel>("%ClassChip");
        _classLabel = GetNode<Label>("%ClassLabel");
        _smallIntro = GetNode<Label>("%SmallIntro");
        _bigIntro = GetNode<Label>("%BigIntro");
        _signature = GetNode<TextureRect>("%Signature");
        _divider1 = GetNode<ColorRect>("%Divider1");
        _divider2 = GetNode<ColorRect>("%Divider2");
        _description = GetNode<RichTextLabel>("%Description");
        _healthChip = GetNode<Panel>("ShowCharacterInfo/HealthChip");
        _healthLabel = GetNode<Label>("%HealthLabel");
        _goldChip = GetNode<Panel>("ShowCharacterInfo/GoldChip");
        _goldLabel = GetNode<Label>("%GoldLabel");
        _relicIcon = GetNode<TextureRect>("%RelicIcon");
        _relicDescription = GetNode<BangDreamRichTextLabel>("%RelicDescription");
        _accentBar = GetNode<ColorRect>("%AccentBar");
        _skinCard = GetNode<Panel>("SkinCard");
        _ascensionPanel = GetNode<BangDreamAscensionPanel>("%AscensionPanel");
        _skinSelector = GetNode<BangDreamSkinSelector>("%SkinSelector");

        _posterRestPosition = _characterPoster.Position;

        // 着色器与 StyleBox 在运行时按乐队主题色重写，先各自复制一份，避免改动共享资源。
        if (_showBg.Material is ShaderMaterial bgMaterial)
        {
            _showBg.Material = (ShaderMaterial)bgMaterial.Duplicate();
        }

        if (_classChip.GetThemeStylebox("panel") is StyleBoxFlat chipStyleBox)
        {
            _classChipStyleBox = (StyleBoxFlat)chipStyleBox.Duplicate();
            _classChip.AddThemeStyleboxOverride("panel", _classChipStyleBox);
        }

        _contentItems =
        [
            _bandLabel, _classChip, _smallIntro, _bigIntro, _signature, _divider1,
            _description, _healthChip, _goldChip, _divider2, _relicIcon, _relicDescription,
            _ascensionPanel, _accentBar
        ];

        _ascensionPanel.Connect(BangDreamAscensionPanel.SignalName.AscensionLevelChanged,
            Callable.From(OnAscensionPanelLevelChanged));

        _skinSelector.Connect(BangDreamSkinSelector.SignalName.SkinSelected,
            Callable.From((Action<string>)OnSelectedSkin));

        StartPosterFloat();
    }

    public void SelectCharacter(CharacterModel character)
    {
        // 同步按钮状态
        foreach (var button in _buttons)
        {
            if (button.Character == character)
            {
                button.Select();
            }
            else
            {
                button.Deselect();
            }
        }

        var bandColor = _buttonGroup?.GetGroupInfo().Color ?? Colors.White;
        var accentColor = bandColor.ToAccentColor();
        var memberColor = character.NameColor;

        ApplyAccent(bandColor, accentColor, memberColor);

        if (_bandLabel != null)
        {
            _bandLabel.Text = _buttonGroup?.GetGroupInfo().Name.ToUpperInvariant() ?? string.Empty;
            _bandLabel.AddThemeColorOverride("font_color", accentColor);
        }

        if (character is IBangDreamMateData mateData)
        {
            if (_characterPoster != null)
            {
                _characterPoster.Texture = LoadTexture(mateData.SelectPoster);
            }

            if (_signature != null)
            {
                _signature.Texture = LoadTexture(mateData.SelectLogo);
                _signature.Visible = _signature.Texture != null;
            }

            if (_smallIntro != null)
            {
                _smallIntro.Text = mateData.MemberNameRoman.ToUpperInvariant();
                _smallIntro.AddThemeColorOverride("font_color", memberColor);
            }

            if (_classLabel != null)
            {
                _classLabel.Text = mateData.MemberClass;
            }
        }

        if (_bigIntro != null)
        {
            _bigIntro.Text = new LocString("characters", character.CharacterSelectTitle)
                .GetFormattedText();
        }

        if (_description != null)
            _description.Text = new LocString("characters", character.CharacterSelectDesc)
                .GetFormattedText();

        if (_healthLabel != null)
        {
            _healthLabel.Text = character.StartingHp.ToString();
        }

        if (_goldLabel != null)
        {
            _goldLabel.Text = character.StartingGold.ToString();
        }

        if (character.StartingRelics.Count > 0)
        {
            var relic = character.StartingRelics[0];
            SetRelicShow(relic);
        }

        var skinSupportCharacter = character as ISkinSupportCharacter;
        if (_skinCard != null)
        {
            _skinCard.Visible = skinSupportCharacter != null;
        }

        if (_delegate != null && _sourceButton != null)
        {
            _delegate.SelectCharacter(_sourceButton, character);
            if (skinSupportCharacter != null)
            {
                _skinSelector?.Init(skinSupportCharacter, Lobby);
            }
        }

        if (_ascensionPanel != null)
        {
            var characterStats = SaveManager.Instance.Progress.GetOrCreateCharacterStats(character.Id);
            if (characterStats.MaxAscension > 0)
            {
                _ascensionPanel.SetMaxAscension(characterStats.MaxAscension);
                _ascensionPanel.SetAscensionLevel(characterStats.PreferredAscension);
            }
        }

        PlayStageSweep();
        PlayPosterIntro();
        PlayContentIntro();
    }

    public void Init(CharacterGroup group, NCharacterSelectButton button, ICharacterSelectButtonDelegate delegateButton)
    {
        _buttonGroup = group;
        _sourceButton = button;
        _delegate = delegateButton;

        _characters = ModelDb.AllCharacters.Where(c => c is IGroupableCharacter ac && ac.Group == group).ToList();
        _buttons = [];

        if (_characters.Count == 0)
        {
            throw new InvalidOperationException($"Group {group} doesn't have any members!");
        }

        if (_characterButtons != null)
        {
            foreach (var child in _characterButtons.GetChildren())
            {
                _characterButtons.RemoveChild(child);
                child.QueueFreeSafely();
            }

            foreach (var character in _characters)
            {
                var child = NCharacterButton.Create(this, character);
                var wrapper = new Control { CustomMinimumSize = NCharacterButton.CardSize };
                wrapper.AddChildSafely(child);
                _characterButtons.AddChildSafely(wrapper);

                _buttons.Add(child);

                if (character is IGroupableCharacter groupableCharacter)
                {
                    child.Visible = !groupableCharacter.IsHidden;
                }

                child.Deselect();
            }

            // 成员卡向右对齐：人数增加时整排向左生长，而不是撑出卡片右缘。
            var visibleCount = _buttons.Count(item => item.Visible);
            if (visibleCount > 0)
            {
                var separation = _characterButtons.GetThemeConstant("separation");
                var rowWidth = visibleCount * NCharacterButton.CardSize.X + (visibleCount - 1) * separation;
                _characterButtons.OffsetLeft = _characterButtons.OffsetRight - rowWidth;
            }

            var defaultButton = _buttons.FirstOrDefault(item => item is
                { Visible: true, Character: IGroupableCharacter { AllowSelect: true } });
            if (defaultButton is { Character: not null })
            {
                SelectCharacter(defaultButton.Character);
            }
            else
            {
                throw new InvalidOperationException("No character can be selected!");
            }
        }
    }

    private static Texture2D? LoadTexture(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path)
            ? BangDreamPreloadManager.GetTexture2D(path)
            : null;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    /// <summary>
    /// 乐队色负责舞台（背景、立绘底、色条），成员色负责个人信息（罗马音、细节），
    /// 过亮的乐队色先压暗再作为文字/描边色使用。
    /// </summary>
    private void ApplyAccent(Color bandColor, Color accentColor, Color memberColor)
    {
        _accentTween?.Kill();
        _accentTween = CreateTween()
            .SetParallel()
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

        if (_showBg?.Material is ShaderMaterial material)
        {
            _accentTween.TweenProperty(material, $"shader_parameter/{BgColorShaderParameter}", bandColor,
                AccentTransitionDuration);
        }

        if (_leftGlow != null)
        {
            _accentTween.TweenProperty(_leftGlow, "self_modulate", WithAlpha(bandColor, 0.85f),
                AccentTransitionDuration);
        }

        if (_particles != null)
        {
            _accentTween.TweenProperty(_particles, "modulate", WithAlpha(bandColor, 0.55f), AccentTransitionDuration);
        }

        if (_posterBackdrop != null)
        {
            _accentTween.TweenProperty(_posterBackdrop, "color", WithAlpha(accentColor, 0.22f),
                AccentTransitionDuration);
        }

        if (_accentBar != null)
        {
            _accentTween.TweenProperty(_accentBar, "color", accentColor, AccentTransitionDuration);
        }

        if (_divider1 != null)
        {
            _accentTween.TweenProperty(_divider1, "color", WithAlpha(accentColor, 0.5f), AccentTransitionDuration);
        }

        if (_divider2 != null)
        {
            _accentTween.TweenProperty(_divider2, "color", WithAlpha(accentColor, 0.28f), AccentTransitionDuration);
        }

        if (_classChipStyleBox != null)
        {
            _accentTween.TweenProperty(_classChipStyleBox, "bg_color", accentColor, AccentTransitionDuration);
        }

        if (_signature != null)
        {
            _accentTween.TweenProperty(_signature, "modulate", WithAlpha(memberColor, 0.95f),
                AccentTransitionDuration);
        }
    }

    private void PlayPosterIntro()
    {
        if (_characterPoster == null)
        {
            return;
        }

        _posterTween?.Kill();
        _floatTween?.Kill();

        _characterPoster.Position = _posterRestPosition;
        _characterPoster.Modulate = WithAlpha(Colors.White, 0f);
        _characterPoster.Scale = new Vector2(1.06f, 1.06f);

        _posterTween = CreateTween();
        _posterTween.SetParallel();
        _posterTween.TweenProperty(_characterPoster, "modulate:a", 1f, PosterFadeDuration);
        _posterTween.TweenProperty(_characterPoster, "scale", Vector2.One, PosterScaleDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _posterTween.Chain().TweenCallback(Callable.From(StartPosterFloat));
    }

    /// <summary>
    /// 立绘极轻微的呼吸浮动，让静止画面不显得是一张贴图。
    /// </summary>
    private void StartPosterFloat()
    {
        if (_characterPoster == null)
        {
            return;
        }

        _floatTween?.Kill();
        _characterPoster.Position = _posterRestPosition;

        _floatTween = CreateTween().SetLoops();
        _floatTween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _floatTween.TweenProperty(_characterPoster, "position:y", _posterRestPosition.Y - PosterFloatAmplitude,
            PosterFloatDuration);
        _floatTween.TweenProperty(_characterPoster, "position:y", _posterRestPosition.Y + PosterFloatAmplitude,
            PosterFloatDuration);
    }

    /// <summary>
    /// 信息区按顺序淡入，切换角色时比整块面板直接跳变更有节奏。
    /// </summary>
    private void PlayContentIntro()
    {
        _contentTween?.Kill();
        _contentTween = CreateTween().SetParallel();

        var delay = 0f;
        foreach (var item in _contentItems)
        {
            item.Modulate = WithAlpha(Colors.White, 0f);
            _contentTween.TweenProperty(item, "modulate:a", 1f, ContentFadeDuration).SetDelay(delay);
            delay += ContentFadeStagger;
        }
    }

    /// <summary>
    /// 乐队色舞台上的斜向扫光，切换成员时给画面一个“换幕”的瞬间。
    /// </summary>
    private void PlayStageSweep()
    {
        if (_stageSweep?.Material is not ShaderMaterial sweepMaterial)
        {
            return;
        }

        _sweepTween?.Kill();
        _stageSweep.Visible = true;
        sweepMaterial.SetShaderParameter("reveal_progress", 0f);

        _sweepTween = CreateTween();
        _sweepTween.TweenMethod(
            Callable.From((float progress) => sweepMaterial.SetShaderParameter("reveal_progress", progress)),
            0f, 1f, 0.55f);
        _sweepTween.Chain().TweenCallback(Callable.From(() =>
        {
            if (_stageSweep != null)
            {
                _stageSweep.Visible = false;
            }
        }));
    }

    private void SetRelicShow(RelicModel relic)
    {
        if (_relicIcon != null)
        {
            _relicIcon.Texture = relic.Icon;
        }

        _relicDescription?.SetTextAutoSize(
            $"[b]{relic.Title.GetFormattedText()}[/b]\n{relic.DynamicDescription.GetFormattedText()}");
    }

    private void OnAscensionPanelLevelChanged()
    {
        if (Lobby == null)
            return;
        if (_ascensionPanel == null)
            return;
        if (Lobby.NetService.Type == NetGameType.Client || Lobby.Ascension == _ascensionPanel.Ascension)
            return;

        Lobby.SyncAscensionChange(_ascensionPanel.Ascension);
    }

    private void OnSelectedSkin(string skinPath)
    {
        var startingRelic = SkinManager.GetSkinInfo(skinPath)?.GetStartingRelics().FirstOrDefault();
        if (startingRelic != null)
        {
            SetRelicShow(startingRelic);
        }
    }
}
