#!/usr/bin/env python3
"""素材本体归属校验。

从资产库导出素材后、复制进工程前跑一遍：判断该素材是不是《杀戮尖塔 2》本体的资源。

- **命中本体** → 不要复制进工程；场景里直接按本体的 uid + res:// path 引用。
- **未命中**   → 按原流程重命名为语义名，复制进 `images/vfx/<特效名>/`。
- **REVIEW**  → 尺寸与本体某张图相同**且感知哈希接近**（疑似在本体图上改绘），人工判定后再决定。

判定分两级：先逐字节 md5；未命中时再比「尺寸 + 解码后的 RGBA 像素」，
以覆盖「库内就是本体的图、但被重新编码过」的情况。

用法：
    python check_asset_origin.py <导出目录|文件...> [--body-root DIR] [--json]

本体根解析顺序：--body-root > $BANGDREAM_BODY_ROOT > <仓库>/BangDreamInSpire/images
                 > $STS2_UNPACKED（含版本子目录时取版本号最大的） > <仓库>/../sts2unpack（同上）

输出（每行一条；BODY 行给出可直接粘进 .tscn 的 uid 与 path）：
    BODY   res://images/vfx/dot.png  uid=uid://d2srbops1xqm0  方式=md5
    LOCAL  my_new_flash.png
    REVIEW my_flash.png  (与本体 1 张同尺寸 128x128 但像素不同)
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys

IMAGE_EXT = {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tga"}
SKIP_EXT = {".import", ".uid", ".md5", ".py", ".pyc"}
SKIP_DIRS = {".godot", ".git", "__pycache__"}

# 「同尺寸 + 感知哈希接近」的汉明距离阈值（16x16 dhash）：
# 超过它视为与本体的同尺寸图只是巧合，不再报 REVIEW。
REVIEW_DISTANCE = 10

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))


def die(msg: str) -> "None":
    print(f"错误：{msg}", file=sys.stderr)
    raise SystemExit(2)


def pick_version_dir(root: str) -> str:
    """源根下若有版本目录（0.111.0 之类），取版本号最大的那个。"""
    if os.path.exists(os.path.join(root, "images")) or os.path.exists(os.path.join(root, "project.godot")):
        return root
    subs = [
        os.path.join(root, d)
        for d in os.listdir(root)
        if os.path.isdir(os.path.join(root, d)) and re.fullmatch(r"\d+(\.\d+)+", d)
    ]
    if not subs:
        return root
    return max(subs, key=lambda p: [int(x) for x in os.path.basename(p).split(".")])


def resolve_body_root(explicit: str | None) -> str:
    candidates = []
    if explicit:
        candidates.append(explicit)
    if os.environ.get("BANGDREAM_BODY_ROOT"):
        candidates.append(os.environ["BANGDREAM_BODY_ROOT"])
    candidates.append(os.path.join(REPO_ROOT, "BangDreamInSpire", "images"))
    if os.environ.get("STS2_UNPACKED"):
        candidates.append(os.environ["STS2_UNPACKED"])
    candidates.append(os.path.abspath(os.path.join(REPO_ROOT, "..", "sts2unpack")))

    for c in candidates:
        if c and os.path.isdir(c):
            return pick_version_dir(os.path.abspath(c))
    die(
        "找不到本体素材根。请用 --body-root 指定，例如：\n"
        "  --body-root BangDreamInSpire/images        （编辑器同步目录，带本体 uid）\n"
        "  --body-root ../sts2unpack/0.111.0      （解包源）"
    )


def res_base_of(body_root: str) -> str:
    """body_root 对应的 res:// 根目录。"""
    if os.path.exists(os.path.join(body_root, "project.godot")):
        return body_root
    parent = os.path.dirname(body_root)
    if os.path.exists(os.path.join(parent, "project.godot")):
        return parent
    return body_root


def walk_files(root: str):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            ext = os.path.splitext(name)[1].lower()
            if ext in SKIP_EXT:
                continue
            yield os.path.join(dirpath, name)


def md5_of(path: str) -> str:
    h = hashlib.md5()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def dhash(im, size: int = 16) -> int:
    """16x16 差分哈希；alpha 参与加权，避免透明区干扰。"""
    small = im.convert("RGBA").resize((size + 1, size), 1)  # 1 = LANCZOS
    px = small.load()
    bits = 0
    i = 0
    for y in range(size):
        for x in range(size):
            a = px[x, y]
            b = px[x + 1, y]
            la = (a[0] * 299 + a[1] * 587 + a[2] * 114) * a[3]
            lb = (b[0] * 299 + b[1] * 587 + b[2] * 114) * b[3]
            if la > lb:
                bits |= 1 << i
            i += 1
    return bits


def read_uid(path: str) -> str | None:
    imp = path + ".import"
    if not os.path.exists(imp):
        return None
    try:
        with open(imp, encoding="utf-8", errors="ignore") as fh:
            m = re.search(r'^uid="([^"]+)"', fh.read(), re.M)
        return m.group(1) if m else None
    except OSError:
        return None


def load_pil():
    try:
        from PIL import Image  # noqa: PLC0415

        return Image
    except ImportError:
        return None


