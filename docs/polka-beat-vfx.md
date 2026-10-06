# 波点律动攻击特效（PolkaBeatVfx）

以**节拍（BPM）**驱动的波点阵为主视觉的 2D 攻击特效。纯表现、不接入任何卡牌；
预览与自毁约定见《特效预览机制》，编写约定见《特效开发指南》。

- 脚本：`BangDreamLib/Scripts/Nodes/VFX/PolkaBeatVfx.cs`
- 场景：`BangDreamLib/BangDreamLib/scenes/vfx/vfx_polka_beat.tscn`（F6 直接预览）
- 着色器：`BangDreamLib/BangDreamLib/shaders/polka_beat.gdshader`
- 素材：`BangDreamLib/BangDreamLib/images/vfx/polka_beat/`（来源见该目录 `SOURCES.md`）

## 1. 为什么基类是 `Node2D`

本特效是纯视觉播放，命中时机不影响玩法结算（调用方只关心「播了」），
因此不继承 `NBangDreamFlyingVfx`。判据见《特效开发指南》第 2 节。

## 2. 节奏结构

```
Beat      = 60 / BeatBpm          (默认 210 BPM → 0.2857s)
HitTime   = (HitBeat - 1) * Beat  (默认第 3 拍 → 0.5714s)
TotalTime = HitTime + TailTime    (默认 TailTime 0.45 → 约 1.02s)
```

| 时刻 | 事件 |
|---|---|
| `t = 0` | 点阵场从中心冒头（扩张前沿半径 0.05），第 1 波波点环（8 点）由 r≈40 外扩 |
| `t = Beat` | 第 2 波波点环（12 点）；扩张前沿推到第二档，波形带波峰从中心出发 |
| `t = 2·Beat` | 第 3 波波点环（16 点）**与命中同帧**：律动核炸开 + 闪光 / 扩散环 / 冲击波 / 波点·星点粒子 |
| `t = TotalTime` | 自毁或循环预览 |

每拍脉冲 `beat_pulse = (1 - 拍内进度)³`：拍点最亮，拍内迅速衰减，构成「律动」。

**全程不使用 Tween**：所有状态由 `ApplyState(elapsed)` 单点求值。
这既避开《特效开发指南》铁律三（并行 Tween 争写同一属性会静默覆盖），
也让「第几拍发生什么」可以按时间线性阅读。逐帧推进与
`VfxPreviewSupport.ShouldContinuePlayback` 的守卫方式与 `MusicEqualizerVfx` 一致。

## 3. 图层

| 节点 | 素材 | 作用 |
|---|---|---|
| `DotField` | 程序化点阵 + 库网点贴图（着色器） | 波点主层：**扩张前沿**、节拍呼吸、波前亮带、命中飞散、主题色 |
| `BeatWave` | `res://images/vfx/dot.png` ×9 | 波点波形带：每拍从中心向两端推一个「波峰」，波峰处的点被放大提亮 |
| `BeatRings/Ring0..2` | `res://images/vfx/dot.png` | 三拍波点环，环内相邻点错帧 0.012s 形成顺时针扫入 |
| `BeatCore` | `res://images/vfx/dot.png` ×8 | 律动核：命中前在中心逐拍搏动，命中瞬间炸开成波点环 |
| `ImpactGroup/*` | 冲击环 / 9 帧扩散环 / 命中闪光 | 命中爆发 |
| `DotParticles` | `res://images/vfx/dot.png` | 波点粒子（跟随主题色） |
| `StarParticles` | `polka_beat_stars.png`（2×2） | 星点粒子 |

**除星点粒子贴图外，所有元素都是同一套波点语言**：主题色柔光点 + 加法混合。
`BeatWave` 与 `BeatCore` 的点由脚本按时间函数摆放/缩放，作者的摆放位置只提供
「距中心的归一化距离」（波形带）与「基准半径 + 起始角度」（律动核），重播时据此复位。

### 3.1 点阵场的「扩张」为什么不能靠缩放

