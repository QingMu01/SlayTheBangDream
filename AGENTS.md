# AGENTS.md

Slay the Spire 2 模组项目：两个 C# 项目，使用 Godot.NET.Sdk/4.5.1 + Harmony

## 项目结构

```
BangDreamInSpire.sln
├── BangDreamLib/     — 共享前置库（依赖 游戏本体 sts2.dll 与 STS2-RitsuLib）
├── ItsCrychic/       — 角色模组合集（依赖 BangDreamLib）
├── tools/            — sync-sts2-assets.sh（同步本体解包资源）
├── docs/             — 项目级规范（卡牌 / 机制 / 特效 / 清理）
└── .agents/skills/   — 工作流 skill（card-development、vfx-authoring、obsolete-file-cleanup），已入库
```

C# 代码位于各项目的 `Scripts/` 目录下，Godot 资源（图片、场景、本地化 JSON）位于以 mod-id 命名的子文件夹中（如
`BangDreamLib/BangDreamLib/`、`ItsCrychic/ItsCrychic/`）。

**三类资源目录必须分清**：

- **mod 自己的资源**：`<mod-id>/`（如上），受 git 跟踪，会进模组 pck。
- **美术资产**：`**/images/**`、`mod_image.png`、Spine 的 `.skel` / `.atlas` / `skeleton.png` 及其 `.import`，
  因**分发合规不入库**，只保留在本机。新克隆必须另行获取并放回对应路径，否则编辑器里图片引用显示缺失
  （C# 编译不受影响）。不要为了「让仓库完整」把它们加回 git，`.gitignore` 已显式排除。
- **本体资源**：`BangDreamLib/{images,scenes,animations,fonts,materials,models,shaders,themes,addons/mega_text}`，
  由 `tools/sync-sts2-assets.sh` 从解包源同步；已被 `.gitignore` 忽略、只服务编辑器、被 `export_presets.cfg`
  的 `exclude_filter` 挡在 pck 之外。场景按**本体 uid**（`res://images/...`）引用它们，**新克隆必须先跑该脚本**，
  否则编辑器里这些引用会显示资源缺失。

## 分支与游戏版本

本体进入双版本并行期（稳定版 / beta 版，存在 API 变动），分支与目标版本对应如下。**改动前先确认当前分支该对哪个版本**。

| 分支 | 用途 | 游戏本体 | 解包源 | 备注 |
|---|---|---|---|---|
| `main` | 稳定版发布线 | 0.111.0 | `../sts2unpack/0.111.0/src` | 与本体稳定版 API 对齐 |
| `beta` | 本体 beta 版适配线（长期并行） | `0.111.0` | `../sts2unpack/0.111.0/src` | 目前本体双版本为合并状态，暂无新beta版 |

- 版本切换走 `Directory.Build.local.props` 或环境变量（见下节），**不要**改被跟踪的 `Directory.Build.props` 默认值。
- 跨分支同步共享改动时，只合并源码、文档与场景文本；合并后必须在目标版本上重新构建 + 无头运行，不能只看能编译。
- `main` 与 `beta` 长期共存，**不要**用 `feature/*` 承载长期分支。
- **RitsuLib 不按分支锁版本**：上游同时支持本体双版本，工程各分支统一引用同一版本；分支差异只来自**本体 API**，不要为了「适配 beta」去改 `Directory.Build.props` 里的 `PackageReference`。

## 依赖文档与源码

- **本体定位可覆盖**：`Sts2Dir` / `Sts2DataDir` / `GodotExe` 的解析优先级为
  命令行 `-p:` > `Directory.Build.local.props`（本机、不入库）> 环境变量 `STS2_DIR` / `GODOT_EXE` > `Directory.Build.props` 默认值。
  切换游戏版本时只改本机文件（模板见 `Directory.Build.local.props.example`），避免污染被跟踪配置、避免跨分支合并冲突。
