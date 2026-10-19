#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""sign_off.py —— 读取人工签字的 `cleanup_decisions.tsv`，校验后 dry-run / apply。

工具链分工（见 `.agents/skills/obsolete-file-cleanup/SKILL.md` 的 S3 闸门）：
  `scan_orphans.py`（判定）→ `cleanup_manifest.py`（分档 + 生成签字表）
  → **人工填 tsv** → `sign_off.py --dry-run`（核对）→ `sign_off.py --apply`（执行 + 记账）

⚠️ 临时目录语义：扫描快照与 `cleanup_decisions.tsv` 默认落在 `.vfx_staging/`（体检目录本来就是
特效设计的临时目录，**随时可能被整体清理**）。因此必须在**同一次会话**内完成
「扫描 → 签字 → dry-run → apply」；**长期留档靠 `docs/cleanup-log.md`**（本脚本 apply 后追加）。

安全域硬校验（即使 `--force` 也拒删）：
  本体同步目录 `BangDreamInSpire/{addons,animations,fonts,images,materials,models,scenes,shaders,themes,src}`、
  `.git` / `.godot` / `bin` / `obj` / `.vfx_staging`、以及**用户未跟踪 WIP**。
  出处：docs/cleanup-authoring.md 第 7 节安全域、references/keep-list.md 第 1 节。

行为约定：
  * `--apply` **先 `git add` 目标**（保证可回滚）再删资源与其 `.import`/`.uid`；
  * **不会自己 `git commit`** —— 提交由人来做；
  * 任何失败立即停止，并报告「已删 / 未删」清单。

CLI:
  python3 sign_off.py --decisions <tsv> [--dry-run|--apply] [--log docs/cleanup-log.md] [--force]

退出码：0 = 校验通过且（dry-run 打印 / apply 成功）；1 = 存在未决 A 项、校验失败、安全域拒删或删除失败。
"""

from __future__ import annotations

import argparse
import datetime as _dt
import os
import subprocess
import sys

VERSION = "1.3.0"
SCRIPT_NAME = "sign_off.py"

VALID_ACTIONS = ("delete", "keep", "defer")

# 安全域（与 scan_orphans.py / docs/cleanup-authoring.md:100-107 同源）
BODY_SYNC_DIRS = {
    "addons", "animations", "fonts", "images", "materials",
    "models", "scenes", "shaders", "themes", "src",
}
FORBIDDEN_PARTS = {".git", ".godot", "bin", "obj", ".vfx_staging"}
SIDECAR_EXTS = (".import", ".uid")

TSV_COLUMNS = ("id", "bucket", "path", "sidecar", "bytes", "reason", "action", "note")

TEMPDIR_NOTICE = (
    "扫描快照与 decisions 默认落在 .vfx_staging/（临时目录，随时可能被整体清理）；"
    "必须在同一次会话内完成「扫描 → 签字 → dry-run → apply」，长期留档靠 docs/cleanup-log.md。"
)


# ---------------------------------------------------------------------------
def run_git(root: str, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", "-C", root, *args], capture_output=True, check=False)


def head_short(root: str) -> str:
    proc = run_git(root, "rev-parse", "--short", "HEAD")
    return proc.stdout.decode("utf-8", "replace").strip() or "(unknown)"


def untracked_files(root: str) -> set[str]:
    proc = run_git(root, "ls-files", "-o", "--exclude-standard", "-z")
    if proc.returncode != 0:
        return set()
    return {p for p in proc.stdout.decode("utf-8", "replace").split("\0") if p}


def safety_violation(rel: str, untracked: set[str]) -> str | None:
    """安全域硬校验：返回拒绝理由，None 表示放行。--force 也不放行。"""
    if os.path.isabs(rel) or rel.startswith("/"):
        return "绝对路径不在处置范围内"
    parts = rel.split("/")
    if ".." in parts:
        return "路径越出仓库根"
    if any(p in FORBIDDEN_PARTS for p in parts):
        return f"落在禁止目录（{'/'.join(sorted(set(parts) & FORBIDDEN_PARTS))}）"
    if len(parts) >= 2 and parts[0] == "BangDreamInSpire" and parts[1] in BODY_SYNC_DIRS:
        return f"本体同步目录 BangDreamInSpire/{parts[1]}/（gitignore，删了编辑器全红）"
    if rel in untracked:
        return "用户未跟踪 WIP（清理任务无处置权）"
    return None


def on_disk_sidecars(root: str, rel: str) -> list[str]:
    return [rel + ext for ext in SIDECAR_EXTS if os.path.exists(os.path.join(root, rel + ext))]


def file_size(root: str, rel: str) -> int:
    try:
        return os.path.getsize(os.path.join(root, rel))
    except OSError:
        return 0


def mb(n: int) -> str:
    return f"{n / 1024 / 1024:.2f} MB"


def read_decisions(path: str) -> tuple[list[dict], list[str]]:
    """读签字表：返回 (数据行, 头部注释行)。# 开头为注释，首行数据行是列名则跳过。"""
    rows: list[dict] = []
    head: list[str] = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line.strip():
                continue
            if line.startswith("#"):
                head.append(line)
                continue
            cells = line.split("\t")
            if len(cells) < len(TSV_COLUMNS):
                cells += [""] * (len(TSV_COLUMNS) - len(cells))
            row = dict(zip(TSV_COLUMNS, cells[: len(TSV_COLUMNS)]))
            if row["id"] == "id" and row["bucket"] == "bucket":
                continue  # 列名行
            row["action"] = row["action"].strip().lower()
            row["note"] = row["note"].strip()
            row["path"] = row["path"].strip()
            row["sidecar"] = [s.strip() for s in row["sidecar"].split(",") if s.strip()]
            rows.append(row)
    return rows, head


