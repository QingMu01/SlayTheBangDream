# 特效开发指南

新增或改造一个特效时该遵守的约定。预览机制（编辑器里为何静止、三种环境如何共存、`VfxPreviewSupport` 各成员）见《特效预览机制》（`vfx-preview-mechanism.md`）——本文假定你已了解那套接缝。

**要与用户协作、从零设计一个新特效**（用户不懂特效、需要先访谈再动手）时，走 skill `.codex/skills/vfx-authoring/`：素材侦察 → 访谈 → 计划 → **用户批准** → 实现。本文仍是实现规范的唯一事实来源。

## 1. 三条铁律（改错会破坏游戏内表现）

**一、不要用硬编码的初值覆盖场景里的美术基准值。** 重播时的重置必须用从场景读回的基础值，不能用 `Colors.White` / `Vector2.One`：

```csharp
// _Ready 里先记录
_spriteBaseScale = _sprites.Scale;      // flying_music_note_default 是 0.7，melody 是 0.5
_noteBaseColor = _musicNote.Modulate;   // music_wave 的 MainNote 是蓝色基调 Color(0.467,0.6,0.8,1)

// Replay 里再恢复
_sprites.Scale = _spriteBaseScale;
_musicNote.Modulate = _noteBaseColor;   // 原实现只动画 alpha，保留色相
```

**二、编辑器里不要写场景共享的 `ShaderMaterial` / `ParticleProcessMaterial`。** 逐帧写共享资源会把场景标记为「已修改」并污染资源。预览时改用实例级接口：

```csharp
if (Engine.IsEditorHint())
    node.SetInstanceShaderParameter(name, value);   // 每实例，不脏场景
else
    material.SetShaderParameter(name, value);        // 游戏内沿用原写法
```

粒子着色同理：编辑器用 `particles.SelfModulate`，游戏内写 `ProcessMaterial.Color`。

**三、并行 Tween 里不要对同一属性建两个 tweener。** `SetParallel(true)` 下每个 tweener 各自写自己的属性，
后创建的会覆盖先创建的，**且不报任何错**。典型翻车是「淡入 + 淡出」写成两条：

```csharp
// 错误：两个 tweener 都写 modulate:a，淡出被静默吃掉，节点永远挂在画面上
var fadeIn = tween.TweenProperty(node, "modulate:a", 1f, 0.10f);
fadeIn.From(0f);
tween.TweenProperty(node, "modulate:a", 0f, 0.20f).SetDelay(0.30f);

// 正确：用一个 TweenMethod 按进度算出全部状态
tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, LifeTime);
```

同一个属性（`scale` 的「弹入」+「扩散」也是）必须合并成单一 `TweenMethod`，
在方法内按 elapsed 分段求值。**症状是「动画看起来没生效」而不是报错**，
所以务必靠录帧（第 3.1 节）而不是靠读代码来确认。

## 2. 基类怎么选：默认 `Node2D`

**除非明确需要「飞行道具」或「异步伤害」，否则一律继承 `Node2D`。** 这是一个容易搞反的默认值，先说明为什么。

`NBangDreamFlyingVfx` 不是「特效基类」，而是**玩法接缝**：它提供 `VfxSpawned / BeforeHit / HitTriggered / AfterHit / VfxFinished` 五个生命周期信号，供 `BangDreamVfxManager.SubmitVfx()` 转成可 `await` 的 Task，从而让**命中时机驱动伤害结算**。它的代价是：

- 节点被当作飞行道具管理，需要 `SetPath` 之类的外部注入，位置不能自己定；
- `_Ready` 里有基类逻辑，子类必须 `base._Ready()`，与「`_Ready` 不写视觉状态」的预览约定叠加后容易出错；
- 自毁走 `EmitFinishSignal()` 而非 `QueueFreeSafely()`，混用会静默不销毁。

一个纯表现、由 `WithHitFx` 或调用方 `AddChild` 播放的特效，不需要以上任何一条。项目现状也印证这点：

| 脚本 | 基类 |
|---|---|
| `MusicHitVfx` / `MusicFlashVfx` / `MusicWaveVfx` / `MusicEqualizerVfx` / `PerformFlashVfx` / `StaffRingVfx` | `Node2D` |
| `MusicNoteFlyingVfx`（音符飞行，需命中结算） | `NBangDreamFlyingVfx` |