def main() -> int:
    ap = argparse.ArgumentParser(description="素材本体归属校验")
    ap.add_argument("paths", nargs="+", help="导出目录或文件")
    ap.add_argument("--body-root", default=None, help="本体素材根（缺省见文件头说明）")
    ap.add_argument("--json", action="store_true", help="输出 JSON")
    args = ap.parse_args()

    body_root = resolve_body_root(args.body_root)
    res_base = res_base_of(body_root)

    candidates: list[str] = []
    for p in args.paths:
        if os.path.isdir(p):
            candidates.extend(sorted(walk_files(p)))
        elif os.path.exists(p):
            candidates.append(p)
        else:
            die(f"路径不存在：{p}")
    if not candidates:
        die("没有可校验的文件")

    print(f"[body] 本体素材根 {os.path.relpath(body_root, REPO_ROOT)}", file=sys.stderr)

    # 第一级：逐字节 md5
    by_md5: dict[str, list[str]] = {}
    body_files: list[str] = []
    for f in walk_files(body_root):
        body_files.append(f)
        by_md5.setdefault(md5_of(f), []).append(f)
    print(f"[body] 索引 {len(body_files)} 个本体文件", file=sys.stderr)

    results: list[dict | None] = []
    idx_of: dict[str, int] = {}
    unmatched = []
    for i, c in enumerate(candidates):
        idx_of.setdefault(c, i)
        d = md5_of(c)
        hit = by_md5.get(d)
        results.append({"file": c, "verdict": "BODY", "via": "md5", "body": hit} if hit else None)
        if not hit:
            unmatched.append(c)

    # 第二级：尺寸 + RGBA 像素（只对未命中且是图片的候选做，按尺寸按需解码）
    Image = load_pil() if unmatched else None
    if unmatched and Image is None:
        print("[warn] 未装 Pillow，跳过像素级比对（重编码过的本体素材会漏判）", file=sys.stderr)

    dim_index: dict[tuple[int, int], list[str]] = {}
    if Image is not None:
        dims_needed = set()
        cand_images = {}
        for c in unmatched:
            if os.path.splitext(c)[1].lower() not in IMAGE_EXT:
                continue
            try:
                with Image.open(c) as im:
                    cand_images[c] = (im.width, im.height)
                    dims_needed.add((im.width, im.height))
            except Exception:
                continue
        for f in body_files:
            if os.path.splitext(f)[1].lower() not in IMAGE_EXT:
                continue
            try:
                with Image.open(f) as im:
                    dim = (im.width, im.height)
            except Exception:
                continue
            if dim in dims_needed:
                dim_index.setdefault(dim, []).append(f)

        px_index: dict[str, list[str]] = {}
        for dim, files in dim_index.items():
            for f in files:
                try:
                    with Image.open(f) as im:
                        px = hashlib.md5(im.convert("RGBA").tobytes()).hexdigest()
                except Exception:
                    continue
                px_index.setdefault(px, []).append(f)

        # 第三级：同尺寸 + 感知哈希接近 → 疑似在本体图上改绘，交人工判定
        hash_index: dict[tuple[int, int], list[tuple[int, str]]] = {}
        for dim, files in dim_index.items():
            for f in files:
                try:
                    with Image.open(f) as im:
                        hash_index.setdefault(dim, []).append((dhash(im), f))
                except Exception:
                    continue

        for c, dim in cand_images.items():
            try:
                with Image.open(c) as im:
                    px = hashlib.md5(im.convert("RGBA").tobytes()).hexdigest()
                    dh = dhash(im)
            except Exception:
                continue
            hit = px_index.get(px)
            if hit:
                results[idx_of[c]] = {"file": c, "verdict": "BODY", "via": "pixels", "body": hit}
                continue
            near = sorted(
                (bin(dh ^ h).count("1"), f) for h, f in hash_index.get(dim, [])
            )
            if near and near[0][0] <= REVIEW_DISTANCE:
                results[idx_of[c]] = {
                    "file": c,
                    "verdict": "REVIEW",
                    "via": "dhash",
                    "body": [f for _, f in near[:3]],
                    "size": dim,
                    "distance": near[0][0],
                }

    for i, c in enumerate(candidates):
        if results[i] is None:
            results[i] = {"file": c, "verdict": "LOCAL", "via": None, "body": None}

    def res_path(body_file: str) -> str:
        return "res://" + os.path.relpath(body_file, res_base).replace(os.sep, "/")

    if args.json:
        payload = []
        for r in results:
            body = r["body"][0] if r["body"] else None
            payload.append(
                {
                    "file": os.path.relpath(r["file"], REPO_ROOT),
                    "verdict": r["verdict"],
                    "via": r["via"],
                    "body": res_path(body) if body else None,
                    "body_uid": read_uid(body) if body else None,
                    "distance": r.get("distance"),
                }
            )
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        for r in results:
            rel = os.path.relpath(r["file"], REPO_ROOT)
            if r["verdict"] == "BODY":
                body = r["body"][0]
                uid = read_uid(body) or "<该本体文件缺 .import，无法取 uid：请先跑 tools/sync-sts2-assets.sh>"
                print(f"BODY   {res_path(body)}  uid={uid}  方式={r['via']}   <- {rel}")
            elif r["verdict"] == "REVIEW":
                others = "、".join(res_path(b) for b in r["body"])
                w, h = r["size"]
                print(
                    f"REVIEW {rel}  （同尺寸 {w}x{h}、感知距离 {r.get('distance')}：疑似在本体图上改绘，"
                    f"最接近 {others}）"
                )
            else:
                print(f"LOCAL  {rel}")

    n_body = sum(1 for r in results if r["verdict"] == "BODY")
    n_review = sum(1 for r in results if r["verdict"] == "REVIEW")
    n_local = len(results) - n_body - n_review
    print(
        f"[汇总] BODY(引用本体)={n_body}  REVIEW(人工判定)={n_review}  LOCAL(可复制进工程)={n_local}",
        file=sys.stderr,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
