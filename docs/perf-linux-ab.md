# Linux present 路径 A/B 对照实验（tools/perf-linux-ab.sh）

面向「Linux 上偶发长帧/卡顿，怀疑根因在 XWayland / Mutter 的 present 路径」的场景。核心思路：**把「换一个环境变量、换一条合成器路径」变成一条可复现的命令**——启动、等待退出、收集本次 perf 报告、归档、打印摘要、追加一条结果，全部自动化。所有运行模式之间只允许一个变量不同。

背景信号（先读，避免重复调研）：

- Mutter frame callback 节流：<https://gitlab.gnome.org/GNOME/mutter/-/work_items/1636>
- Godot 在 XWayland 下的 present/帧率问题：<https://github.com/godotengine/godot/issues/80550>
- 本机环境：GNOME Wayland + XWayland（`displayServer=X11`）、Mesa RADV、vsync Off / MaxFps 120 / 1920x1080 窗口化 / 90Hz 屏。

perf 报告的生成端（BDS-Perf，字段定义、口径、归因边界）见 [performance-monitoring.md](performance-monitoring.md)；本文只讲**怎么用它做对照实验**。

## 1. 依赖与前置

| 依赖 | 必要性 | 说明 |
| --- | --- | --- |
| `jq` | 建议 | 打印结构化摘要、提取 results.md 数值；缺失时脚本仍会复制报告并写 `meta.txt`，只是摘要变成提示、结果行填 `n/a` |
| `mangohud` | 仅 `mangohud` 模式 | 需要通过 Vulkan implicit layer 生效；脚本会检查 layer 注册文件并告警 |
| `gamescope` | 仅 `gamescope` 模式 | 该模式下缺失会直接报错退出（真跑），不会静默降级 |
| `python3` | 不需要 | 脚本是纯 bash + jq |

前置检查：

1. 游戏为本机原生 Linux 构建，默认安装在
   `~/.local/share/Steam/steamapps/common/Slay the Spire 2`，可执行文件为 `SlayTheSpire2`。
2. 游戏内开启 **BDS-Perf 性能诊断**（RitsuLib 设置 → BangDreamInSpire → 诊断 → 性能诊断），**需要重启游戏生效**；只有开启后才会写 `perf-*.json`。
3. 报告目录默认为 `~/.local/share/SlayTheSpire2/logs/bangdream/`；如果实际不同，用 `--logs-dir` 指定日志根目录（报告在 `<logs-dir>/bangdream/`）。
4. 运行前先正常关闭所有游戏实例（脚本会检测 `SlayTheSpire2` 进程并拒绝启动，`--force` 可强制跳过，但会污染报告归属，不建议）。

## 2. 快速开始

```bash
# 1) 看有哪些模式、每个模式注入什么、怎么判定
tools/perf-linux-ab.sh list

# 2) 先确认命令，不启动游戏（安全，不写任何文件）
tools/perf-linux-ab.sh run baseline --dry-run
tools/perf-linux-ab.sh run fifo --dry-run

# 3) 真跑：脚本启动游戏并等待退出；按固定路线玩一局，然后正常退出
tools/perf-linux-ab.sh run baseline

# 4) 换一个变量再跑，其余一切保持不变
tools/perf-linux-ab.sh run fifo
tools/perf-linux-ab.sh run immediate
```

每个模式跑 **2–3 次**（同一路线、同一存档、同一设置），看中位数而不是单次结果。循环示例：

```bash
for m in baseline fifo mailbox immediate wayland gamescope; do
    tools/perf-linux-ab.sh run "$m" || printf '模式 %s 未拿到报告\n' "$m"
done
```

退出码：`0` = 找到并归档了本次报告；`1` = 没找到新报告（`meta.txt` 仍保留）或启动前的校验失败。

## 3. 模式表

`show <mode>` 会额外打印可直接粘进 Steam「启动选项」的 `%command%` 字符串。