- **STS2-RitsuLib**：本地源码 `~/Codes/SlayTheSpire/STS2-RitsuLib`（当前 `main` = v0.6.7；
  工程 NuGet 引用 **0.5.20**，读源码时注意版本差）。文档见其 `README.zh.md` 与 `docs/pages/guide/*.md`；
  XML 文档在 NuGet 包内：`~/.nuget/packages/sts2.ritsulib/0.5.20/lib/net9.0/STS2-RitsuLib.xml`。
  上游同时支持本体双版本，因此**工程统一引用同一版本、不按分支切换**；双版本差异只在游戏本体侧（见「分支与游戏版本」）。
- **游戏本体**：`sts2.dll` 的同名 XML 文档在本体安装目录
  （`cd BangDreamLib && dotnet msbuild BangDreamLib.csproj -getProperty:Sts2DataDir`）。
  查本体实现读解包源 `../sts2unpack/<版本>/src`（当前 0.111.0；可用 `$STS2_UNPACKED` 覆盖，或用
  `tools/sync-sts2-assets.sh -s` 指定）。

## 关键约定

- **命名空间**与 `Scripts/` 下目录结构完全对应，且第一段等于 mod 目录名（`BangDreamLib` / `ItsCrychic`）；卡图路径依赖它，写错会静默回退默认立绘（见 `docs/card-authoring.md` 第 1.1 节）
- **抽象类命名**：`ItsCrychic` 的角色卡 / 圣物基类用 `Abstract` 前缀（`AbstractSakikoCard` 等，见 `docs/card-authoring.md` 第 1.1 节）；`BangDreamLib` 的共享抽象类**不带**前缀（`BandCardModel`、`MusicCardModel`、`BandPowerModel`、`BandRelicModel`、`BandTemporaryPowerModel`、`HiddenRelic`、`BandMemberModel`）
- 项目全局启用 `<Nullable>enable</Nullable>`、`<LangVersion>13.0</LangVersion>`、`<ImplicitUsings>true</ImplicitUsings>`
- 保持程序风格一致
- 执行修改任务前，先定制修改方案再进行修改工作
- 每次改动项目后，以表格的形式列举被修改的文件
- **特效基类默认继承 `Node2D`**；仅当明确需要「飞行道具」或「异步伤害」（即命中时刻要驱动玩法结算）时才继承 `NBangDreamFlyingVfx`。详见 `docs/vfx-authoring.md` 第 2 节；预览机制见 `docs/vfx-preview-mechanism.md`。
- **新增、修改、重构卡牌**遵循 `docs/card-authoring.md`（规范与验收）；执行流程见 `.agents/skills/card-development/SKILL.md`。
- **特效素材入库**：从资产库导出的素材，先用 `.agents/skills/vfx-authoring/scripts/check_asset_origin.py` 判定——命中本体的**不复制**、直接引用本体 uid；未命中的才复制进 `images/vfx/<特效名>/`。特效目录**不产出** `README.md` / `SOURCES.md`（来源凭据写进计划文件）。详见 `docs/vfx-authoring.md` 第 4 节。
- **删除任何文件前**遵循 `docs/cleanup-authoring.md`（判定与安全域）；执行流程见 `.agents/skills/obsolete-file-cleanup/SKILL.md`。要点：以引用图为唯一判据（不得凭 grep 无命中）、四态分流、三档分档、**签字后才删**、sidecar 与源同生共死、删除后必须残留搜索 + 构建验证。本体同步目录与用户未跟踪的 WIP 一律不碰。

## 本地化

- 当修改Card，Power或者Relic时需要同步修改其本地化文件。
- JSON 文件位于 `BangDreamLib/BangDreamLib/localization/zhs/` 和 `ItsCrychic/ItsCrychic/localization/zhs/`，是运行时加载的资源文件。
- `固有`、`消耗`、`虚无`等关键字由游戏自动添加说明，无需主动在本地化文件中添加。


## 游戏钩子

- 原版游戏提供`Hook`类（`MegaCrit.Sts2.Core.Hooks.Hook`）统一处理游戏事件
- `BangDreamLib` 通过`BangDreamHook`类处理一系列自定义 Hook 接口（在`Scripts/Interfaces/GameHook/` 下）供模组实现自定义游戏行为。
