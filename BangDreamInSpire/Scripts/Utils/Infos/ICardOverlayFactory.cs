using Godot;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>
/// 卡面覆盖层工厂契约。机制层只通过注册表请求覆盖层，不直接引用具体 UI 节点。
/// </summary>
public interface ICardOverlayFactory
{
    /// <summary>为指定卡牌创建覆盖层节点；返回 <see langword="null" /> 表示不提供。</summary>
    Control? Create(CardModel card);
}