# ---------------------------------------------------------------------------
def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="签字并执行清理（读 cleanup_decisions.tsv → 校验 → dry-run / apply）",
        epilog=TEMPDIR_NOTICE,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--decisions", required=True,
                        help="cleanup_manifest.py 产出的 cleanup_decisions.tsv")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true", help="只打印将被执行的 rm 清单（默认）")
    mode.add_argument("--apply", action="store_true", help="真正执行删除并追加账本（不会 git commit）")
    parser.add_argument("--log", default="docs/cleanup-log.md",
                        help="长期账本路径（相对 --project-root；不存在则创建）")
    parser.add_argument("--project-root", default=".", help="仓库根目录（默认 .）")
    parser.add_argument("--force", action="store_true",
                        help="跳过「keep/defer 必须写 note」的校验；**不**能跳过安全域拒删")
    args = parser.parse_args(argv)

    root = os.path.abspath(args.project_root)
    apply_mode = bool(args.apply)
    decisions_rel = os.path.relpath(args.decisions, root).replace(os.sep, "/")
    decisions_path = os.path.abspath(args.decisions)
    log_path = args.log if os.path.isabs(args.log) else os.path.join(root, args.log)

    print(f"mode {'apply' if apply_mode else 'dry-run'}")
    print(f"project_root {root}")
    print(f"decisions_file {decisions_rel}")

    if not os.path.exists(decisions_path):
        print(f"PROBLEM decisions_missing {decisions_rel}"
              f"（{TEMPDIR_NOTICE}）")
        return 1

    rows, head = read_decisions(decisions_path)
    problems: list[str] = []
    warnings: list[str] = []

    # ---- 1. 校验 --------------------------------------------------------
    unresolved_a: list[str] = []
    for row in rows:
        rid, bucket, rel, action = row["id"], row["bucket"].strip().upper(), row["path"], row["action"]
        if bucket not in ("A", "B", "C"):
            problems.append(f"invalid_bucket {rid} bucket={row['bucket']}")
            continue
        if action and action not in VALID_ACTIONS:
            problems.append(f"invalid_action {rid} action={action}（只允许 {'/'.join(VALID_ACTIONS)}）")
            continue
        if bucket == "A" and not action:
            unresolved_a.append(rid)
            continue
        if action in ("keep", "defer") and not row["note"] and not args.force:
            problems.append(f"missing_note {rid} action={action} 必须写 note 说明理由（或 --force）")

    print(f"rows {len(rows)}")
    print(f"column_errors {len(problems)}")
    print(f"unresolved_a {len(unresolved_a)}")
    if unresolved_a:
        problems.append(f"unresolved_a_items {len(unresolved_a)}: {', '.join(unresolved_a[:20])}"
                        + (" …" if len(unresolved_a) > 20 else ""))

    # ---- 2. 安全域 + 存在性 --------------------------------------------
    untracked = untracked_files(root)
    deletes: list[dict] = []
    blocked: list[tuple[str, str]] = []
    for row in rows:
        if row["action"] != "delete":
            continue
        rel = row["path"]
        violation = safety_violation(rel, untracked)
        if violation:
            blocked.append((rel, violation))
            continue
        if not os.path.exists(os.path.join(root, rel)):
            problems.append(f"delete_target_missing {rel}（快照过期，请重跑 scan_orphans.py）")
            continue
        sidecars = sorted(set(on_disk_sidecars(root, rel)) | set(row["sidecar"]))
        for sc in sidecars:
            sc_violation = safety_violation(sc, untracked)
            if sc_violation:
                blocked.append((sc, sc_violation))
        deletes.append({"row": row, "path": rel, "sidecars": sidecars,
                        "bytes": file_size(root, rel) + sum(file_size(root, s) for s in sidecars)})

    actions = {"delete": 0, "keep": 0, "defer": 0, "": 0}
    for row in rows:
        actions[row["action"]] = actions.get(row["action"], 0) + 1
    print(f"action_delete {actions.get('delete', 0)}")
    print(f"action_keep {actions.get('keep', 0)}")
    print(f"action_defer {actions.get('defer', 0)}")
    print(f"action_blank {actions.get('', 0)}")
    print(f"safety_blocked {len(set(blocked))}")
    print(f"delete_items {len(deletes)}")
    print(f"delete_bytes {sum(d['bytes'] for d in deletes)}")

    seen_blocked: set[tuple[str, str]] = set()
    for rel, why in blocked:
        if (rel, why) in seen_blocked:
            continue
        seen_blocked.add((rel, why))
        problems.append(f"safety_blocked {rel} —— {why}（--force 也不放行）")

    # ---- 3. dry-run / apply ---------------------------------------------
    if not apply_mode:
        for d in deletes:
            print(f"WARN would_delete {d['path']} (+{len(d['sidecars'])} sidecar, {mb(d['bytes'])})")
            for sc in d["sidecars"]:
                print(f"WARN would_delete_sidecar {sc}")
        for row in rows:
            if row["action"] in ("keep", "defer"):
                print(f"WARN kept {row['path']} action={row['action']}"
                      + (f" note={row['note']}" if row["note"] else ""))
        for message in problems:
            print(f"PROBLEM {message}")
        print(f"warnings {len(warnings) + len(deletes) + sum(1 for r in rows if r['action'] in ('keep', 'defer'))}")
        print(f"problems {len(problems)}")
        return 1 if problems else 0

    if problems:
        for message in problems:
            print(f"PROBLEM {message}")
        print("PROBLEM apply_aborted 校验未通过，未删除任何文件")
        print(f"warnings {len(warnings)}")
        print(f"problems {len(problems) + 1}")
        return 1

    # apply：先 git add（可回滚），再删资源 + sidecar
    deleted: list[dict] = []
    remaining = list(deletes)
    failure: str | None = None
    for d in deletes:
        targets = [d["path"], *d["sidecars"]]
        add = run_git(root, "add", "--", *targets)
        if add.returncode != 0:
            failure = f"git add 失败：{add.stderr.decode('utf-8', 'replace').strip()}"
            break
        for rel in targets:
            full = os.path.join(root, rel)
            try:
                if os.path.exists(full):
                    os.remove(full)
            except OSError as exc:
                failure = f"删除 {rel} 失败：{exc}"
                break
        if failure:
            break
        deleted.append(d)
        remaining.remove(d)

    for d in deleted:
        print(f"WARN deleted {d['path']} (+{len(d['sidecars'])} sidecar, {mb(d['bytes'])})")

    if failure:
        print(f"PROBLEM apply_failed {failure}")
        print(f"INFO deleted_items {len(deleted)}")
        print(f"INFO not_deleted_items {len(remaining)}")
        for d in remaining:
            print(f"INFO not_deleted {d['path']}")
        print(f"warnings {len(deleted)}")
        print(f"problems 1")
        return 1

    # 账本追加（不自动 commit）
    os.makedirs(os.path.dirname(log_path) or root, exist_ok=True)
    local = _dt.datetime.now().astimezone()
    total_bytes = sum(d["bytes"] for d in deleted)
    new_file = not os.path.exists(log_path)
    lines: list[str] = []
    if new_file:
        lines.append("# 过时文件清理账本")
        lines.append("")
        lines.append("本文件是清理执行的**长期留档**（`.vfx_staging/` 是临时目录，随时可能被清理）。")
        lines.append("由 `.agents/skills/obsolete-file-cleanup/scripts/sign_off.py` 追加；**不自动 git commit**。")
        lines.append("")
    lines.append(f"## {local.strftime('%Y-%m-%d %H:%M:%S%z')} —— 删除 {len(deleted)} 项 / {mb(total_bytes)}")
    lines.append("")
    lines.append(f"- HEAD: `{head_short(root)}`")
    lines.append(f"- 脚本: `{SCRIPT_NAME}` v{VERSION}")
    lines.append(f"- decisions: `{decisions_rel}`")
    snapshot = next((h for h in head if "generated_at_utc" in h), "")
    if snapshot:
        lines.append(f"- 快照: `{snapshot.lstrip('# ').strip()}`")
    lines.append(f"- --force: {args.force}")
    lines.append("")
    lines.append("| # | 路径 | 体积 | sidecar |")
    lines.append("|---:|---|---:|---|")
    for i, d in enumerate(deleted, 1):
        sc = ", ".join(f"`{s}`" for s in d["sidecars"]) or "—"
        lines.append(f"| {i} | `{d['path']}` | {mb(d['bytes'])} | {sc} |")
    lines.append("")
    with open(log_path, "a", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print(f"log_appended {os.path.relpath(log_path, root)}")
    print(f"applied_items {len(deleted)}")
    print(f"applied_bytes {total_bytes}")
    print(f"git_commit none（按约定不自动提交，请人工 review 后 commit）")
    print(f"warnings {len(deleted)}")
    print(f"problems 0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
