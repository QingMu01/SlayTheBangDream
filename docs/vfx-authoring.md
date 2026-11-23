# 特效开发指南

本文规定特效实现与验证。创作流程见 [vfx-authoring skill](../.agents/skills/vfx-authoring/SKILL.md)；预览原理见 [特效预览机制](vfx-preview-mechanism.md)。

## 0. 质量要求

1. 先确定主体运动、峰值与收尾，再选素材。
2. 角色意象应进入形状、运动或节奏，不能只换主题色。
3. 删除不服务主体、节奏或角色意象的装饰层。
4. 最终观感以真实战斗原速录像为准；单场景、静态合成与抽帧只用于辅助和诊断。

## 1. 三条铁律（改错会破坏游戏内表现）

**一、不要用硬编码的初值覆盖场景里的美术基准值。** 重播时的重置必须用从场景读回的基础值，不能用 `Colors.White` / `Vector2.One`：

```csharp
// _Ready 里先记录
_spriteBaseScale = _sprites.Scale;      // flying_music_note_default 是 0.7，melody 是 0.5
_noteBaseColor = _musicNote.Modulate;   // music_wave 的 MainNote 是蓝色基调

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

**三、并行 Tween 里不要对同一属性建两个 tweener。** `SetParallel(true)` 下每个 tweener 各自写自己的属性，后创建的会覆盖先创建的，**且不报任何错**。典型翻车是「淡入 + 淡出」写成两条：

```csharp
// 错误：两个 tweener 都写 modulate:a，淡出被静默吃掉，节点永远挂在画面上
var fadeIn = tween.TweenProperty(node, "modulate:a", 1f, 0.10f);
fadeIn.From(0f);
tween.TweenProperty(node, "modulate:a", 0f, 0.20f).SetDelay(0.30f);

// 正确：用一个 TweenMethod 按进度算出全部状态
tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, LifeTime);
```

同一个属性（`scale` 的「弹入」+「扩散」也是）必须合并成单一 `TweenMethod`，在方法内按 elapsed 分段求值。**症状是「动画看起来没生效」而不是报错**。

## 2. 基类怎么选：默认 `Node2D`

**除非明确需要「飞行道具」或「异步伤害」，否则一律继承 `Node2D`。**

`NBangDreamFlyingVfx` 不是「特效基类」，而是**玩法接缝**：它提供 `VfxSpawned / BeforeHit / HitTriggered / AfterHit / VfxFinished` 五个生命周期信号，供 `BangDreamVfxManager.SubmitVfx()` 转成可 `await` 的 Task，从而让**命中时机驱动伤害结算**。它的代价是：

- 节点被当作飞行道具管理，需要 `SetPath` 之类的外部注入，位置不能自己定；
- `_Ready` 里有基类逻辑，子类必须 `base._Ready()`，与「`_Ready` 不写视觉状态」的预览约定叠加后容易出错；
- 自毁走 `EmitFinishSignal()` 而非 `QueueFreeSafely()`，混用会静默不销毁。

判定标准：

- **继承 `Node2D`** —— 纯视觉播放，命中/结束时机由 Tween 自身控制，调用方只关心「播了」。绝大多数攻击、命中、UI 特效都属于此类。
- **继承 `NBangDreamFlyingVfx`** —— 特效需要「飞过去」，且**命中时刻要驱动玩法状态**（如 `MusicNoteAsyncDamageEffect` 用 `HitTriggered` 决定何时施加伤害）。这是少数派。

参考实现：`MusicHitVfx` / `MusicFlashVfx` / `MusicWaveVfx` 等为 `Node2D`；`MusicNoteFlyingVfx` 为飞行类。写新特效时若不确定，先按 `Node2D` 写；等真的需要 `await` 命中信号时再改基类。部分参考实现没有配套 `.tscn`，不能按 F6 直接预览。

### 2.1 确认特效是否真的被接入

**做完特效不等于它在游戏里会出现。** 交付前用类名与场景路径搜索调用点；只命中定义文件时，将其列为孤儿候选，而不是直接判死或删除。删除判定必须走 [清理规范](cleanup-authoring.md) 的引用图与签字流程。

预留、参考与预览件的保留状态统一维护在 [keep-list](../.agents/skills/obsolete-file-cleanup/references/keep-list.md)，本节不复制清单。

**注意 `WithHitFx` 的路径解析规则**：游戏内走 `VfxCmd` → `SceneHelper.GetScenePath`，规则是 `"res://scenes/" + innerPath + ".tscn"`，解析到的是**游戏本体**的资源目录。所以 `WithHitFx("vfx/vfx_attack_slash")` 拿到的是原版斩击，**永远到不了 mod 自己的 `res://BangDreamInSpire/scenes/vfx/...`**。想让某张卡用上自定义特效，必须走注入路径（如 `PreloadManager.Cache.GetScene(...)` + `AddChildSafely`），不能只改字符串。