| 模式 | 注入的 env / 参数 | 在测什么 | 期望与判定 |
| --- | --- | --- | --- |
| `baseline` | 无 | 现状对照 | 作为所有比较的基准；先确认它的 `spike_count`、`p99_ms`、`max_ms` 与平时体感一致 |
| `fifo` | `MESA_VK_WSI_PRESENT_MODE=fifo` | 强制 Vulkan FIFO present（传统 vsync 队列） | 帧率被 refresh 限制属预期；若长帧 spike 明显减少 → 默认 present 模式在排队/等待环节出问题 |
| `mailbox` | `MESA_VK_WSI_PRESENT_MODE=mailbox` | 三缓冲/丢旧帧式 present | 理论上阻塞更低；XWayland 下 mailbox 可能被驱动回退，需结合日志与 `present_wait_ms` 确认 |
| `immediate` | `MESA_VK_WSI_PRESENT_MODE=immediate` | 不排队、立即 present | `present_wait_ms` 应显著下降；若 spike 消失但出现撕裂 → 阻塞来自 present 排队；若 `present_wait_ms` 不降，说明问题不在 WSI 等待 |
| `wayland` | `GODOT_WAYLAND_DISABLE_LIBDECOR=1` + `--display-driver wayland` | 绕过 XWayland，直接走 Godot Wayland 后端 | 若 spike 基本消失 → 根因在 XWayland / Mutter frame callback 路径；若窗口起不来，见第 8 节 |
| `gamescope` | `gamescope -W 1920 -H 1080 -f -r 90 -- <游戏>` | 换成 gamescope 嵌套合成器的 present 调度 | 若 spike 消失 → 问题在 Mutter/XWayland 合成器一侧；注意 gamescope 自身也限帧/改分辨率，不要和 `--display-driver` 混用 |
| `mangohud` | `MANGOHUD=1` | 只观测，不改变 present 路径 | 看 GPU/CPU 利用率与频率：若 GPU 空闲而帧卡住 → CPU/合成器等待；同时与 `baseline` 对比确认叠加本身没有显著扰动 |
| `nocache` | `MESA_SHADER_CACHE_DISABLE=true` | 反向对照：禁用 Mesa 磁盘 shader cache | 卡顿显著变多 → 现有 shader cache 正在救场，spike 不是 cache miss 造成；几乎无变化 → shader cache 与本次卡顿无关 |
| `psocache` | `RADV_DEBUG=psocachestats` | 只让 RADV 输出 pipeline(PSO) cache 统计 | 不改善卡顿，用于检查启动/战斗切换处是否有 PSO 未命中（stdout/stderr 会多出统计行） |

单变量原则：一次只换一个模式；不要同时启用 MangoHud 和别的模式（MangoHud 只作为独立观测组）。`wayland` 与 `gamescope` 不要与 Steam 启动选项里已有的 env 叠加。

## 4. 输出在哪、怎么看

每次真跑生成：

```text
<logs-dir>/bangdream/ab/<mode>-<yyyyMMdd-HHmmss>/
├── perf-<...>.json     # 本次运行最新的报告（启动时先记录旧报告，取 mtime 更新的）
├── perf-<...>.md       # 同名可读报告
└── meta.txt            # 模式 / env / 参数 / 完整命令 / 起止时间 / 退出码 / 报告文件名
<logs-dir>/bangdream/ab/results.md  # 每次运行追加一行
```

`results.md` 的列：`时间 | 模式 | session_ms | spike_count | p50_ms | p95_ms | p99_ms | max_ms | 报告目录`。
`n/a` 表示当时没有 jq 或没拿到报告。逐行对比 p99/max/spike_count 的变化，再点进对应目录看 md 报告里的「长帧 Top 20」。

`meta.txt` 每次都写（包括没找到报告的情况），用于事后确认「这一行结果到底是哪次运行、用的什么 env」。

### 4.1 摘要里各字段的含义（与报告口径一致）

| 摘要字段 | 对应 JSON | 说明 |
| --- | --- | --- |
| `session_ms` | `.session_ms` | 会话时长（mod 初始化开始为 0） |
| `spike_count` | `.frames.spike_count` | 超过阈值（默认 50ms 且不低于近期中位数 2×）的长帧数量 |
| `p50/p95/p99` | `.frames.p50_ms` 等 | 全会话帧直方图分位数（已排除失焦/后台限帧帧） |
| `max_ms` | `.frames.max_ms` | 最大单帧 |
| `各 phase` | `.frames.phases` | 每个阶段（`startup` / `main-menu` / `run` / `combat:<遭遇>` / `room:<类型>`）的帧数 / 平均 / 最大 |
| `Top5 spike` | `.frames.spikes[]` | 按 `ms` 降序前 5 条，列 `at_ms / ms / present_wait_ms / render_submit_ms / phase` |

