using System;
using Godot;
using HarmonyLib;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// 把手牌资源显示并进 BangDreamInSpire：注册设置页（挂在依赖项下）+ 打补丁。
/// 只对 Bang 系角色生效的判定在 <see cref="HandPipsGate"/>。
/// </summary>
public static class HandPipsBootstrap
{
    private static bool _done;

    public static void Initialize()
    {
        if (_done)
        {
            return;
        }

        _done = true;

        try
        {
            Config.Load(ModEntry.ModDir);
            var harmony = new Harmony(ModEntry.ModId + ".handpips");
            var patched = 0;
            foreach (var type in typeof(HandPipsBootstrap).Assembly.GetTypes())
            {
                // 只打本命名空间下的补丁：避免以后 BDI 里出现 HarmonyPatch 类时被重复打到
                if (type.Namespace != typeof(HandPipsBootstrap).Namespace
                    || type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    patched++;
                }
                catch (Exception e)
                {
                    GD.PushWarning($"[BangDreamInSpire/handpips] patch {type.Name} failed: {e.Message}");
                }
            }

            GD.Print($"[BangDreamInSpire/handpips] ready ({patched} patch classes)");
        }
        catch (Exception e)
        {
            GD.PushError($"[BangDreamInSpire/handpips] init failed: {e}");
        }
    }
}