判定标准：

- **继承 `Node2D`** —— 纯视觉播放，命中/结束时机由 Tween 自身控制，调用方只关心「播了」。绝大多数攻击、命中、UI 特效都属于此类。
- **继承 `NBangDreamFlyingVfx`** —— 特效需要「飞过去」，且**命中时刻要驱动玩法状态**（如 `MusicNoteAsyncDamageEffect` 用 `HitTriggered` 决定何时施加伤害）。

后者是少数派。写新特效时若不确定，先按 `Node2D` 写；等真的需要 `await` 命中信号时再改基类。

### 2.1 确认特效是否真的被接入

**做完特效不等于它在游戏里会出现。** 新建的特效脚本与场景很容易是「孤儿」——
F6 里看着一切正常，游戏内却永远不会触发。交付前用全仓库搜索确认有调用点：

```bash
# 搜类名与场景路径，只命中定义文件本身即为孤儿
grep -rn "MusicSlashVfx\|vfx_music_slash" --include=*.cs --include=*.tscn .
```

**注意 `WithHitFx` 的路径解析规则**：游戏内走 `VfxCmd` → `SceneHelper.GetScenePath`，
规则是 `"res://scenes/" + innerPath + ".tscn"`，解析到的是**游戏本体**的资源目录。
所以 `WithHitFx("vfx/vfx_attack_slash")` 拿到的是原版斩击，**永远到不了 mod 自己的
`res://BangDreamLib/scenes/vfx/...`**。想让某张卡用上自定义特效，必须走注入路径
（如 `PreloadManager.Cache.GetScene(...)` + `AddChildSafely`），不能只改字符串。

## 3. 新增特效接入清单

0. **先定基类**：默认继承 `Node2D`；只有明确需要「飞行道具」或「异步伤害」时才继承 `NBangDreamFlyingVfx`。见第 2 节。
1. 给脚本加 `[Tool]` 特性。
2. 把 `_Ready` 里的播放逻辑抽成公开的 `Replay()`；`_Ready` 只在 `VfxPreviewSupport.AutoPlayOnReady` 时调用它。
3. `_Ready` 里**不写任何视觉状态**（别隐藏子节点、别改缩放/颜色/着色参数），否则编辑器里会「看起来在动」或丢失作者摆放。所有初始化视觉的代码放进 `Replay()`。
4. `Replay()` 开头 `Kill()` 掉旧 `Tween`、把状态字段复位到**从场景记录的基础值**。
5. 播放结束的回调按 `ShouldSelfFree(this)` 决定自毁还是 `LoopPreview` 下重播。继承 `Node2D` 时自毁写 `this.QueueFreeSafely()`；继承 `NBangDreamFlyingVfx` 时写 `EmitFinishSignal()`。
6. 需要外部数据的（战斗状态、角色色）改为向 `VfxPreviewSupport` 提问。
7. 加 `[Export] Play` 与 `[Export] bool LoopPreview = true`；可调参数加 `[Export]`，setter 里用 `IsPreviewRun(this)` 判定后才重播（编辑器里不重播）。
8. 需要外部路径/目标的（如飞行类）用 `[Export]` 提供预览缺省值，并靠「外部未注入」判定预览态。
9. 由**外部管理器驱动**的特效（如 `NLingeredOrbitVfx` 由 `LingeredOrbitManager` 驱动）要特别小心：自播判断必须用 `IsPreviewRun(this)` 而非 `AutoPlayOnReady`，否则游戏内会凭空多出一份自驱动实例。同理，任何「外部未注入时的兜底行为」都要加 `IsPreviewRun` 守卫，否则游戏内会走到预览兜底路径。
10. 预览默认摆放见《特效预览机制》第 3 节。
11. 素材来源见第 4 节。
12. **调观感必须录帧核对**（第 3.1 节），不要只靠 `--headless` 跑通就认为完成。

### 3.1 录制成视频逐帧核对（调观感的唯一可靠手段）

