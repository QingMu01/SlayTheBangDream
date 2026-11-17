using BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 演出时间线上的一步的**不透明句柄**。内容模组用 <c>VisualsAnimExtensions</c> 的工厂方法构造它，
/// 再交给 <c>PlayVisualsAnim</c> 一起提交；不要自行创建，也不要读取内部数据。
/// <para>
/// 存在的理由：步骤的真实契约 <c>IVisualsAnimStep</c> 的 ExecuteAsync 要接触视觉代理等机制内部类型，
/// 不适合直接暴露给内容模组（见 <c>tools/check-architecture.py</c> 的 H1 规则）。
/// </para>
/// </summary>
public readonly struct VisualsAnimStep
{
    /// <summary>机制内部的步骤实现。仅同程序集可见。</summary>
    internal IVisualsAnimStep Inner { get; }

    internal VisualsAnimStep(IVisualsAnimStep inner)
    {
        Inner = inner;
    }
}
