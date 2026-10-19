# 过时文件清理规范

本文是**「某个文件能不能删」的唯一事实来源**。新增、修改、重构、清理任何资源或脚本时都必须满足这里的约束。
执行流程（按什么顺序做、跑哪些脚本、什么算完成）见 `.agents/skills/obsolete-file-cleanup/SKILL.md`；本文只回答「判定长什么样」。

相关文档：卡牌见 `docs/card-authoring.md`，机制见 `docs/mechanic-authoring.md`，特效见 `docs/vfx-authoring.md`。

## 1. 三条铁律

**一、无引用 ≠ 已死，已死 ≠ 可删。** 本仓库有两套模型注册机制（见第 4 节），`AbstractModel` 子类会被游戏无条件实例化。判定只能得出「没有静态引用」这一事实，**删除决策由人签字**。

**二、判定必须凭引用图，不得凭 grep 无命中。** 本仓库存在五种 grep 抓不到的引用形态（见第 3 节）。历史上已经因此出错：`ba7b495` 删了 `perform_glint.png` / `perform_shard.png`，而同提交的 `perform_flash_vfx.tscn` 仍以 uid+path 引用它们，直到 `69e9f96` 才修好。

**三、sidecar 与源文件同生共死。** `X.cs` ↔ `X.cs.uid`、`X.png` ↔ `X.png.import`、`X.gdshader` ↔ `X.gdshader.uid` 一律成对操作；uid 只能由 Godot 生成，**禁止手写或复制伪造**（`docs/card-authoring.md:21`、`docs/vfx-authoring.md:168`）。

## 2. 文件生命周期分类

| 目录 / 类型 | 性质 | 能否按孤儿规则删 | 依据 |
|---|---|---|---|
| `BangDreamLib/{addons,animations,fonts,images,materials,models,scenes,shaders,themes,src}/**` | 本体同步（`tools/sync-sts2-assets.sh` 生成，gitignore） | **禁止**。要重建就整目录跑 sync 脚本 | `.gitignore:17-25`、`tools/sync-sts2-assets.sh:62`、`BangDreamLib/export_presets.cfg:11`、`AGENTS.md` 项目结构节 |
| `BangDreamLib/.godot/**`、`ItsCrychic/.godot/**` | 引擎缓存（可再生） | 默认可删，**但其中有 4 个手写文件必须先迁出**（见 `references/keep-list.md`） | `.gitignore:11` |
| `bin/**` | 构建产物 | 可删，**但 `bin/spine-godot-extension-*` 是官方扩展，删了要重新下载** | `.gitignore:8`、`Directory.Build.targets:10-19` |
| `.vfx_staging/**` | 录帧/导出/计划/审计快照（**临时目录，随时可能被整体清理**） | 可删；扫描快照与签字用的 `decisions.tsv` 都是易失产物，**长期留档只认 `docs/cleanup-log.md`** | `.gitignore:14` |
| `BangDreamLib/BangDreamLib/**`、`ItsCrychic/ItsCrychic/**` | **mod 自有真资源**（入库、进 pck） | 按引用图判定 | 两个 `export_presets.cfg` 未排除 |
| `BangDreamLib/Scripts/**`、`ItsCrychic/Scripts/**` | mod 自有代码（`.cs` + `.cs.uid` 成对） | 按引用图判定 | 各 csproj |
| `*.import` / `*.uid` | 引擎生成，但**必须随源一起提交** | 不可单独删；源删则同批删 | `docs/vfx-authoring.md:171-172` |
| `BangDreamLib.json` / `ItsCrychic.json` | mod loader 入口清单 | 必须保留 | csproj `Content Include` |
| `docs/**`、`tools/**`、`.agents/**` | 工程文档与工具 | 按引用图判定 | —— |

**打包边界不构成过时判据**：两个工程的 `export_presets.cfg` 都是 `export_filter="all_resources"`，mod 自有目录里的文件**即使无人引用也会被无条件打进 pck**。所以「游戏里没出现」不能说明文件过时，只能靠引用图。

