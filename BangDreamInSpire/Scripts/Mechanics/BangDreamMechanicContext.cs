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

namespace BangDreamInSpire.Scripts.Mechanics;

/// <summary>集中提供 RitsuLib 注册表，避免机制模块直接依赖框架静态调用。</summary>
public sealed class BangDreamMechanicContext(string modId, RunSavedDataStore runData)
{
    public string ModId { get; } = modId;

    public RunSavedDataStore RunData { get; } = runData;

    public ModContentRegistry Content => RitsuLibFramework.GetContentRegistry(ModId);

    public ModKeywordRegistry Keywords => RitsuLibFramework.GetKeywordRegistry(ModId);

    public ModCardTagRegistry CardTags => ModCardTagRegistry.For(ModId);

    public ModCardPileRegistry CardPiles => ModCardPileRegistry.For(ModId);

    public ModRewardRegistry Rewards => ModRewardRegistry.For(ModId);

    public ModSecondaryResourceRegistry SecondaryResources => RitsuLibFramework.GetSecondaryResourceRegistry(ModId);

    public ModNodeAttachmentRegistry NodeAttachments => ModNodeAttachmentRegistry.For(ModId);

    public IDisposable SubscribeLifecycle<TEvent>(Action<TEvent> handler, bool replayCurrentState = true)
        where TEvent : IFrameworkLifecycleEvent
    {
        return RitsuLibFramework.SubscribeLifecycle(handler, replayCurrentState);
    }

    /// <summary>按本地化命名空间约定注册卡牌关键字。</summary>
    public CardKeyword RegisterCardKeyword(string localStem)
    {
        return Keywords.RegisterCardKeywordOwnedByLocNamespace(localStem).CardKeywordValue;
    }

    public CardTag RegisterCardTag(string localStem)
    {
        return CardTags.RegisterOwned(localStem).CardTagValue;
    }
}