## 3. 新增特效接入清单

0. **先定基类**：默认 `Node2D`；只有明确需要「飞行道具」或「异步伤害」时才继承 `NBangDreamFlyingVfx`。见 §2。
1. 给脚本加 `[Tool]` 特性。
2. 把 `_Ready` 里的播放逻辑抽成公开的 `Replay()`；`_Ready` 只在 `VfxPreviewSupport.AutoPlayOnReady` 时调用它。
3. `_Ready` 里**不写任何视觉状态**（别隐藏子节点、别改缩放/颜色/着色参数），否则编辑器里会「看起来在动」或丢失作者摆放。所有初始化视觉的代码放进 `Replay()`。
4. `Replay()` 开头 `Kill()` 掉旧 `Tween`、把状态字段复位到**从场景记录的基础值**。
5. 播放结束的回调按 `ShouldSelfFree(this)` 决定自毁还是 `LoopPreview` 下重播。继承 `Node2D` 时自毁写 `this.QueueFreeSafely()`；继承 `NBangDreamFlyingVfx` 时写 `EmitFinishSignal()`。
6. 需要外部数据的（战斗状态、角色色）改为向 `VfxPreviewSupport` 提问。
7. 加 `[Export] Play` 与 `[Export] bool LoopPreview = true`；可调参数加 `[Export]`，setter 里用 `IsPreviewRun(this)` 判定后才重播（编辑器里不重播）。
8. 需要外部路径/目标的（如飞行类）用 `[Export]` 提供预览缺省值，并靠「外部未注入」判定预览态。
9. 由**外部管理器驱动**的特效（如 `NLingeredOrbitVfx` 由 `LingeredOrbitManager` 驱动）要特别小心：自播判断必须用 `IsPreviewRun(this)` 而非 `AutoPlayOnReady`，否则游戏内会凭空多出一份自驱动实例。同理，任何「外部未注入时的兜底行为」都要加 `IsPreviewRun` 守卫。
10. 预览默认摆放见《特效预览机制》§3。
11. 素材来源见 §4。
12. **调观感必须录成视频并原速观看**（§3.1），按问题抽帧定位；不要只靠 `--headless` 或静态帧就认为完成。

### 3.1 录像核对（技术诊断 + 实战验收）

无头运行不能替代目视验收。录像分两类，职责不能互相替代：

- **单场景技术录像**：用下面的命令核对素材形状、层生命周期、伪影与精确时刻；裁切、慢放和机器帧校验都属于诊断工具。
- **真实战斗原速录像**：观感的最终验收。按实际尺寸连同角色、敌人、背景与 UI 观看；连打、群攻或常驻效果还要覆盖预期的重叠/持续情况。尚未接入或拿不到实战录像时，必须明确「实战观感未验收」，不能宣布完整通过。

先原速完整观看，再按问题抽帧；不要反过来用漂亮单帧证明节奏与冲击力。下方命令只负责第一类：

```bash
# Godot 可执行文件取自 Directory.Build.props 的 <GodotExe>（单属性查询直接打印值）
GODOT=$(dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:GodotExe)

# 录成 PNG 序列（注意：--write-movie 与 --headless 不能同时用，headless 无渲染录不到画面）
"$GODOT" \
  --path BangDreamInSpire \
  --write-movie "$PWD/.scratch/vfx/rec/frame.png" \
  --fixed-fps 60 --quit-after 90 \
  "res://BangDreamInSpire/scenes/vfx/<场景>.tscn"

# 合成 mp4 交付（record.sh 会自动挑可用编码器；手工合成时 Fedora 等发行版需换 libopenh264）
ffmpeg -y -loglevel error -framerate 60 \
  -i "$PWD/.scratch/vfx/rec/frame%08d.png" \
  -c:v libx264 -pix_fmt yuv420p -crf 18 "$PWD/.scratch/vfx/<场景>_v2.mp4"
```

