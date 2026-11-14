using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes.MegeScript;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Scaffolding.Characters.Visuals;
using STS2RitsuLib.Scaffolding.Godot;

namespace BangDreamInSpire.Scripts.Nodes;

public partial class BangDreamSkinSelector : Control
{
    // 预览实例必须每个选择器各存一份：标准 / 快速 / Dread 三个屏各有一份自己的选择器与详情弹窗，
    // 共用静态缓存会让后一份把「已挂在另一份 SkinContainer 下」的节点再 AddChild 一次，
    // 报 Can't add child ... already has a parent。
    private readonly Dictionary<string, NCreatureVisuals> _visualsCache = [];

    private static readonly Color DisabledColor = new(0.5f, 0.5f, 0.5f);
    private static readonly Color EnabledColor = new(1f, 1f, 1f);

    [Signal]
    public delegate void SkinSelectedEventHandler(string skinPath);

    private BangDreamGoldArrowButton? _leftArrow;
    private BangDreamGoldArrowButton? _rightArrow;

    private Control? _skinContainer;

    private Label? _skinNameLabel;

    private Label? _skinDescriptionLabel;

    private Label? _indexLabel;

    // 皮肤名与说明的本地化表：皮肤 JSON 里只存键名，实际文本在 localization/<lang>/gameplay_ui.json。
    private const string SkinLocTable = "gameplay_ui";

    private int _currentSkinIndex;

    private StartRunLobby? _lobby;

    private readonly List<(string, SkinInfo)> _skinInfos = [];

    /// <summary>当前皮肤的索引（1 起）。</summary>
    public int SkinIndex => _currentSkinIndex + 1;

    /// <summary>该角色可用的皮肤数量。</summary>
    public int SkinCount => _skinInfos.Count;

    /// <summary>当前选中的皮肤路径；没有皮肤时为 null。</summary>
    public string? CurrentSkinPath =>
        _skinInfos.Count == 0
            ? null
            : _skinInfos[Math.Clamp(_currentSkinIndex, 0, _skinInfos.Count - 1)].Item1;

    public override void _Ready()
    {
        _leftArrow = GetNode<BangDreamGoldArrowButton>("%LeftArrow");
        _rightArrow = GetNode<BangDreamGoldArrowButton>("%RightArrow");

        _skinContainer = GetNode<Control>("%SkinContainer");
        _skinNameLabel = GetNode<Label>("%SkinNameLabel");
        _skinDescriptionLabel = GetNodeOrNull<Label>("%SkinDescriptionLabel");
        _indexLabel = GetNode<Label>("%IndexLabel");

        _leftArrow.Connect(NClickableControl.SignalName.Released,
            Callable.From<NButton>(delegate { DecrementSkinIndex(); }));
        _rightArrow.Connect(NClickableControl.SignalName.Released,
            Callable.From<NButton>(delegate { IncrementSkinIndex(); }));
    }

    public override void _ExitTree()
    {
        foreach (var visualsCacheValue in _visualsCache.Values)
        {
            if (IsInstanceValid(visualsCacheValue))
            {
                visualsCacheValue.QueueFreeSafely();
            }
        }

        _visualsCache.Clear();
    }

    public void Init(ISkinSupportCharacter skinSupportCharacter, StartRunLobby? lobby)
    {
        _lobby = lobby;
        _skinInfos.Clear();
        foreach (var skinPath in skinSupportCharacter.CharacterSkinList)
        {
            var skinInfo = SkinManager.GetSkinInfo(skinPath);
            if (skinInfo != null)
            {
                _skinInfos.Add((skinPath, skinInfo));
            }
        }

        if (_skinInfos.Count == 0)
        {
            BangDreamInSpireCore.Logger.Error($"{skinSupportCharacter} has no skin");
            return;
        }

        _currentSkinIndex = 0;
        RefreshState();
    }

    public void SetSkinIndex(int index)
    {
        _currentSkinIndex = index;
        RefreshState();
    }

    private void IncrementSkinIndex()
    {
        if (_currentSkinIndex < _skinInfos.Count - 1)
        {
            SetSkinIndex(_currentSkinIndex + 1);
        }
    }

    private void DecrementSkinIndex()
    {
        if (_currentSkinIndex > 0)
        {
            SetSkinIndex(_currentSkinIndex - 1);
        }
    }

