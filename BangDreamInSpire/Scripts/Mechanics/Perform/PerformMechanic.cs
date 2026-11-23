using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

/// <summary>
/// 演奏机制：歌单容量、入队/重排/溢出的网络同步、演奏触发、演奏区节点与音乐卡牌能力。
/// 由实现 <see cref="IPerformableCharacter" /> 的角色启用。
/// </summary>
public sealed class PerformMechanic : IBangDreamMechanic
{
    public string Id => "perform";

    public int Order => 200;

    public IReadOnlyList<string> Dependencies => ["extra_deck"];

    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(IPerformableCharacter)];

    public IEnumerable<AbstractModel> InstantiatePlayerState(Player player)
    {
        var manager = (PerformManager)ModelDb.RunModel<PerformManager>().MutableClone();
        manager.Player = player;
        yield return manager;
    }

    public void RegisterContent(BangDreamMechanicContext context)
    {
        BangDreamConst.MusicCardType = RitsuLibFramework.RegisterDynamicEnumValue<CardType>(context.ModId, "Music");
        BangDreamConst.Music = context.RegisterCardKeyword("Music");
        BangDreamConst.Instant = context.RegisterCardKeyword("Instant");
        BangDreamConst.Perform = context.RegisterCardKeyword("Perform");
        BangDreamConst.PerformArea = context.RegisterCardKeyword("PerformArea");
        BangDreamConst.AnySlot = context.RegisterCardKeyword("AnySlot");
        BangDreamConst.RequestGroup = context.RegisterCardKeyword("RequestGroup");
        BangDreamConst.SymbolCard = context.RegisterCardTag("Symbol");

        PerformTargetTypes.EnsureRegistered();
        // 内置元素之印在此登记（含战斗预载）；其它模组的自定义反馈在各自 Initialize 里注册。
        PerformSlotVfxRegistry.RegisterBuiltIns();

        BangDreamConst.PerformPile = context.CardPiles.RegisterOwned("Perform", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.Headless,
            FlightStartPositionResolver = ctx => ResolvePerformPileFlightPosition(ctx.CardModel),
            FlightTargetPositionResolver = ctx => ResolvePerformPileFlightPosition(ctx.CardModel)
        }).PileType;

        PerformManager.InitializeNetwork();

        context.SubscribeLifecycle<ModelRegistryInitializedEvent>(_ =>
        {
            foreach (var cardModel in ModelDb.AllCards.Where(BangDreamCapabilities.IsPerformCard))
            {
                cardModel.GetOrCreateCapability<PerformCapability>();
            }
        });
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatch<MusicCardTypePatch>();
        patcher.RegisterPatch<PerformLifecycleNotPlayerDrivenPatch>();
        patcher.RegisterPatches<PerformSlotTargetingPatches>();
    }

    public IEnumerable<AbstractModel> GetCombatHookModels(Player player)
    {
        yield return player.AttachedData().PerformManager;
    }

    public void SubmitCombatState(Player player)
    {
        player.AttachedData().PerformManager.SubmitCombatState();
    }

    public void UnsubscribeCombatState(Player player)
    {
        player.AttachedData().PerformManager.UnsubscribeCombatState();
    }

    private static Vector2? ResolvePerformPileFlightPosition(CardModel? cardModel)
    {
        if (cardModel == null) return null;

        if (cardModel.Owner.AttachedData().PerformManager.TryGetPerformArea(out var performArea) &&
            performArea.IsInsideTree() && performArea.TryGetCardSlotCenter(cardModel, out var slotCenter))
        {
            return slotCenter;
        }

        return cardModel.Owner.Creature.GetCreatureNode()?.VfxSpawnPosition;
    }
}
