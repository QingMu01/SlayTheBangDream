using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Combat.Rewards;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace BangDreamLib.Scripts.Mechanics;

/// <summary>
/// 传递给 <see cref="IBangDreamMechanic.RegisterContent" /> 的注册上下文。
/// 将 RitsuLib 的各注册表收拢为单一入口，使机制模块不直接依赖框架静态调用。
/// </summary>
public sealed class BangDreamMechanicContext(string modId, RunSavedDataStore runData)
{
    /// <summary>当前模组 id。</summary>
    public string ModId { get; } = modId;

    /// <summary>当前模组的 Run 持久化数据存储。</summary>
    public RunSavedDataStore RunData { get; } = runData;

    /// <summary>内容注册表（角色、单例、能力等）。</summary>
    public ModContentRegistry Content => RitsuLibFramework.GetContentRegistry(ModId);

    /// <summary>关键字注册表。</summary>
    public ModKeywordRegistry Keywords => RitsuLibFramework.GetKeywordRegistry(ModId);

    /// <summary>卡牌标签注册表。</summary>
    public ModCardTagRegistry CardTags => ModCardTagRegistry.For(ModId);

    /// <summary>自定义牌堆注册表。</summary>
    public ModCardPileRegistry CardPiles => ModCardPileRegistry.For(ModId);

    /// <summary>自定义奖励注册表。</summary>
    public ModRewardRegistry Rewards => ModRewardRegistry.For(ModId);

    /// <summary>二级资源注册表。</summary>
    public ModSecondaryResourceRegistry SecondaryResources => RitsuLibFramework.GetSecondaryResourceRegistry(ModId);

    /// <summary>玩家节点附加注册表。</summary>
    public ModNodeAttachmentRegistry NodeAttachments => ModNodeAttachmentRegistry.For(ModId);

    /// <summary>订阅 RitsuLib 生命周期事件。</summary>
    public IDisposable SubscribeLifecycle<TEvent>(Action<TEvent> handler, bool replayCurrentState = true)
        where TEvent : IFrameworkLifecycleEvent
    {
        return RitsuLibFramework.SubscribeLifecycle(handler, replayCurrentState);
    }

    /// <summary>以本地化命名空间约定注册卡牌关键字，并返回值对象。</summary>
    public CardKeyword RegisterCardKeyword(string localStem)
    {
        return Keywords.RegisterCardKeywordOwnedByLocNamespace(localStem).CardKeywordValue;
    }

    /// <summary>注册归属本模组的卡牌标签，并返回值对象。</summary>
    public CardTag RegisterCardTag(string localStem)
    {
        return CardTags.RegisterOwned(localStem).CardTagValue;
    }
}
