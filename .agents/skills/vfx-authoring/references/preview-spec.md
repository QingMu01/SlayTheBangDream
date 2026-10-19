# 静态预演规格与边界

`preview_candidates.py` 用于在正式资源修改前检查素材轮廓、染色、像素尺寸与混合风险。输出是 **CPU 静态近似**，不等于 Godot 最终渲染；颜色空间、过滤、shader、粒子、运动和动态叠加以 S5 引擎录制为准。不得以此声称动态验收已完成。

```json
{
  "background": "#4B4B4B",
  "composition": {"width": 600, "height": 600},
  "items": [
    {"path": ".vfx_staging/export/glow.jpg", "label": "glow",
     "role": "glow", "blend": "crush_add", "tint": "#6BCCFF",
     "display": 365, "alpha": 0.55, "black_point": 0.035,
     "softness": 0.07, "intensity": 1.0, "x": 300, "y": 300, "z": 0},
    {"path": ".vfx_staging/export/ring.png", "label": "ring",
     "role": "ring", "blend": "alpha_add", "tint": "#6BCCFF",
     "display": 250, "alpha": 0.75, "rotation": 0, "x": 300, "y": 300, "z": 10}
  ]
}
```

路径相对**运行命令的工作目录**。`display` 为整块画布最长边的显示像素，非主体包围框尺寸。
`role` 为 `burst`/`glow`/`flash`/`ring`/`smoke`/`spark`/`trail`/`debris`
（与 Polaris 核心 8 个角色一致；旧的 `atlas` 已弃用，仍可传入但会告警并按 `spark` 判据继续）。
角色集合与共用阈值的权威来源是 `get_selection_spec`；与库内特征的口径对照见
[selection-contract.md](selection-contract.md)。

| 字段 | 语义 |
|---|---|
| blend | alpha = MIX；alpha_add = 带纹理 alpha 的 ADD；crush_add = 黑底压黑后 ADD。省略时兼容旧规格取 alpha 并告警 |
| tint / alpha / intensity | #RRGGBB；节点 alpha 在 0–1；强度非负 |
| black_point / softness | crush_add 必填，0≤black_point<1、softness>0、两者和≤1；阈值按素材测定 |
| rotation | 角度；近似旋转采样，不能作为 Godot 过滤效果的证据 |
| veto_reason | 对洞/粒子放大等默认否决的明确艺术理由；脚本降为告警，仍须目视和计划解释 |
| silhouette_threshold | 可选内容阈值（1–255，**默认 32 = 核心 `ALPHA_TRANSPARENT`**）；只影响形态分析，不改变渲染。**不要为了让数字好看而调它**——调了就无法与库内 `fill_ratio`/连通/洞交叉校验 |
| composition | 可选静态合成画布尺寸；各素材按 z 排序，同 z 保持输入顺序，x/y 为中心位置，缺省居中 |

- 候选表每格带编号、混合模式和判定；加标记的表只用于诊断。
- 设置 composition 后，同时输出 `<out stem>_composite.png`，无参考框/文字覆盖；对主体补洞、层间过曝检查此图。
- MIX 近似 `背景×(1-a)+颜色×a`；ADD 近似 `背景+颜色×a`，8 位饱和截断。
- 压黑近似使用配方的 `k=smoothstep(black_point, black_point+softness,max(rgb))`，贡献为 `rgb×k×tint×alpha×intensity`。适用于不透明黑底；有纹理 alpha 时拒绝此模式，改用保留 alpha 的模式或以真实 shader 验证。
- 形态分析使用纹理 alpha；crush_add 使用压黑后的可见亮度。低于阈值只会告警“主体不足”，不把仍可见的柔光当成全透明。阈值产生的洞并不必然是缺陷。
- **脚本不检测深色描边**，此项使用 MCP features 与目视记录。脚本退出 0 只表示其检测项通过/告警，不能替代这项检查或引擎验收。
- 退出码：0 通过/仅告警，4 有未解释的否决，3 规格、文件或依赖错误。
