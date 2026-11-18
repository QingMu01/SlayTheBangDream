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
        // 演奏区节点改为在玩家专属的 SubmitCombatState 里按需创建（见该方法说明），
        // 这里没有静态内容要注册。
    }

    public void RegisterPatches(ModPatcher patcher)
    {
    }

    /// <summary>
    /// 战斗开始时为玩家创建演奏区节点并回填给 <see cref="PerformManager" />。
    /// </summary>
    /// <remarks>
    /// 早先这里只做回填，节点由 <c>NodeAttachments.RegisterReadyChild&lt;NCreature, NPerformArea&gt;</c>
    /// 挂在生物节点的 <c>_Ready</c> 上。但该注册的父类型 <see cref="NCreature" /> 敌我共用，
    /// 而 RitsuLib 的工厂必须返回有效节点（返回 <see langword="null" /> 会被判为附加失败并记错误日志），
    /// 无法在工厂里给敌人跳过 —— 结果每只敌人也会白建一个隐藏节点并算一次摆放位置。
    /// 改为在玩家专属的生命周期入口按需创建：管理器本来就只在 <c>AttachPerformArea</c> 之后
    /// 才使用这个节点，因此行为等价，敌人不再产生多余节点。
    /// </remarks>
    public void SubmitCombatState(Player player)
    {
        var manager = player.AttachedData().PerformManager;
        if (manager.TryGetPerformArea(out _)) return;

        var creatureNode = player.Creature.GetCreatureNode();
        // 必须在已进入树时创建：NPerformArea 的槽位容器在 _Ready 里初始化，
        // 未就绪就 SyncSlots 会读到空容器（原实现在生物节点 _Ready 里创建，天然满足该前提）。
        if (creatureNode == null || !creatureNode.IsInsideTree()) return;

        var areaNode = NPerformArea.Create(player);
        areaNode.Name = "PerformArea";
        creatureNode.AddChildSafely(areaNode);
        areaNode.Visible = BangDreamCapabilities.HasPerform(player);
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