## 3. 引用关系的六种形态

判定「被引用」时**三选一命中即算被引用**（字面路径 / uid / 显式约定规则），下面六种形态必须全部覆盖：

| # | 形态 | 例子 | 抓不到会怎样 |
|---|---|---|---|
| 1 | **字面 `res://` 路径** | `ext_resource ... path="res://ItsCrychic/..."` | —— |
| 2 | **uid 引用** | `uid="uid://by15p1sy5ryvu"` | 实测 8582 条 `ext_resource` 中 uid-only 为 **0** 条，path-only 有 **1962 条（22.9%）** → 只按 uid 找会漏 23%，只按 path 找当前不漏但不能长期依赖 |
| 3 | **命名约定引用** | 卡图 `<类名>.png`、升级图 `_Bate.png`、音乐牌共享图（`XSakiko` → `X.png`）、圣物图、`mod_image.png`、`.atlas` 内的裸文件名 | **删了不报错，只静默回退默认立绘**——最危险的一类 |
| 4 | **跨工程引用** | `ItsCrychic` 里字面引用 `res://BangDreamLib/scenes/vfx/flying_music_note_default.tscn`（`MultiplayerVfx.MusicNote`） | 只看单工程会误判为断链/孤儿 |
| 5 | **JSON 字符串引用** | `skins/*/*.json` 的 `StartingRelics` / 牌组用 entry 字符串（如 `ITS_CRYCHIC_RELIC_SAKIKO_SPECIAL_RULES`） | 删 Relic/卡后 skin 挂空 |
| 6 | **反射 / 特性注册** | 见第 4 节 | 删了可能让游戏启动崩或功能静默失效 |

各形态的解析规则、实现出处与已知误报，见 `references/reference-graph.md`。

## 4. 注册机制：为什么「无引用」不能证明类死了

模型类走**双轨**注册，缺一不可：

1. **RitsuLib 特性继承注册**：`AbstractSakikoCard.cs:9`（`[RegisterCard(typeof(SakikoStandardCardPool), Inherit = true)]`）、`AbstractMutsumiCard.cs:9`、`AbstractSakikoMusicCard.cs:11`、`BandPowerModel.cs:8`、`BandTemporaryPowerModel.cs:6`、`AbstractSakikoRelic.cs:7`、`AbstractMutsumiRelic.cs:7`；Token 卡自标（`Cards/Token/BasicScale.cs:15`）。
2. **游戏无条件反射注入**：`BangDreamLib/src/Core/Models/ModelDb.cs:68-87` 汇总 `ReflectionHelper.GetSubtypesInMods<AbstractModel>()`（`src/Core/Helpers/ReflectionHelper.cs:60-63`＝模组程序集里**全部非抽象子类**），`ModelDb.Init`（`ModelDb.cs:423-431`）逐个 `Activator.CreateInstance`。

结论与推论：

- card / power / relic 类**不可能**靠「无引用」判死；特性只决定「进哪个池」「有没有模组前缀 ID」。
- 一个无人引用的模型类仍然活着并占用 entry（如 `RhapsodyRequestPower`）——删它不会崩，但要连同 `powers.json` 僵尸键一起处理。
- 反过来，**带必填构造参数的「死」model 类会让启动崩**，这是删除模型类时必须构建验证的原因。
- 其他反射入口同样零引用但活着：`IBangDreamMechanic`（`BangDreamLibCore.cs:45` → `BangDreamMechanicRegistry.Discover`）、`AbstractConsoleCmd`（游戏 `src/Core/DevConsole/DevConsole.cs:33`）、`IPatchMethod`（同文件 `IModPatches.AddTo` 泛型注册）、`static class` 扩展类（按方法名调用）、`EnsureGodotScriptsRegistered`（`BangDreamLibCore.cs:28`、`ItsCrychic.cs:26`）。

**池归属不能按名字或文档猜**：`AbstractMutsumiCard.cs:9` 把标准卡注册进了额外牌堆 `MutsumiMusicalCardPool`，而 `WakabaMutsumi.cs:18` 的标准池是 `MutsumiStandardCardPool` —— 这类问题在清理时会被误当成「文件没用」，实际是接线错误。

