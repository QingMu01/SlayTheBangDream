using BangDreamLib.Scripts.Features.Rule;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace BangDreamLib.Scripts.Nodes;

public partial class BangDreamHeadTip : Control
{
    private CardModel _cardModel = null!;

    private TextureRect? _head;

    private Tween? _tween;

    private bool _shouldUpdate;

    public static BangDreamHeadTip Create(CardModel? cardModel)
    {
        var headTip = PreloadKey.HeadTip.GetScene().Instantiate<BangDreamHeadTip>();
        if (BangDreamTools.CardIsInCombat(cardModel))
        {
            headTip._cardModel = cardModel!;
            headTip._shouldUpdate = true;
        }

        return headTip;
    }

    public override void _Ready()
    {
        _head = GetNode<TextureRect>("Head");
        if (_shouldUpdate)
        {
            if (ModSecondaryResourceRegistry.TryGet(BangDreamConst.LingeredResource, out var definition))
            {
                var amount = SecondaryResourceCmd.Get(_cardModel.Owner, BangDreamConst.LingeredResource);
                LingeredResourcesChanged(new SecondaryResourceChangedEvent(_cardModel.Owner, definition, amount, amount,
                    SecondaryResourceChangeReason.Unknown, null));
            }
        }

        Position = new Vector2(0f, -210f);
    }

    public override void _EnterTree()
    {
        if (_shouldUpdate)
        {
            SecondaryResourceStateStore.Get(_cardModel.Owner).Changed += LingeredResourcesChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_shouldUpdate)
        {
            SecondaryResourceStateStore.Get(_cardModel.Owner).Changed -= LingeredResourcesChanged;
        }
    }

    private void LingeredResourcesChanged(SecondaryResourceChangedEvent changedEvent)
    {
        if (changedEvent.Definition.Id == BangDreamConst.LingeredResource && _head != null)
        {
            _tween?.Kill();
            _tween = _head.CreateTween();

            float from, to;
            if (LingeredResourcesRule.IsSufficient(_cardModel))
            {
                from = 0.0f;
                to = 1.0f;
            }
            else
            {
                from = 1.0f;
                to = 0.0f;
            }

            _tween.TweenMethod(
                Callable.From<float>(value => _head.SetInstanceShaderParameter("progress", value)),
                from,
                to,
                0.25f
            );
        }
    }
}