using BangDreamInSpire.Scripts.Utils;
using STS2RitsuLib.Patching.Core;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>全局破绽与弱点击破机制。</summary>
public sealed class BreakMechanic : IBangDreamMechanic
{
    public string Id => "break";

    public int Order => 600;

    public void RegisterContent(BangDreamMechanicContext context)
    {
        // 注册破绽、弱点击破与击破关键字。
        BangDreamConst.WeaknessBreak = context.RegisterCardKeyword("WeaknessBreak");
        BangDreamConst.WeakPointBreak = context.RegisterCardKeyword("WeakPointBreak");
        BangDreamConst.BreakEffect = context.RegisterCardKeyword("BreakEffect");

    }

    public void RegisterPatches(ModPatcher patcher)
    {
        // 仅使用模型钩子与广播。
    }
}
