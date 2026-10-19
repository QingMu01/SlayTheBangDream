# 性能监测（BDS-Perf）

面向「玩家说装了 mod 很卡、作者本机复现不出来」的场景。核心思路：**把判断依据从「作者猜」变成「玩家发一份带时间线的报告」**。

## 1. 先看已有信号（零成本）

游戏本体与 RitsuLib 已经在 `user://logs/godot.log` 里记录了相当多可直接对比的数字，排查时先要这些：

| 日志行 | 含义 |
| --- | --- |
| `[Startup] Time to main menu: 25,959ms` | 启动到主菜单总耗时（本机实测） |
| `Preloading '<name>' assets... count=N vfx=M` / `Complete: assets=N time_elapsed=Xms` | 各预加载会话的资源数与墙钟耗时（`Common` 会随 mod 注入变大） |
| `=== RitsuLib Startup Audit: launch to main menu ===` | RitsuLib 框架自举的分项耗时（`patchAll.Core`、`settingsStore` 等） |
| `Asset not cached: <path>` | 该资源在主线程同步加载（首次卡顿的常见来源） |
| `Threaded load status ... falling back to sync load for <path>` | 线程加载退化为同步加载（玩家机上更常见的尖峰） |
| `[AutoRegister] Processed assembly 'ItsCrychic': N operation(s)` | RitsuLib 在 `ModelDb.Init` 内自动注册的 mod 内容量 |
| `Loading Godot PCK` / `Finished mod initialization for 'X'` | mod 初始化顺序（可与 BDS-Perf 时间线对齐） |

玩家侧收集日志的最短路径：游戏内按 `` ` `` 打开控制台 → 输入 `getlogs 你的名字` → 游戏会在用户数据目录生成 `BugReport-*.zip`（含 `logs/`、存档、截图）并自动打开文件管理器。

> 注意：作者如果是在 Godot 编辑器里跑（日志里出现 `Steam initialization skipped (editor mode)`），会跳过发布版才执行的 `ModelDb.Preload()`、`PrewarmJit()` 等路径，且资源加载方式不同。**复现玩家问题应使用发布版构建**，不要只用编辑器。

## 2. BDS-Perf 做什么

BDS-Perf 是 `BangDreamInSpire` 内的轻量诊断模块（`Scripts/Diagnostics/`），**默认关闭**、开启后只写本地文件、不上传任何数据。

唯一的入口是 **RitsuLib 设置页里的一个开关**（主菜单 / 暂停菜单的 RitsuLib 设置入口 → 本 mod 的「诊断」页 →「性能诊断」），默认关闭。改动**需要重启游戏生效**：模块只在启动时读一次设置，关闭时不会订阅日志、不会注册任何补丁、不会挂采样节点——等于完全不存在，对性能与内存零影响。

采集内容：

1. **启动时间线**：mod 初始化各阶段（Godot 脚本注册、类型发现、各机制注册/补丁、基础设施补丁、公共内容、皮肤 JSON）的耗时；同时捕获 RitsuLib 生命周期事件（框架初始化、ModelDb 初始化、预加载、主菜单就绪）与游戏日志里的关键行，统一成 `T+ms` 时间线。
2. **资源预加载会话**：每个 `Preloading '<name>'` 会话的资源数、mod 注入数、墙钟耗时、**主线程耗时**、`Process` 调用次数、单帧最大耗时。
3. **主线程同步加载**：每次缓存未命中（`AssetCache.LoadAsset`）的路径与单次耗时，区分是否 mod 资源；以及线程加载退化为同步的次数。
4. **帧时间**：每帧一次 `Time.GetTicksUsec` 差值（零分配）与 `GC.GetAllocatedBytesForCurrentThread()` 分配差分，每秒聚合一次 FPS / 平均 / 最大 / 节点数 / orphan 节点 / 新增节点数（SceneTree.NodeAdded，作为「场景实例化批次」的低成本代理）/ draw call / shader 编译增量 / 分配字节 / 显存，并统计 >33ms、>50ms、>100ms 帧数与微卡顿（>2.5× 近期中位数）；同时记录 pipeline 编译总量（Canvas/Surface/Draw/Mesh/Specialization 五类之和）与纹理显存的变化量。**窗口失焦/后台限帧（30fps）的帧会被整体排除**，报告里单独给出被排除的帧数与时长，避免把「切出去挂机」误判成掉帧。`p50/p95/p99` 用**全会话帧直方图**（0–5000ms）计算，不受窗口长度影响。只对超过阈值（默认 50ms，且不低于近 120 帧中位数 2 倍）的长帧记录上下文：**卡住之前与恢复之后**的阶段（精确到 `combat:<遭遇>` / `room:<房间类型>`）、场景、正在进行的资源加载、节点Δ、draw call、pipelineΔ、纹理Δ、**渲染提交耗时 / Present 等待耗时**、GC0Δ/GC2Δ/GC 总暂停、该帧分配字节、托管内存，以及**最近 3 条游戏日志**。写报告时还会扫一次当前场景（如主菜单的 `BackBufferCopy` 数量）。
5. **环境与 mod 清单**：OS/CPU/GPU/内存/屏幕/VSync/FPS 上限、游戏版本、是否编辑器、全部已加载 mod 及版本。

## 3. 报告在哪、怎么发

报告写入 `user://logs/bangdream/`（Windows 为 `%APPDATA%…` 下的用户数据目录，Linux 为 `~/.local/share/SlayTheSpire2/logs/bangdream/`）：

