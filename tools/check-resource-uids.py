#!/usr/bin/env python3
"""校验 Godot 场景/资源里 ext_resource 声明的 uid 与磁盘 sidecar 是否一致。

背景
----
场景里 `[ext_resource uid="uid://..." path="res://..."]` 的 uid 必须和该资源
自己的 uid 来源一致：

  - 被导入的资源（图片、Spine 的 .atlas/.skel ……）：`X.import` 里的 `uid=`
  - 脚本/着色器/场景/tres：`X.uid` 同目录 sidecar，或 `X.tscn/.tres` 文件头 `uid=`

不一致时，引擎加载会按 text path 兜底并打印
`ext_resource, invalid UID: ... - using text path instead`。美术资产不进仓库
（见 AGENTS.md「三类资源目录」），若恢复美术时漏了 `.import`，`--import`
会重新生成随机 uid，把这类漂移一次性刷出来 —— 本脚本就是给 `dotnet publish`
打包加的那道闸（见 Directory.Build.targets 的 ExportPck）。

判据
----
- 目标文件不存在：视为「资源缺失」（常见于未同步打法/未取得美术的本机），默认只警告，
  不影响退出码；加 `--strict-missing` 才当错误。
- 目标存在但 uid 不一致：一律是错误，退出码 1。
- 只写 uid 不写 path 的引用不参与校验（当前仓库实测为 0 条）。

用法
----
    python3 tools/check-resource-uids.py                 # 自动发现仓库内所有 Godot 工程
    python3 tools/check-resource-uids.py --project BangDreamInSpire
    python3 tools/check-resource-uids.py --strict-missing
    python3 tools/check-resource-uids.py --json

退出码：0 = 无 uid 漂移；1 = 有漂移（或 strict 下资源缺失）；2 = 参数/环境错误。
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 扫描时整棵跳过的目录名（本地缓存、构建产物、备份、同步进来的本体资源无关目录）
PRUNE_DIRS = {
    ".git", ".godot", ".backup", ".vfx_staging", "bin", "obj", "node_modules",
}
# 解析 ext_resource 时不区分属性顺序，只要求同一行/同一标签内同时有 uid 与 path
EXT_RESOURCE_RE = re.compile(r"\[ext_resource\b([^\]]*)\]")
UID_ATTR_RE = re.compile(r'\buid="(uid://[^"]+)"')
PATH_ATTR_RE = re.compile(r'\bpath="(res://[^"]+)"')
IMPORT_UID_RE = re.compile(r'^uid="(uid://[^"]+)"', re.MULTILINE)
HEADER_UID_RE = re.compile(r'uid="(uid://[^"]+)"')


def discover_projects(explicit: list[str]) -> list[str]:
    if explicit:
        return [os.path.abspath(p) for p in explicit]
    found: list[str] = []
    for dirpath, dirnames, filenames in os.walk(REPO_ROOT):
        dirnames[:] = [
            d for d in dirnames
            if d not in PRUNE_DIRS and not d.startswith(".")
        ]
        if "project.godot" in filenames:
            found.append(dirpath)
    return sorted(found)


def read_actual_uid(target: str) -> str | None:
    """返回资源自身的 uid；None 表示该资源类型可校验但读不到 uid。"""
    import_path = target + ".import"
    if os.path.isfile(import_path):
        with open(import_path, encoding="utf-8", errors="replace") as fh:
            m = IMPORT_UID_RE.search(fh.read())
        return m.group(1) if m else None
    uid_path = target + ".uid"
    if os.path.isfile(uid_path):
        with open(uid_path, encoding="utf-8", errors="replace") as fh:
            return fh.read().strip() or None
    if target.endswith((".tscn", ".tres")):
        with open(target, encoding="utf-8", errors="replace") as fh:
            first = fh.readline(512)
        m = HEADER_UID_RE.search(first)
        return m.group(1) if m else None
    return None


def scan_project(project_root: str) -> dict:
    mismatches: list[dict] = []
    missing: list[dict] = []
    refused = 0
    for dirpath, dirnames, filenames in os.walk(project_root):
        dirnames[:] = sorted(d for d in dirnames if d not in PRUNE_DIRS)
        for filename in filenames:
            if not filename.endswith((".tscn", ".tres")):
                continue
            path = os.path.join(dirpath, filename)
            rel = os.path.relpath(path, project_root).replace(os.sep, "/")
            with open(path, encoding="utf-8", errors="replace") as fh:
                for lineno, line in enumerate(fh, 1):
                    m = EXT_RESOURCE_RE.search(line)
                    if not m:
                        continue
                    uid_m = UID_ATTR_RE.search(m.group(1))
                    path_m = PATH_ATTR_RE.search(m.group(1))
                    if not uid_m or not path_m:
                        continue
                    refused += 1
                    declared = uid_m.group(1)
                    target = os.path.join(
                        project_root, path_m.group(1)[len("res://"):]
                    )
                    record = {
                        "file": rel, "line": lineno,
                        "res_path": path_m.group(1), "declared": declared,
                    }
                    if not os.path.exists(target):
                        missing.append(record)
                        continue
                    actual = read_actual_uid(target)
                    if actual != declared:
                        record["actual"] = actual
                        mismatches.append(record)
    return {
        "project": os.path.relpath(project_root, REPO_ROOT).replace(os.sep, "/"),
        "checked": refused, "mismatches": mismatches, "missing": missing,
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description="校验 Godot ext_resource uid 与 .import/.uid sidecar 是否一致",
    )
    parser.add_argument(
        "--project", action="append", default=[],
        help="Godot 工程目录（含 project.godot），可重复；默认自动发现",
    )
    parser.add_argument(
        "--strict-missing", action="store_true",
        help="目标资源文件缺失时也视为失败（默认仅警告）",
    )
    parser.add_argument("--quiet", action="store_true", help="只输出汇总与错误")
    parser.add_argument("--json", action="store_true", help="输出 JSON")
    args = parser.parse_args(argv)

    projects = discover_projects(args.project)
    if not projects:
        print("check-resource-uids: 未找到含 project.godot 的工程", file=sys.stderr)
        return 2

    reports = []
    failed = False
    for project_root in projects:
        report = scan_project(project_root)
        reports.append(report)
        if report["mismatches"] or (args.strict_missing and report["missing"]):
            failed = True

    if args.json:
        print(json.dumps(reports, ensure_ascii=False, indent=2))
        return 1 if failed else 0

    total_checked = total_mismatch = total_missing = 0
    for report in reports:
        total_checked += report["checked"]
        total_mismatch += len(report["mismatches"])
        total_missing += len(report["missing"])
        if not report["mismatches"] and (args.quiet or not report["missing"]):
            continue
        print(f"[{report['project']}]")
        for item in report["mismatches"]:
            print(
                f"  UID 漂移: {item['file']}:{item['line']} "
                f"{item['res_path']} 声明={item['declared']} "
                f"实际={item['actual'] or '<无 uid>'}"
            )
        if report["missing"] and not args.quiet:
            for item in report["missing"]:
                print(
                    f"  资源缺失: {item['file']}:{item['line']} {item['res_path']}"
                )

    verdict = "FAIL" if failed else "OK"
    print(
        f"check-resource-uids: {verdict} | 工程 {len(reports)} 个 | "
        f"ext_resource {total_checked} 条 | uid 漂移 {total_mismatch} 条 | "
        f"资源缺失 {total_missing} 条"
    )
    if failed:
        print(
            "提示：uid 漂移通常意味着资源恢复时丢了 .import/.uid，"
            "或场景引用了旧 uid。请恢复随源一起的 sidecar（或修正场景）后重新打包。",
            file=sys.stderr,
        )
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
