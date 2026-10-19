# 引用图：解析规则、实现出处与已知误报

本文是 `scan_orphans.py` 的判定依据说明。**规则与实现必须同源**：下面每条约定都标注了它在 C# / 配置里的出处，实现变更时必须同步改脚本，否则会把有效资源判成孤儿。

## 1. 五条补边（缺一即误删）

| # | 补边 | 为什么必须 | 出处 |
|---|---|---|---|
| 1 | **剥离 `.import` 自引用** | `.import` 的 `source_file="res://…/X.png"` 就是该资源自己的路径。不剥离时「凡有 `.import` 的资源全部被判被引用」→ 整个扫描结构性失效 | 失败样本 `.vfx_staging/audit/orphan_scan.py`（v1）；修正见同目录 `orphan_scan2.py:76-79`。实例：`ItsCrychic/ItsCrychic/images/card_portraits/DejaVu.png.import` |
| 2 | **剔除自身与自身 sidecar** | 引用来源等于候选自身、`候选+.import`、`候选+.uid` 时一律不计 | `orphan_scan3.py:108-120` |
| 3 | **uid 优先 + path 兜底** | 实测 8582 条 `ext_resource` 中 uid-only **0** 条、path-only **1962 条（22.9%）**、uid/path 不一致 0 条、重复 uid 0 条 | uid 三来源：`*.uid` 内容 / `*.import` 的 `uid=` / `*.tscn|*.tres|*.gdshader` 头部 |
| 4 | **跨工程建图** | `ItsCrychic` 字面引用 `res://BangDreamLib/scenes/vfx/flying_music_note_default.tscn`（`MultiplayerVfx.MusicNote`），在 ItsCrychic 工程里该文件不存在 | `ItsCrychic/ItsCrychic/skins/*/*.json` |
| 5 | **本体内嵌资源合法** | VFX 场景大量 `ext_resource` 指向 `res://images/**`（本体同步目录、gitignored、运行时由本体 pck 提供）→ **图不在 mod 目录 ≠ 没用到** | `docs/vfx-authoring.md:170,186` |

## 2. 约定式引用（grep 抓不到，删了静默回退）

| 约定 | 规则 | 实现出处 |
|---|---|---|
| 卡图 | `{namespace 第一段}/images/card_portraits/{类名}.png`，缺失回退 `res://BangDreamLib/images/sceneui/default_portrait.png` | `BangDreamLib/Scripts/Extensions/PathExtensions.cs:9-15` |
| 升级图 | `{类名}_Bate.png`，缺失返回 `null` | `PathExtensions.cs:17-23` |
| 音乐牌共享图（仅 Saki 侧） | 类名以 `Sakiko` 结尾时优先 `{去掉Sakiko}.png`（`_Bate` 同理），否则回退 `GetCardImg()` | `ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs:22-39`；默认入口 `Cards/BandCardModel.cs:22-26` |
| 圣物图 | `images/relics/{类名}.png` 与 `images/relics/big/{类名}.png` | `PathExtensions.cs:25-39` |
| mod 图标 | `res://{modId}/mod_image.png` | `BangDreamLib/src/Core/Nodes/Screens/ModdingScreen/NModInfoContainer.cs:39` |
| Spine 贴图 | `.atlas` 文本里是**裸文件名**（如 `skeleton.png`），没有 `res://` | 需按 basename 补边；不做会让 8–33 张 Spine png 落入孤儿 |
| 本地化 | 游戏按**本体表文件名**遍历，把 `res://{mod.id}/localization/{lang}/{file}` 并入同名表；文件名不命中本体 46 张表名则整文件被静默忽略 | 游戏 `src/Core/Modding/ModManager.cs:970-978`、`src/Core/Localization/LocManager.cs:409-480` |
| power 图标 | `res://images/powers/{entry}.png`（由 `Id.Entry` 派生） | 游戏 `ModManager.cs:970` |

**近似匹配必须标为「弱约定」**：卡图共享图规则在实现里是「类名以 `Sakiko` 结尾」的精确后缀判断，脚本若退化成 `startswith` 前缀匹配（v3 的做法）会变成**超集**，把「图存在但归属别的类」的情况掩盖掉。

## 3. 本地化键的构造与双向盘点

- 运行时键：`new LocString("cards", Id.Entry + ".title")`（游戏 `src/Core/Models/CardModel.cs:108`，描述 `:127`）。
- `Id.Entry`：RitsuLib 对**已注册**类型强制 `NORM(modId)_NORM(category)_NORM(TypeName)`（参考 `STS2-RitsuLib` tag `v0.5.20` 的 `src/Content/ModContentRegistry.cs`）。`NORM` 的两条边界正则 `([A-Z]+)([A-Z][a-z])`、`([a-z0-9])([A-Z])`，结果与 `docs/card-authoring.md:92-97` 的 `ITS_CRYCHIC_CARD_<UPPER_SNAKE>` 一致。
- 合法子键：`.title`（必有）、非音乐 `.description`、音乐 `.perform`（非 instant）/`.instant`（instant）（`PerformCapability.cs:16-28`）、`ISubsideCard` 用 `.subside`；**非标准子键也存在**：`Cards/Saki/Attack/Desuwa.cs:38` 手工消费 `.verbal_tic`。
- 双向盘点要同时报 `类存在但本地化缺` 与 `本地化键无对应类`。当前基线：卡 127↔127（0 僵尸）、Power 33↔36（僵尸键 3 个）、Relic 3↔3。

