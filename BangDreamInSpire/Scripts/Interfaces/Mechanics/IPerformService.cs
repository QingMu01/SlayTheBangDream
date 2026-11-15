using System.Collections.Generic;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Enums;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 演奏机制对外的稳定服务接口。内容模组只依赖本接口，不直接引用 <c>PerformManager</c> 实现。
/// </summary>
public interface IPerformService
{
    /// <summary>当前战斗的歌单牌堆。</summary>
    CardPile PerformPile { get; }

    /// <summary>当前已激活的演奏槽位总数。</summary>
    int Capacity { get; }

    /// <summary>全部演奏分组的容量上限之和。</summary>
    int MaxCapacity { get; }

    /// <summary>把卡牌加入歌单并完成进入结算。</summary>
    Task AddInitialCard(CardModel cardModel);

    /// <summary>让指定卡牌在入场动画完成后追加一次即兴演奏。</summary>
    Task TryInstant(CardModel cardModel, PlayerChoiceContext? choiceContext = null);

    /// <summary>让指定卡牌立即执行一次演奏。</summary>
    Task PerformCard(CardModel cardModel, bool isAutoPerform = false);

    /// <summary>按指定顺序重排歌单，并重新触发每张牌的进入效果。</summary>
    Task ReorderAndReenter(PlayerChoiceContext choiceContext, IReadOnlyList<CardModel> orderedCards);

    /// <summary>请求奏响指定槽位。</summary>
    void RequestSlotPerform(int slotIndex, bool isSubsideTriggered);

    /// <summary>请求奏响指定和弦覆盖的全部分组。</summary>
    void RequestChordPerform(PerformChord chord);

    /// <summary>增加演奏容量。</summary>
    void AddCapacity(int amount, PerformChord? chord = null);

    /// <summary>减少演奏容量。</summary>
    void ReduceCapacity(int amount, PerformChord? chord = null);

    /// <summary>读取卡牌上下文记录的槽位索引；尚未进入歌单时返回 -1。</summary>
    int GetSlotIndex(CardModel cardModel);

    /// <summary>把来源卡牌的槽位继承给目标卡牌（用于原地变形/替换）。</summary>
    void InheritSlot(CardModel source, CardModel target);

    /// <summary>移除卡牌的歌单上下文（变形失败等回收路径）。</summary>
    void RemoveCardContext(CardModel cardModel);
}
