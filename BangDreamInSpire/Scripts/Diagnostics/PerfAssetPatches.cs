using System.Diagnostics;
using MegaCrit.Sts2.Core.Assets;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>
/// 只观测、不改变行为的资源加载探针：记录每个预加载会话的主线程耗时、以及主线程同步加载（缓存未命中）的单次耗时。
/// 所有补丁都是 optional（失败只是少一点数据，不影响游戏与 mod 功能）。
/// </summary>
public sealed class PerfAssetPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<PerfAssetSessionCreatedPatch>();
        patcher.RegisterPatch<PerfAssetSessionProcessPatch>();
        patcher.RegisterPatch<PerfAssetSyncLoadPatch>();
    }
}

/// <summary>记录预加载会话的名字与请求资源数（对应 <c>AssetCache.CreateSession</c>）。</summary>
internal sealed class PerfAssetSessionCreatedPatch : IPatchMethod
{
    public static string PatchId => "bds_perf_asset_session_created";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(AssetCache), nameof(AssetCache.CreateSession),
                [typeof(string), typeof(IEnumerable<string>)], true)
        ];
    }

    public static void Postfix(string name, IEnumerable<string> paths, AssetLoadingSession __result)
    {
        int requested = 0;
        try
        {
            if (!paths.TryGetNonEnumeratedCount(out requested))
            {
                requested = 0;
            }
        }
        catch
        {
            requested = 0;
        }

        PerfTimeline.OnSessionCreated(__result, name, requested);
    }
}

/// <summary>累计每个预加载会话在 <c>Process</c> 里占用的主线程时间。</summary>
internal sealed class PerfAssetSessionProcessPatch : IPatchMethod
{
    public static string PatchId => "bds_perf_asset_session_process";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(AssetLoadingSession), "Process", [], true)
        ];
    }

    public static void Prefix(out long __state)
    {
        __state = Stopwatch.GetTimestamp();
    }

    public static void Postfix(AssetLoadingSession __instance, long __state)
    {
        long usec = (long)((Stopwatch.GetTimestamp() - __state) * 1_000_000.0 / Stopwatch.Frequency);
        PerfTimeline.OnSessionProcess(__instance, usec, __instance.IsCompleted);
    }
}

/// <summary>记录主线程同步加载（<c>AssetCache.LoadAsset</c>，即日志里的 \"Asset not cached\" 路径）的单次耗时。</summary>
internal sealed class PerfAssetSyncLoadPatch : IPatchMethod
{
    public static string PatchId => "bds_perf_asset_sync_load";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(AssetCache), "LoadAsset", [typeof(string)], true)
        ];
    }

    public static void Prefix(out long __state)
    {
        __state = Stopwatch.GetTimestamp();
    }

    public static void Postfix(string path, long __state)
    {
        long usec = (long)((Stopwatch.GetTimestamp() - __state) * 1_000_000.0 / Stopwatch.Frequency);
        PerfTimeline.RecordSyncLoad(path, usec);
    }
}
