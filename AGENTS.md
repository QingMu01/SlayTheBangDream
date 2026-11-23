# AGENTS.md

Slay the Spire 2 模组项目：两个 C# 项目，使用 Godot.NET.Sdk/4.5.1 + Harmony。

## 项目结构

```
SlayTheBangDream.sln          — 企划总解决方案（仓库里唯一的 .sln，两个模组一起构建）
├── BangDreamInSpire/         — 核心模组：前置依赖（组件 / 机制 / 特效）+ 新层级与新怪物
│   └── BangDreamInSpire.csproj
├── ItsCrychic/               — 角色内容模组（祥子、睦、灯、立希、素世），依赖 BangDreamInSpire
│   └── ItsCrychic.csproj
├── tools/                    — 构建前使用的工具（资源 uid 校验、本体资源同步、本地化校验、架构边界检查）
├── docs/                     — 项目级规范（卡牌 / 机制 / 特效 / 清理 / 注释 / 性能 / 架构决策）
└── .agents/skills/           — 工作流 skill（card-development、vfx-authoring、obsolete-file-cleanup），已入库
```

只构建单个模组时用 `dotnet build <项目>/<项目>.csproj`，不要引用不存在的分项目 `.sln`。

企划名 **SlayTheBangDream**；两个 mod 的 id 分别是 `BangDreamInSpire`（核心依赖）与 `ItsCrychic`（角色与内容）。
mod id 是存档与本地化键的前缀（`BANG_DREAM_IN_SPIRE_*` / `ITS_CRYCHIC_*`），**改名等于破坏性变更**，不要随手动。

C# 代码位于各项目的 `Scripts/` 目录下，Godot 资源（图片、场景、本地化 JSON）位于以 mod-id 命名的子文件夹中（如
`BangDreamInSpire/BangDreamInSpire/`、`ItsCrychic/ItsCrychic/`）。

**三类资源目录必须分清**：

- **mod 自己的资源**：`<mod-id>/`（如上），受 git 跟踪，会进模组 pck。
- **美术资产**：`**/images/**`、`mod_image.png`、Spine 的 `.skel` / `.atlas` / `skeleton.png` 及其 `.import`，
  因**分发合规不入库**，只保留在本机。新克隆必须另行获取并放回对应路径，否则编辑器里图片引用显示缺失
  （C# 编译不受影响）。不要为了「让仓库完整」把它们加回 git，`.gitignore` 已显式排除。
- **本体资源**：`BangDreamInSpire/{images,scenes,animations,fonts,materials,models,shaders,themes,addons/mega_text}`，
  由 `tools/sync-sts2-assets.sh` 从解包源同步；已被 `.gitignore` 忽略、只服务编辑器、被 `export_presets.cfg`
  的 `exclude_filter` 挡在 pck 之外。场景按**本体 uid**（`res://images/...`）引用它们，**新克隆必须先跑该脚本**，
  否则编辑器里这些引用会显示资源缺失。

## 分支与游戏版本

本体进入双版本并行期（稳定版 / beta 版，存在 API 变动），分支与目标版本对应如下。**改动前先确认当前分支该对哪个版本**。
两分支目标版本**已不同**（`main` 落后于 `beta`），版本结论不要跨分支套用。

| 分支 | 用途 | 游戏本体 | 解包源 | 备注 |
|---|---|---|---|---|
| `main` | 游戏本体稳定分支发布线 | 0.111.0 | `../sts2unpack/0.111.0/src` | 与本体稳定版 API 对齐 |
| `beta` | 游戏本体测试分支发布线 | 0.113.1 | `../sts2unpack/0.113.0/src` | 与本体测试版 API 对齐；代码里的「0.113 复核结论」均出自此线 |

版本号口径：游戏本体取游戏日志的 `Release Version`（`~/.local/share/SlayTheSpire2/logs/godot.log`，
也可看 `[RitsuLib.Loader] Host=`），解包源取 `../sts2unpack/<目录名>`。两者差一个补丁位是正常的
（0.113.1 本体 / 0.113.0 解包）。

**两个 mod 清单里的版本声明随分支走，不跨分支合并**（与 `Directory.Build.props` 的 `Sts2Dir` 默认值同理，
属分支专属配置）：

