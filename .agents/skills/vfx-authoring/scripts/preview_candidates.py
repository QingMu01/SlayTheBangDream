#!/usr/bin/env python3
"""素材形态与静态混合预演（CPU 近似，不替代引擎动态验收）。

规格、混合公式与限制见 references/preview-spec.md。
深色描边需 MCP/目视检查，本脚本不自动检测。
退出码：0 通过/仅告警；4 有未解释的否决；3 规格/文件/依赖错误。
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import deque
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageChops
except ImportError:  # pragma: no cover
    print("[preview] 需要 Pillow：pip install Pillow", file=sys.stderr)
    raise SystemExit(3) from None

# 分析用的降采样边长：**与 Polaris 核心 WORK_SIZE(256) 一致**——
# 同一边长才能让"预演算出的连通/洞"与库内 features 的「剪影 连通/洞」直接比对。
ANALYSIS_MAX = 256

# ---------------------------------------------------------------------------
# 与 Polaris 核心共用的阈值（权威来源：get_selection_spec /
# resource polaris://spec/selection.json；口径对照见 references/selection-contract.md）
# ---------------------------------------------------------------------------
# 内容像素阈值：alpha ≥ 该值即"看得见的内容"。
# 与核心 features 的 ALPHA_TRANSPARENT(32) **完全一致**——否则同一张柔光图，
# 库内算 fill_ratio=0.31、预演算 0.22，两个工具给出不同结论就无法交叉校验。
CONTENT_ALPHA = 32
# 「边缘实心」阈值 = 核心 DARK_OUTLINE_MIN_ALPHA(128)：
# 只在判断"边缘是否真的实心（而非渐隐）"时使用，**不是主体阈值**。
SOLID_EDGE_ALPHA = 128

# 角色 → 否决规则。角色集合与核心 selection::ROLES 一致（8 个，见契约）：
#   burst / glow / flash / ring / smoke / spark / trail / debris
# `atlas` 已弃用：图集是**组织方式**（representation=grid_animation|packed_atlas|
# image_sequence），不是用途。旧规格仍可用，但会打弃用告警并退化为 spark 判据。
#   holes:      "none"=必须为 0（爆散/光斑有洞即缺陷）  "any"=允许（环形/散点属预期）
#               "warn"=只告警（烟雾的空洞可能是缺陷）
#   min_fill:   填充率低于此值即告警；None = 该角色不判定填充率
#               （**这是预演侧启发式**：核心不按填充率做角色否决，见契约对照表）
#   note:       命中时给出的解释
VETO: dict[str, dict] = {
    "burst": {"holes": "none", "min_fill": 0.25, "note": "爆散/光斑/闪光核心：有洞则中心会露窟窿"},
    "glow": {"holes": "none", "min_fill": None, "note": "光斑：中心空洞需说明或用核心层填补"},
    "flash": {"holes": "none", "min_fill": 0.25, "note": "闪光核心：有洞则中心会露窟窿"},
    "ring": {"holes": "any", "min_fill": None, "note": "圆环/框线：洞与低填充率都属预期"},
    "smoke": {"holes": "warn", "min_fill": None, "note": "烟雾：有洞可能是空洞，需目视确认"},
    "spark": {"holes": "any", "min_fill": 0.3, "note": "火花/粒子：洞属预期，但小图放大会糊"},
    "trail": {"holes": "any", "min_fill": None, "note": "拖尾：细长条带，填充率天然偏低，不据此否决"},
    "debris": {"holes": "any", "min_fill": None, "note": "碎片/碎屑：多连通属预期，看单块是否可辨"},
}
DEPRECATED_ROLES: dict[str, str] = {"atlas": "spark"}

MAX_UPSCALE = 3.0      # 放大倍数超过它即告警；粒子角色下直接否决


def parse_color(text: str) -> tuple[int, int, int]:
    value = text.strip().lstrip("#")
    if len(value) != 6:
        raise ValueError(f"颜色需为 #RRGGBB：{text}")
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))  # type: ignore[return-value]


def count_regions(mask: list[bool], w: int, h: int) -> tuple[int, int]:
    """返回 (主体连通数, 洞数)。4 连通。

    - **主体连通数**：主体像素的连通块个数。1 = 单颗，多 = 散点或图集。
    - **洞数**：不与画面边界相连的**背景**连通块个数（即被主体包围的空洞）。

    与库内 features 的「剪影 连通 / 洞」语义一致，用于交叉校验。
    """
    def idx(x: int, y: int) -> int:
        return y * w + x

    def flood(start: int, want: bool) -> tuple[bool, list[int]]:
        """从 start 出发泛洪同值区域，返回 (是否触边, 该区域像素列表)。"""
        region: list[int] = []
        queue = deque([start])
        seen[start] = 1
        touches_border = False
        while queue:
            cur = queue.popleft()
            region.append(cur)
            cx, cy = cur % w, cur // w
            if cx == 0 or cy == 0 or cx == w - 1 or cy == h - 1:
                touches_border = True
            for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                if 0 <= nx < w and 0 <= ny < h:
                    nxt = idx(nx, ny)
                    if not seen[nxt] and mask[nxt] == want:
                        seen[nxt] = 1
                        queue.append(nxt)
        return touches_border, region

    # 主体连通数：必须数 mask == True 的区域（数背景会得到几十万的荒谬值）
    seen = bytearray(w * h)
    components = 0
    for start in range(w * h):
        if seen[start] or not mask[start]:
            continue
        flood(start, True)
        components += 1

    # 洞：数 mask == False 且不触边的区域
    seen = bytearray(w * h)
    holes = 0
    for start in range(w * h):
        if seen[start] or mask[start]:
            continue
        touches_border, _ = flood(start, False)
        if not touches_border:
            holes += 1

    return components, holes


def analyze(alpha: Image.Image, threshold: int = CONTENT_ALPHA) -> dict:
    """在降采样后的 alpha 上算内容框、填充率、连通、洞。

    默认阈值 = `CONTENT_ALPHA`（与核心 ALPHA_TRANSPARENT 一致），
    使本函数的 fill_ratio / components / holes 可与库内 features 交叉校验。
    """
    w, h = alpha.size
    scale = ANALYSIS_MAX / max(w, h)
    if scale < 1:
        alpha = alpha.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.NEAREST)

    aw, ah = alpha.size
    data = alpha.tobytes()
    mask = [value >= threshold for value in data]

    xs = [i % aw for i, solid in enumerate(mask) if solid]
    ys = [i // aw for i, solid in enumerate(mask) if solid]
    if not xs:
        return {"empty": True}

    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    box_area = (x1 - x0 + 1) * (y1 - y0 + 1)
    solid_count = sum(1 for solid in mask if solid)
    components, holes = count_regions(mask, aw, ah)

    return {
        "empty": False,
        "bbox": (x0 / aw, y0 / ah, (x1 + 1) / aw, (y1 + 1) / ah),
        "content_px": ((x1 - x0 + 1) / aw * w, (y1 - y0 + 1) / ah * h),
        "fill_ratio": solid_count / box_area if box_area else 0.0,
        "coverage": solid_count / (aw * ah),
        "components": components,
        "holes": holes,
    }


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def prepare(item: dict) -> dict:
    path = Path(item["path"])
    label = str(item.get("label") or path.stem)
    role = str(item.get("role") or "burst")
    role_warning = None
    if role in DEPRECATED_ROLES:
        # 旧规格兼容：role=atlas 已弃用（图集是组织方式不是用途），退化为 spark 判据并告警
        fallback = DEPRECATED_ROLES[role]
        role_warning = (
            f"role={role} 已弃用（图集是组织方式，不是用途）：改用 "
            f"representation=grid_animation|packed_atlas|image_sequence 与相应角色；"
            f"本次按 {fallback} 判据继续"
        )
        print(f"[preview] {label}: {role_warning}", file=sys.stderr)
        role = fallback
    if role not in VETO:
        raise ValueError(
            f"{label}: 未知 role {role}（可选 {'/'.join(VETO)}；atlas 已弃用）"
        )
    blend = item.get("blend", "alpha")
    if blend not in {"alpha", "alpha_add", "crush_add"}:
        raise ValueError(f"{label}: 未知 blend {blend}")
    display = int(item["display"])
    alpha = float(item.get("alpha", 1.0))
    intensity = float(item.get("intensity", 1.0))
    if display <= 0 or not 0 <= alpha <= 1 or not 0 <= intensity < float("inf"):
        raise ValueError(f"{label}: display/alpha/intensity 无效")
    rotation = float(item.get("rotation", 0.0))
    if not -float("inf") < rotation < float("inf"):
        raise ValueError(f"{label}: rotation 必须有限")
    threshold = int(item.get("silhouette_threshold", CONTENT_ALPHA))
    if not 1 <= threshold <= 255:
        raise ValueError(f"{label}: silhouette_threshold 应为 1–255")
    tint = parse_color(item.get("tint", "#FFFFFF"))
    with Image.open(path) as raw:
        source = raw.convert("RGBA")
    warnings = []
    if role_warning:
        warnings.append(role_warning)
    if "blend" not in item:
        warnings.append("未声明 blend，兼容取 alpha；不能据此确认发光效果")
    silhouette = source.getchannel("A")
    if blend == "crush_add":
        if silhouette.getextrema()[0] != 255:
            raise ValueError(f"{label}: crush_add 仅支持不透明黑底，纹理 alpha 应使用 alpha/alpha_add")
        black = float(item["black_point"])
        softness = float(item["softness"])
        if not 0 <= black < 1 or not 0 < softness <= 1 or black + softness > 1:
            raise ValueError(f"{label}: 压黑参数无效")
        colors, mask = [], []
        pixels = source.get_flattened_data() if hasattr(source, "get_flattened_data") else source.getdata()
        for r, g, b, _ in pixels:
            lum = max(r, g, b) / 255.0
            k = smoothstep(black, black + softness, lum)
            # alpha/intensity participate in contribution, not silhouette topology.
            mask.append(round(lum * k * 255))
            colors.append(tuple(min(255, round(c * t / 255 * k * alpha * intensity))
                                for c, t in zip((r, g, b), tint)) + (255,))
        prepared = Image.new("RGBA", source.size)
        prepared.putdata(colors)
        silhouette = Image.new("L", source.size)
        silhouette.putdata(mask)
    elif blend == "alpha_add":
        # Compute additive contribution before 8-bit clipping, so intensity >1
        # combined with low alpha does not lose brightness prematurely.
        pixels = source.get_flattened_data() if hasattr(source, "get_flattened_data") else source.getdata()
        colors = [tuple(min(255, round(c * t / 255 * a / 255 * alpha * intensity))
                        for c, t in zip((r, g, b), tint)) + (255,)
                  for r, g, b, a in pixels]
        prepared = Image.new("RGBA", source.size)
        prepared.putdata(colors)
    else:
        r, g, b, a = source.split()
        prepared = Image.merge("RGBA", (
            r.point(lambda v: min(255, round(v * tint[0] / 255 * intensity))),
            g.point(lambda v: min(255, round(v * tint[1] / 255 * intensity))),
            b.point(lambda v: min(255, round(v * tint[2] / 255 * intensity))),
            a.point(lambda v: round(v * alpha)),
        ))
    stats = analyze(silhouette, threshold)
    problems = []
    if stats["empty"]:
        if silhouette.getextrema()[1] == 0:
            problems.append("素材无可见主体（全透明或压黑后全黑）")
        else:
            warnings.append("主体低于分析阈值，形态须目视或降低 silhouette_threshold")
    else:
        rules = VETO[role]
        if stats["holes"]:
            message = f"洞 {stats['holes']}：{rules['note']}"
            if rules["holes"] == "none":
                problems.append(message)
            elif rules["holes"] == "warn":
                warnings.append(message)
        if stats["fill_ratio"] < (rules["min_fill"] or 0):
            warnings.append(f"填充率 {stats['fill_ratio']:.2f}，主体稀疏，需目视")
    scale = display / max(source.size)
    if scale > MAX_UPSCALE:
        message = f"放大 {scale:.2f}×，细节可能模糊"
        (problems if role == "spark" else warnings).append(message)
    reason = str(item.get("veto_reason") or "").strip()
    if reason and problems and not stats["empty"]:
        warnings.extend(f"例外（{reason}）：{problem}" for problem in problems)
        problems = []
    shown = (max(1, round(source.width * scale)), max(1, round(source.height * scale)))
    prepared = prepared.resize(shown, Image.Resampling.LANCZOS if scale < 1 else Image.Resampling.BILINEAR)
    if rotation:
        prepared = prepared.rotate(-rotation, resample=Image.Resampling.BICUBIC, expand=True)
    return dict(label=label, role=role, blend=blend, stats=stats, native=source.size,
                shown=shown, scale=scale, image=prepared, problems=problems, warnings=warnings,
                x=item.get("x"), y=item.get("y"), z=float(item.get("z", 0)))


def composite_layer(background: Image.Image, entry: dict, center: tuple[int, int]) -> Image.Image:
    """Crop through an RGBA layer canvas; apply MIX or saturated ADD."""
    layer = Image.new("RGBA", background.size)
    shown = entry["image"]
    layer.paste(shown, (center[0] - shown.width // 2, center[1] - shown.height // 2))
    alpha = layer.getchannel("A")
    color = layer.convert("RGB")
    if entry["blend"] == "alpha":
        return Image.composite(color, background, alpha)
    contribution = ImageChops.multiply(color, Image.merge("RGB", (alpha, alpha, alpha)))
    return ImageChops.add(background, contribution)


def main() -> int:
    parser = argparse.ArgumentParser(description="1:1 素材形态与静态混合近似")
    parser.add_argument("--spec", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--background", help="#RRGGBB")
    args = parser.parse_args()
    try:
        spec = json.loads(Path(args.spec).read_text(encoding="utf-8"))
        items = spec.get("items")
        if not isinstance(items, list) or not items:
            raise ValueError("规格需要非空 items 数组")
        bg = parse_color(args.background or spec.get("background", "#4B4B4B"))
        entries = [prepare(item) for item in items]
        out = Path(args.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        pad, label_h = 24, 40
        cell_w = max(entry["image"].width for entry in entries) + pad * 2
        cell_h = max(entry["image"].height for entry in entries) + pad * 2 + label_h
        sheet = Image.new("RGB", (cell_w * len(entries), cell_h), bg)
        for index, entry in enumerate(entries):
            cell = Image.new("RGB", (cell_w, cell_h), bg)
            cell = composite_layer(cell, entry, (cell_w // 2, label_h + (cell_h - label_h) // 2))
            status = "VETO" if entry["problems"] else "WARN" if entry["warnings"] else "PASS"
            draw = ImageDraw.Draw(cell)
            draw.text((8, 8), f"#{index + 1} {entry['label']} [{status}]", fill="white")
            draw.text((8, 22), f"{entry['blend']}  scale={entry['scale']:.2f}x", fill="white")
            sheet.paste(cell, (index * cell_w, 0))
            print(f"[preview] #{index + 1} {entry['label']} {entry['blend']} {status}")
            print(f"[preview]   原生 {entry['native']} → 显示 {entry['shown']}，放大 {entry['scale']:.2f}×")
            if not entry["stats"]["empty"]:
                stats = entry["stats"]
                print(f"[preview]   内容框 {stats['bbox']}，填充率 {stats['fill_ratio']:.2f}，洞 {stats['holes']}")
            for message in entry["warnings"]:
                print(f"[preview]   ! {message}")
            for message in entry["problems"]:
                print(f"[preview]   × {message}")
        sheet.save(out)
        print(f"[preview] 已输出 {out}（{sheet.width}×{sheet.height}，像素尺寸 1:1）")
        composition = spec.get("composition")
        if composition is not None:
            width, height = int(composition["width"]), int(composition["height"])
            if width <= 0 or height <= 0:
                raise ValueError("composition 尺寸必须为正数")
            canvas = Image.new("RGB", (width, height), bg)
            for entry in sorted(entries, key=lambda item: item["z"]):
                x = width // 2 if entry["x"] is None else round(float(entry["x"]))
                y = height // 2 if entry["y"] is None else round(float(entry["y"]))
                canvas = composite_layer(canvas, entry, (x, y))
            composite_path = out.with_name(out.stem + "_composite.png")
            canvas.save(composite_path)
            print(f"[preview] 已输出无标记静态合成 {composite_path}")
        print("[preview] CPU 静态近似；深色描边须 MCP/目视检查，最终 shader、动态和叠加以引擎录制为准。")
        return 4 if any(entry["problems"] for entry in entries) else 0
    except (OSError, ValueError, TypeError, KeyError, OverflowError) as error:
        print(f"[preview] 规格/文件错误：{error}", file=sys.stderr)
        return 3


if __name__ == "__main__":
    raise SystemExit(main())