要点：

- `--fixed-fps 60` 让帧号与时间严格对应（`t = frame/60`），便于定位「第几秒出问题」；`--quit-after` 控制录制区间。
- 产物是 1920×1080 全屏，定位局部伪影时可**裁中心区域**：
  `ffmpeg -i frame00000030.png -vf "crop=760:760:580:160" crop/c30.png`
  裁切只用于诊断，不能代替实际尺寸与完整战斗画面的观感判断。
- 判断「某元素是否按时消失」时，**裁切目视比采样亮度更可靠**——中心区域往往同时有粒子/闪光，亮度采样会被污染。
- 本项目未设置清屏色，背景是 Godot 默认灰（约 76/255）；判断元素消失时不要按黑底预期。
- **预览态会循环重播**（`ShouldSelfFree` 为 false、`LoopPreview` 为 true）。要核对单次播放，就把 `--quit-after` 控制在一个循环内。
- 录制输出目录建议放 `.scratch/vfx/`，不要污染仓库。受限环境若崩溃在 Godot 用户目录/日志写入，加 `--log-file <工作区内路径>`，或把 `XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`XDG_CACHE_HOME` 指到可写目录。

> 录制与校验已封装成脚本，优先用它们而不是手敲命令：
> `.agents/skills/vfx-authoring/scripts/record.sh`（录制 + 合成 mp4/gif）、
> `verify_frames.py`（帧序列机器校验，背景自适应）。

## 4. 素材接入（资产库导出 + 本体归属校验）

素材应服务已经成立的动态草样，可来自资产库、工程已有资源、本体、程序化形状/shader 或新绘；资产库不是设计起点，也不是唯一来源。**使用 Polaris MCP 从资产库取素材时**（库在 MCP 服务侧，本仓库不硬编码它的路径），流程：

1. `search_assets` 定位候选。除 `tags: ["VFX"]` / 关键词 / `kinds` 外，**优先用结构化过滤**（SQL 级，`total` 即真实命中数）：`alpha`、`fill_ratio`、`glow_range`、`edge_softness`、`dark_outline`、`layout`、`colors` + `color_min_ratio`、`sub_types`。首轮定位用 `detail: "minimal"` 省 token，再用 `render_contact_sheet` 拼图确认形态。主体级选材（`role` / `mode` / `facets` / `dimensions` / `target_px` / `tint` / `blend` / `unknown_features`）的受控取值先读 `get_selection_spec`，**不要凭记忆猜**；重点看 `risks` 与 `required_review`。
2. `get_asset_group_preview` 读取图集的切分参数（行列数、单帧尺寸）——**这是设置 `hframes`/`vframes` 的唯一依据**，不要目测。
3. `export_assets` 导出到仓库内但不入库的 `.scratch/vfx/`，**不要在资产库内改名**。每个资产的导出子目录会附 `_provenance.json`（uuid / 名称 / 标签 / 描述 / 逐文件 hash），这是溯源依据。
4. **复制前先做本体归属校验**（见 §4.1）：`BODY` 直接引用本体 uid，`REVIEW` 先人工判定，只有 `LOCAL` 才继续复制。
5. 将 `LOCAL` 素材复制进 `BangDreamInSpire/BangDreamInSpire/images/vfx/<特效名>/`，并重命名为语义化文件名（`music_slash_main.png` 而非 `fx_301403_tex_002.png`）。
6. 运行 `--headless --import` 让引擎生成 `.import` 与 uid，**不要手写 uid**。手写的 uid 不保证合法，且可能与全库冲突。
7. 场景里引用资源用**引擎生成的 uid**（从 `.import` 的 `uid=` 行读），`path=` 同时保留作可读性兜底。

`.import` 文件必须与 PNG 留在同一目录，否则场景引用会失效。

### 4.1 本体归属校验（复制素材前必做）

**从资产库导出的素材，先判定它是不是本体资源**——是则直接引用本体，不是才复制进工程：

```bash
python .agents/skills/vfx-authoring/scripts/check_asset_origin.py <导出目录>
```

判定分三级（脚本实现，不靠文件名或肉眼）：

| 结论 | 含义 | 处理 |
|---|---|---|
| `BODY <res://…> uid=…` | 逐字节 md5 命中，或**尺寸 + RGBA 像素**都相同（库内是本体图但被重新编码过） | **不复制**；场景里直接写输出的本体 `uid` + `path` |
| `LOCAL` | 本体里没有对应图 | 按上面第 5–7 步复制、导入并引用 uid |
| `REVIEW` | 与本体某张图**同尺寸且感知哈希接近**（疑似在本体图上改绘） | 人工判定后，按上两条之一处理 |

