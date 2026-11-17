using Godot;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>标准模式：背景是根节点的直接子节点，覆盖层插到它之后即可（本体原生布局本身可正常共存）。</summary>
internal sealed class StandardSelectAdapter : CharacterSelectAdapter
{
    internal static readonly StandardSelectAdapter Instance = new();

    internal override string ModeName => "标准";

    internal override bool SupportsAscension => true;

    internal override void InsertOverlay(Node screen, Control overlay) =>
        InsertAfterBackground(screen, overlay);
}
