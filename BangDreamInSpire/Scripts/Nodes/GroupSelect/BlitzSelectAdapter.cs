using Godot;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>Blitz 装饰大字仅显示不接收输入，避免遮挡成员书签。</summary>
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