- `perf-latest.md` / `perf-latest.json`：最近一次报告；
- `perf-<yyyyMMdd-HHmmss>.md|json`：历史报告，默认保留 6 份。

自动生成时机：

- 启动到主菜单后 20 秒（仅当开启检测发生在主菜单后 2 分钟内；此时菜单后的 Common 预加载通常已结束）；
- 每局结束；
- 游戏退出（同步写，保证关闭后一定有文件）；
- 周期刷新（默认 180 秒检查一次）**只在最近几个 1Hz 样本出现明显长帧/微卡时才写盘**，安静会话不会反复产生文件。

自动报告在主线程只做数据快照与文本生成，文件写入放到后台串行队列，避免监测自己在启动/掉帧时刻制造磁盘尖峰；退出路径同步写盘。

玩家上报方式（报告开头也会写）：直接发 `perf-latest.md` + `perf-latest.json`，或在游戏内按 `` ` `` 执行 `getlogs 名字`，把生成的 `BugReport-*.zip` 发出来。

> `user://logs/` 会被 `getlogs` **递归打包**，所以报告会自动包含在 `BugReport-*.zip` 里，不需要玩家手动找文件。

## 4. 开关（RitsuLib 设置页）

| 位置 | 项 | 说明 |
| --- | --- | --- |
| RitsuLib 设置 → BangDreamInSpire → 诊断 | 性能诊断 | 默认关闭；打开后**重启游戏**开始检测 |

- 设置存储在 RitsuLib 的 `SaveScope.Global` 数据槽（`syncToCloud: false`），不写入存档、不参与云同步。
- 关闭时：不订阅 `Log.LogCallback`、不订阅生命周期事件、不注册资源探针补丁、不挂帧采样节点、不写任何文件。
- 开启时：启动阶段开始记录时间线；帧采样从挂载后开始；报告在主菜单后 20s / 每局结束 / 退出时写入。

## 5. 作者读报告的顺序

1. **先看「启动时间线」**：`mod:BangDreamInSpire.Initialize` 的总耗时、各 `mechanic:*` 与 `mod:infrastructure-patches`、`mod:BangDreamInSpire.Initialize` 之后到 `main-menu-ready` 之间的大段空白（由 RitsuLib / 本体占用）。
2. **再对齐 `[Startup]` 与 `Preloading` 日志**：`Common` 会话的墙钟与主线程耗时、`mod 注入` 数量；对比玩家与作者本机的 `count`、`time_elapsed`。
3. **看同步加载表**：`mod 资源` 列命中的路径（如 `card_trail_sakiko.tscn`、`strike_sakiko_vfx.tscn`、`sakiko_night.png`）是首次触发尖峰的候选；`Threaded load status ... falling back to sync load` 计数高说明 I/O/磁盘或资源格式在玩家机上更差。
4. **看帧统计口径**：`总帧数` 与 `p50/p95/p99` 都基于**全会话帧直方图**（0–5000ms），已排除窗口失焦/后台限帧的帧；报告里会给出被排除的帧数与时长。

