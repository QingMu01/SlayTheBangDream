using Godot;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>
/// 快速模式：场景在屏幕中上方多了一个装饰性大字 <c>BlitzPlaceholder</c>（“000 Blitz 000”），
/// 它的树序在所有按钮之后，且 <see cref="RichTextLabel" /> 默认 <c>MouseFilter=Stop</c>。
/// 本模组的成员书签正好落在它的矩形里，于是点击全部被它吃掉。
/// <para>
/// 处理：该节点纯装饰、不接收任何输入 —— 常驻 <c>MouseFilter=Ignore</c>，让点击穿过它到达成员书签；
/// 大字本身保留显示（它是该模式的视觉标识）。
/// </para>
/// </summary>
internal sealed class BlitzSelectAdapter : CharacterSelectAdapter
{
    private const string PlaceholderNodePath = "BlitzPlaceholder";

    internal static readonly BlitzSelectAdapter Instance = new();

    internal override string ModeName => "快速";

    internal override bool SupportsAscension => true;

    internal override void Prepare(Node screen)
    {
        if (screen.GetNodeOrNull<Control>(PlaceholderNodePath) is { } placeholder)
        {
            placeholder.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
    }

    internal override void InsertOverlay(Node screen, Control overlay) =>
        InsertAfterBackground(screen, overlay);
}