    private void RefreshState()
    {
        if (_skinInfos.Count > 0)
        {
            if (_leftArrow != null)
            {
                if (_currentSkinIndex == 0)
                {
                    _leftArrow.Disable();
                    _leftArrow.Modulate = DisabledColor;
                }
                else
                {
                    _leftArrow.Enable();
                    _leftArrow.Modulate = EnabledColor;
                }
            }

            if (_rightArrow != null)
            {
                if (_currentSkinIndex == _skinInfos.Count - 1)
                {
                    _rightArrow.Disable();
                    _rightArrow.Modulate = DisabledColor;
                }
                else
                {
                    _rightArrow.Enable();
                    _rightArrow.Modulate = EnabledColor;
                }
            }

            if (_indexLabel != null)
            {
                _indexLabel.Text = $"{_currentSkinIndex + 1} / {_skinInfos.Count}";
            }

            var (currentSkinPath, currentSkinInfo) = _skinInfos[_currentSkinIndex];

            if (_skinNameLabel != null)
            {
                // 本地化键优先；未配置或未翻译时回退到 JSON 文件名，避免显示空白。
                _skinNameLabel.Text = ResolveSkinLocText(currentSkinInfo.SkinTemplate.NameKey)
                                      ?? currentSkinPath.GetFile().GetBaseName();
            }

            if (_skinDescriptionLabel != null)
            {
                var description = ResolveSkinLocText(currentSkinInfo.SkinTemplate.DescriptionKey);
                _skinDescriptionLabel.Text = description ?? string.Empty;
                _skinDescriptionLabel.Visible = description != null;
            }

            if (_skinContainer != null)
            {
                var visualScene = _skinInfos[_currentSkinIndex].Item2.SkinTemplate.MultiplayerVisual.VisualScene;
                if (!string.IsNullOrEmpty(visualScene))
                {
                    _skinContainer.Visible = true;
                    foreach (var child in _skinContainer.GetChildren())
                    {
                        _skinContainer.RemoveChild(child);
                    }

                    NCreatureVisuals visuals;
                    if (_visualsCache.TryGetValue(visualScene, out var cache))
                    {
                        visuals = cache;
                    }
                    else
                    {
                        visuals = RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(visualScene);
                        _visualsCache.Add(visualScene, visuals);
                    }

                    visuals.Scale = new Vector2(0.7f, 0.7f);
                    _skinContainer.AddChild(visuals);
                    EmitSignalSkinSelected(_skinInfos[_currentSkinIndex].Item1);
                    CallDeferred(nameof(SetAnim), visuals, "Idle");
                }
            }

            if (_lobby != null)
            {
                BangDreamConst.PlayerSkin.Lobby.Modify(_lobby, _lobby.NetService.NetId,
                    mutate =>
                    {
                        mutate.SkinPath = _skinInfos[_currentSkinIndex].Item1;
                        BangDreamInSpireCore.Logger.Info(
                            $"Player NetId: {_lobby.NetService.NetId} Set skin: {_skinInfos[_currentSkinIndex].Item1}.");
                    });
            }
        }
        else
        {
            if (_leftArrow != null) _leftArrow.Visible = false;
            if (_rightArrow != null) _rightArrow.Visible = false;
            if (_skinContainer != null) _skinContainer.Visible = false;
            if (_skinNameLabel != null) _skinNameLabel.Visible = false;
            if (_indexLabel != null) _indexLabel.Visible = false;
        }
    }

    private void SetAnim(NCreatureVisuals visuals, string anim)
    {
        // SpineAnimation 是 null-safe 包装：静态纹理皮肤下 SetAnimation 会静默空转，
        // 预览就一直停在第一帧。非 Spine 时改走 RitsuLib 的播放层（AnimatedSprite2D / AnimationPlayer）。
        if (visuals.SpineAnimation.IsValid)
        {
            visuals.SpineAnimation.SetAnimation(anim);
        }
        else
        {
            ModCreatureVisualPlayback.TryPlayCue(visuals, null, anim, loop: true);
        }

        visuals.Position = new Vector2(120, 200);
    }

    /// <summary>
    /// 读取皮肤名/说明的本地化文本（gameplay_ui 表）。键缺失或译文为空时返回 null，
    /// 由调用方决定回退（名字回退到文件名，说明则隐藏）。
    /// </summary>
    private static string? ResolveSkinLocText(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || !LocString.Exists(SkinLocTable, key))
        {
            return null;
        }

        var text = new LocString(SkinLocTable, key).GetFormattedText();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

}