## 5. 判定模型：四态 + 死簇

**禁止输出单一 orphan 列表**（历史失败样本：`.vfx_staging/audit/orphan_scan.py` v1，因未剥离 `.import` 自引用而结构性失效）。必须分桶落盘：

| 状态 | 定义 | 处置 |
|---|---|---|
| **被引用** | 字面路径或 uid 命中 | 不动 |
| **约定引用** | 显式建模的命名约定命中 | 不动；近似匹配（如去后缀的前缀匹配）必须标为「弱约定」 |
| **弱命中** | 候选 basename 出现在任意语料文本中，但不成其为可解析引用 | **待人工确认**，不得当孤儿 |
| **确认无引用** | 以上三者皆无 | 进三档分档，**仍不等于可删** |
| **死簇** | 自身无引用，且唯一引用者是「确认无引用」或同簇成员 | **单列，不并入确认孤儿**；默认落 B |

死簇的三条硬规则：

- **不得并入确认孤儿**。把死簇混进确认孤儿会制造假阳性：`MusicCrystalHitVfx.cs` 曾被这样误判——它其实被保留的预览场景 `music_crystal_hit_vfx.tscn:3` 以 uid+path 引用，而该场景按保留规则没有死。
- **级联必须区分保留意图**：引用者若命中**活跃保留**规则（有意保留的预览件、模板），就不算「已确认无引用」，其被引用者不得因级联进入死簇；若只命中**预留待实装**规则（未实装角色的素材、等实装的场景），它本身就是「无引用但先别删」的死簇形态，**不得阻断级联**，只决定分档落 B。两类性质在 `references/keep-list.md` 中逐条标注，脚本里对应 `keep_rules.py` 的 `cascade_blocks` 字段。
- **级联必须迭代到不动点**，不能只算一轮（A←B、B←C、C←A 的环要能收敛）。
- **阻断只在种子层判定**：当前实现里「引用者命中活跃保留 ⇒ 阻断」只对**初始种子**（自身无任何引用者）生效。若某个普通孤儿引用了一个活跃保留文件，该保留文件仍可能被拉进死簇并继续级联——影响仅限桶归属（都会落 B，不会进 A 档删除），但签字时要按簇整体看，不要逐条拍板。

另有三个旁路桶：`broken`（引用指向不存在的资源）、`sidecar_orphan`（源已删只剩副产物）、`sidecar_missing`（源在但 sidecar 缺）。

### 5.1 三档分档

| 档 | 含义 | 要求 |
|---|---|---|
| **A 可直接删** | 明确废件（旧版素材、无引用的 shader/材质/场景） | 仍须先建引用图并留下证据 |
| **B 建议保留** | 未实装角色预留素材、有意保留的预览件、模板、等实装的死簇 | **每条必须写理由**；清单见 `references/keep-list.md` |
| **C 需人工确认** | 结构性冗余、空目录、跨工程边界、判定不了的一切；**`images/card_portraits/**` 一律落 C** | **默认落 C**，不得默认进 A |

卡图不直接进 A 档：即使确认「无对应类」，也要按第 6 节第 10 条与 `references/deprecation-ledger.md` 记账，经美术确认为可删后才转 A。

## 6. 硬性条款

1. **搜索域固定**：只扫 `git ls-files` + `git ls-files -co --exclude-standard`，并排除 `.git`、`.godot`、`bin`、`obj`、`.vfx_staging` 与本体同步目录（第 2 节表 1）。`.godot/uid_cache.bin`、`.git/index`、`**/.godot/editor/filesystem_cache*` **不是**证据源——不排除它们会让每个文件都「被引用」。
   **输出目录也必须排除**：扫描/清单的 `--out` 若落在搜索域内，其产物（清单、快照）本身会成篇枚举文件名，重跑时会把真孤儿洗成「文档弱命中」（实测 `--out .` 时 `confirmed_orphan 1→0`、exit code 翻成 0）。默认 `.vfx_staging/audit` 在域外，是安全的；自定义 `--out` 时脚本动态排除该目录并在快照头记 `excluded_out_dir`。