**关键判据**：长帧的 `present_wait_ms` 与 `render_submit_ms` 谁占大头。

- `present_wait_ms` 高、`render_submit_ms` 低 → 引擎已把帧交出去，CPU 卡在**呈现/合成器等待**（本实验的主要假设，对应报告里的「Present(ms)」）。
- `render_submit_ms` 高 → 卡在**主线程渲染提交**（管线编译、材质构建、纹理上传等，对应报告里的「渲染(ms)」）。
- 两者都接近 0 而 `ms` 很大 → 卡在 `_Process` 之外的线程/驱动层，需要外部 profiler，本脚本只能缩小范围。

### 4.2 只关心战斗阶段时

摘要的 Top5 是按全会话取，启动/预加载的长帧通常会占据前几名。需要分阶段统计时，用 `jq` 直接过滤归档报告（示例取 `combat` 阶段长帧）：

```bash
jq -r '.frames.spikes[]
       | select((.phase_after // "") | startswith("combat"))
       | [.at_ms, .ms, .present_wait_ms, .render_submit_ms, .phase_after] | @tsv' \
   ~/.local/share/SlayTheSpire2/logs/bangdream/ab/fifo-*/perf-*.json | sort -k2 -nr | head
```

### 4.3 同时采集逐帧 GPU 时间（可选）

游戏内的 perf 工具支持默认关闭的 GPU 计时。用脚本跑时把它 `export` 到当前 shell 即可被子进程继承（脚本用 `env ...` 启动，不清空父进程环境）：

```bash
export BDS_PERF_GPU_TIMING=1
./tools/perf-linux-ab.sh run baseline
unset BDS_PERF_GPU_TIMING
```

归档报告里的长帧表会多出 `GPU(ms)` 列、1Hz 表会多出 `GPU avg/max(ms)` 列。测量本身有开销，**只在这组实验里开**，并和同模式关闭时的结果对比，别把它当默认配置。判读：长帧 `GPU(ms)` 高 → GPU 真忙；`GPU(ms)` 低而 `Present(×周期)` 高 → 等合成器/驱动。

## 5. 判读顺序（建议照做）

1. 先确认 `baseline` 重复 2–3 次的离散程度：若单次之间 `p99` 波动就很大，任何单次对比都不可信，先加次数。
2. 看 `fifo / mailbox / immediate`：区分「WSI present 排队」与「合成器节流」。`immediate` 是判别力最强的一组（若它让 present 等待消失，假设成立）。
3. 看 `wayland`：验证 XWayland 是否就是差异来源。若 wayland 下 spike 消失，直接指向 Mutter/XWayland present 路径。
4. 看 `gamescope`：交叉验证合成器假设（gamescope 自带调度，绕开 Mutter 的 present 行为）。
5. 看 `nocache` / `psocache`：排除首次加载/管线编译造成的假阳性；若 `nocache` 让情况显著变差，说明其余模式的改善可能只是「shader 已预热」的干扰。
6. `mangohud` 只用于解释现象（GPU/CPU 是否受限），不作为假设检验组。

## 6. Steam 启动选项 vs 脚本直跑

两种方式都支持，但不要混用：

- **Steam 启动选项**：`show <mode>` 打印的 `%command%` 一行，粘到 Steam 库 → 右键游戏 → 属性 → 通用 → 启动选项。这种方式由 Steam 启动，天然带 `SteamAppId`、Steam 覆盖层与 Steam Input；但**不会自动收集报告**，需要手动去日志目录找。
- **脚本直跑**：`run <mode>` 直接在终端启动，脚本额外设置 `SteamAppId=2868840 SteamGameId=2868840`，并在退出后自动归档。缺点是没有 Steam 覆盖层/Steam Input，日志里出现 `Cannot initialize Steam Input ... Falling back to standard input` / `Steamworks is not initialized` 属**预期**，不影响 perf 数据。

无论哪种方式，实验期间都不要再叠加其他环境变量（例如同时设了 `MESA_VK_WSI_PRESENT_MODE` 又设 `vblank_mode`），否则不再是单变量对照。

