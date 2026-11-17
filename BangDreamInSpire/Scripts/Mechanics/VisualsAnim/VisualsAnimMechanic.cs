using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 角色演出机制：把「角色位移 / 姿态编排」做成**脱手且按生物串行**的时间线，
/// 使带演出的卡牌在动画期间不占动作队列、玩家仍可继续出牌。
/// <para>
/// 与 <c>async_damage</c> 的分工：异步伤害负责「有动画的伤害不卡手」，演出负责「有位移的表现不卡手」。
/// 两者通过 <see cref="VisualsAnimCmd.Callback" /> 组合——演出推进到某个位置时发射一次异步伤害。
/// </para>
/// </summary>
public sealed class VisualsAnimMechanic : IBangDreamMechanic
{
    public string Id => "visuals_anim";

    public int Order => 360;

    /// <summary>含玩法的演出会租 <c>async_damage</c> 的回合结束屏障，故必须在其之后装配。</summary>
    public IReadOnlyList<string> Dependencies => ["async_damage"];

    public void RegisterContent(BangDreamMechanicContext context)
    {
        context.SubscribeLifecycle<CombatEndedEvent>(_ => VisualsAnimTrack.ResetCombat());
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatches<VisualsAnimPatches>();
    }
}
