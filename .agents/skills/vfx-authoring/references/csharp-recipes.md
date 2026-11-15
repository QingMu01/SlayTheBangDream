# C# 实现配方（BangDreamInSpire）

实现规范的唯一事实来源是 `docs/vfx-authoring.md`。本文件只给**可直接抄的骨架**。

## 1. 基类决策

| 选 | 条件 |
|---|---|
| `Node2D` | 纯视觉播放，命中/结束时机由 Tween 控制。**默认选这个。** |
| `NBangDreamFlyingVfx` | 特效要"飞过去"**且**命中时刻要驱动玩法状态（伤害结算） |

不确定时先按 `Node2D` 写。参考实现：`MusicHitVfx.cs` / `MusicFlashVfx.cs`（均为 `Node2D`）。

## 2. 脚本骨架（照抄这个结构）

```csharp
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

[Tool]
public partial class MusicBurstSparkVfx : Node2D
{
    private const float Life = 0.30f;

    /// <summary>层定义，与 plan.md 的层结构表逐行对应。</summary>
    private sealed record LayerDef(
        string NodePath, float T0, float T1, float S0, float S1,
        float A0, float A1, float Rot, float AlphaPow);

    private static readonly LayerDef[] Layers =
    [
        new("Glow",  0.00f, 1.00f, 0.20f, 0.85f, 0.60f, 0f,  0f, 1.7f),
        new("Burst", 0.00f, 0.34f, 0.14f, 0.66f, 1.00f, 0f, 18f, 0.80f),
        new("Flash", 0.00f, 0.11f, 0.20f, 0.54f, 1.00f, 0f, 12f, 0.45f),
    ];

    private readonly List<(Sprite2D Node, LayerDef Def, Vector2 BaseScale, Color BaseColor, float BaseRotation)> _layers = [];
    private Tween? _tween;

    /// <summary>勾选即重播一次（自动复位，便于反复触发）。</summary>
    [Export]
    public bool Play
    {
        get => false;                       // 永远读回 false，保证可反复勾选
        set { if (value) Replay(); }
    }

    /// <summary>预览态下播放结束后自动重播。</summary>
    [Export] public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        foreach (var def in Layers)
        {
            if (GetNodeOrNull<Sprite2D>(def.NodePath) is not { } sprite)
                continue;

            // 铁律一：从场景读回美术基准值，不要硬编码 Vector2.One
            _layers.Add((sprite, def, sprite.Scale, sprite.Modulate, sprite.RotationDegrees));
        }

        // _Ready 里不写任何视觉状态；只在运行态播放
        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>重置并重新播放一次。游戏内由 _Ready 触发，预览可手动/循环触发。</summary>
    public void Replay()
    {
        _tween?.Kill();

        VfxPreviewSupport.CenterForPreview(this);   // 预览时摆到视口中心

        // 铁律一：复位到场景基准值，而不是 Colors.White / Vector2.One
        foreach (var (node, _, baseScale, baseColor, baseRotation) in _layers)
        {
            node.Scale = baseScale;
            node.Modulate = baseColor;
            node.RotationDegrees = baseRotation;
        }

        // 铁律三：单一 TweenMethod 算全部状态
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(ApplyState), 0f, 1f, Life);
        _tween.Finished += OnPlaybackFinished;
    }

    private void ApplyState(float progress)
    {
        foreach (var (node, def, baseScale, baseColor, baseRotation) in _layers)
        {
            var span = Mathf.Max(def.T1 - def.T0, 0.0001f);
            var local = Mathf.Clamp((progress - def.T0) / span, 0f, 1f);

            var eScale = 1f - Mathf.Pow(1f - local, 3f);        // 缓出
            var fade = Mathf.Pow(local, def.AlphaPow);         // 插值从 0 到 1；越小消失越快

            node.Scale = baseScale * Mathf.Lerp(def.S0, def.S1, eScale);
            node.RotationDegrees = baseRotation + def.Rot * eScale;
            var alpha = progress < def.T0 || progress >= def.T1
                ? 0f : Mathf.Lerp(def.A0, def.A1, fade);
            node.Modulate = new Color(baseColor.R, baseColor.G, baseColor.B, baseColor.A * alpha);
        }
    }

    private void OnPlaybackFinished()
    {
        if (VfxPreviewSupport.ShouldSelfFree(this))
        {
            this.QueueFreeSafely();
            return;
        }

        if (LoopPreview)
            Replay();
    }
}
```

**为什么必须用单一 `TweenMethod`**：`SetParallel(true)` 下两个 tweener 写同一属性，后建的会**静默**覆盖先建的，不报任何错。症状是"动画看起来没生效"，只能靠录帧发现。

## 3. 黑底素材的运行时着色

预览脚本里的 `crush_add` 是**静态近似**；运行时不要依赖一个并不存在的 `crush_add.gdshader`。实际工程做法是**用亮度重建颜色**再加法输出，避免 `CanvasItemMaterial(ADD)` + 彩色 `modulate` 在线性空间乘法压出偏红边。参考 `res://BangDreamInSpire/shaders/strike_sakiko_glow.gdshader`：

```glsl
shader_type canvas_item;
render_mode blend_add;

uniform vec3 glow_color : source_color = vec3(0.541176, 0.498039, 0.815686);
uniform float intensity : hint_range(0.5, 2.5) = 1.2;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float mx = max(max(tex.r, tex.g), tex.b);
    vec4 modulate = COLOR;
    COLOR = vec4(glow_color * mx * intensity * modulate.rgb, tex.a * modulate.a);
}
```

