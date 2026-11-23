using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics;
using BangDreamInSpire.Scripts.Nodes.VFX;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Nodes;

/// <summary>在 Nodes 层注册演奏区。</summary>
public sealed class PerformAreaMechanic : IBangDreamMechanic
{
    public string Id => "perform_area";

    public int Order => 210;

    public IReadOnlyList<string> Dependencies => ["perform"];

    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(IPerformableCharacter)];

    public void RegisterContent(BangDreamMechanicContext context)
    {
        // 演奏区节点由 SubmitCombatState 按需创建；这里只把角色身上的身印加入战斗预载，
        // 避免首次演奏时在主线程同步加载（身印与槽位印框共用元素键）。
        PerformBodyVfx.RegisterPreloads();
    }

    public void RegisterPatches(ModPatcher patcher)
    {
    }

    /// <summary>战斗开始创建并回填演奏区；在玩家专属入口创建，避免为敌人附加节点。</summary>
    public void SubmitCombatState(Player player)
    {
        var manager = player.AttachedData().PerformManager;
        if (manager.TryGetPerformArea(out _)) return;

        var creatureNode = player.Creature.GetCreatureNode();
        // 必须入树后创建，确保槽位容器已初始化。
        if (creatureNode == null || !creatureNode.IsInsideTree()) return;

        var areaNode = NPerformArea.Create(player);
        areaNode.Name = "PerformArea";
        creatureNode.AddChildSafely(areaNode);
        areaNode.Visible = BangDreamCapabilities.HasPerformArea(player);
        areaNode.GlobalPosition = ResolvePerformAreaPosition(creatureNode);
        manager.AttachPerformArea(areaNode);
    }

    /// <summary>按演奏方案定位演奏区。</summary>
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
