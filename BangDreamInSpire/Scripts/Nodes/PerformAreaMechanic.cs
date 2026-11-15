using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace BangDreamInSpire.Scripts.Nodes;

/// <summary>
/// 演奏区节点的 UI 侧注册机制。把 <c>NPerformArea</c> 的创建与摆放留在 Nodes 层，
/// 让 <see cref="Mechanics.Perform.PerformMechanic" /> 不再直接引用具体节点。
/// </summary>
public sealed class PerformAreaMechanic : IBangDreamMechanic
{
    public string Id => "perform_area";

    public int Order => 210;

    public IReadOnlyList<string> Dependencies => ["perform"];

    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(IPerformableCharacter)];

    public void RegisterContent(BangDreamMechanicContext context)
    {
        context.NodeAttachments.RegisterReadyChild<NCreature, NPerformArea>(
            "perform_area",
            static creature => NPerformArea.Create(creature.Entity.Player),
            static (creature, area) =>
            {
                area.Visible = BangDreamCapabilities.HasPerform(creature.Entity.Player);
                area.GlobalPosition = ResolvePerformAreaPosition(creature);
            },
            new NodeAttachmentOptions
            {
                Name = "PerformArea",
                Order = 10,
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReplaceExistingByName,
                SetupTiming = NodeAttachmentSetupTiming.AfterAdd,
            });
    }

    public void RegisterPatches(ModPatcher patcher)
    {
    }

    /// <summary>
    /// 战斗开始时把已就绪的演奏区节点回填给 <see cref="PerformManager" />。
    /// 时序与原实现一致：节点在战斗开始前已挂到生物节点上（由 <c>RegisterReadyChild</c> 创建）。
    /// </summary>
    public void SubmitCombatState(Player player)
    {
        var manager = player.AttachedData().PerformManager;
        if (manager.TryGetPerformArea(out _)) return;

        var creatureNode = player.Creature.GetCreatureNode();
        if (creatureNode == null) return;

        if (ModNodeAttachmentRegistry.For(BangDreamConst.ModId)
            .TryGetAttached<NCreature, NPerformArea>(creatureNode, "perform_area", out var areaNode))
        {
            manager.AttachPerformArea(areaNode);
        }
    }

    /// <summary>按角色演奏方案给出的摆放规则定位演奏区域。</summary>
    private static Vector2 ResolvePerformAreaPosition(NCreature creature)
    {
        var scheme = creature.Entity.Player?.Character is IPerformableCharacter performable
            ? performable.CreatePerformScheme()
            : null;
        var placement = scheme?.AreaPlacement ?? new PerformAreaPlacement(PerformAreaAnchor.LeftOfHitbox, 50f);

        return placement.Anchor switch
        {
            PerformAreaAnchor.HitboxCenter => creature.Hitbox.GlobalPosition + creature.Hitbox.Size / 2f,
            _ => creature.VfxSpawnPosition +
                 Vector2.Left * (creature.Hitbox.Size.X / 2 + placement.HorizontalMargin)
        };
    }
}
