using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace BangDreamLib.Scripts.Nodes.SubNode;

public partial class NPerformanceItem : NClickableControl
{
    private TextureRect? _cardPortrait;

    private bool _isLocal;

    private CardModel? _model;

    public CardModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            if (value != null)
            {
                if (_cardPortrait != null)
                {
                    _cardPortrait.Texture = value.Portrait;
                    StartRotateLoop();
                }

                if (value is IPerformanceCard performanceCard)
                {
                    performanceCard.Handle = this;
                }
            }
        }
    }

    private Tween? _rotateTween;

    public static NPerformanceItem Create(bool isLocal, CardModel? model = null)
    {
        var item = PreloadKey.PerformanceItem.GetScene().Instantiate<NPerformanceItem>();
        item._isLocal = isLocal;
        item.Model = model;
        return item;
    }

    public override void _Ready()
    {
        _cardPortrait = GetNode<TextureRect>("%CardPortrait");
        _cardPortrait.Texture = Model?.Portrait;
        if (Model != null)
        {
            StartRotateLoop();
        }

        if (!_isLocal)
        {
            Modulate = new Color(0.5f, 0.5f, 0.5f);
        }
    }

    private void StartRotateLoop()
    {
        RotationDegrees %= 360;
        _rotateTween?.Kill();
        _rotateTween = CreateTween();
        _rotateTween.TweenProperty(this, "rotation_degrees", RotationDegrees + 360, 8f)
            .SetTrans(Tween.TransitionType.Linear);
        _rotateTween.Finished += StartRotateLoop;
    }

    // TODO:获取焦点时展示当前卡牌
    protected override void OnFocus()
    {
        if (Model != null)
        {
            base.OnFocus();
        }
    }

    // TODO:失去焦点时隐藏当前卡牌
    protected override void OnUnfocus()
    {
        if (Model != null)
        {
            base.OnUnfocus();
        }
    }
}