- 本体根缺省为 `BangDreamInSpire/images`（编辑器同步目录，其 `.import` 里的 uid 就是本体 uid）；也可 `--body-root ../sts2unpack/<版本>` 指向解包源。
- 引用本体要求编辑器工程里已同步本体资源：新克隆先跑 `tools/sync-sts2-assets.sh`。
- **特效目录不产出 `README.md` / `SOURCES.md`**；来源凭据（库内 uuid/名称、语义名、用途、校验判定）写进 `.scratch/vfx/plans/<slug>.md`。计划稿是**过程文件**（不入库、随时可清）：实现后把实际触发点、最终层级、素材 uuid 与 BODY·LOCAL 判定回写 `docs/mechanic-authoring.md` 对应机制章节；这里记录最终实现，不要求恢复草样阶段的层表。

### 4.2 选材与取证的实操规则

- **判断透明与边缘以 `features` 的数字为准，形态用图看。** `get_asset` / `get_assets` 的 `features` 读「透明 86%」这类数字最可靠；`render_contact_sheet` / `render_asset_preview` 看形态与边缘。早期 `render_asset_preview` 丢 alpha 的问题已修复，但**数字仍比肉眼可靠**。
- **库里的描述可能是错的，选材必须目视确认。** 同一批导入的资产可能被套统一模板，而特征校验只能比对主色/辉光，查不出「是光环还是音符」这类形状误标。选中素材后一定用 `render_contact_sheet` 看一眼实际形态；怀疑批量误标时用 `list_import_batches` 按导入时间聚类，再逐格比对。
- **溯源靠 `_provenance.json`。** 导出时已记录 uuid / 名称 / 标签 / 导出时间 / 网格参数 / 逐文件 hash，素材在项目里被重命名后仍可反查；把 uuid / 名称写进计划文件即可。
- **黑底素材的主体框按 v4 口径读。** 核心会检测**贴边均匀背景**（`border_uniform`）：识别到黑底时主体框是**剔除背景后的内容框**，可直接据此定缩放；只有「未识别到背景、整幅即主体」（掩码方法 `full_frame`）时才等于满幅。**不要再一律自己算亮度包围盒**。口径与交叉校验见 `.agents/skills/vfx-authoring/references/selection-contract.md`。
- **`export_assets` 会自动创建目标目录，但同名不覆盖**：已存在同名文件/文件夹的资产会报错，其余继续。重导出前先清掉旧目录，否则拿到的可能是上一次的产物。

## 5. 验证与常见坑

```bash
# 从 Directory.Build.props 读 Godot 与本体数据目录（单属性查询直接打印值）
GODOT=$(dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:GodotExe)
STS2_DATA=$(dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:Sts2DataDir)

# 编译（改完脚本先过这一步）；部署到仓库内 bin/BuildDeploy，避免写 Steam 安装目录
dotnet build BangDreamInSpire/BangDreamInSpire.csproj -c Debug \
  -p:Sts2Dir="$PWD/bin/BuildDeploy" -p:Sts2DataDir="$STS2_DATA"

# 无头运行单个特效场景，检查脚本异常（F6 等价）
"$GODOT" --headless \
  --path BangDreamInSpire --quit-after 120 "res://BangDreamInSpire/scenes/vfx/<场景>.tscn"

# 无头加载编辑器，检查场景/资源导入错误
"$GODOT" --headless --editor \
  --path BangDreamInSpire --quit-after 150
```

以上两条 Godot 命令加 `| grep -iE "error|script error|failed"`，**无输出即通过**。`--headless --editor` 不会把新场景的 uid 回写进 `.tscn`；没有引擎生成的场景 uid 时保留 path 引用，由编辑器保存生成后再记录，不能手写或猜测 uid。