## 7. 实验纪律（决定结论是否可信）

- **同一路线**：固定存档、固定起点、固定操作（例如「主菜单 → 进同一局 → 打同一场战斗 → 退出」），并在 `meta.txt` 之外自己记录一遍操作。
- **冷/热启动分开**：Mesa shader cache、游戏资源缓存都会影响首帧与首次战斗；每个模式至少一次「冷」（清空/禁用 cache 或换可复现的冷启动条件）会更有说服力。`nocache` 是快速探针，不等价于真正的冷启动。
- **窗口焦点**：BDS-Perf 会排除失焦/后台限帧帧；实验期间不要切走或最小化，否则报告的统计口径会变。
- **报告保留数量**：游戏默认只保留最近 6 份 `perf-*`；脚本的 `ab/<mode>-<ts>/` 归档是**持久的**，需要回看时看归档，不要指望日志目录里的历史文件一直在。
- **报告生成时机**：主菜单后 20 秒、每局结束、周期刷新（有长帧时）、退出时都会写报告。脚本取「比启动时刻更新、mtime 最新」的那份（通常是退出报告）；若你要分析局中数据，去归档目录里手动挑对应文件，或直接用报告里的 1Hz 采样表。
- **一次只跑一个实例**：两个实例会抢同一份 `perf-latest`/历史报告，脚本默认用 `pgrep` 拒绝启动。

## 8. 限制与故障排查

限制：

- 直接启动没有 Steam 覆盖层、Steam Input 与 Steam 成就，属预期差异；若实验需要 Steam 侧行为，请用 `show` 的启动选项方式。
- `GODOT_WAYLAND_DISABLE_LIBDECOR=1` 是否被当前构建识别**未在本机验证**；该模式真正的自变量是 `--display-driver wayland`。如果设置了该变量但行为无变化，属正常，不要据此否定 wayland 结论。
- gamescope 是嵌套合成器，会改变分辨率/刷新/合成路径；它的结论是「合成器侧是否可疑」，不能直接换算成「Mutter 具体哪个函数慢」。
- 脚本只改本机用户目录下的日志文件，不修改游戏安装目录、不删除任何文件；`ab/` 在用户主目录，天然不进仓库（除非显式 `--logs-dir` 指到仓库内）。
- 报告只能到「何时/多久/哪个阶段/是否在加载或编译」粒度，不能定位到函数；要函数级需外部 profiler（见 `performance-monitoring.md` 的归因边界）。

排查：

| 现象 | 处理 |
| --- | --- |
| `未找到比启动时刻更新的 perf 报告` | 确认 BDS-Perf 已开启并重启过；确认 `--logs-dir` 指向正确的日志根目录；确认游戏是正常退出（不是被 kill）；`meta.txt` 已保留 |
| `jq` 缺失导致摘要为 `n/a` | 安装 `jq` 后重新解析归档 json：`jq '.frames | {p50_ms,p95_ms,p99_ms,spike_count}' <json>` |
| `wayland` 模式起不来/闪退 | 当前会话需在 Wayland 下（`echo $WAYLAND_DISPLAY`）；部分 Godot/驱动组合不支持直连，改用 `baseline`/`fifo` 对照组，或试 `--game-dir` 指向的 `launch_vulkan.sh` 方式 |
| `gamescope` 起不来 | 确认在图形会话内运行；`gamescope` 嵌套 Wayland 时可能需要额外参数（本脚本按 1920x1080@90 固定，与实验设置一致） |
| MangoHud 不显示 | 检查 `/usr/share/vulkan/implicit_layer.d/MangoHud.x86_64.json` 是否存在；`mangohud` 版本与 Vulkan 层是否匹配 |
| 报告里 spike 很多但 `present_wait_ms=0` | 卡顿不在 present 等待，优先看 `render_submit_ms`、`pipeline_delta`、`texture_mem_delta` 与「最近日志」 |

## 9. 与仓库其它文档的关系

- 报告生成端、字段口径、开关位置、归因边界：[performance-monitoring.md](performance-monitoring.md)。
- 本脚本只消费报告，不修改生成端；两条实验线的产物都在用户目录，不进入仓库。
