# 保留清单：禁止删与建议保留

本文件是分档脚本默认保留规则的**人类可读版本**。`cleanup_manifest.py` 内置的 keep 规则必须与这里一致；改这里就要同步改脚本，反之亦然。

## 1. 绝对禁止删（安全域）

| 目标 | 原因 | 依据 |
|---|---|---|
| `BangDreamInSpire/{addons,animations,fonts,images,materials,models,scenes,shaders,themes,src}/**` | 本体同步资源：gitignore、不进 mod pck、编辑器引用它们的**本体 uid**。删了编辑器里所有本体引用显示资源缺失 | `.gitignore:17-25`、`tools/sync-sts2-assets.sh:62`、`BangDreamInSpire/export_presets.cfg:11` |
| `BangDreamInSpire/src/**` | 解包游戏源码（3508 个 `.cs`），是查本体实现的唯一来源 | `AGENTS.md` 依赖文档与源码节 |
| `BangDreamInSpire.json` / `ItsCrychic.json` | mod loader 入口清单，缺了模组不加载 | 各 csproj `Content Include` |
| `bin/spine-godot-extension-*` | 官方 Spine 运行时扩展（44M），删了要重新下载 | `Directory.Build.targets:10-19` |
| `BangDreamInSpire/.godot/{polka_probe.gd,polka_rebuild_check.gd,polka_long.tscn,polka_short.tscn}` | **手写文件却躺在 gitignore 的缓存目录里**。任何「删 `.godot` 重开编辑器」都会连带丢失，应迁出后再删缓存 | 文件 mtime 2026-10-07；`.gitignore:11` |
| `*.import` / `*.uid` | 引擎副产物，但必须随源一起提交；单独删会让场景报 `ext_resource, invalid UID` | `docs/card-authoring.md:21`、`docs/vfx-authoring.md:171-172` |
| 用户工作区里未提交/未跟踪的 WIP | 清理任务没有处置权，误删即不可恢复（本仓库有未跟踪文件在 rebase 中静默消失的前科） | 每次任务前 `git status --short` |

## 2. 建议保留（B 档，必须写理由）

每条保留规则还必须声明**性质**，它决定死簇级联行为（见 `docs/cleanup-authoring.md` 第 5 节）：

| 性质 | 含义 | `cascade_blocks` | 对死簇级联的影响 |
|---|---|---|---|
| **活跃保留** | 文件本身是活的（预览件、模板），只是暂无代码引用 | `True` | 它的出边算活引用，**阻断**级联——被它引用的东西不得判成死簇 |
| **预留待实装** | 文件本来就是「无引用但先别删」（等角色/玩法实装） | `False` | **不阻断**级联——它和被它引用的东西本来就是一条待实装的死簇，keep 只决定落 B |

| 目标 | 性质 | 保留理由 | 何时可以重新评估 |
|---|---|---|---|
| `ItsCrychic/ItsCrychic/images/charui/anon/**`（约 28 张） | 预留待实装 | **未实装角色**的预留素材，不是过时 | 角色实装并接线后 |
| `ItsCrychic/ItsCrychic/scenes/creature/anon01.tscn`、`anon02.tscn` | 预留待实装 | 同上，预留场景 | 同上 |
| `ItsCrychic/ItsCrychic/images/charui/img_{soyo,taki,tomori}*.webp`（3 张） | 预留待实装 | 未实装角色的立绘预留 | 对应角色实装后 |
| `BangDreamInSpire/BangDreamInSpire/scenes/vfx/music_crystal_hit_vfx.tscn` | **活跃保留** | **有意保留的预览件**：有 `.vfx_staging/rec/music_crystal_hit_vfx-*` 录帧记录，等接入卡牌；它引用的 `MusicCrystalHitVfx.cs` 因此是活的 | 接入完成后 |
| `BangDreamInSpire/BangDreamInSpire/skins/skin_template.json` | **活跃保留** | 皮肤模板，供新增角色复制，本身是活的 | 模板机制变更时 |
| `docs/vfx-authoring.md` / `.agents/skills/vfx-authoring/references/csharp-recipes.md` 提到的 `MusicFlashVfx` / `MusicHitVfx` / `MusicWaveVfx` | 活跃保留（代码侧） | 被文档当作 `Node2D` 参考实现引用。若要删，必须**同批改文档**，否则文档指向不存在的代码 | 文档改写后 |

> 上表第 6 行不是 keep 规则：这些是 `.cs` 脚本，靠扫描器的代码保护规则（有基类/接口即不判死）活着，不靠 `cascade_blocks`。机制不同，别混用。

**「建议保留」不是永久豁免**：每条都要写理由与重新评估条件；理由不成立（例如角色已实装但没接线）就应转成功能缺陷单，而不是继续挂在 B 档。

## 3. 需人工确认（C 档，默认落这里）

- 结构性冗余：`SlayTheBangDream.sln`（企划总解决方案）/ `BangDreamInSpire.sln` / `ItsCrychic.sln`、空目录（如 `ItsCrychic/Scripts/Utils/Info/`）。
- 跨工程边界：只在另一个工程被引用的文件（例如 `BangDreamInSpire` 的场景被 `ItsCrychic` 的 skin 引用）。
- 判定不出来的任何东西：**默认 C，不得默认 A**。

## 4. 判定「预留」还是「废件」的口径

问四个问题，任一答不上来就落 C：

1. 它对应的是**已实装**的角色/卡/机制吗？（未实装 → B）
2. 它在文档、计划文件（`.vfx_staging/plans/`）、录帧记录里被承诺过吗？（被承诺 → B）
3. 它是模板、参考实现、或给未来复制用的骨架吗？（是 → B）
4. 删除后会不会让某个**文档或清单**指向不存在的路径？（会 → 先改文档，或落 C）

## 5. 与脚本的对应关系

`cleanup_manifest.py` 内置的默认 keep 规则（可用 `--keep-file` 追加）：

```
images/charui/anon/**
**/anon0*.tscn
images/charui/img_{soyo,taki,tomori}*.webp
scenes/vfx/music_crystal_hit_vfx.tscn
skins/skin_template.json
```

本文件第 1 节的「绝对禁止删」由扫描脚本的搜索域排除实现（本体同步目录、`.godot`、`bin`、`obj`、`.vfx_staging` 根本不进候选集），不依赖 keep 规则。
