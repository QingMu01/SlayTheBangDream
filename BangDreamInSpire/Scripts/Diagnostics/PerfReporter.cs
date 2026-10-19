using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;

namespace BangDreamInSpire.Scripts.Diagnostics;

/// <summary>
/// 把当前会话的诊断数据写成 Markdown（给人看）+ JSON（给脚本/作者看）两份报告。
/// 报告写在 <c>user://logs/bangdream/</c>：该目录会被游戏本体 <c>getlogs</c> 命令无条件递归打包，
/// 玩家无需了解文件系统就能把数据随日志一起发出来。
/// <para>
/// 主线程只调用 <see cref="Prepare" /> 采集数据并生成文本；<see cref="WriteFiles" /> 只做文件 IO，
/// 自动报告会把它丢到后台线程，避免监测自己制造尖峰。
/// </para>
/// </summary>
internal static class PerfReporter
{
    private const string Schema = "bangdream.perf.v1";

    private static readonly object WriteLock = new();

    public static string ReportDirectory => ProjectSettings.GlobalizePath("user://logs/bangdream");

    /// <summary>主线程：采集快照 + 生成报告文本。文件写入交给 <see cref="WriteFiles" />。</summary>
    public static PerfReportPayload? Prepare(string reason, PerfConfig config, PerfFrameSampler? sampler)
    {
        PerfTimelineSnapshot timeline = PerfTimeline.Snapshot();
        PerfFrameSnapshot? frames = sampler?.Snapshot();
        Dictionary<string, string> environment = CollectEnvironment();
        Dictionary<string, string> settings = CollectSettings();
        List<string> mods = CollectMods();
        Dictionary<string, string> sceneDiagnostics = CollectSceneDiagnostics();

        string directory = ReportDirectory;
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string baseName = $"perf-{stamp}";
        string markdownPath = UniquePath(directory, baseName, ".md");
        string jsonPath = Path.ChangeExtension(markdownPath, ".json");

        (string markdown, string json) = BuildReport(
            reason, config, timeline, frames, environment, settings, mods, sceneDiagnostics);

        var payload = new PerfReportPayload
        {
            Reason = reason,
            Directory = directory,
            MarkdownPath = markdownPath,
            JsonPath = jsonPath,
            Markdown = markdown,
            Json = json,
            RetainReports = PerfSession.RetainReports,
            ElapsedMs = PerfTimeline.ElapsedMs,
            TotalFrames = frames?.TotalFrames ?? 0,
            P95Ms = frames?.P95Ms ?? 0,
            MaxMs = frames?.MaxMs ?? 0,
            SpikeCount = frames?.SpikeCount ?? 0,
            PreloadSessions = timeline.Sessions.Count,
            SyncLoadCount = timeline.SyncLoadCount
        };

        PerfSession.LoggerRef?.Info(
            $"[BDS-Perf] reason={reason} session={payload.ElapsedMs}ms frames={payload.TotalFrames} " +
            $"p95={payload.P95Ms:F1}ms max={payload.MaxMs:F1}ms spikes={payload.SpikeCount} " +
            $"preload={payload.PreloadSessions} syncload={payload.SyncLoadCount} report={payload.MarkdownPath}");

        return payload;
    }

    /// <summary>任意线程：落盘（自动报告在后台线程执行，退出/控制台报告在主线程同步执行）。</summary>
    public static void WriteFiles(PerfReportPayload payload)
    {
        lock (WriteLock)
        {
            Directory.CreateDirectory(payload.Directory);
            File.WriteAllText(payload.MarkdownPath, payload.Markdown, new UTF8Encoding(false));
            File.WriteAllText(payload.JsonPath, payload.Json, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(payload.Directory, "perf-latest.md"), payload.Markdown, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(payload.Directory, "perf-latest.json"), payload.Json, new UTF8Encoding(false));

            try
            {
                Rotate(payload.Directory, payload.RetainReports);
            }
            catch
            {
                // 清理失败不影响报告本身。
            }
        }
    }

