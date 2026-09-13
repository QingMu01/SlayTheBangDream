using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;

namespace BangDreamLib.Scripts.Mechanics.AsyncDamage;

/// <summary>
/// 异步伤害机制：把「发射动画 → 命中 → 结算」拆成脱手表征与独立同步结算 Action，
/// 使动画驱动的伤害不再阻塞动作队列。游戏内表现为音符、余音等效果的公共底座。
/// </summary>
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
