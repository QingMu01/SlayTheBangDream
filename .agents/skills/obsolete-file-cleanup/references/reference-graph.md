# 引用图：解析规则、实现出处与已知误报

本文是 `scan_orphans.py` 的判定依据说明。**规则与实现必须同源**：下面每条约定都标注了它在 C# / 配置里的出处，实现变更时必须同步改脚本，否则会把有效资源判成孤儿。

## 1. 五条补边（缺一即误删）

| # | 补边 | 为什么必须 | 出处 |
|---|---|---|---|
| 1 | **剥离 `.import` 自引用** | `.import` 的 `source_file="res://…/X.png"` 就是该资源自己的路径。不剥离时「凡有 `.import` 的资源全部被判被引用」→ 整个扫描结构性失效 | 扫描脚本的自引用剥离逻辑 |
| 2 | **剔除自身与自身 sidecar** | 引用来源等于候选自身、`候选+.import`、`候选+.uid` 时一律不计 | 扫描脚本的候选过滤 |
| 3 | **uid 优先 + path 兜底** | 场景引用中既有 path-only 也有 uid/path 并存；只按 uid 找会漏 path-only，只按 path 找不能长期依赖 | uid 三来源：`*.uid` 内容 / `*.import` 的 `uid=` / `*.tscn·*.tres·*.gdshader` 头部 |
| 4 | **跨工程建图** | `ItsCrychic` 字面引用 `res://BangDreamInSpire/scenes/vfx/flying_music_note_default.tscn`（`MultiplayerVfx.MusicNote`），在 ItsCrychic 工程里该文件不存在 | `ItsCrychic/ItsCrychic/skins/*/*.json` |
| 5 | **本体内嵌资源合法** | VFX 场景大量 `ext_resource` 指向 `res://images/**`（本体同步目录、gitignored、运行时由本体 pck 提供）→ **图不在 mod 目录 ≠ 没用到** | `docs/vfx-authoring.md` §4.1 |

## 2. 约定式引用（grep 抓不到，删了静默回退）

| 约定 | 规则 | 实现出处 |
|---|---|---|
| 卡图 | `{namespace 第一段}/images/card_portraits/{类名}.png`，缺失回退 `res://BangDreamInSpire/images/sceneui/default_portrait.png` | `BangDreamInSpire/Scripts/Extensions/PathExtensions.cs` |
| 升级图 | `{类名}_Bate.png`，缺失返回 `null` | 同上 |
| 音乐牌共享图（仅 Saki 侧） | 类名以 `Sakiko` 结尾时优先 `{去掉Sakiko}.png`（`_Bate` 同理），否则回退 `GetCardImg()` | `ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs`；默认入口 `Cards/BandCardModel.cs` |
| 圣物图 | `images/relics/{类名}.png` 与 `images/relics/big/{类名}.png` | `PathExtensions.cs` |
| mod 图标 | `res://{modId}/mod_image.png` | 游戏 `NModInfoContainer` |
| Spine 贴图 | `.atlas` 文本里是**裸文件名**（如 `skeleton.png`），没有 `res://` | 需按 basename 补边；不做会让 Spine png 落入孤儿 |
| 本地化 | 游戏按**本体表文件名**遍历，把 `res://{mod.id}/localization/{lang}/{file}` 并入同名表；文件名不命中本体表名则整文件被静默忽略 | 游戏 `ModManager`、`LocManager` |
| power 图标 | `res://images/powers/{entry}.png`（由 `Id.Entry` 派生） | 游戏 `ModManager` |

**近似匹配必须标为「弱约定」**：卡图共享图规则在实现里是「类名以 `Sakiko` 结尾」的精确后缀判断，脚本若退化成 `startswith` 前缀匹配会变成**超集**，把「图存在但归属别的类」的情况掩盖掉。

## 3. 本地化键的构造与双向盘点

- 运行时键：`new LocString("cards", Id.Entry + ".title")`（游戏 `CardModel`，描述同理）。
- `Id.Entry`：RitsuLib 对**已注册**类型强制 `NORM(modId)_NORM(category)_NORM(TypeName)`（规则实现见 `STS2-RitsuLib` 的 `NormalizePublicStem`；版本以工程 NuGet 引用为准，本地源码仅作参考）。`NORM` 的两条边界正则 `([A-Z]+)([A-Z][a-z])`、`([a-z0-9])([A-Z])`，结果与 `docs/card-authoring.md` §3.1 的 `ITS_CRYCHIC_CARD_<UPPER_SNAKE>` 一致。
- 合法子键：`.title`（必有）、非音乐 `.description`、音乐 `.perform`（非 instant）/ `.instant`（instant）、`ISubsideCard` 用 `.subside`；**非标准子键也存在**：`Cards/Saki/Attack/Desuwa.cs` 手工消费 `.verbal_tic`。
- 双向盘点要同时报 `类存在但本地化缺` 与 `本地化键无对应类`；当前基线以 `audit_cards.py` 输出为准，不在此处写死数字。

