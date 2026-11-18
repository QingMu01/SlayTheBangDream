# SlayTheBangDream

《Slay the Spire 2》的 BanG Dream! 企划：一个核心模组 + 一个角色内容模组。

| 子项目 | mod id | 定位 |
|---|---|---|
| [`BangDreamInSpire/`](BangDreamInSpire/) | `BangDreamInSpire` | **核心模组**：通用前置依赖（组件 / 机制 / 特效）+ 新增层级与新怪物 |
| [`ItsCrychic/`](ItsCrychic/) | `ItsCrychic` | **角色内容模组**：Crychic 乐队角色与配套卡牌 / 圣物 / 机制，依赖核心模组 |

## 构建

需要 .NET 9 SDK、Godot 4.5.1（Mono 版）、游戏本体与 [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)。

```bash
# 指向自己的游戏本体（三种方式任选，优先级从高到低）
#   1) 命令行： -p:Sts2Dir=/path/to/Slay\ the\ Spire\ 2
#   2) 本机文件：复制 Directory.Build.local.props.example 为 Directory.Build.local.props 并填写
#   3) 环境变量：STS2_DIR / GODOT_EXE

# 企划总解决方案（两个模组一起构建；仓库里只有这一个 .sln）
dotnet build SlayTheBangDream.sln -c Debug

# 或只构建其中一个
dotnet build BangDreamInSpire/BangDreamInSpire.csproj -c Debug
dotnet build ItsCrychic/ItsCrychic.csproj -c Debug
```

部署到游戏目录时建议用工作区内目录重定向，避免写进 Steam 安装目录。
**重定向 `Sts2Dir` 时必须同时固定 `Sts2DataDir`**（它的默认值由 `Sts2Dir` 派生，否则本体引用会解析到空目录）：

```bash
dotnet build SlayTheBangDream.sln -c Debug \
  -p:Sts2Dir="$PWD/bin/BuildDeploy" \
  -p:Sts2DataDir="$(dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:Sts2DataDir)"
```

打包模组 pck 用 `dotnet publish`：`ExportPck` 目标会先 `--import`，再跑
`tools/check-resource-uids.py` 校验场景 `ext_resource` 的 uid 与 `.import`/`.uid` 实际值，
发现漂移（典型原因：恢复美术时漏了 `.import`，引擎重新生成了新 uid）即中止打包，
最后才 `--export-pack`。

```bash
dotnet publish BangDreamInSpire/BangDreamInSpire.csproj -c Release
python3 tools/check-resource-uids.py   # 也可单独跑，只校验不打包
```

## 资源与仓库边界

三类资源目录的完整约定见 [`AGENTS.md`](AGENTS.md) 的「项目结构」，这里只列新克隆必须做的两件事：

- **本体资源**（`BangDreamInSpire/{images,scenes,animations,fonts,materials,models,shaders,themes,addons/mega_text}`）
  由 `tools/sync-sts2-assets.sh` 从解包源同步，**新克隆必须先跑**：

  ```bash
  tools/sync-sts2-assets.sh
  ```

- **美术资产**（`**/images/**`、`mod_image.png`、Spine 的 `.skel` / `.atlas` / `skeleton.png` 及其 `.import`）
  因分发合规不随仓库分发，需另行获取并放回对应路径，否则编辑器里图片引用显示缺失（**C# 编译不受影响**）。
  不要为了「让仓库完整」把它们加回 git。

## 分支与游戏版本

本体双版本（稳定版 / beta 版）并行期间，`main` 对应稳定版、`beta` 对应测试版。
分支约定、各分支对应的本体版本与解包源见 [`AGENTS.md`](AGENTS.md) 的「分支与游戏版本」。

## 项目规范

开发规范在 [`docs/`](docs/)，可执行工作流在 [`.agents/skills/`](.agents/skills/)：

| 文档 | 内容 |
|---|---|
| [`AGENTS.md`](AGENTS.md) | 仓库结构、硬性约定、分支与版本（改任何东西前先读） |
| [`docs/card-authoring.md`](docs/card-authoring.md) | 卡牌实现规范 |
| [`docs/mechanic-authoring.md`](docs/mechanic-authoring.md) | 机制接入规范 |
| [`docs/vfx-authoring.md`](docs/vfx-authoring.md) | 特效规范；预览机制见 [`docs/vfx-preview-mechanism.md`](docs/vfx-preview-mechanism.md) |
| [`docs/cleanup-authoring.md`](docs/cleanup-authoring.md) | 删除文件前的判定与安全域 |
| [`docs/performance-monitoring.md`](docs/performance-monitoring.md) | 性能诊断（BDS-Perf）报告与读法 |
| [`docs/architecture/coupling-boundaries.md`](docs/architecture/coupling-boundaries.md) | 模块依赖边界与检查规则 |
| [`docs/architecture/music-cardtype-and-skin-isolation.md`](docs/architecture/music-cardtype-and-skin-isolation.md) | 音乐牌 CardType 与皮肤隔离的决策记录 |
| [`docs/cleanup-log.md`](docs/cleanup-log.md) | 已执行清理的追加式历史账本 |
| [`tools/localization.py`](tools/localization.py) | 本地化表校验 / 骨架生成 / 术语表抽取 |

可执行工作流：[卡牌开发](.agents/skills/card-development/SKILL.md) · [特效创作](.agents/skills/vfx-authoring/SKILL.md) · [过时文件清理](.agents/skills/obsolete-file-cleanup/SKILL.md)。规范回答「应该长什么样」，skill 回答「按什么顺序做」。
