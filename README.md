# SlayTheBangDream

《Slay the Spire 2》的 BanG Dream! 企划：一个核心模组 + 一个角色内容模组。

| 子项目 | mod id | 定位 |
|---|---|---|
| [`BangDreamInSpire/`](BangDreamInSpire/) | `BangDreamInSpire` | **核心模组**：通用前置依赖（组件 / 机制 / 特效）+ 新增层级与新怪物 |
| [`ItsCrychic/`](ItsCrychic/) | `ItsCrychic` | **角色内容模组**：Crychic 乐队角色与配套卡牌 / 圣物 / 机制，依赖核心模组 |

> mod id 是玩家存档与本地化键的前缀（`BANG_DREAM_IN_SPIRE_*` / `ITS_CRYCHIC_*`）。**改名属于破坏性变更**，会同时打断旧存档与所有依赖声明。

## 构建

需要 .NET 9 SDK、Godot 4.5.1（Mono 版）、游戏本体与 [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)）。

```bash
# 指向自己的游戏本体（三种方式任选，优先级从高到低）
#   1) 命令行： -p:Sts2Dir=/path/to/Slay\ the\ Spire\ 2
#   2) 本机文件：复制 Directory.Build.local.props.example 为 Directory.Build.local.props 并填写
#   3) 环境变量：STS2_DIR / GODOT_EXE

# 企划总解决方案（两个模组一起构建）
dotnet build SlayTheBangDream.sln -c Debug

# 或只构建其中一个
dotnet build BangDreamInSpire/BangDreamInSpire.sln -c Debug
dotnet build ItsCrychic/ItsCrychic.sln -c Debug
```

部署到游戏目录时建议用工作区内目录重定向，避免写进 Steam 安装目录：

```bash
dotnet build SlayTheBangDream.sln -c Debug -p:Sts2Dir="$PWD/bin/BuildDeploy"
```

## 美术资源不入库

出于分发合规，**美术资产不随仓库分发**（`.gitignore` 已显式排除）：

- `**/images/**`、`mod_image.png`、Spine 的 `.skel` / `.atlas` / `skeleton.png` 及其 `.import`

克隆仓库后需要另行获取美术资源并放回对应路径，否则编辑器里图片引用会显示缺失（**C# 编译不受影响**）。

本体资源（`BangDreamInSpire/{images,scenes,animations,fonts,materials,models,shaders,themes,addons/mega_text}`）
同样不入库，由 `tools/sync-sts2-assets.sh` 从解包源同步，**新克隆必须先跑该脚本**：

```bash
tools/sync-sts2-assets.sh
```

## 分支与游戏版本

本体双版本（稳定版 / beta 版）并行期间的分支约定、以及各分支对应的本体版本与解包源，见 [`AGENTS.md`](AGENTS.md) 的「分支与游戏版本」。

## 项目规范

开发规范在 [`docs/`](docs/)，可执行工作流在 [`.agents/skills/`](.agents/skills/)：

| 文档 | 内容 |
|---|---|
| [`AGENTS.md`](AGENTS.md) | 仓库结构与硬性约定（改任何东西前先读） |
| [`docs/card-authoring.md`](docs/card-authoring.md) | 卡牌实现规范 |
| [`docs/mechanic-authoring.md`](docs/mechanic-authoring.md) | 机制接入规范 |
| [`docs/vfx-authoring.md`](docs/vfx-authoring.md) | 特效规范；预览机制见 [`docs/vfx-preview-mechanism.md`](docs/vfx-preview-mechanism.md) |
| [`docs/cleanup-authoring.md`](docs/cleanup-authoring.md) | 删除文件前的判定与安全域 |