无头检查通过后，仍须按 §3.1 录制并原速观看。单场景录像负责技术诊断，真实战斗录像负责最终观感；没有真实战斗录像时，不得把「能运行」或「单场景好看」写成「实战已验收」。

已知的非问题（实测确认，不要误当新缺陷）：

- **`Error loading GDExtension configuration file: 'res://addons/fmod/fmod.gdextension'`（sentry 同）**——`.godot/extension_list.cfg` 里残留了指向已不存在的 addons 的旧条目。删掉那两行即可（该文件是可再生缓存）；工程的 `addons/` 下只有 `mega_text`。
- **`ObjectDB instances leaked at exit`** —— 继承 `NBangDreamFlyingVfx` 的场景都会出现 1 条（对照 `flying_music_note_default.tscn` 同样有）。这是基类场景的固有现象，不是新特效引入的缺陷；继承 `Node2D` 的场景不会出现。
- **`Sentry GDExtension not loaded`** —— 无头运行与录帧时的常规提示，与特效无关。
- **`ERROR: Error opening file 'res://icon.svg'`** —— `project.godot` 的 `config/icon` 指向了仓库里不存在的 `icon.svg`（既有问题，与特效无关）。要根治就补一个图标文件或删掉该配置行。

场景文件头 `load_steps` 需与实际资源数一致（`ext_resource` + `sub_resource` + 1）。写错不会导致加载失败，但会让引擎重新计算并可能覆盖你的手改。

删除或替换已被场景引用的素材时，`--headless --import` 会报 `Failed loading resource: ...` / `ext_resource, invalid UID`。若你正准备改写该场景的引用，这是**预期内的中间态**，改完场景即消失；若改完仍报，说明场景里还有残留引用。

### 5.1 只能靠录帧发现的实现陷阱

**一、alpha 曲线方向写反。** `alpha = Lerp(A0, A1, 1 - x^pow)` 在 `A1 = 0` 时等价于 `A0 * x^pow`：alpha 从 0 涨到 `A0`，并在 `x` 被 clamp 到 1 后**永久停在 A0**。症状是「前段正常、后段整片挂着不消失」，且**不报任何错**。正确写法是让 `fade` 从 0 走到 1 当插值参数：`Lerp(A0, A1, fade)`。

**二、径向粒子贴图的朝向。** 贴图长轴沿局部 **Y 轴**时，要对齐径向 `offset.Angle() = φ` 需要旋转 `φ - π/2`；写成 `φ` 会让火花**切向横躺**，看起来像风车的碎片而不是飞散的拖尾。

**三、`CanvasItemMaterial`（ADD）+ 紫色 `modulate` 染「烘焙了颜色」的贴图会偏红。** 2D 加法混合在线性空间做乘法，粉色边 × 蓝紫 modulate 会压出偏红的灰紫。带颜色的贴图不要用 modulate 染色，改为 shader 里用**亮度重建颜色**（`glow_color * max(r,g,b)`）再 `blend_add` 输出（参考 `res://BangDreamInSpire/shaders/strike_sakiko_glow.gdshader`）。纯白/灰阶贴图可以直接 modulate。

**四、辉光层若固定用主帧贴图，主帧消失后会留下一整块刀影。** 辉光层应跟随主层**逐帧换同一张序列帧**，或与主层同生命周期隐藏；否则录帧会看到「白刀按帧消失、紫色大刀还在」。

**五、纯加色层在浅色背景上等于不存在。** `blend_add` 只能把画面加亮：卡面本身已经是米白/浅蓝时，
加色高光几乎看不出颜色，只剩一点白雾。给"会落在任意底色上的槽位/UI 反馈"做特效，必须同时有
一层 **MIX 的深色墨**（元素色压暗）与加法高光，两层才构成"深色饱和体 + 亮部"的可读结构；
只用加法会在亮底素材上白做。判定依据是录帧，不是算式。

**六、槽位驱动型特效注入的变换必须保留自身位移。** `ConfigureSlot(size, relativeTransform)` 内部会
`Transform = relativeTransform`。若调用方传 `Transform2D.Identity`（例如把多个特效并排摆进合成预览
场景），所有特效会被一起搬回容器原点——录出来是"整块特效消失了"，实际是位置被重置。合成预览要传
`vfx.Transform`（带上自身摆放），游戏内则传槽位相对变换。