## 4. 已知误报来源（每条附仓库实例）

判定「无引用」时，下列十一类必须逐条排除，否则会误删。

| # | 误报来源 | 实例 |
|---|---|---|
| 1 | 特性继承注册 | 角色卡 / Power / Relic 大量零代码引用，但由 `[RegisterX(Inherit = true)]` 活着（`AbstractSakikoCard` 等） |
| 2 | 游戏无条件反射注入 | `ModelDb` 扫模组程序集全部非抽象 `AbstractModel` 子类；无人引用的 Power 仍被实例化 |
| 3 | 反射发现 | 各 `*Mechanic`（`BangDreamInSpireCore` → `BangDreamMechanicRegistry`）、`VfxConsoleCmd`（游戏 `DevConsole`）、数十个 `IPatchMethod` |
| 4 | 场景按 uid/path 引用脚本 | mod 脚本被 `.tscn` 的 `ext_resource type="Script"` 引用（如 `scenes/vfx/music_crystal_hit_vfx.tscn`） |
| 5 | `static class` 扩展按方法名调用 | `PathExtensions`、`AttachedPlayExtensions`、`BangDreamClassExtensions` 等类名零引用 |
| 6 | JSON 字符串引用 | `RolandKeyboard` / `SwordOfLord` / `SakikoSpecialRules` 只被 skin 的 entry 字符串引用 |
| 7 | 仅文档/IDE 命中 | `MusicFlashVfx` 等只被 `.md` 与 `.idea/workspace.xml` 提到；「仅 doc 命中」必须单列 |
| 8 | **共享立绘（最大一类）** | `XSakiko` 用 `X.png` → 按类名逐张找会把共享立绘误判「无对应类」、把有共享图的卡误判「缺立绘」 |
| 9 | 本地化按 entry + 非标准子键 | `HiddenRelic` 把 Title 硬编码为 `gameplay_ui/...EMPTY_LOC_STRING`，故其「无 loc」是假阳性 |
| 10 | 基类是游戏本体类型 | `PathDependencePower` / `RhapsodyRequestPower` / `SheetMusicMoonPower` 等继承游戏 `Temporary*Power`，仓内继承链断裂，只按仓内基类判族会漏 |
| 11 | 可再生缓存与旁路目录 | `.godot/uid_cache.bin`、`**/.godot/editor/filesystem_cache*`、`.git/index`、`BangDreamInSpire/src`、`addons` |

## 5. 动态拼接路径（声明为盲区）

含 `{` 或插值的字符串不得当作引用 token，但删除候选必须人工核对动态加载点：

- `ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs` 里的 `res://{modFolder}` 模板串，曾被旧扫描器当成断链路径写进 `broken.txt`。
- 同类盲区还包括一切 `string.Format` / 插值拼出来的 `res://` 路径。交付时必须显式声明「本次未覆盖动态加载点」。

## 6. 命令速查

```bash
# 四态扫描（脚本入库后）
python3 .agents/skills/obsolete-file-cleanup/scripts/scan_orphans.py --project-root .

# 分档清单
python3 .agents/skills/obsolete-file-cleanup/scripts/cleanup_manifest.py

# sidecar 配对（输出为空即完全配对）
comm -3 \
  <(find BangDreamInSpire/Scripts ItsCrychic/Scripts -name '*.cs'     | sed 's/\.cs$//'      | sort) \
  <(find BangDreamInSpire/Scripts ItsCrychic/Scripts -name '*.cs.uid' | sed 's/\.cs\.uid$//' | sort)

# 被场景以 uid 引用的脚本数
grep -rho 'ext_resource type="Script"[^>]*' --include=*.tscn BangDreamInSpire/BangDreamInSpire ItsCrychic/ItsCrychic | sort -u | wc -l

# skin 的 entry 字符串引用点
grep -rn 'ITS_CRYCHIC_RELIC_\|ITS_CRYCHIC_CARD_' ItsCrychic/ItsCrychic/skins BangDreamInSpire/BangDreamInSpire
```
