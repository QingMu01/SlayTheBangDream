using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>Dread 覆盖层插到 SubViewportContainer 后，不支持进阶。</summary>
internal sealed class DreadSelectAdapter : CharacterSelectAdapter
{
    private const string BackgroundHostPath = "SubViewportContainer";

    /// <summary>轮换说明文字节点。</summary>
    private const string RotationLabelPath = "RotatingTickbox/Container/RotatingExplanation";

    /// <summary>纸面正文墨色。</summary>
    private static readonly Color PanelInk = new(0.086f, 0.125f, 0.18f);

    internal static readonly DreadSelectAdapter Instance = new();

    internal override string ModeName => "恐惧";

    internal override bool SupportsAscension => false;

    internal override void InsertOverlay(Node screen, Control overlay)
    {
        screen.AddChildSafely(overlay);

        if (screen.GetNodeOrNull<Control>(BackgroundHostPath) is not { } backgroundHost)
        {
            BangDreamInSpireCore.Logger.Warn(
                "[GroupSelect] NDreadScreen 上找不到 SubViewportContainer，覆盖层将留在末尾（可能遮挡确认 / 返回）。");
            return;
        }

        screen.MoveChild(overlay, backgroundHost.GetIndex() + 1);
    }

    internal override void OnViewChanged(Node screen, bool groupView)
    {
        // 分组视图用墨色，详情视图恢复本体配色。
        if (screen.GetNodeOrNull<Control>(RotationLabelPath) is not { } rotationLabel)
        {
            return;
        }

        if (groupView)
        {
            rotationLabel.AddThemeColorOverride("default_color", PanelInk);
        }
        else
        {
            rotationLabel.RemoveThemeColorOverride("default_color");
        }
    }
}