`--headless` 只能验「有没有报错」，**验不了「看起来对不对」**。调观感必须录帧：
「粒子挤在一起」「旋转很怪」「动画没生效」这类问题，读代码看不出来，跑一次才知道。

```bash
# Godot 可执行文件取自 Directory.Build.props 的 <GodotExe>（单属性查询直接打印值）
GODOT=$(dotnet msbuild BangDreamLib/BangDreamLib.csproj -getProperty:GodotExe)

# 录成 PNG 序列（注意：--write-movie 与 --headless 不能同时用，headless 无渲染录不到画面）
"$GODOT" \
  --path BangDreamLib \
  --write-movie "$PWD/.vfx_staging/rec/frame.png" \
  --fixed-fps 60 --quit-after 90 \
  "res://BangDreamLib/scenes/vfx/<场景>.tscn"

# 合成 mp4 交付（本机 ffmpeg 可用）。部分发行版（如 Fedora）不打包 libx264，
# 此时换 libopenh264（见下条要点，record.sh 会自动挑可用编码器）
ffmpeg -y -loglevel error -framerate 60 -i frame%08d.png \
  -c:v libx264 -pix_fmt yuv420p -crf 18 ../<场景>_v2.mp4
```

要点：

- `--fixed-fps 60` 让帧号与时间严格对应（`t = frame/60`），便于定位「第几秒出问题」。
- `--quit-after 90` 约等于 1.5 秒；本类特效整段约 0.86 秒，录 90~130 帧够看两个循环。
- 产物是 1920×1080 全屏，**裁中心区域**再看更省事：
  `ffmpeg -i frame00000030.png -vf "crop=760:760:580:160" crop/c30.png`
- 判断「某元素是否按时消失」时，**裁切目视比采样亮度更可靠**——
  中心区域往往同时有粒子/闪光，亮度采样会被污染，得出错误结论。
- 录制输出目录建议放 `.vfx_staging/`（已在 `.gitignore` 内），不要污染仓库。
- 若命令在启动阶段就崩在 `RotatedFileLogger::rotate_file()` 或 `Failed to open 'user://logs/...'`
  （受限环境 / 沙箱写不了 Godot 的日志与用户目录），两条出路：加 `--log-file <工作区内的路径>`，
  或把 `XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`XDG_CACHE_HOME` 指到可写目录（如 `$(mktemp -d)`）。
- **编码器**：`libx264` 并非到处都有（Fedora 的 ffmpeg 就没有，只有 `libopenh264`）；
  `record.sh` 按 `libx264 → libopenh264 → mpeg4` 自动挑，也可用 `--encoder` 指定。
- **实测速度**：12 帧约 2 秒，90 帧约 12–13 秒（固定启动开销约 1 秒，之后每帧约 130ms）。
  录 130 帧请预留 20 秒以上。
- **背景不是黑，是灰。** 本项目未设置清屏色，用的是 Godot 默认 `default_clear_color`
  （0.3 灰 ≈ 76/255，实测帧角像素 75）。判断「某元素是否按时消失」时不要按黑底预期，
  亮度采样阈值也要相应调整。
- **预览态会循环重播。** F6 / 场景根运行时 `ShouldSelfFree` 为 false、`LoopPreview` 为 true，
  录制区间超过一个 `LIFE` 就会录到第二个循环。要核对单次播放，就把 `--quit-after`
  控制在一个循环内。

> 录制与校验已封装成脚本，优先用它们而不是手敲命令：
> `.codex/skills/vfx-authoring/scripts/record.sh`（录制 + 合成 mp4/gif）、
> `verify_frames.py`（帧序列机器校验，背景自适应）。

## 4. 素材接入（资产库导出 + 本体归属校验）

素材由 Polaris MCP 工具从资产库取出（库在 MCP 服务侧，本仓库不硬编码它的路径），流程：

1. `search_assets` 定位候选。除 `tags: ["VFX"]` / 关键词 / `kinds` 外，**优先用结构化过滤**（SQL 级，`total` 即真实命中数）：`alpha`（透明占比）、`fill_ratio`（剪影填充率）、`glow_range`（辉光强度）、`edge_softness`（边缘柔硬）、`dark_outline`（深色描边）、`layout`（构图）、`colors` + `color_min_ratio`（主色及其占比）、`sub_types`（`grid_atlas` / `image_sequence` 等）。首轮定位可用 `detail: "minimal"` 省 token。再用 `render_contact_sheet` 一次拼图目视确认形态。

   > **v1 起还支持主体级选材**（候选是**具体帧**）：`role`（8 个用途角色）+ `mode`（`scout`/`strict`）
   > + `facets`（受控形态事实）+ `dimensions`（维度区间）+ `target_px`/`tint`/`blend`（按使用条件排序）
   > + `unknown_features`。受控取值先读 `get_selection_spec`（或 resource
   > `polaris://spec/selection.json`）——**不要凭记忆猜取值**。返回里重点看 `risks` 与
   > `required_review`（未知证据不会被当作通过），以及 `effective_constraints` 里候选池是否被截断。
