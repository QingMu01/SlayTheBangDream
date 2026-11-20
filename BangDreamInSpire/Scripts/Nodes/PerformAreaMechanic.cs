using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Core;

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
        // 演奏区节点由 SubmitCombatState 按需创建，无静态内容需要注册。
    }

    public void RegisterPatches(ModPatcher patcher)
    {
    }

    /// <summary>
    /// 战斗开始时为玩家创建演奏区节点并回填给 <see cref="PerformManager" />。
    /// </summary>
    /// <remarks>
    /// <c>NodeAttachments</c> 的父类型 <see cref="NCreature" /> 敌我共用，工厂也不能返回
    /// <see langword="null" /> 跳过敌人，因此在玩家专属的生命周期入口按需创建，避免为敌人创建隐藏节点。
    /// 管理器只会在 <c>AttachPerformArea</c> 之后使用这个节点。
    /// </remarks>
    public void SubmitCombatState(Player player)
    {
        var manager = player.AttachedData().PerformManager;
        if (manager.TryGetPerformArea(out _)) return;

        var creatureNode = player.Creature.GetCreatureNode();
        // 必须在已进入树时创建：NPerformArea 的槽位容器在 _Ready 里初始化，
        // 未就绪就 SyncSlots 会读到空容器。
        if (creatureNode == null || !creatureNode.IsInsideTree()) return;

        var areaNode = NPerformArea.Create(player);
        areaNode.Name = "PerformArea";
        creatureNode.AddChildSafely(areaNode);
        areaNode.Visible = BangDreamCapabilities.HasPerformArea(player);
        areaNode.GlobalPosition = ResolvePerformAreaPosition(creatureNode);
        manager.AttachPerformArea(areaNode);
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
