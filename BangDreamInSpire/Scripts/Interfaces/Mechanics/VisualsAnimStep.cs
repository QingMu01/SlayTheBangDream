using BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>演出步骤的不透明句柄，隔离内容模组与内部执行契约。</summary>
public readonly struct VisualsAnimStep
{
    /// <summary>内部步骤实现。</summary>
    internal IVisualsAnimStep Inner { get; }

    internal VisualsAnimStep(IVisualsAnimStep inner)
    {
        Inner = inner;
    }
}