2. `get_asset_group_preview` 读取图集的切分参数（行列数、单帧尺寸）——**这是设置 `hframes`/`vframes` 的唯一依据**，不要目测。
3. `export_assets` 导出到项目外的临时目录（如 `.vfx_staging`），**不要在资产库内改名**。每个资产的导出子目录里会附带 `_provenance.json`（uuid / 名称 / 标签 / 描述 / 逐文件 hash），这是溯源依据。
4. 复制进 `BangDreamLib/BangDreamLib/images/vfx/<特效名>/` 并重命名为语义化文件名（`music_slash_main.png` 而非 `fx_301403_tex_002.png`）。资产库中的数字名对项目无意义，语义名才便于后续维护。
5. 运行 `--headless --import` 让引擎生成 `.import` 与 uid，**不要手写 uid**。手写的 uid 不保证合法，且可能与全库冲突。
6. 场景里引用资源用**引擎生成的 uid**（从 `.import` 的 `uid=` 行读），`path=` 同时保留作可读性兜底。
7. **复制进工程之前先做本体归属校验**（见 4.1）：命中本体的素材**不复制**，改为直接引用本体 uid。

`.import` 文件必须随 PNG 一起提交，否则他人拉取后 Godot 需要重新导入才能打开场景。

### 4.1 本体归属校验（复制素材前必做）

**从资产库导出的素材，先判定它是不是本体资源**——是则直接引用本体，不是才复制进工程：

```bash
python .codex/skills/vfx-authoring/scripts/check_asset_origin.py <导出目录>
```

判定分三级（脚本实现，不靠文件名或肉眼）：

| 结论 | 含义 | 处理 |
|---|---|---|
| `BODY <res://…> uid=…` | 逐字节 md5 命中，或**尺寸 + RGBA 像素**都相同（库内是本体图但被重新编码过） | **不复制**；场景里直接写输出的本体 `uid` + `path` |
| `LOCAL` | 本体里没有对应图 | 按上面第 2–4 步复制进 `images/vfx/<特效名>/`、导入、取 uid |
| `REVIEW` | 与本体某张图**同尺寸且感知哈希接近**（疑似在本体图上改绘） | 人工判定后，按上两条之一处理 |

- 本体根缺省为 `BangDreamLib/images`（编辑器同步目录，其 `.import` 里的 uid 就是本体 uid）；也可 `--body-root ../sts2unpack/<版本>` 指向解包源。
- 引用本体要求编辑器工程里已同步本体资源：新克隆先跑 `tools/sync-sts2-assets.sh`。
- **特效目录不再产出 `README.md` / `SOURCES.md`**；来源凭据（库内 uuid/名称、语义名、用途、校验判定）写进 `.vfx_staging/plans/<slug>.md`。

### 4.2 选材与取证的实操坑

**一、判断透明与边缘：以 `features` 的数字为准，棋盘格底看形态。**
`render_asset_preview` 与 `render_contact_sheet` 现在都会为透明素材自动叠**棋盘格底**
（文本块里 `background=checkerboard`），`render_asset_preview` 还附带 `features`。所以：

- `get_asset` / `get_assets` 的 `features` 字段——读「透明 86%」这类**数字**，最可靠；
- `render_contact_sheet` / `render_asset_preview`——看**形态与边缘**。