`DotField` 早期只有一条 `Scale` 补间（0.62 → 1.0）。缩放的问题是**点跟着一起变大**：
整块贴图等比放大，读起来是「点阵变大了」，而不是「点阵扩张开」。
现改为由着色器的 `reveal_radius` 驱动一条**透明度前沿**：前沿之外的点完全透明，
前沿按拍向外推（拍内 `EaseOutQuad` 先快后慢），并叠加 `field_alpha` 的整场呼吸。
于是扩张的节奏与波点环、波峰同源，且膨胀感来自「点逐圈显现」而非「点变大」。

- `RevealStart = 0.14` → `RevealAtHit = 0.44` → `RevealEnd = 0.48`（UV 半径；再大就会被画布边界切方）。
- 每拍推进量 `RevealPerBeat` 由 `HitBeat` 反推，改拍数后前沿仍恰好在命中那拍抵达 `RevealAtHit`。
- 波前亮带固定在 `reveal_radius - 0.05`，读作「正在扩张的边缘」。

这条前沿踩过两个坑，都表现为「点阵场整个不见了」：

1. **场景里作者的默认值必须是「前沿推到底」的满场**（`reveal_radius = 0.48`），
   否则编辑器打开场景时看到的是空场，无法摆位——预览约定要求编辑器里保留作者的摆放。
   运行期每帧都会重写这个 uniform，所以它只影响编辑器的静态视图。
2. **前沿不能小于一个格子，柔度不能太大。** `cells = 13` 时每格约 `512/13 ≈ 39px`（UV 0.077）；
   起初把 `RevealStart` 取 0.05（25px）、`reveal_softness` 取 0.14，
   等于「实心核心只有 20px、其余全是渐变」，起播瞬间几乎什么都看不到。
   现取 `RevealStart = 0.14`、`reveal_softness = 0.08`：起播即有约 10 个点的实心小块，
   再一路扩张到 225px。

### 3.2 为什么不放异质色的贴图装饰

首版曾用 `special_object_angel2_01`（金白音波条）与 `special_object_angel2_#6_08`
（金色谱号装饰）做节奏点缀，录帧后发现两个问题：金橙色与主题色冲突、读作「外来元素」；
谱号用 Back-Out 弹入 + 保持 + 消隐，等于把一个硬剪影「盖章」在画面上。
现改为纯波点实现（波形带 / 律动核），这两个素材已从特效目录移除。
`oblvns_skin_05`（音符图集）曾作为 `NoteParticles` 使用，因与波点语言重复也一并移除。

波点环的**环数与每环点数由场景结构决定**（`BeatRings` 的子节点数），
加一个 `Ring3` 不需要改代码；每个点的起始角度与起始半径由作者摆放的
`Position` 反解，重播时按此复位（不写死初值）。

## 4. 着色器 `polka_beat.gdshader`

`canvas_item` + `blend_add`。之所以必须用着色器：逐像素的节拍波前、
主题色重着色、命中时的径向飞散，这三件事用精灵摆放做不到。
尤其库网点贴图本身是青色的，`modulate` 染不成主题色——
着色器**只取它的 alpha 作「网点浓度」**，颜色完全由 `tint` 决定。

| uniform | 默认 | 含义 |
|---|---|---|
| `dot_pattern` | 库网点贴图 | 网点浓度遮罩（只用 alpha）；未绑定时退化为纯程序化点阵，不报错 |
| `pattern_weight` | 0.55 | 网点浓度在遮罩里的权重 |
| `cells` / `dot_radius` / `dot_softness` | 13 / 0.17 / 0.04 | 程序化点阵的密度与单点半径、边缘柔度 |
| `jitter_amount` / `size_variation` | 0.22 / 0.45 | 每格抖动与点径差异，打破死板的屏幕点阵 |
| `wave_radius` / `wave_width` / `wave_strength` | 0 / 0.12 / 1.8 | 向外传播的波前位置、厚度、放大加亮强度 |
| `beat_pulse` | 0 | 当前拍脉冲 |
| `scatter` | 0 | 命中后点阵向外飞散量 |
| `reveal_radius` / `reveal_softness` | 0.48 / 0.08 | **扩张前沿**半径与边缘柔度：之外的点完全透明（点阵「变大」靠这条前沿，不靠缩放）。场景里的作者默认值是「满场」，运行期每帧重写 |
| `field_alpha` | 1.0 | 整场不透明度增益，随拍呼吸（拍点 1.0、拍内回落到 0.7） |
| `brightness` | 1.0 | 亮度倍率（命中瞬间冲到 2.6） |
| `tint` | 白 | 主题色；**alpha 同时充当整段包络**（淡入 / 收尾） |

