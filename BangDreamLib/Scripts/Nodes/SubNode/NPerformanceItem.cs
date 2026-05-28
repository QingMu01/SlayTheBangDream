using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace BangDreamLib.Scripts.Nodes.SubNode;

public partial class NPerformanceItem : NClickableControl
{
    private TextureRect? _cardPortrait;

    private bool _isLocal;

    private CardModel? _model;

    private NCard? _card;

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

    private Tween? _fadeInTween;
    private Tween? _fadeOutTween;

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

        ConnectSignals();
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

    protected override void OnFocus()
    {
        if (Model != null)
        {
            var cardNode = _card ?? NCard.Create(Model);
            if (cardNode != null)
            {
                _card = cardNode;
                GetParent().AddChild(_card);

                _card.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);

                _card.Scale = Vector2.Zero;
                _card.GlobalPosition = GetViewport().GetVisibleRect().GetCenter();

                _fadeInTween?.Kill();
                _fadeOutTween?.Kill();

                _fadeInTween = CreateTween();
                _fadeInTween.TweenProperty(_card, "scale", Vector2.One, 0.2f);
            }
        }
    }

    protected override void OnUnfocus()
    {
        if (Model != null && _card != null)
        {
            _fadeInTween?.Kill();
            _fadeOutTween?.Kill();

            _fadeOutTween = CreateTween();
            _fadeOutTween.TweenProperty(_card, "scale", Vector2.Zero, 0.2f);
            _fadeOutTween.Finished += () => GetParent().RemoveChild(_card);
        }
    }
}