> 历史记录：早期版本 `render_asset_preview` 会丢 alpha 通道，把透明底渲染成整块实色
> （实测 `oblvns_skin_05` 透明 86%，却渲染成整块蓝色底）。该问题已修复，
> 但**数字仍比肉眼可靠**——判断透明占比一律以 `features` 为准。

**二、库里的描述可能是错的，选材必须目视确认。** 本次发现同一批导入的三个资产
（`oblvns_03` / `oblvns_skin_04` / `oblvns_skin_05`）被套了统一模板「光环纹样图集（TPS）」，
实际分别是红色速度线和两款音符图集。`validate_description` **查不出这类错误**——
它只比对「描述 vs 主色/辉光」，而「是光环还是音符」属于形状语义，特征里没有这个维度。

所以：**选中素材后一定用 `render_contact_sheet` 看一眼实际形态**，不要只凭描述文字决策。
怀疑有批量误标时，用 `list_import_batches` 按导入时间**自动聚类**（相邻资产创建时间间隔 ≥
`gap_minutes` 即算新批次），再配合 `render_contact_sheet` + `get_assets` 逐格比对——
比手工按时间戳聚类可靠，也能直接拿到可复用的时间窗口。

**三、溯源靠 `_provenance.json`，不必再用文件字节数猜。** `export_assets` 会在每个资产的导出
子目录里写 `_provenance.json`，记录 uuid / 名称 / 标签 / 描述 / 导出时间 / 网格参数 / 逐文件 hash——
**素材在项目里被重命名后仍可反查来源**。把 uuid / 名称一列写进本次的计划文件
（`.vfx_staging/plans/<slug>.md`）即可——**特效目录不再产出 `SOURCES.md` / `README.md`**。

> 旧做法是用文件字节数去全库反查（`music_slash_note.png` 23827 字节 → `oblvns_skin_05`），
> 仅在没有 `_provenance.json` 的历史导出上才需要。

**四、黑底素材（加法混合）的 `主体框` 不可用。** 黑底被当作内容，包围盒永远是 `x0.00-1.00 y0.00-1.00`，
无法据此定缩放。需要自己算亮度包围盒得到真实内容尺寸——本次月牙弧画布 128×128、
实际内容仅 53×110，据此才定下 `scale ≈ 1.9`（补偿与 256px 画布素材的差异）。

> **⚠️ 该条已被特征 v4 修正（选材契约 v1，2026-10）**：核心现在会检测**贴边均匀背景**
> （`border_uniform`），识别到黑底时主体框是**剔除背景后的内容框**，会收紧，可直接据此定缩放；
> 只有「未识别到背景、整幅即主体」（掩码方法 `full_frame`）时才等于满幅。
> 读 `features` 的「背景」与掩码方法即可区分这两种情况——**不要再一律自己算亮度包围盒**。
> 口径与交叉校验见 `.codex/skills/vfx-authoring/references/selection-contract.md`。

**五、`export_assets` 会自动创建目标目录。** `directory` 传绝对路径，不存在时自动建。
导出到 `.vfx_staging/`（已在 `.gitignore` 内），不要在资产库内改名或删除。

> **同名不覆盖**：已存在同名文件/文件夹的资产会报错，其余继续。重导出前先清掉旧目录，
> 否则拿到的可能是上一次的产物。

**六、`search_assets` 的 `detail` 三档均可用**：`minimal`（只回 uuid/name/kind/subType，
首轮定位最省 token）、`standard`（默认）、`full`（完整描述与全部文件名）。

### 4.3 回写描述与修正误标

写回描述用 `update_asset_description`，注意七字段受控词表：

- 辉光字段的合法写法是**组合词**「不发光不透明」「弱发光」「中发光」「强发光」，
  写单独的「不发光」会被判为不在词表内；
- 帧数**不属于**「构图或运动」字段（该字段只收「居中对称」「斜向」「放射」「扩散」这类词），
  帧数信息写进最后一个「用途」字段；
- 辉光分档必须与 `features` 一致（如 `glow=0.00` 只能写「不发光不透明」），否则报 `featureConflict`。

