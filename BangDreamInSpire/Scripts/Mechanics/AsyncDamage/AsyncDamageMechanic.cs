using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics.AsyncDamage;

/// <summary>将脱手动画与独立同步结算 Action 分离，使命中驱动伤害而不阻塞动作队列。</summary>
public sealed class AsyncDamageMechanic : IBangDreamMechanic
{
    public string Id => "async_damage";

    public int Order => 350;

    public void RegisterContent(BangDreamMechanicContext context)
    {
        AsyncDamageResolveCmd.InitializeNetwork();

        context.SubscribeLifecycle<CombatEndedEvent>(evt =>
        {
            if (evt.CombatState != null)
                CombatAsyncDamageManager.Shared.OnCombatEnded(evt.CombatState);
        });
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatches<AsyncDamagePatches>();
    }
}