2. **自引用必须剥离（两种形态）**：
   - ①`.import` 自引用：提取 token 前把 `.import` 的 `source_file=` / `dest_files=` / `path=` 行整行去掉；任何来源文件等于候选自身或其 sidecar 的命中一律不计。
   - ②**清理自述文档**：`.agents/skills/obsolete-file-cleanup/**` 与 `docs/cleanup-authoring.md` 按性质会成篇列举候选文件名，属于「描述候选」而不是「使用候选」，必须从引用语料中排除（快照头部记 `excluded_self_docs`）。这个坑真实发生过：`references/keep-list.md` 一写进 `anon01.tscn`，anon 死簇整条链就因「文档引用」而消失。
   - 其余 `docs/**` 与另外两个 skill **不排除**——它们提及某个文件是合法的「意图保留」证据（例如 `docs/vfx-authoring.md` 把 `MusicFlashVfx` 当 `Node2D` 参考实现引用）。
3. **uid 优先、path 兜底**：先建 uid→磁盘路径 表（三来源：`*.uid` 内容、`*.import` 的 `uid=`、`*.tscn/*.tres/*.gdshader` 头部），uid 解析不到时回落 path。
4. **动态拼接路径是盲区**：含 `{` 或插值的字符串不入 token，但必须要求删除候选人工核对动态加载点，并在交付里声明该盲区（历史实例：`AbstractSakikoMusicCard.cs` 的 `res://{modFolder}` 模板串曾被 v1 当成断链路径）。
5. **卡图必须双向盘点**：类→图（`audit_cards.py` 的 `missing portrait`）与图→类（孤儿扫描）都要跑。只跑一向会得出 `.vfx_staging/audit/portraits_report.txt` 那种结论——它把 31 张共享立绘误报成孤儿、把 24 张卡的立绘误报为缺失，真实缺图只有 14 张。
6. **本体归属先行**：候选文件先用 `.agents/skills/vfx-authoring/scripts/check_asset_origin.py` 判定；命中 `BODY` 的正确动作是**改为引用本体 uid**——既不删也不复制。
7. **删除前逐条 `os.path.exists` 复核**：快照必然过期（`v3_confirmed_orphan.txt` 39 条里 6 条已不存在）。过期项单列 `stale`，不得静默混入。
8. **删除前先 `git add`**：保证可回滚。本仓库既有误删前科，也有未跟踪文件在 rebase/reset 中静默消失的前科。
9. **删除后残留验证必须全绿**：旧类名、entry 字符串、本地化键、资源名的 `rg` 命中 = 0；JSON 可解析且无重复键；`git diff --check`；完整构建通过；**硬编码清单已同步**（如 `BangDreamPreloadManager.cs:55-63` 的 `VfxAssets`）。
10. **不直接删除仍可能复用的卡图**：写进 `ItsCrychic/ItsCrychic/images/card_portraits/DEPRECATED.md`，格式见 `references/deprecation-ledger.md`；资源不存在就如实记「无对应资源」，不得虚构文件。
11. **工具与快照可复现，但快照是易失品**：脚本入库在 `.agents/skills/obsolete-file-cleanup/scripts/`；扫描快照与签字用的 `decisions.tsv` 落 `.vfx_staging/audit/`，**该目录是特效设计的临时目录，随时可能被整体清理**。因此：
    - 每份快照头部必须写「脚本名 + 版本 + 时间 + 输入域」，**禁止无出处的裸 `.txt`**（现有快照就出现过「文件名与脚本版本不对应」的情况，照脚本名反推产物会得出错误结论）；
    - 「扫描 → 签字 → dry-run → apply」应在**同一次会话内**完成，不要跨天；快照一旦可能过期，就当场重跑；
    - **长期留档只认 `docs/cleanup-log.md`**（由 `sign_off.py --apply` 自动追加，随提交入库）。