| 字段 | `beta` | `main` | 取值依据 |
|---|---|---|---|
| `min_game_version` | `0.113.0` | `0.111.0` | 编译所用解包源的 API 地板值；0.113.x 全系可加载（本体 0.113.1 ≥ 0.113.0） |
| `dependencies[STS2-RitsuLib].min_version` | `0.6.11` | `0.6.10` | 与 `Directory.Build.props` 的 `PackageReference` 同版本 |

改本体/依赖版本时**三处一起对**：`Directory.Build.props` 的 `PackageReference`、两个 mod 的 `min_game_version`、
两个 mod 的 RitsuLib `min_version`。清单里的版本声明只是加载门槛，不会自动跟随编译引用，漏改的表现是
「在旧版本本体/依赖上仍被允许加载，随后在运行期炸 API 不匹配」。

- 版本切换走 `Directory.Build.local.props` 或环境变量（见下节），**不要**改被跟踪的 `Directory.Build.props` 默认值。
- 跨分支同步共享改动时，只合并源码、文档与场景文本；合并后必须在目标版本上重新构建 + 无头运行，不能只看能编译。
- `main` 与 `beta` 长期共存，**不要**用 `feature/*` 承载长期分支。

## 依赖文档与源码

- **本体定位可覆盖**：`Sts2Dir` / `Sts2DataDir` / `GodotExe` 的解析优先级为
  命令行 `-p:` > `Directory.Build.local.props`（本机、不入库）> 环境变量 `STS2_DIR` / `GODOT_EXE` > `Directory.Build.props` 默认值。
  `Sts2DataDir` 默认由 `Sts2Dir` 派生；**把 `Sts2Dir` 重定向到工作区时，必须同时固定 `Sts2DataDir`**，否则本体引用会解析到空目录。
  切换游戏版本时只改本机文件（模板见 `Directory.Build.local.props.example`），避免污染被跟踪配置、避免跨分支合并冲突。
- **STS2-RitsuLib 的版本以 NuGet 编译引用为准**：唯一权威是 `Directory.Build.props` 里的
  `PackageReference Include="STS2.RitsuLib" Version="..."`（当前 `beta` = 0.6.11、`main` = 0.6.10）。
  **不要为了「适配 beta」改这个引用**：上游同时支持本体双版本，分支差异只来自本体 API。
  本地源码与游戏目录 `mods/STS2-RitsuLib/mod_manifest.json` 只作参考，不得据此下版本结论。
  文档见其 `README.zh.md` 与 `docs/pages/guide/*.md`。
  XML 文档在 NuGet 包内且**按程序集分片**：`~/.nuget/packages/sts2.ritsulib/<版本>/lib/net9.0/` 下
  `STS2-RitsuLib.Runtime.xml`（主 API）、`.Ui.xml`、`.Settings.xml`、`.Shared.xml`；
  同目录的 `STS2-RitsuLib.xml` 是**空壳**，查文档别读它。
- **游戏本体**：`sts2.dll` 的同名 XML 文档在本体安装目录
  （`dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:Sts2DataDir`）。
  查本体实现读解包源 `../sts2unpack/<版本>/src`（`main` 用 0.111.0、`beta` 用 0.113.0；可用 `$STS2_UNPACKED` 覆盖，或用
  `tools/sync-sts2-assets.sh -s` 指定）。
- **日志**：当前环境下，游戏日志默认放置于 `~/.local/share/SlayTheSpire2/logs`。

## 开发约定

- **命名空间**与 `Scripts/` 下目录结构完全对应，且第一段等于 mod 目录名（`BangDreamInSpire` / `ItsCrychic`）；卡图路径依赖它，写错会静默回退默认立绘（见 `docs/card-authoring.md` 第 1.1 节）。
- **抽象类命名**：`ItsCrychic` 的角色卡 / 遗物基类用 `Abstract` 前缀（`AbstractSakikoCard` 等，见 `docs/card-authoring.md` 第 1.1 节）；`BangDreamInSpire` 的共享抽象类**不带**前缀（`BandCardModel`、`MusicCardModel`、`BandPowerModel`、`BandRelicModel`、`BandTemporaryPowerModel`、`HiddenRelic`、`BandMemberModel`）。
- 项目全局启用 `<Nullable>enable</Nullable>`、`<LangVersion>13.0</LangVersion>`、`<ImplicitUsings>true</ImplicitUsings>`。
- **代码注释只写「为什么」**：不复述代码与调用顺序，不写「先…再…最后」的分步流程或箭头时间线，不把设计散文
  和工作过程痕迹（本次 / 实测 / 录帧 / 计划 § / 阶段编号）留在注释里。长设计说明写进 `docs/`，代码里留一句摘要 +
  章节指路；同一处行内注释不超过 2 行。判定、正反例与自检三问见 `docs/comment-authoring.md`，写码后按第 4 节自查。
