using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>脱手且按生物串行的角色演出机制，通过 Callback 组合异步伤害。</summary>
public sealed class VisualsAnimMechanic : IBangDreamMechanic
{
    public string Id => "visuals_anim";

    public int Order => 360;

    /// <summary>在异步伤害机制后装配。</summary>
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