## 5. 可调参数（Inspector）

节奏：`BeatBpm`(60–400) / `HitBeat`(1–8) / `TailTime` / `RingExpand`。
点阵：`FieldCells` / `DotRadius` / `PatternWeight` / `WaveStrength`。
着色：`PreviewTint` / `RandomizeTint`。
开关：`Play`（勾选重播一次） / `LoopPreview`。

参数 setter 只在 `VfxPreviewSupport.IsPreviewRun(this)` 成立时重播，编辑器里不重播。

## 6. 三条铁律的落实

1. **不硬编码初值**：`_Ready` 只读回场景里的美术基准值（各 Sprite 的 Scale/Modulate、
   每个点的极角与基准缩放），`Replay()` 按基准复位。
2. **编辑器不写共享材质**：着色参数走 `SetInstanceShaderParameter`（游戏内才写
   `ShaderMaterial.SetShaderParameter`）；波点粒子同理用 `SelfModulate` / `ProcessMaterial.Color`。
3. **不用并行 Tween**：见第 2 节。

编辑器里 `_Ready` 不写任何视觉状态，因此打开场景完全静止、不会把场景标记为已修改；
预览默认位置由 `VfxPreviewSupport.CenterForPreview` 在 `Replay()` 里设置。

## 7. 验证

```bash
# 编译
dotnet build BangDreamLib/BangDreamLib.csproj -c Debug \
  -p:Sts2Dir="bin/BuildDeploy" \
  -p:Sts2DataDir="D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64"

# 无头跑场景 / 无头开编辑器，各 grep error 应为空
D:/Megadot/MegaDot_v4.5.1-stable_mono_win64_console.exe --headless --path BangDreamLib \
  --quit-after 120 "res://BangDreamLib/scenes/vfx/vfx_polka_beat.tscn"

# 录帧核对（调观感的唯一可靠手段；受限环境需 --log-file 绕开 user:// 写入）
D:/Megadot/MegaDot_v4.5.1-stable_mono_win64_console.exe --path BangDreamLib \
  --write-movie ".vfx_staging/rec_polka/frame.png" --fixed-fps 60 --quit-after 90 \
  "res://BangDreamLib/scenes/vfx/vfx_polka_beat.tscn"
```

录帧后确认的几件事（首版录帧暴露、已修）：点阵过疏且点过软 → `cells` 9→13、
`dot_radius` 0.22→0.17、`dot_softness` 0.06→0.04；死板栅格 → 加 `jitter_amount`/`size_variation`；
音波条与谱号装饰异质、突兀 → 换成 `BeatWave`（波点波形带）与 `BeatCore`（律动核），
且两者的点必须**比点阵场的点更大更亮**才读得出来（波形带 0.10 基准缩放、波峰 1.5×；
律动核 r=20、0.09 缩放、命中时炸开到 4.2×）；
点阵场只播缩放显得「没动效」→ 去掉 `DotField.Scale` 补间，改由 `reveal_radius`
透明度前沿 + `field_alpha` 呼吸驱动扩张（见 §3.1）；改完又一度「整个不可见」，
原因是作者默认值取成了起播值、且柔度过大（同见 §3.1 的两个坑）。

## 8. 接入卡牌时注意（本次刻意未做）

`WithHitFx("vfx/...")` 走 `VfxCmd` → `SceneHelper.GetScenePath`，解析到的是**游戏本体**的
`res://scenes/`，永远到不了 mod 自己的 `res://BangDreamLib/scenes/vfx/...`。
要接入必须走注入路径（`PreloadManager.Cache.GetScene(...)` + `AddChildSafely`），
不能只改字符串。当前全仓库搜索 `PolkaBeatVfx` / `vfx_polka_beat` 只命中定义文件自身，
即本特效是**孤儿**——这是本次的预期状态。