写完用 `validate_description` 自检，`ok: true` 才算通过。
**批量误写的恢复路径**是 `revert_asset_meta`（可按修订号或 batchId 整批回滚），
所以修正误标是安全操作——这也是本次能放心批量改描述的原因。

## 5. 验证与常见坑

```bash
# 从 Directory.Build.props 读 Godot 与本体数据目录（单属性查询直接打印值）
GODOT=$(dotnet msbuild BangDreamLib/BangDreamLib.csproj -getProperty:GodotExe)
STS2_DATA=$(dotnet msbuild BangDreamLib/BangDreamLib.csproj -getProperty:Sts2DataDir)

# 编译（改完脚本先过这一步）；部署到仓库内 bin/BuildDeploy，避免写 Steam 安装目录
dotnet build BangDreamLib/BangDreamLib.csproj -c Debug \
  -p:Sts2Dir="$PWD/bin/BuildDeploy" -p:Sts2DataDir="$STS2_DATA"

# 无头运行单个特效场景，检查脚本异常（F6 等价）
"$GODOT" --headless \
  --path BangDreamLib --quit-after 120 "res://BangDreamLib/scenes/vfx/<场景>.tscn"

# 无头加载编辑器，检查场景/资源导入错误
"$GODOT" --headless --editor \
  --path BangDreamLib --quit-after 150
```

以上两条命令加 `| grep -iE "error|script error|failed"`，**无输出即通过**。注意 `--headless --editor` 不会把新场景的 uid 回写进 `.tscn`；没有引擎生成的场景 uid 时保留 path 引用，由编辑器保存生成后再记录，不能手写或猜测 uid。

**这两条只验证「没有报错」，不验证「观感对不对」——调观感请看第 3.1 节录帧核对。**

已知的非问题（实测确认，不要误当新缺陷）：

- **`Error loading GDExtension configuration file: 'res://addons/fmod/fmod.gdextension'`（sentry 同）**——
  `.godot/extension_list.cfg` 里残留了指向已不存在的 addons 的旧条目。删掉那两行即可（该文件是可再生缓存）；
  工程的 `addons/` 下只有 `mega_text`。
- **`ObjectDB instances leaked at exit`** —— 继承 `NBangDreamFlyingVfx` 的场景都会出现 1 条（对照 `flying_music_note_default.tscn` 同样有）。这是基类场景的固有现象，不是新特效引入的缺陷。继承 `Node2D` 的场景不会出现。
- **`Sentry GDExtension not loaded`** —— 无头运行与录帧时的常规提示，与特效无关。
- **`ERROR: Error opening file 'res://icon.svg'`** —— `project.godot` 的 `config/icon` 指向了
  仓库里不存在的 `icon.svg`（既有问题，与特效无关）。要根治就补一个图标文件或删掉该配置行。

场景文件头 `load_steps` 需与实际资源数一致（`ext_resource` + `sub_resource` + 1）。写错不会导致加载失败，但会让引擎重新计算并可能覆盖你的手改，建议手写时就数对。

删除或替换已被场景引用的素材时，`--headless --import` 会报
`Failed loading resource: ...` / `ext_resource, invalid UID`。若你正准备改写该场景的引用，
这是**预期内的中间态**，改完场景即消失；若改完仍报，说明场景里还有残留引用。

### 5.1 两条只能靠录帧发现的实现陷阱

**一、alpha 曲线方向写反。** `alpha = Lerp(A0, A1, 1 - x^pow)` 在 `A1 = 0` 时等价于 `A0 * x^pow`：
alpha 从 0 涨到 `A0`，并在 `x` 被 clamp 到 1 后**永久停在 A0**。症状是「前段正常、后段整片挂着不消失」，
且**不报任何错**。正确写法是让 `fade` 从 0 走到 1 当插值参数：`Lerp(A0, A1, fade)`。

**二、径向粒子贴图的朝向。** 贴图长轴沿局部 **Y 轴**时，要对齐径向 `offset.Angle() = φ`
需要旋转 `φ - π/2`；写成 `φ` 会让火花**切向横躺**，看起来像风车的碎片而不是飞散的拖尾。

两条都只能靠**录帧目视**发现——编译、无头运行、单帧截图全部正常。