## 4. 已知误报来源（每条附仓库实例）

判定「无引用」时，下列十一类必须逐条排除，否则会误删。

| # | 误报来源 | 实例 |
|---|---|---|
| 1 | 特性继承注册 | 127 卡 + 33 Power + 3 Relic 全部零代码引用，但由 `[RegisterX(Inherit = true)]` 活着（`AbstractSakikoCard.cs:9`） |
| 2 | 游戏无条件反射注入 | `ModelDb.cs:81` 扫模组程序集全部非抽象 `AbstractModel` 子类；`RhapsodyRequestPower` 无人引用仍被实例化 |
| 3 | 反射发现 | 5 个 `*Mechanic`（`BangDreamLibCore.cs:45`）、`VfxConsoleCmd`（游戏 `DevConsole.cs:33`）、约 54 个 `IPatchMethod` |
| 4 | 场景按 uid/path 引用脚本 | 29 个 mod 脚本被 `.tscn` 的 `ext_resource type="Script"` 引用（如 `scenes/vfx/music_crystal_hit_vfx.tscn:3`） |
| 5 | `static class` 扩展按方法名调用 | `PathExtensions`、`AttachedPlayExtensions`、`BangDreamClassExtensions` 等类名零引用 |
| 6 | JSON 字符串引用 | `RolandKeyboard` / `SwordOfLord` / `SakikoSpecialRules` 只被 skin 的 entry 字符串引用 |
| 7 | 仅文档/IDE 命中 | `MusicFlashVfx` 等只被 `.md` 与 `.idea/workspace.xml` 提到；「仅 doc 命中」必须单列 |
| 8 | **共享立绘（最大一类）** | `XSakiko` 用 `X.png` → 旧工具把 27 张有效卡图判「无对应类」、24 张卡判「缺立绘」；真实缺图只有 14 张，样本 `.vfx_staging/audit/portraits_report.txt` |
| 9 | 本地化按 entry + 非标准子键 | `HiddenRelic.cs:10` 把 Title 硬编码为 `gameplay_ui/BANG_DREAM_LIB_EMPTY_LOC_STRING`，故 2 个 HiddenRelic「无 loc」是假阳性 |
| 10 | 基类是游戏本体类型 | `PathDependencePower` / `RhapsodyRequestPower` / `SheetMusicMoonPower` / `SheetMusicSunPower` / `ImprisonedXiiDownPower` 继承游戏 `Temporary*Power`，仓内继承链断裂，只按仓内基类判族会漏 |
| 11 | 可再生缓存与旁路目录 | `.godot/uid_cache.bin`、`**/.godot/editor/filesystem_cache*`、`.git/index`、`BangDreamLib/src`（3508 个解包 `.cs`）、`addons` |

## 5. 动态拼接路径（声明为盲区）

含 `{` 或插值的字符串不得当作引用 token，但删除候选必须人工核对动态加载点：

- `ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs` 里的 `res://{modFolder}` 模板串，曾被 v1 当成断链路径写进 `broken.txt`。
- 同类盲区还包括一切 `string.Format` / 插值拼出来的 `res://` 路径。交付时必须显式声明「本次未覆盖动态加载点」。

## 6. 命令速查

```bash
# 四态扫描（脚本入库后）
python3 .agents/skills/obsolete-file-cleanup/scripts/scan_orphans.py --project-root .

# 分档清单
python3 .agents/skills/obsolete-file-cleanup/scripts/cleanup_manifest.py

# sidecar 配对（输出为空即完全配对）
comm -3 \
  <(find BangDreamLib/Scripts ItsCrychic/Scripts -name '*.cs'     | sed 's/\.cs$//'      | sort) \
  <(find BangDreamLib/Scripts ItsCrychic/Scripts -name '*.cs.uid' | sed 's/\.cs\.uid$//' | sort)

# 被场景以 uid 引用的脚本数
grep -rho 'ext_resource type="Script"[^>]*' --include=*.tscn BangDreamLib/BangDreamLib ItsCrychic/ItsCrychic | sort -u | wc -l

# skin 的 entry 字符串引用点
grep -rn 'ITS_CRYCHIC_RELIC_\|ITS_CRYCHIC_CARD_' ItsCrychic/ItsCrychic/skins BangDreamLib/BangDreamLib
```