- **纯白/灰阶贴图**可以直接 `modulate`；**带颜色的贴图不要**用 modulate 染色。
- 黑底不透明素材的压黑阈值要按素材实测（JPG 的"黑"不是纯黑），在预览规格里写 `black_point` / `softness`；运行时用上述亮度重建或等效 shader，并保留 `tex.a`。
- 有 alpha 的素材不要走忽略纹理 alpha 的压黑路径，否则透明区域会叠出方块。

## 4. 每实例材质（铁律二）

编辑器里写场景共享的 `ShaderMaterial` / `ParticleProcessMaterial` 会把场景标记为已修改并污染资源：

```csharp
private static void SetShaderParam(CanvasItem node, StringName name, Variant value)
{
    if (node.Material is not ShaderMaterial material)
        return;

    if (Engine.IsEditorHint())
        node.SetInstanceShaderParameter(name, value);   // 每实例，不脏场景
    else
        material.SetShaderParameter(name, value);       // 游戏内沿用原写法
}
```

粒子着色同理：编辑器用 `particles.SelfModulate`，游戏内写 `ProcessMaterial.Color`（见 `MusicFlashVfx.ApplyTint`）。

## 5. 图集 / flipbook

行列数**必须**来自 `get_asset_group_preview`，不要目测：

```csharp
sprite.Hframes = 4;      // 来自 get_asset_group_preview
sprite.Vframes = 4;
// 在 ApplyState 里按层进度选帧
sprite.Frame = Mathf.Min((int)(local * 16f), 15);
```

## 6. 场景与资源

- `.import` 与 uid 由 `--headless --import` 生成，**不要手写 uid**。
- 场景引用资源用引擎生成的 uid（读 `.import` 的 `uid=` 行），`path=` 保留作可读性兜底。
- `.import` 必须与 PNG 留在同一目录，否则场景引用失效。
- `load_steps` = `ext_resource` + `sub_resource` + 1。

### 6.1 尺寸归一化（写场景前必须先做）

库内贴图的像素尺寸差异极大，若各层共用一组 `s0`/`s1` 而不归一，尺寸会严重失衡。
约定：把每张贴图归一到统一的 **384px 参考框**，由**场景节点 `scale`** 承担归一化；脚本里的 `s0`/`s1` 保持为**相对倍数**。

```
节点 scale       = 384 / max(贴图宽, 贴图高)
实际显示像素      = 节点 scale × 贴图原始边长 × 层 s
放大倍数          = 节点 scale × 层 s
```

| 贴图原生 | 节点 scale | 层 s1=0.95 时实际显示 | 放大倍数 |
|---|---|---|---|
| 72×80 | 4.80 | 328px | 4.55×（偏糊） |
| 128×128 | 3.00 | 365px | 2.85× |
| 256×256 | 1.50 | 365px | 1.43× |
| 512×512 | 0.75 | 365px | 0.71× |

> **放大倍数超过 3× 就要预期插值模糊**。选材时优先挑原生尺寸够大的（见 [asset-selection.md](asset-selection.md) 陷阱 8）。

## 7. 接入确认（做完不等于会出现）

```bash
grep -rn "MusicBurstSparkVfx\|vfx_music_burst_spark" --include=*.cs --include=*.tscn .
```

只命中定义文件本身 = **孤儿特效**，游戏里永远不会触发。

**`WithHitFx` 的坑**：游戏内走 `VfxCmd` → `SceneHelper.GetScenePath`，规则是 `"res://scenes/" + innerPath + ".tscn"`，解析到**游戏本体**资源目录。所以 `WithHitFx("vfx/xxx")` 拿到的是原版特效，**永远到不了 mod 自己的 `res://BangDreamInSpire/scenes/vfx/...`**。要让某张卡用上自定义特效，必须走注入路径（`PreloadManager.Cache.GetScene(...)` + `AddChildSafely`），不能只改字符串。

## 8. 编译与无头验证

```bash
# 从 Directory.Build.props 读 Godot 与本体数据目录（单属性查询直接打印值）
GODOT=$(dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:GodotExe)
STS2_DATA=$(dotnet msbuild BangDreamInSpire/BangDreamInSpire.csproj -getProperty:Sts2DataDir)

dotnet build BangDreamInSpire/BangDreamInSpire.csproj -c Debug \
  -p:Sts2Dir="$PWD/bin/BuildDeploy" -p:Sts2DataDir="$STS2_DATA"

"$GODOT" --headless \
  --path BangDreamInSpire --quit-after 120 "res://BangDreamInSpire/scenes/vfx/<场景>.tscn"
```

**这两条只证明"没有报错"，不证明"看起来对"。** 调观感必须录帧——见 [SKILL.md](../SKILL.md) 的 S5，以及 `docs/vfx-authoring.md` §3.1 / §5.1 的陷阱清单。

## 9. 已知非问题

- `ObjectDB instances leaked at exit`：仅 `NBangDreamFlyingVfx` 场景出现，基类固有现象。
- `Sentry GDExtension not loaded`：无头/录制运行的常规提示。
- `ERROR: Error opening file 'res://icon.svg'`：`project.godot` 引用了不存在的图标，既有问题，与特效无关。

**背景是灰（约 76/255），不是黑。** 判断元素消失时不要按黑底预期；`verify_frames.py` 用众数自适应背景，黑底与灰底都能处理。
