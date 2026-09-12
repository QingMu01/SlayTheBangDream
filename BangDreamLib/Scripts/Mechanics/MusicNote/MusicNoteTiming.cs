using Godot;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Mechanics.MusicNote;

/// <summary>
/// 让玩法结算与表现同拍的计时器。与 <c>Cmd.Wait</c> 不同，这里不因 FastMode 或战斗结束判定而跳过等待，
/// 因为音符必须在飞抵目标的那一刻结算；仅在无交互模式（无渲染 / 测试）下直接返回。
/// </summary>
internal static class MusicNoteTiming
{
    internal static async Task WaitAsync(float seconds)
    {
        if (seconds <= 0f || NonInteractiveMode.IsActive)
        {
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            return;
        }

        var timer = sceneTree.CreateTimer(seconds);
        await timer.ToSignal(timer, SceneTreeTimer.SignalName.Timeout).ToTask();
    }
}