- **特效基类默认继承 `Node2D`**；仅当明确需要「飞行道具」或「异步伤害」（即命中时刻要驱动玩法结算）时才继承 `NBangDreamFlyingVfx`。详见 `docs/vfx-authoring.md` 第 2 节；预览机制见 `docs/vfx-preview-mechanism.md`。
- **新增、修改、重构卡牌**遵循 `docs/card-authoring.md`（规范与验收）；执行流程见 `.agents/skills/card-development/SKILL.md`。
- **特效素材入库**：从资产库导出的素材，先用 `.agents/skills/vfx-authoring/scripts/check_asset_origin.py` 判定——命中本体的**不复制**、直接引用本体 uid；未命中的才复制进 `images/vfx/<特效名>/`。特效目录**不产出** `README.md` / `SOURCES.md`（来源凭据写进计划文件）。详见 `docs/vfx-authoring.md` 第 4 节。
- **临时文件只落 `.scratch/`**：所有中间产物（录帧、预览、素材导出、扫描快照、探针脚本、一次性验证）统一放仓库根
  `.scratch/`，按用途分子目录（`.scratch/vfx/`、`.scratch/cleanup/` 等）。不得在仓库根、`docs/`、脚本目录另建 `tmp` /
  `scratch` / `.tmp` 目录，也不要散落到系统临时目录；需要长期留档的结论写进 `docs/` 或计划文件。该目录已被
  `.gitignore` 忽略、随时可整体删除。
- **删除任何文件前**遵循 `docs/cleanup-authoring.md`（判定与安全域）；执行流程见 `.agents/skills/obsolete-file-cleanup/SKILL.md`。要点：以引用图为唯一判据（不得凭 grep 无命中）、四态分流、三档分档、**签字后才删**、sidecar 与源同生共死、删除后必须残留搜索 + 构建验证。本体同步目录与用户未跟踪的 WIP 一律不碰。
- **模块耦合边界由脚本守**：`python3 tools/check-architecture.py` 检查依赖方向（内容模组只用稳定契约、机制层不直接依赖 UI、核心不出现角色专属标识符），HARD 违规时 exit 1。规则与例外登记见 `docs/architecture/coupling-boundaries.md`；新增例外必须写进脚本 `ALLOWLIST` 并注明原因，收口时用 `--strict`。

## Git 约定

- 一切改动以 `beta` 分支作为基准点，日常开发从 beta 开分支并合回 beta；main 只在发布时从 beta 合并，不直接接收 feature。
- 改动前：确认 `git status` 干净，然后根据任务类型新开一条分支。
- 改动中：按逻辑单元提交，message 写明改了什么、为什么；不改历史、不自动 push。
- 改动后：使用表格汇报变更内容。

## 本地化

- 当修改 Card、Power 或 Relic 时需要同步修改其本地化文件。
- JSON 文件位于 `BangDreamInSpire/BangDreamInSpire/localization/zhs/` 和 `ItsCrychic/ItsCrychic/localization/zhs/`，是运行时加载的资源文件。
- 改完跑 `python3 tools/localization.py check`：校验表名、键集合、占位符与重复键；新增语言用 `stub` 铺骨架。
- `固有`、`消耗`、`虚无`等关键字由游戏自动添加说明，无需主动在本地化文件中添加。

## 游戏钩子

- 原版游戏提供 `Hook` 类（`MegaCrit.Sts2.Core.Hooks.Hook`）统一处理游戏事件。
- `BangDreamInSpire` 通过 `BangDreamHook` 类处理一系列自定义 Hook 接口（在 `Scripts/Interfaces/GameHook/` 下）供模组实现自定义游戏行为。