    private static string UniquePath(string directory, string baseName, string extension)
    {
        string path = Path.Combine(directory, baseName + extension);
        int suffix = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
            suffix++;
        }

        return path;
    }

    private static (string Markdown, string Json) BuildReport(
        string reason,
        PerfConfig config,
        PerfTimelineSnapshot timeline,
        PerfFrameSnapshot? frames,
        Dictionary<string, string> environment,
        Dictionary<string, string> settings,
        List<string> mods,
        Dictionary<string, string> sceneDiagnostics)
    {
        string markdown = BuildMarkdown(reason, config, timeline, frames, environment, settings, mods, sceneDiagnostics);
        string json = BuildJson(reason, config, timeline, frames, environment, settings, mods, sceneDiagnostics);
        return (markdown, json);
    }

    private static string BuildMarkdown(
        string reason,
        PerfConfig config,
        PerfTimelineSnapshot timeline,
        PerfFrameSnapshot? frames,
        Dictionary<string, string> environment,
        Dictionary<string, string> settings,
        List<string> mods,
        Dictionary<string, string> sceneDiagnostics)
    {
        var builder = new StringBuilder(32 * 1024);
        builder.AppendLine("# BangDreamInSpire 性能诊断报告（BDS-Perf）");
        builder.AppendLine();
        builder.AppendLine($"- 生成原因：`{reason}`");
        builder.AppendLine($"- 生成时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"- 会话时长：{PerfTimeline.ElapsedMs / 1000.0:F1}s（以 mod 初始化开始为 0）");
        builder.AppendLine($"- 引擎已运行：{timeline.EngineOffsetMsec}ms 时 mod 开始初始化");
        builder.AppendLine($"- 采样配置：启用={config.Enabled}，长帧阈值={PerfSession.FrameSpikeThresholdMs}ms");
        builder.AppendLine("- 归因边界：本报告能说明「何时卡、卡多久、处于哪个阶段、当时是否在加载/大量分配/编译 shader」；");
        builder.AppendLine("  不能直接定位到具体函数调用（那不是 v1 的目标）。");
        builder.AppendLine();

        builder.AppendLine("## 0. 怎么把这份报告发给作者");
        builder.AppendLine();
        builder.AppendLine("1. 直接把本文件（以及同目录的 `perf-latest.json`）发给作者；或");
        builder.AppendLine("2. 在游戏内按 `` ` `` 打开控制台，输入 `bdperf zip`（或 `getlogs 你的名字`），");
        builder.AppendLine("   把游戏根目录生成的 `BugReport-*.zip` 发给作者。");
        builder.AppendLine("3. 报告只包含本机运行数据，不会自动上传到任何服务器。");
        builder.AppendLine();

        builder.AppendLine("## 1. 环境");
        builder.AppendLine();
        builder.AppendLine("| 项 | 值 |");
        builder.AppendLine("| --- | --- |");
        foreach (KeyValuePair<string, string> pair in environment)
        {
            builder.AppendLine($"| {Escape(pair.Key)} | {Escape(pair.Value)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 2. 游戏设置与已加载 Mod");
        builder.AppendLine();
        builder.AppendLine("| 项 | 值 |");
        builder.AppendLine("| --- | --- |");
        foreach (KeyValuePair<string, string> pair in settings)
        {
            builder.AppendLine($"| {Escape(pair.Key)} | {Escape(pair.Value)} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"内容注册计数（来自日志）：卡 {timeline.RegisteredCards} / 能力 {timeline.RegisteredPowers} / " +
            $"遗物 {timeline.RegisteredRelics} / 角色 {timeline.RegisteredCharacters}");
        builder.AppendLine();
        builder.AppendLine("已加载 Mod：");
        builder.AppendLine();
        if (mods.Count == 0)
        {
            builder.AppendLine("- （无）");
        }
        else
        {
            foreach (string mod in mods)
            {
                builder.AppendLine($"- {Escape(mod)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 3. 启动/阶段时间线（T+ms）");
        builder.AppendLine();
        builder.AppendLine("| T+ms | 事件 | 耗时(ms) | 说明 |");
        builder.AppendLine("| ---: | --- | ---: | --- |");
        foreach (PerfMark mark in timeline.Marks)
        {
            string duration = mark.DurationMs > 0 ? mark.DurationMs.ToString(CultureInfo.InvariantCulture) : "";
            builder.AppendLine($"| {mark.AtMs} | {Escape(mark.Label)} | {duration} | {Escape(mark.Detail ?? "")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 4. 资源预加载会话");
        builder.AppendLine();
        builder.AppendLine("| 会话 | 请求资源 | mod 注入 | 墙钟(ms) | 主线程(ms) | Process 调用 | 单次最大(ms) | 完成 |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (PreloadSessionRecord session in timeline.Sessions)
        {
            builder.AppendLine(
                $"| {Escape(session.Name)} | {session.Requested} | {session.Injected} | {session.WallMs} | " +
                $"{session.MainThreadMs:F1} | {session.ProcessCalls} | {session.MaxProcessMs:F1} | {session.Completed} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 5. 主线程同步加载（缓存未命中）");
        builder.AppendLine();
        builder.AppendLine(
            $"- 观测到 {timeline.SyncLoadCount} 次同步加载，合计 {timeline.SyncLoadTotalUsec / 1000.0:F1}ms，" +
            $"单次最大 {timeline.SyncLoadMaxUsec / 1000.0:F1}ms；其中 mod 资源 {timeline.SyncLoadModCount} 次。");
        builder.AppendLine(
            $"- 游戏日志 `Asset not cached`：{timeline.NotCachedCount} 条（其中 mod 资源 {timeline.NotCachedModCount} 条）；" +
            $"线程加载退化为同步 {timeline.SyncFallbackCount} 次。");
        builder.AppendLine();
        builder.AppendLine("| 资源路径 | 耗时(ms) | T+ms | mod 资源 |");
        builder.AppendLine("| --- | ---: | ---: | --- |");
        foreach (SyncLoadRecord load in timeline.SyncLoads.OrderByDescending(item => item.Usec).Take(30))
        {
            builder.AppendLine(
                $"| {Escape(load.Path)} | {load.Usec / 1000.0:F1} | {load.AtMs} | {(load.ModOwned ? "是" : "")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 6. 帧时间");
        builder.AppendLine();
        if (frames == null)
        {
            builder.AppendLine("帧采样未启用或尚未启动。");
        }
        else
        {
            builder.AppendLine(
                $"- 总帧数 {frames.TotalFrames}，平均 {frames.AvgMs:F2}ms，p50 {frames.P50Ms:F2}ms，" +
                $"p95 {frames.P95Ms:F2}ms，p99 {frames.P99Ms:F2}ms，最大 {frames.MaxMs:F2}ms，长帧 {frames.SpikeCount} 次。");
            builder.AppendLine(
                $"- 分位数基于全会话帧直方图（0–5000ms，>5000ms 归入末桶，共 {frames.WindowFrames} 帧；已排除失焦帧）；总帧数同样是全会话累计。");
            builder.AppendLine("- 计数器列中的 Δ 表示相对上一次 1Hz 采样的增量；长帧的「前→后」表示卡住前 / 恢复后状态。");
            builder.AppendLine($"- 帧率上限 Engine.MaxFps={Engine.MaxFps}，VSync={DisplayServer.WindowGetVsyncMode()}。");
            if (frames.BackgroundFrames > 0)
            {
                builder.AppendLine(
                    $"- 已排除窗口失焦/后台限帧 {frames.BackgroundFrames} 帧（约 {frames.BackgroundMs / 1000.0:F1}s），不计入以上帧统计。");
            }
            builder.AppendLine();
            builder.AppendLine("### 6.1 分阶段（按 1Hz 聚合）");
            builder.AppendLine();
            builder.AppendLine("| 阶段 | 帧数 | 平均(ms) | 最大(ms) |");
            builder.AppendLine("| --- | ---: | ---: | ---: |");
            foreach (KeyValuePair<string, PhaseFrameStats> pair in frames.PhaseStats)
            {
                builder.AppendLine($"| {Escape(pair.Key)} | {pair.Value.Frames} | {pair.Value.AvgMs:F2} | {pair.Value.MaxMs:F2} |");
            }

            builder.AppendLine();
            builder.AppendLine("### 6.2 长帧 Top 20（含当时上下文）");
            builder.AppendLine();
            builder.AppendLine("| T+ms | 时长(ms) | 阶段(前→后) | 场景(前→后) | 加载(前→后) | 节点(Δ) | DrawCall | pipelineΔ | 纹理Δ(MB) | 渲染(ms) | Present(ms) | GC暂停(ms) | GC0Δ | 分配(KB) | 最近日志 |");
            builder.AppendLine("| ---: | ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
            foreach (FrameSpike spike in frames.Spikes.Take(20))
            {
                builder.AppendLine(
                    $"| {spike.AtMs} | {spike.Ms:F1} | {Escape(Transition(spike.PhaseBefore, spike.PhaseAfter))} | " +
                    $"{Escape(Transition(spike.SceneBefore, spike.SceneAfter))} | " +
                    $"{Escape(Transition(spike.LoadingBefore, spike.LoadingAfter))} | " +
                    $"{spike.Nodes}({Delta(spike.NodeDelta)}) | {spike.DrawCalls} | {spike.PipelineDelta} | " +
                    $"{spike.TextureMemDelta / 1048576.0:F1} | {spike.RenderSubmitUs / 1000.0:F1} | " +
                    $"{spike.PresentWaitUs / 1000.0:F1} | {spike.GcPauseMs:F1} | {Delta(spike.Gc0Delta)} | " +
                    $"{spike.AllocBytes / 1024.0:F1} | {Escape(spike.RecentLog)} |");
            }

            builder.AppendLine();
            builder.AppendLine("### 6.3 每秒采样（最近 30 秒）");
            builder.AppendLine();
            builder.AppendLine("| T+ms | 帧数 | 平均(ms) | 最大(ms) | FPS | 阶段 | 节点 | orphan | DrawCall | pipelineΔ | 纹理(MB) | 分配(KB) | 新增节点 | >33/>50/>100 | 微卡 | GC暂停(ms) |");
            builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: |");
            foreach (SecondSample sample in frames.Seconds.TakeLast(30))
            {
                builder.AppendLine(
                    $"| {sample.AtMs} | {sample.Frames} | {sample.AvgMs:F2} | {sample.MaxMs:F2} | {sample.Fps:F1} | " +
                    $"{Escape(sample.Phase)} | {sample.Nodes} | {sample.OrphanNodes} | {sample.DrawCalls} | " +
                    $"{sample.PipelineDelta} | {sample.TextureMemBytes / 1048576.0:F1} | {sample.AllocBytes / 1024.0:F1} | {sample.NodesAdded} | " +
                    $"{sample.Over33}/{sample.Over50}/{sample.Over100} | {sample.MicroStutters} | {sample.GcPauseMs:F1} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 6.4 当前场景诊断（写报告时）");
        builder.AppendLine();
        if (sceneDiagnostics.Count == 0)
        {
            builder.AppendLine("（无数据）");
        }
        else
        {
            builder.AppendLine("| 项 | 值 |");
            builder.AppendLine("| --- | --- |");
            foreach (KeyValuePair<string, string> pair in sceneDiagnostics)
            {
                builder.AppendLine($"| {Escape(pair.Key)} | {Escape(pair.Value)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 7. 警告/错误");
        builder.AppendLine();
        builder.AppendLine($"- 警告 {timeline.WarnCount} 条，错误 {timeline.ErrorCount} 条。");
        if (timeline.ModWarnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("mod 相关样本：");
            builder.AppendLine();
            foreach (string warning in timeline.ModWarnings)
            {
                builder.AppendLine($"- {Escape(warning)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## 8. 关键日志（过滤后，最近 80 条）");
        builder.AppendLine();
        builder.AppendLine("```text");
        foreach (PerfLogLine line in FilterInterestingLogs(timeline.RecentLogs).TakeLast(80))
        {
            builder.AppendLine($"[+{line.AtMs}ms] [{line.Level}] {line.Text}");
        }

        builder.AppendLine("```");
        return builder.ToString();
    }

    private static string BuildJson(
        string reason,
        PerfConfig config,
        PerfTimelineSnapshot timeline,
        PerfFrameSnapshot? frames,
        Dictionary<string, string> environment,
        Dictionary<string, string> settings,
        List<string> mods,
        Dictionary<string, string> sceneDiagnostics)
    {
        var root = new JsonObject
        {
            ["schema"] = Schema,
            ["generated_at"] = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            ["reason"] = reason,
            ["session_ms"] = PerfTimeline.ElapsedMs,
            ["engine_offset_ms"] = timeline.EngineOffsetMsec,
            ["config"] = new JsonObject
            {
                ["enabled"] = config.Enabled,
                ["frame_spike_threshold_ms"] = PerfSession.FrameSpikeThresholdMs,
                ["periodic_report_seconds"] = PerfSession.PeriodicReportSeconds
            },
            ["environment"] = ToJsonObject(environment),
            ["settings"] = ToJsonObject(settings),
            ["mods"] = ToJsonArray(mods),
            ["content_counts"] = new JsonObject
            {
                ["cards"] = timeline.RegisteredCards,
                ["powers"] = timeline.RegisteredPowers,
                ["relics"] = timeline.RegisteredRelics,
                ["characters"] = timeline.RegisteredCharacters
            },
            ["scene_diagnostics"] = ToJsonObject(sceneDiagnostics),
            ["warnings"] = new JsonObject
            {
                ["warning_count"] = timeline.WarnCount,
                ["error_count"] = timeline.ErrorCount,
                ["mod_samples"] = ToJsonArray(timeline.ModWarnings)
            }
        };

        var markArray = new JsonArray();
        foreach (PerfMark mark in timeline.Marks)
        {
            markArray.Add(new JsonObject
            {
                ["at_ms"] = mark.AtMs,
                ["label"] = mark.Label,
                ["duration_ms"] = mark.DurationMs,
                ["detail"] = mark.Detail ?? ""
            });
        }

        root["timeline"] = markArray;

        var sessionArray = new JsonArray();
        foreach (PreloadSessionRecord session in timeline.Sessions)
        {
            sessionArray.Add(new JsonObject
            {
                ["name"] = session.Name,
                ["requested"] = session.Requested,
                ["injected"] = session.Injected,
                ["start_ms"] = session.StartMs,
                ["end_ms"] = session.EndMs,
                ["wall_ms"] = session.WallMs,
                ["main_thread_ms"] = Math.Round(session.MainThreadMs, 2),
                ["max_process_ms"] = Math.Round(session.MaxProcessMs, 2),
                ["process_calls"] = session.ProcessCalls,
                ["completed"] = session.Completed
            });
        }

        root["preload_sessions"] = sessionArray;

        var syncArray = new JsonArray();
        foreach (SyncLoadRecord load in timeline.SyncLoads.OrderByDescending(item => item.Usec))
        {
            syncArray.Add(new JsonObject
            {
                ["path"] = load.Path,
                ["usec"] = load.Usec,
                ["at_ms"] = load.AtMs,
                ["mod_owned"] = load.ModOwned
            });
        }

        root["sync_loads"] = new JsonObject
        {
            ["count"] = timeline.SyncLoadCount,
            ["mod_count"] = timeline.SyncLoadModCount,
            ["total_ms"] = Math.Round(timeline.SyncLoadTotalUsec / 1000.0, 2),
            ["max_ms"] = Math.Round(timeline.SyncLoadMaxUsec / 1000.0, 2),
            ["not_cached_log_count"] = timeline.NotCachedCount,
            ["not_cached_log_mod_count"] = timeline.NotCachedModCount,
            ["sync_fallback_count"] = timeline.SyncFallbackCount,
            ["items"] = syncArray
        };

        if (frames != null)
        {
            var spikeArray = new JsonArray();
            foreach (FrameSpike spike in frames.Spikes.Take(30))
            {
                spikeArray.Add(new JsonObject
                {
                    ["at_ms"] = spike.AtMs,
                    ["ms"] = Math.Round(spike.Ms, 2),
                    ["phase_before"] = spike.PhaseBefore,
                    ["phase_after"] = spike.PhaseAfter,
                    ["scene_before"] = spike.SceneBefore,
                    ["scene_after"] = spike.SceneAfter,
                    ["loading_before"] = spike.LoadingBefore,
                    ["loading_after"] = spike.LoadingAfter,
                    ["nodes"] = spike.Nodes,
                    ["node_delta"] = spike.NodeDelta,
                    ["draw_calls"] = spike.DrawCalls,
                    ["shader_compile_delta"] = spike.ShaderCompileDelta,
                    ["pipeline_delta"] = spike.PipelineDelta,
                    ["texture_mem_bytes"] = spike.TextureMemBytes,
                    ["texture_mem_delta"] = spike.TextureMemDelta,
                    ["gc0_delta"] = spike.Gc0Delta,
                    ["alloc_bytes"] = spike.AllocBytes,
                    ["managed_bytes"] = spike.ManagedBytes,
                    ["render_submit_ms"] = Math.Round(spike.RenderSubmitUs / 1000.0, 2),
                    ["present_wait_ms"] = Math.Round(spike.PresentWaitUs / 1000.0, 2),
                    ["gc2_delta"] = spike.Gc2Delta,
                    ["gc_pause_ms"] = Math.Round(spike.GcPauseMs, 2),
                    ["recent_log"] = spike.RecentLog
                });
            }

            var secondArray = new JsonArray();
            foreach (SecondSample sample in frames.Seconds)
            {
                secondArray.Add(new JsonObject
                {
                    ["at_ms"] = sample.AtMs,
                    ["frames"] = sample.Frames,
                    ["max_ms"] = Math.Round(sample.MaxMs, 2),
                    ["avg_ms"] = Math.Round(sample.AvgMs, 2),
                    ["fps"] = Math.Round(sample.Fps, 1),
                    ["phase"] = sample.Phase,
                    ["nodes"] = sample.Nodes,
                    ["orphan_nodes"] = sample.OrphanNodes,
                    ["draw_calls"] = sample.DrawCalls,
                    ["static_mem_bytes"] = sample.StaticMemBytes,
                    ["video_mem_bytes"] = sample.VideoMemBytes,
                    ["alloc_bytes"] = sample.AllocBytes,
                    ["nodes_added"] = sample.NodesAdded,
                    ["shader_compiles"] = sample.ShaderCompiles,
                    ["pipeline_delta"] = sample.PipelineDelta,
                    ["texture_mem_bytes"] = sample.TextureMemBytes,
                    ["texture_mem_delta"] = sample.TextureMemDelta,
                    ["over_33ms"] = sample.Over33,
                    ["over_50ms"] = sample.Over50,
                    ["over_100ms"] = sample.Over100,
                    ["micro_stutters"] = sample.MicroStutters,
                    ["gc_pause_ms"] = Math.Round(sample.GcPauseMs, 2),
                    ["gc2_delta"] = sample.Gc2Delta
                });
            }

            var phaseObject = new JsonObject();
            foreach (KeyValuePair<string, PhaseFrameStats> pair in frames.PhaseStats)
            {
                phaseObject[pair.Key] = new JsonObject
                {
                    ["frames"] = pair.Value.Frames,
                    ["avg_ms"] = Math.Round(pair.Value.AvgMs, 2),
                    ["max_ms"] = Math.Round(pair.Value.MaxMs, 2)
                };
            }

            root["frames"] = new JsonObject
            {
                ["total"] = frames.TotalFrames,
                ["avg_ms"] = Math.Round(frames.AvgMs, 2),
                ["p50_ms"] = Math.Round(frames.P50Ms, 2),
                ["p95_ms"] = Math.Round(frames.P95Ms, 2),
                ["p99_ms"] = Math.Round(frames.P99Ms, 2),
                ["max_ms"] = Math.Round(frames.MaxMs, 2),
                ["spike_count"] = frames.SpikeCount,
                ["window_frames"] = frames.WindowFrames,
                ["background_frames"] = frames.BackgroundFrames,
                ["background_ms"] = frames.BackgroundMs,
                ["spikes"] = spikeArray,
                ["seconds"] = secondArray,
                ["phases"] = phaseObject
            };
        }

        var logArray = new JsonArray();
        foreach (PerfLogLine line in FilterInterestingLogs(timeline.RecentLogs).TakeLast(200))
        {
            logArray.Add(new JsonObject
            {
                ["at_ms"] = line.AtMs,
                ["level"] = line.Level.ToString(),
                ["text"] = line.Text
            });
        }

        root["logs"] = logArray;

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, string> CollectEnvironment()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        Try(() => environment["os"] = OS.GetName());
        Try(() => environment["os_version"] = OS.GetVersion());
        Try(() => environment["locale"] = OS.GetLocale());
        Try(() => environment["editor_mode"] = OS.HasFeature("editor").ToString());
        Try(() => environment["modded_session"] = ModManager.IsRunningModded().ToString());
        Try(() => environment["game_version"] = ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "unknown");
        Try(() => environment["cpu"] = OS.GetProcessorName());
        Try(() => environment["cpu_cores"] = OS.GetProcessorCount().ToString(CultureInfo.InvariantCulture));
        Try(() => environment["ram_total_mb"] = MemoryMb("physical"));
        Try(() => environment["ram_available_mb"] = MemoryMb("available"));
        Try(() => environment["gpu"] = RenderingServer.GetVideoAdapterName());
        Try(() => environment["gpu_vendor"] = RenderingServer.GetVideoAdapterVendor());
        Try(() => environment["gpu_driver"] = string.Join(" | ", OS.GetVideoAdapterDriverInfo()));
        Try(() => environment["window"] =
            $"{DisplayServer.WindowGetSize().X}x{DisplayServer.WindowGetSize().Y}");
        Try(() => environment["refresh_rate_hz"] = DisplayServer
            .ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen())
            .ToString("F0", CultureInfo.InvariantCulture));
        Try(() => environment["vsync_mode"] = DisplayServer.WindowGetVsyncMode().ToString());
        Try(() => environment["engine_max_fps"] = Engine.MaxFps.ToString(CultureInfo.InvariantCulture));
        Try(() => environment["static_memory_mb"] =
            (OS.GetStaticMemoryUsage() / 1048576.0).ToString("F1", CultureInfo.InvariantCulture));
        return environment;
    }

    private static Dictionary<string, string> CollectSettings()
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        Try(() => settings["fps_limit"] = SaveManager.Instance.SettingsSave.FpsLimit.ToString(CultureInfo.InvariantCulture));
        Try(() => settings["vsync"] = SaveManager.Instance.SettingsSave.VSync.ToString());
        Try(() => settings["fullscreen"] = SaveManager.Instance.SettingsSave.Fullscreen.ToString());
        Try(() => settings["msaa"] = SaveManager.Instance.SettingsSave.Msaa.ToString(CultureInfo.InvariantCulture));
        Try(() => settings["window_size"] =
            $"{SaveManager.Instance.SettingsSave.WindowSize.X}x{SaveManager.Instance.SettingsSave.WindowSize.Y}");
        Try(() => settings["language"] = SaveManager.Instance.SettingsSave.Language ?? "auto");
        Try(() => settings["gpu_rendering_method"] =
            ProjectSettings.GetSetting("rendering/renderer/rendering_method").ToString());
        Try(() => settings["low_processor_mode"] =
            ProjectSettings.GetSetting("application/run/low_processor_mode").ToString());
        return settings;
    }

    private static List<string> CollectMods()
    {
        var mods = new List<string>();
        try
        {
            foreach (Mod mod in ModManager.GetLoadedMods())
            {
                string id = mod.manifest?.id ?? "unknown";
                string name = mod.manifest?.name ?? id;
                string version = mod.manifest?.version ?? mod.version?.ToString() ?? "unknown";
                string workshop = mod.workshopId.HasValue ? $" workshop:{mod.workshopId.Value}" : "";
                mods.Add($"{id} {version} ({name}){workshop}");
            }
        }
        catch
        {
            mods.Add("（读取 mod 列表失败）");
        }

        return mods;
    }

    /// <summary>写报告时扫一次当前场景，补充「主菜单叠加层」这类只能看节点树的证据。</summary>
    private static Dictionary<string, string> CollectSceneDiagnostics()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            Node? scene = NGame.Instance?.GetTree()?.CurrentScene;
            result["current_scene"] = scene?.Name.ToString() ?? "";
            result["current_scene_path"] = scene?.SceneFilePath ?? "";
            if (scene != null)
            {
                int backBufferCopies = 0;
                foreach (Node node in scene.FindChildren("*", "BackBufferCopy", true, false))
                {
                    _ = node;
                    backBufferCopies++;
                }

                result["back_buffer_copy_count"] = backBufferCopies.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // 场景诊断失败不影响报告其他部分。
        }

        return result;
    }

    private static IEnumerable<PerfLogLine> FilterInterestingLogs(List<PerfLogLine> logs)
    {
        foreach (PerfLogLine line in logs)
        {
            string text = line.Text;
            bool interesting =
                line.Level >= LogLevel.Warn ||
                text.StartsWith("[Startup]", StringComparison.Ordinal) ||
                text.StartsWith("Preloading '", StringComparison.Ordinal) ||
                text.StartsWith("Asset not cached:", StringComparison.Ordinal) ||
                text.StartsWith("[AutoRegister]", StringComparison.Ordinal) ||
                text.StartsWith("Finished mod initialization", StringComparison.Ordinal) ||
                text.Contains("RitsuLib Startup Audit", StringComparison.Ordinal) ||
                text.Contains("RitsuLib exclusive self-time", StringComparison.Ordinal) ||
                (text.Length > 2 && text[0] == ' ' && text.TrimEnd().EndsWith(" ms", StringComparison.Ordinal));

            if (interesting)
            {
                yield return line;
            }
        }
    }

    private static string MemoryMb(string key)
    {
        try
        {
            Godot.Collections.Dictionary info = OS.GetMemoryInfo();
            if (info.TryGetValue(key, out Variant value))
            {
                long bytes = value.AsInt64();
                return bytes <= 0
                    ? "unknown"
                    : (bytes / 1048576.0).ToString("F0", CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // 忽略：不同平台可用字段可能不同。
        }

        return "unknown";
    }

    private static JsonObject ToJsonObject(Dictionary<string, string> values)
    {
        var result = new JsonObject();
        foreach (KeyValuePair<string, string> pair in values)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static void Rotate(string directory, int keep)
    {
        var markdownFiles = Directory.GetFiles(directory, "perf-*.md")
            .Where(path => !Path.GetFileName(path).StartsWith("perf-latest", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        int removeCount = markdownFiles.Count - Math.Max(1, keep);
        for (int i = 0; i < removeCount; i++)
        {
            string markdownPath = markdownFiles[i];
            string? jsonPath = Path.ChangeExtension(markdownPath, ".json");
            try
            {
                File.Delete(markdownPath);
                if (File.Exists(jsonPath))
                {
                    File.Delete(jsonPath);
                }
            }
            catch
            {
                // 单个文件删除失败不影响其他文件。
            }
        }
    }

    private static string Transition(string before, string after)
    {
        if (string.IsNullOrEmpty(after))
        {
            return before;
        }

        if (string.IsNullOrEmpty(before) || string.Equals(before, after, StringComparison.Ordinal))
        {
            return after;
        }

        return $"{before}→{after}";
    }

    private static string Delta(long value)
    {
        return value == 0 ? "0" : value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string text)
    {
        return text
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // 环境信息采集失败不影响报告生成。
        }
    }
}
