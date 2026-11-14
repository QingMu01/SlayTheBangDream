using BangDreamInSpire.Scripts.Utils;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 破绽 / 弱点击破机制：破绽层数、弱点击破状态、击破触发规则与常驻视觉。
/// <para>
/// **全局启用**（不声明 <see cref="RequiredCharacterCapabilities" />）：破绽可以施加给任意单位，
/// 任何角色、敌人与遗物都会用到，因此不绑定角色能力。
/// </para>
/// </summary>
public sealed class BreakMechanic : IBangDreamMechanic
{
    public string Id => "break";

    public int Order => 600;

    public void RegisterContent(BangDreamMechanicContext context)
    {
        // 关键字：破绽 / 弱点击破 / 击破。卡牌文案用「击破：……」句式引用第三个。
        BangDreamConst.WeaknessBreak = context.RegisterCardKeyword("WeaknessBreak");
        BangDreamConst.WeakPointBreak = context.RegisterCardKeyword("WeakPointBreak");
        BangDreamConst.BreakEffect = context.RegisterCardKeyword("BreakEffect");

        // 击破触发规则的战斗钩子实例（每局克隆一份）。
        context.Content.RegisterSingleton<BreakRules>();
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        // 本机制不需要 Harmony 补丁：全部走原版 hook + 自定义广播。
    }
}
