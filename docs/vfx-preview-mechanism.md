# 特效预览机制

特效脚本同时运行在三种环境下，行为各不相同：

| 环境 | 表现 |
|---|---|
| **编辑器里打开场景** | **完全静止**。节点保持 `.tscn` 中的作者摆放，脚本不播放、不写视觉状态，便于摆位设计 |
| **F6 单独运行场景** | 自动播放，循环预览，可实时调参 |
| **游戏内** | 由战斗流程驱动，行为与改造前逐行等价 |

本文说明三者如何共存。要动手写一个新特效，请看《特效开发指南》（`vfx-authoring.md`）。

## 1. 判定接口

不能只用 `Engine.IsEditorHint()`：F6 运行时它为 `false`。`VfxPreviewSupport`（`Scripts/Utils/VfxPreviewSupport.cs`）是唯一的接缝，特效脚本只向它提问：

| 成员 | 用途 |
|---|---|
| `AutoPlayOnReady` | `_Ready` 时是否自动播放。**仅运行态为 true**，编辑器里编辑场景时为 false → 静止 |
| `IsPreviewRun(node)` | 是否正处于「F6 单独运行场景」的预览中（编辑器里为 false）。用于参数改动即时重播 |
| `IsPreview(node)` | 是否处于单独预览（编辑器打开 或 F6 运行时的场景根） |
| `IsCombatLive` | 真实战斗是否进行中；编辑器内恒 `true` |
| `ShouldContinuePlayback(node)` | 是否允许继续播放（预览态或真实战斗） |
| `ShouldSelfFree(node)` | 播放结束是否自我销毁；编辑器里永不销毁 |
| `RandomCharacterColor(node)` | 角色主题色；预览态用桩调色板，游戏内用 `ModelDb` |

判定「场景根」的依据：游戏内的特效都是被 `AddChild` 到容器或角色上的**子节点**，因此

```csharp
// 编辑器里被编辑的场景根 / F6 运行时的场景根
Engine.IsEditorHint()
    ? node.GetTree().GetEditedSceneRoot() == node
    : node.GetTree().CurrentScene == node;
```

`IsPreview` 在编辑器里也为 true（编辑的场景根），用于让 `ShouldSelfFree` 在编辑器里返回 false；而**自动播放只看 `AutoPlayOnReady`**，所以编辑器里不会自己跑起来。

## 2. 播放开关约定

每个特效都提供一致的 Inspector 开关，照搬游戏 `play_vfx_tool.gd` 的自复位技巧：

```csharp
/// <summary>勾选即重播一次（自动复位，便于反复触发）。</summary>
[Export]
public bool Play
{
    get => false;                       // 永远读回 false，保证可反复勾选
    set { if (value) Replay(); }
}

/// <summary>预览态下播放结束后自动重播。</summary>
[Export] public bool LoopPreview { get; set; } = true;
```

`[Tool]` + `Replay()` 是核心：`_Ready` 里只在 `VfxPreviewSupport.AutoPlayOnReady` 成立时调用 `Replay()`，于是编辑器里静止、F6 与游戏内播放。

因为编辑器里不自动播放，想让编辑器预览也完全不动，`_Ready` 里**不要写任何视觉状态**（别隐藏子节点、别改缩放/颜色/着色参数）——这些交给 `Replay()` 在真正播放时做。例如均衡器只在非编辑器时才隐藏柱子，编辑器里保留作者摆放的可见性。

## 3. 预览默认位置

场景根节点都在 `(0,0)`，而 `(0,0)` 是视口左上角（本项目视口 1920×1080，无 Camera2D），所以不做处理时特效会贴在右上角附近。约定：

| 类型 | 预览默认位置 | 实现 |
|---|---|---|
| 静止类 | 视口中心 `(W/2, H/2)` | `Replay()` 里调 `VfxPreviewSupport.CenterForPreview(this)` |
| 移动类 | y 取中线，x 从 `W/4` 到 `3W/4` | `VfxPreviewSupport.PreviewTravelSpan(this)` |
| card_trail | 傀儡在 `W/4`↔`3W/4` 间往返，y 取中线 | 同上 |

相关辅助：`PreviewViewportSize(node)`、`PreviewPoint(node, fraction)`、`PreviewTravelSpan(node)`、`CenterForPreview(node)`。

**这些方法都只在 `IsPreviewRun` 成立时生效**，游戏内位置仍由调用方决定（`PerformFlashVfx` 由演奏区、`NLingeredOrbitVfx` 由绑定生物、飞行类由 `SetPath`）。编辑器里也不写位置，保持作者摆放。飞行类的自动航程可用 `[Export] AutoPreviewSpan` 关闭，改用手填的 `PreviewStart`/`PreviewEnd` 相对偏移。

## 4. card_trail 的特殊处理

`BangDreamCardTrailVfx` 继承游戏 `NCardTrailVfx`，必须注入一个跟随目标才能工作。游戏内由 `NCardTrailVfx.Create(card, path)` 注入；F6 预览时 `_Ready` 会自造一个傀儡 `Control` 并让它来回平移，从而拖出轨迹。

编辑器里它**完全惰性**：`_Ready` 早在开头就把 `ProcessMode` 设为 `Disabled` 并直接返回，连基类 `_Ready` 都不调用（基类会启动跟随与入场补间）。想预览就按 F6，或在 Inspector 勾选 `Play`（会恢复进程并补上基类初始化）。

跟随目标挂在**根节点的父级**下（不是 `GetTree().Root`），以保证与根处于同一视口；且必须是根节点的「非后代」，否则根跟随傀儡、傀儡又随根移动会形成递归漂移。注入私有的 `_nodeToFollow` 走 `AccessTools.Field`（本仓库未公开游戏私有成员）。

## 5. 编辑器里为什么是静止的

编辑器打开场景不写任何节点状态，因此不会把场景标记为「已修改」，也不会丢作者的摆放。想调参又不想保存，就在 F6 里调；F6 会写节点属性（动画本身），那属于运行期瞬时状态，关掉即丢弃，不会影响 `.tscn`。写共享材质/粒子材质的路径已在《特效开发指南》第 1 节「三条铁律」中规避，不会污染 `SubResource`。

## 6. `_Process` 驱动的特效

`MusicWaveVfx`、`MusicEqualizerVfx`、`MusicNoteFlyingVfx`、`NLingeredOrbitVfx` 把逐帧推进与「是否允许继续播放」判断分开：编辑器里因未启动播放而静止，F6/游戏内正常推进。用 `ShouldContinuePlayback(this)` 做守卫，而不是自行判断环境。