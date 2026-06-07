using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Nodes.MegeScript;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Scaffolding.Godot;

namespace BangDreamLib.Scripts.Nodes;

public partial class BangDreamSkinSelector : Control
{
    private BangDreamGoldArrowButton? _leftArrow;
    private BangDreamGoldArrowButton? _rightArrow;

    private Control? _skinContainer;

    private int _currentSkinIndex;
    private List<SkinInfo> _skinInfos = [];

    private static readonly Dictionary<string, NCreatureVisuals> VisualsCache = [];

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

    public void Init(CharacterModel character)
    {
        var (currentIndex, skinInfos) = SkinManager.GetCharacterSkins(character.GetType());
        if (currentIndex >= 0)
        {
            _currentSkinIndex = currentIndex;
            _skinInfos = skinInfos;
            RefreshVisibility();
        }
        else
        {
            BangDreamLibCore.Logger.Error($"{character.GetType()} has no skin");
        }
    }

    public void SetSkinIndex(int index)
    {
        _currentSkinIndex = index;
        RefreshVisibility();
    }

    private void IncrementSkinIndex()
    {
        if (_currentSkinIndex < _skinInfos.Count && _currentSkinIndex < _skinInfos.Count)
        {
            SetSkinIndex(_currentSkinIndex + 1);
        }
    }

    private void DecrementSkinIndex()
    {
        if (_currentSkinIndex > 0 && _currentSkinIndex < _skinInfos.Count)
        {
            SetSkinIndex(_currentSkinIndex - 1);
        }
    }

    private void RefreshVisibility()
    {
        if (_skinInfos.Count > 0)
        {
            if (_leftArrow != null) _leftArrow.Visible = _currentSkinIndex != 0;
            if (_rightArrow != null) _rightArrow.Visible = _currentSkinIndex != _skinInfos.Count - 1;
            if (_skinContainer != null)
            {
                var visualScene = _skinInfos[_currentSkinIndex].SkinTemplate.MultiplayerVisual.VisualScene;
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