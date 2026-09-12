using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace BangDreamLib.Scripts.Mechanics.Perform;

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
        var manager = (PerformManager)ModelDb.Singleton<PerformManager>().MutableClone();
        manager.Player = player;
        yield return manager;
    }

    public void RegisterContent(BangDreamMechanicContext context)
    {
        BangDreamConst.Music = context.RegisterCardKeyword("Music");
        BangDreamConst.Instant = context.RegisterCardKeyword("Instant");
        BangDreamConst.Perform = context.RegisterCardKeyword("Perform");
        BangDreamConst.PerformArea = context.RegisterCardKeyword("PerformArea");
        BangDreamConst.SymbolCard = context.RegisterCardTag("Symbol");

        BangDreamConst.PerformPile = context.CardPiles.RegisterOwned("Perform", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.Headless,
            FlightStartPositionResolver = ctx => ResolvePerformPileFlightPosition(ctx.CardModel),
            FlightTargetPositionResolver = ctx => ResolvePerformPileFlightPosition(ctx.CardModel)
        }).PileType;

        context.NodeAttachments.RegisterReadyChild<NCreature, NPerformArea>(
            "perform_area",
            static creature => NPerformArea.Create(creature.Entity.Player),
            static (creature, area) =>
            {
                area.Visible = creature.Entity.Player is
                    { Character: IPerformableCharacter { GetDefaultCapacity: > 0 } };
                area.GlobalPosition = creature.VfxSpawnPosition + Vector2.Left * (creature.Hitbox.Size.X / 2 + 50f);
            },
            new NodeAttachmentOptions
            {
                Name = "PerformArea",
                Order = 10,
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReplaceExistingByName,
                SetupTiming = NodeAttachmentSetupTiming.AfterAdd,
            });

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

        var performArea = cardModel.Owner.AttachedData().PerformManager.PerformArea;
        if (GodotObject.IsInstanceValid(performArea) && performArea.TryGetCardSlotCenter(cardModel, out var slotCenter))
        {
            return slotCenter;
        }

        return cardModel.Owner.Creature.GetCreatureNode()?.VfxSpawnPosition;
    }
}
