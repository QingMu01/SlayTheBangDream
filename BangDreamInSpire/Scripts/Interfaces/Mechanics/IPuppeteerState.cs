namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 傀儡师形态的只读契约。内容模组通过它读取当前形态，不直接依赖 <c>PuppeteerFormManager</c>。
/// </summary>
public interface IPuppeteerState
{
    /// <summary>当前是否处于傀儡形态（墨缇丝）。</summary>
    bool IsPuppetForm { get; }
}