5. **看长帧 Top 20**：按「先看拆分、再看增量」的顺序：
   - `渲染(ms)` = FramePreDraw→FramePostDraw（主线程渲染提交耗时，管线编译/材质构建通常落在这里）；
   - `Present(ms)` = FramePostDraw→下一帧逻辑（GPU 队列/呈现阻塞，纹理上传、GPU 过载、驱动等待通常落在这里）；
   - `GC暂停(ms)` 与 `GC0Δ/GC2Δ`：若暂停时长≈长帧时长，就是托管 GC（GC0 看不到 gen2 停顿，必须看 GC2 + 总暂停）；
   - `pipelineΔ > 0` = 首次管线编译；`纹理Δ(MB)` 大 = 首次纹理上传；`节点(Δ)` = 场景实例化批次；`最近日志` 可对上「刚打出的牌/刚触发的特效」。
   若这些列都为空/接近 0，则卡顿在 `_Process` 之外的线程/驱动层，需要 v2 的函数级 scope 或外部 profiler。
6. **看 1Hz 采样表**：`orphan` 持续上涨提示节点泄漏/重复附加；`新增节点` 在某秒突然变大对应大量 Instantiate（音符批、每 creature 附加、进房）；`pipelineΔ`/`纹理(MB)` 用于确认「新特效首次出现时编译/上传」；`GC暂停(ms)` 用于确认 GC 停顿；`微卡` 与 `>33/>50/>100` 区分「掉帧」与「卡死」。
7. **做 A/B 基线**：需要「不开 mod」的对照时，用启动参数 `--nomods`（配合 Godot 自带的 `--print-fps` 每秒打印 FPS）跑同一路线，和开启诊断时的报告对比 `[Startup] Time to main menu`、`Common` 预加载耗时与战斗阶段平均帧时间；差值才是 mod 的可归因部分。可重复实验用 `--fixed-fps 60 --quit-after N`。

## 6. 归因边界与已知限制

- 报告只能说明「何时卡、卡多久、当时处于哪个阶段/是否在加载什么」，**不能**直接定位到某个 mod 的某次函数调用。需要更细粒度时，下一阶段可对候选方法做 scope 计时或 Harmony 采样（成本更高，暂未内置）。
- 关闭时模块完全不加载（无订阅、无补丁、无节点、无文件写入）；开启后每秒只做一次聚合，对帧率的影响可以忽略（每帧一次 native 计时 + 一次数组写入）。
- 资源探针补丁全部是 optional（`IsCritical=false` + `ignoreIfMissing`）：游戏版本变化导致目标缺失时，只是报告缺该部分数据，不影响 mod 功能。
- 报告会包含硬件信息、mod 列表与游戏日志片段（可能含本地路径），但**只落在本机**；发不发、发哪些由玩家决定。
- 所有 `Δ` 字段都是「相对上一次 1Hz 采样」的增量，不是累计值；「前→后」字段来自上一帧结束时的缓存上下文，用来近似「卡住之前」的状态（`_Process` 在卡住期间不会运行）。
- 帧统计只统计窗口聚焦期间：失焦时游戏主动压到 30fps，这部分帧会被排除并在报告中单列，避免把「挂机」算成卡顿。
- `渲染/Present/GC暂停` 是把长帧拆成「主线程提交 / GPU 呈现 / 托管 GC」三块的近似：Godot 的绘制提交与呈现边界不是严格同步的，结论要结合 `纹理Δ`、`pipelineΔ` 一起看。
- 自动报告写盘在后台线程；极端情况下（玩家在报告生成后立即强杀进程）可能丢失最新一份，但 `perf-latest` 由下一次报告覆盖，历史报告最多丢一份。
- 编辑器与发布版路径不同，见第 1 节；作者侧复现优先发布版。
- 帧采样器在 mod 初始化阶段就会挂到 NGame 上（GameReady/GameTreeEntered 都做了 replay 兜底），覆盖 RitsuLib 自举之后到主菜单、进战斗、房间切换的全过程；mod 初始化之前的引擎/本体阶段由日志里的 `[Startup]` 绝对 tick 提供。
