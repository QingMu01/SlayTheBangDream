#!/usr/bin/env python3
"""录帧校验：把「特效真的渲染出来了」变成机器可判定的证据。

背景色**自适应**：取每帧的众数亮度作为背景。
  - BangDreamLib 的清屏色是灰 75，不是黑；
  - 独立 demo 可能是近黑 4。
两者都能正确判定，不需要手工传阈值。

用法：
    python verify_frames.py <rec 目录> --fps 60
    python verify_frames.py <rec 目录> --fps 60 --expect-peak 0.05 0.20
    python verify_frames.py <rec 目录> --fps 60 --background 75   # 强制指定背景

退出码：
    0  正常（画面有内容，且峰值时刻落在期望窗口内）
    1  全部帧为空白 —— 特效没渲染出来
    2  峰值时刻偏离期望窗口
    3  找不到帧文件 / 依赖缺失
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    print("[verify] 需要 Pillow：pip install Pillow", file=sys.stderr)
    raise SystemExit(3) from None


def analyze(path: Path, delta: int, forced_background: int | None) -> tuple[float, float, int]:
    """返回 (内容像素占比, 内容像素平均亮度 0..1, 判定出的背景亮度)。"""
    with Image.open(path) as image:
        gray = image.convert("L")

    histogram = gray.histogram()
    total = sum(histogram)
    if total == 0:
        return 0.0, 0.0, 0

    if forced_background is None:
        # 众数即背景：清屏色占绝对多数
        background = max(range(256), key=lambda value: histogram[value])
    else:
        background = forced_background

    active = 0
    weighted = 0
    for value, count in enumerate(histogram):
        if abs(value - background) > delta:
            active += count
            weighted += value * count

    ratio = active / total
    mean = (weighted / active / 255.0) if active else 0.0
    return ratio, mean, background


def detect_freeze(
    active: list[tuple[int, float, float, int]],
    fps: float,
    window_seconds: float,
    tolerance: float,
) -> dict | None:
    """检测"占比长时间几乎不变"。

    返回最长的冻结区间信息；没有达到 window_seconds 的区间则返回 None。

    做法：把**相邻两帧占比差 < tolerance** 视为"连续"，找出最长的连续平段。
    平段可以跨越峰值——本次真实案例里占比从 f5 起缓慢下降、f8~f24 完全持平，
    只从峰值开始找会漏掉前一半。
    帧号不连续（特效已归零后又重启）时不跨越空白区比较。
    """
    if len(active) < 2:
        return None

    window_frames = max(2, int(round(window_seconds * fps)))
    first: dict | None = None
    plateau_count = 0
    start = 0

    for index in range(1, len(active)):
        prev_index, prev_ratio = active[index - 1][0], active[index - 1][1]
        cur_index, cur_ratio = active[index][0], active[index][1]

        # 帧号跳空说明中间有全零帧，平段在此断开
        if cur_index != prev_index + 1:
            start = index
            continue

        span = cur_index - active[start][0] + 1
        if span < window_frames:
            continue

        base = active[start][1]
        if base <= 0:
            start = index
            continue

        # 与平段起点比较（而非相邻帧），避免"每帧只掉一点"被逐帧判为变化
        drift = abs(cur_ratio - base) / base
        if drift <= tolerance:
            if first is None:
                # 报**第一段**而不是最长段：循环预览下后一圈因对齐可能更长，
                # 但开发时想看的是最早出现的症状。
                first = {
                    "start": active[start][0],
                    "end": cur_index,
                    "seconds": (cur_index - active[start][0] + 1) / fps,
                    "ratio": base,
                    "end_ratio": cur_ratio,
                    "drift": drift,
                    "count": 1,
                }
            else:
                first["count"] += 1
                first["end"] = cur_index
        else:
            start = index

    return first


def main() -> int:
    parser = argparse.ArgumentParser(description="录帧校验")
    parser.add_argument("directory", help="包含 frame*.png 的目录")
    parser.add_argument("--fps", type=float, default=60.0, help="录制帧率（默认 60）")
    parser.add_argument("--delta", type=int, default=12,
                        help="与背景的亮度差超过此值才算内容像素（默认 12）")
    parser.add_argument("--background", type=int, default=None,
                        help="强制指定背景亮度；缺省取每帧众数（自适应）")
    parser.add_argument("--min-ratio", type=float, default=0.0005,
                        help="判定「有内容」的最小内容像素占比（默认 0.0005）")
    parser.add_argument("--expect-peak", nargs=2, type=float, metavar=("START", "END"),
                        help="期望的峰值时刻窗口（秒）")
    parser.add_argument("--expect-frames", type=int, default=None,
                        help="期望的帧数（与 --quit-after 一致）")
    parser.add_argument("--freeze-window", type=float, default=0.20,
                        help="冻结判定的最短时长（秒，默认 0.20）；设为 0 关闭检测")
    parser.add_argument("--freeze-tolerance", type=float, default=0.01,
                        help="占比波动小于此比例即视为冻结（默认 0.01 = 1%%）")
    parser.add_argument("--fail-on-freeze", action="store_true",
                        help="检测到冻结帧时返回非零退出码（默认只告警）")
    args = parser.parse_args()

    directory = Path(args.directory)
    frames = sorted(directory.glob("frame*.png"))
    if not frames:
        print(f"[verify] 目录里没有 frame*.png：{directory}", file=sys.stderr)
        return 3

    if args.expect_frames is not None and len(frames) != args.expect_frames:
        print(f"[verify] 警告：帧数 {len(frames)} != 期望 {args.expect_frames}", file=sys.stderr)

    samples: list[tuple[int, float, float, int]] = []
    for index, frame in enumerate(frames):
        ratio, mean, background = analyze(frame, args.delta, args.background)
        samples.append((index, ratio, mean, background))

    background = samples[0][3]
    active = [item for item in samples if item[1] >= args.min_ratio]

    print(f"[verify] 帧数        {len(frames)}")
    print(f"[verify] 背景亮度    {background}（{'自适应众数' if args.background is None else '手工指定'}）")
    print(f"[verify] 内容阈值    与背景差 > {args.delta}")

    if not active:
        print(f"[verify] 失败：{len(frames)} 帧全部为空白，特效没有渲染出来。", file=sys.stderr)
        return 1

    first_index = active[0][0]
    last_index = active[-1][0]
    peak_index, peak_ratio, peak_mean, _ = max(active, key=lambda item: item[1])

    print(f"[verify] 有效区间    第 {first_index} 帧 ~ 第 {last_index} 帧"
          f"（{first_index / args.fps:.3f}s ~ {last_index / args.fps:.3f}s）")
    print(f"[verify] 峰值        第 {peak_index} 帧（{peak_index / args.fps:.3f}s）"
          f"，占比 {peak_ratio:.4f}，平均亮度 {peak_mean:.3f}")

    print("[verify] 逐帧：")
    for index, ratio, mean, _ in samples:
        bar = "#" * min(int(ratio * 400), 40)
        print(f"  {index:>4} {index / args.fps:6.3f}s  {ratio:8.5f} {mean:6.3f} {bar}")

    # ---- 冻结帧检测 ----
    # alpha 曲线写反（Lerp 的插值参数用成 1 - x^pow）时，占比会在某点后完全冻结。
    # 实测症状：f8~f24 占比恒为 0.05296~0.05299，肉眼很容易忽略，机器一眼可见。
    frozen = detect_freeze(active, args.fps, args.freeze_window, args.freeze_tolerance)
    if frozen:
        print(
            f"[verify] ⚠ 疑似未淡出：第 {frozen['start']} 帧起 {frozen['seconds']:.3f}s 内，"
            f"内容占比几乎不变（{frozen['ratio']:.5f} → {frozen['end_ratio']:.5f}，"
            f"波动 {frozen['drift'] * 100:.2f}%）。",
            file=sys.stderr,
        )
        if frozen["end"] > frozen["start"]:
            total = (frozen["end"] - frozen["start"] + 1) / args.fps
            print(
                f"[verify]   该平段从第 {frozen['start']} 帧延续到第 {frozen['end']} 帧"
                f"（共约 {total:.2f}s）——不是一帧的抖动。",
                file=sys.stderr,
            )
        print(
            "[verify]   典型原因是 alpha 曲线方向写反：写成 Lerp(A0, A1, 1 - x^pow)，\n"
            "[verify]   A1=0 时等价于 A0 * x^pow，alpha 反而从 0 涨到 A0 并永久停住。\n"
            "[verify]   正确写法：fade = x^pow，再 alpha = Lerp(A0, A1, fade)。",
            file=sys.stderr,
        )

    if args.expect_peak:
        start, end = args.expect_peak
        peak_time = peak_index / args.fps
        if not (start <= peak_time <= end):
            print(f"[verify] 失败：峰值 {peak_time:.3f}s 不在期望窗口 "
                  f"[{start:.3f}, {end:.3f}] 内。", file=sys.stderr)
            return 2
        print(f"[verify] 峰值落在期望窗口 [{start:.3f}, {end:.3f}] 内。")

    if frozen and args.fail_on_freeze:
        return 4

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
