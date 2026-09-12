using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace BangDreamLib.Scripts.Mechanics.Lingered;

/// <summary>
/// 余音机制：余音二级资源与卡面费用 UI、休止卡结算与驱动、环绕资源预览。
/// 由实现 <see cref="ILingeredResourceCharacter" /> 的角色启用。
/// </summary>
public sealed class LingeredMechanic : IBangDreamMechanic
{
    public string Id => "lingered";

    public int Order => 300;

    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(ILingeredResourceCharacter)];

    public IEnumerable<AbstractModel> InstantiatePlayerState(Player player)
    {
        var manager = (LingeredOrbitManager)ModelDb.Singleton<LingeredOrbitManager>().MutableClone();
        manager.Player = player;
        yield return manager;
    }

    public void RegisterContent(BangDreamMechanicContext context)
    {
        BangDreamConst.Lingered = context.RegisterCardKeyword("Lingered");

        BangDreamConst.LingeredResource = context.SecondaryResources.Register("Lingered",
            new SecondaryResourceDefinition(
                defaultAmount: 0,
                baseMaxAmount: 7,
                turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
                persistencePolicy: SecondaryResourcePersistencePolicy.None,
                locTable: "card_keywords",
                titleKey: "BANG_DREAM_LIB_KEYWORD_LINGERED.title",
                descriptionKey: "BANG_DREAM_LIB_KEYWORD_LINGERED.description",
                smallIconPath: "res://BangDreamLib/images/sceneui/xz-energy_lingered_small.png",
                largeIconPath: "res://BangDreamLib/images/sceneui/xz-energy_lingered.png"
            )
            {
                DefaultInsufficientPayment = SecondaryResourceInsufficientPayment.AllowPlay(spendAvailable: false)
            }).Id;

        context.SecondaryResources.RegisterCardUi<NSecondaryResourceCardCostUi>("LingeredCardUi", nCard =>
        {
            var ui = NSecondaryResourceCardCostUi.Create(BangDreamConst.LingeredResource,
                new SecondaryResourceCardCostUiStyle
                {
                    SlotSize = new Vector2(58f, 58f),
                    IconSize = new Vector2(48f, 48f),
                    LabelOffset = new Vector2(-5f, 0f),
                    FontSize = 24,
                    OutlineSize = 10,
                    ReserveVanillaStarCostSlot = true,
                    AffordableOutlineColor = new Color("#664531")
                });
            var energyIcon = nCard.GetNode<TextureRect>("%StarIcon");
            ui.Position = energyIcon.Position + new Vector2(5f, 5f);
            return ui;
        }, ctx => { ctx.Node.Refresh(ctx); });

        context.NodeAttachments.RegisterReadyChild<NCreature, NLingeredOrbitVfx>(
            NLingeredOrbitVfx.AttachmentId,
            static creature => NLingeredOrbitVfx.Create(creature),
            new NodeAttachmentOptions
            {
                Name = NLingeredOrbitVfx.AttachmentName,
                Order = 20,
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReplaceExistingByName,
                SetupTiming = NodeAttachmentSetupTiming.AfterAdd,
            });

        context.Content.RegisterSingleton<LingeredResourcesRule>();

        context.SubscribeLifecycle<ModelRegistryInitializedEvent>(_ =>
        {
            foreach (var cardModel in ModelDb.AllCards)
            {
                if (cardModel is not ISubsideCard subsideCard) continue;

                if (subsideCard.LingeredResourceCost == -1)
                {
                    cardModel.SecondaryCosts()
                        .Set(BangDreamConst.LingeredResource, SecondaryResourceCost.X());
                }
                else
                {
                    cardModel.SecondaryCosts()
                        .Set(BangDreamConst.LingeredResource, subsideCard.LingeredResourceCost);
                }

                cardModel.GetOrCreateCapability<SubsideCapability>();
            }
        });
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatches<LingeredOrbitCardDragPatches>();
    }

    public void SubmitCombatState(Player player)
    {
        player.AttachedData().LingeredOrbitManager.SubmitCombatState();
    }

    public void UnsubscribeCombatState(Player player)
    {
        player.AttachedData().LingeredOrbitManager.UnsubscribeCombatState();
    }
}
