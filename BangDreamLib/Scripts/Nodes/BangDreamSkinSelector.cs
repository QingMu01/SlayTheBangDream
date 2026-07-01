using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes.MegeScript;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Scaffolding.Godot;

namespace BangDreamLib.Scripts.Nodes;

public partial class BangDreamSkinSelector : Control
{
    private static readonly Dictionary<string, NCreatureVisuals> VisualsCache = [];

    private BangDreamGoldArrowButton? _leftArrow;
    private BangDreamGoldArrowButton? _rightArrow;

    private Control? _skinContainer;

    private int _currentSkinIndex;

    private StartRunLobby? _lobby;

    private readonly List<(string, SkinInfo)> _skinInfos = [];

    public override void _Ready()
    {
        _leftArrow = GetNode<BangDreamGoldArrowButton>("VSplitContainer/HBoxContainer/LeftArrowContainer/LeftArrow");
        _rightArrow = GetNode<BangDreamGoldArrowButton>("VSplitContainer/HBoxContainer/RightArrowContainer/RightArrow");

        _skinContainer = GetNode<Control>("%SkinContainer");

        _leftArrow.Connect(NClickableControl.SignalName.Released,
            Callable.From<NButton>(delegate { DecrementSkinIndex(); }));
        _rightArrow.Connect(NClickableControl.SignalName.Released,
            Callable.From<NButton>(delegate { IncrementSkinIndex(); }));
    }

    public override void _ExitTree()
    {
        foreach (var visualsCacheValue in VisualsCache.Values)
        {
            visualsCacheValue.QueueFreeSafely();
        }

        VisualsCache.Clear();
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
            BangDreamLibCore.Logger.Error($"{skinSupportCharacter} has no skin");
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
            if (_currentSkinIndex == 0)
            {
                _leftArrow?.Disable();
            }
            else
            {
                _leftArrow?.Enable();
            }

            if (_currentSkinIndex == _skinInfos.Count - 1)
            {
                _rightArrow?.Disable();
            }
            else
            {
                _rightArrow?.Enable();
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
                    if (VisualsCache.TryGetValue(visualScene, out var cache))
                    {
                        visuals = cache;
                    }
                    else
                    {
                        visuals = RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(visualScene);
                        VisualsCache.Add(visualScene, visuals);
                    }

                    visuals.Scale = new Vector2(0.7f, 0.7f);
                    _skinContainer.AddChild(visuals);
                    CallDeferred(nameof(SetAnim), visuals, "Idle");
                }
            }

            if (_lobby != null)
            {
                BangDreamConst.PlayerSkin.Lobby.Modify(_lobby, _lobby.NetService.NetId,
                    mutate =>
                    {
                        mutate.SkinPath = _skinInfos[_currentSkinIndex].Item1;
                        BangDreamLibCore.Logger.Info($"Player NetId: {_lobby.NetService.NetId} Set skin: {_skinInfos[_currentSkinIndex].Item1}.");
                    });
            }
        }
        else
        {
            if (_leftArrow != null) _leftArrow.Visible = false;
            if (_rightArrow != null) _rightArrow.Visible = false;
            if (_skinContainer != null) _skinContainer.Visible = false;
        }
    }

    private void SetAnim(NCreatureVisuals visuals, string anim)
    {
        _ = visuals.SpineAnimation.SetAnimation(anim);
        visuals.Position = new Vector2(120, 200);
    }
}