12. **输出契约**：stdout 给 `key value` 统计行 + `PROBLEM`/`WARN` 前缀的逐行提示；存在 `confirmed_orphan` 时 `exit 1`，否则 `exit 0`（对齐 `.agents/skills/card-development/scripts/audit_cards.py:526-546`）。
13. **交付即证据**：每条删除或保留都要给「判定依据 + 搜索命令 + 结果」，不接受结论式汇报。

## 7. 安全域（禁止直接删，或必须先迁移）

- 本体同步目录与 `BangDreamLib/src/**`（解包游戏源码，3508 个 `.cs`）。
- `*.import` / `*.uid` 单独删除（必须与源同批）。
- `.godot/` 内的手写文件（`polka_probe.gd`、`polka_rebuild_check.gd`、`polka_long.tscn`、`polka_short.tscn`）——应先迁出 `.godot` 再加白名单，否则「删缓存重开编辑器」会连带丢失。
- `bin/spine-godot-extension-*`。
- 被 `.tscn` 以 uid 引用的脚本（当前 29 个），删脚本必须同批处理引用它的场景。
- 用户工作区里未提交、未跟踪的 WIP 文件：任何清理都必须先 `git status --short` 识别并绕开。

## 8. 验证与交付

清理任务的验收顺序（详见 skill）：

1. `scan_orphans.py --project-root .` → 四态 + 死簇清单，`exit 0/1` 有语义。
2. `cleanup_manifest.py` → 紧凑清单 `cleanup_manifest.md`（人看）+ `cleanup_decisions.tsv`（人改）。
3. 在 tsv 里逐条填 `action`（`delete`/`keep`/`defer`）与 `note`；**A 档必须填满**，`keep`/`defer` 必须写理由。
4. `sign_off.py --dry-run` 复核 rm 清单；有未决 A 项会 `exit 1`。
5. `sign_off.py --apply` 执行：先 `git add`、删资源 + sidecar、向 `docs/cleanup-log.md` 追加批次记录。安全域与未跟踪 WIP 会被硬拒绝。
6. 残留搜索、JSON 校验、`git diff --check`、完整构建。
7. 交付：清单 + 每条依据 + 命令与输出 + 修改文件表 + `cleanup-log.md` 里的批次条目。

**脚本只证明「静态引用关系」，不证明「运行时是否被动态加载」。** 编译通过也不能替代第 5 步。存在未解释的集合差异、断链、构建错误或残留引用时，不得宣布完成。

## 9. 已知限制（判定盲区，交付时必须声明）

| # | 限制 | 影响 | 处置 |
|---|---|---|---|
| 1 | 含 `{` / 插值的动态拼接路径不入 token | 运行时按名拼出的引用扫不到（如 `res://{modFolder}` 模板串） | 删除候选必须人工核对动态加载点，交付里声明未覆盖 |
| 2 | 弱命中是**子串**匹配，不是词边界 | 同族文件可能被拆到不同档：`orb/layer1d-3d.png` 因命中 `..._0002_layer1d.png` 落 C，而 `layer4d/5d.png` 落 A | 同族素材按目录成簇人工复核，不要逐文件拍板 |
| 3 | `.py` 不在引用语料 | 仅影响 `.py` 内写死的 `res://` 路径（当前 0 影响） | 若新增 Python 工具产生资源引用，需扩语料或声明盲区 |
| 4 | 代码侧采用保守规则（有基类/接口/`[Register*]` 即不判死） | 真死脚本会被保住（漏报而非误删） | 死代码清理需要额外做「类名 + entry 字符串 + 池注册」的专项盘点，不能只靠本工具 |
| 5 | 死簇与其根引用者可能分属不同档 | 例如 `edge_blurring.tres` 在 A，它引用的两项在 B | S4 删除时按「成簇」原则成组处理，不要把链拆开删 |

**方向性约定**：以上限制一律按「宁可漏报、不可误删」处理。任何一条没覆盖到时，落 C 档并写明原因，不得为了凑数落 A。
