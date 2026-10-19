# AGENTS.md

Slay the Spire 2 模组项目：两个 C# 项目，使用 Godot.NET.Sdk/4.5.1 + Harmony

## 项目结构

```
BangDreamInSpire.sln
├── BangDreamLib/     — 共享前置库（依赖 游戏本体 sts2.dll 与 STS2-RitsuLib）
├── ItsCrychic/       — 角色模组合集（依赖 BangDreamLib）
├── tools/            — sync-sts2-assets.sh（同步本体解包资源）
├── docs/             — 项目级规范（卡牌 / 机制 / 特效）
└── .agents/skills/   — 工作流 skill（card-development、vfx-authoring），已入库
```

C# 代码位于各项目的 `Scripts/` 目录下，Godot 资源（图片、场景、本地化 JSON）位于以 mod-id 命名的子文件夹中（如
`BangDreamLib/BangDreamLib/`、`ItsCrychic/ItsCrychic/`）。

**两类资源目录必须分清**：

- **mod 自己的资源**：`<mod-id>/`（如上），受 git 跟踪，会进模组 pck。
- **本体资源**：`BangDreamLib/{images,scenes,animations,fonts,materials,models,shaders,themes,addons/mega_text}`，
  由 `tools/sync-sts2-assets.sh` 从解包源同步；已被 `.gitignore` 忽略、只服务编辑器、被 `export_presets.cfg`
  的 `exclude_filter` 挡在 pck 之外。场景按**本体 uid**（`res://images/...`）引用它们，**新克隆必须先跑该脚本**，
  否则编辑器里这些引用会显示资源缺失。

## 依赖文档与源码

- **STS2-RitsuLib**：本地源码 `/home/QingMu/Codes/SlayTheSpire/STS2-RitsuLib`（当前 `main` = v0.6.7；
  工程 NuGet 引用 **0.5.20**，读源码时注意版本差）。文档见其 `README.zh.md` 与 `docs/pages/guide/*.md`；
  XML 文档在 NuGet 包内：`~/.nuget/packages/sts2.ritsulib/0.5.20/lib/net9.0/STS2-RitsuLib.xml`。
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

## 本地化

- 当修改Card，Power或者Relic时需要同步修改其本地化文件。
- JSON 文件位于 `BangDreamLib/BangDreamLib/localization/zhs/` 和 `ItsCrychic/ItsCrychic/localization/zhs/`，是运行时加载的资源文件。
- `固有`、`消耗`、`虚无`等关键字由游戏自动添加说明，无需主动在本地化文件中添加。


## 游戏钩子

- 原版游戏提供`Hook`类（`MegaCrit.Sts2.Core.Hooks.Hook`）统一处理游戏事件
- `BangDreamLib` 通过`BangDreamHook`类处理一系列自定义 Hook 接口（在`Scripts/Interfaces/GameHook/` 下）供模组实现自定义游戏行为。
