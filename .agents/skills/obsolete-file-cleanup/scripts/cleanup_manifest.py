#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""cleanup_manifest.py —— 把 scan_orphans.py 的四态快照整理成「A/B/C 三档 + sidecar 连带」清单。

工具只产出**证据**，不产出**决定**。A 档必须人工逐条签字后才可执行删除
（`.agents/skills/obsolete-file-cleanup/SKILL.md` S3 闸门）。

判定依据出处：
  * 三档定义 ......... docs/cleanup-authoring.md:76-82
  * 默认保留规则 ..... docs/cleanup-authoring.md:81、.agents/skills/obsolete-file-cleanup/references/keep-list.md:5 节
  * 快照过期单列 ..... docs/cleanup-authoring.md:92（硬性条款 7，`v3_confirmed_orphan.txt` 39 条里 6 条已不存在）
  * sidecar 连带 ..... docs/cleanup-authoring.md:14（铁律三）、SKILL.md S4 第 3 点
  * 未跟踪 WIP 不处理 . SKILL.md 通用前置第 2 点、docs/cleanup-authoring.md:107
  * 输出契约 ......... .agents/skills/card-development/scripts/audit_cards.py:526-546

CLI:
  python3 cleanup_manifest.py [--audit-dir .vfx_staging/audit] [--keep-file <path>] [--out .vfx_staging/audit]

退出码（Lead 已认可、勿改）：
  0 = 清单生成成功；
  1 = 存在 `stale`（快照过期，证据失效，必须先重跑 scan_orphans.py）
      或存在「工程内本地断链」（需先修，例如 BangDreamPreloadManager 里指向不存在场景的硬编码）。
  **exit 1 不代表 A 档为空** —— A 档待签字只打 `WARN tier_a_pending_signature`，不改变退出码：
  签字是人的动作，不是脚本的判定（`.agents/skills/obsolete-file-cleanup/SKILL.md` S3 闸门）。
"""

from __future__ import annotations

import argparse
import datetime as _dt
import fnmatch
import os
import re
import subprocess
import sys

# keep 规则与 scan_orphans.py 共享同一份定义（keep_rules.py）：
# 扫描端用它划死簇级联边界，分档端用它决定 B 档，漂移会让两边各说各话。
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import keep_rules as kr  # noqa: E402  (路径就绪后再导入)

VERSION = "1.2.0"
SCRIPT_NAME = "cleanup_manifest.py"

FOUR_STATES = ("referenced.txt", "convention_referenced.txt", "weak.txt", "confirmed_orphan.txt")
# 死簇（v1.2.0）：scan_orphans 已把它从 confirmed_orphan.txt 里拆出来单列
DEAD_CLUSTER_STATE = "dead_cluster.txt"
SIDECAR_STATES = ("sidecar_orphan.txt", "sidecar_missing.txt")  # broken.txt 列序不同，单独解析

# 本体同步目录：绝不允许出现在任何桶（docs/cleanup-authoring.md:20、:86）
BODY_SYNC_DIRS = {
    "addons", "animations", "fonts", "images", "materials",
    "models", "scenes", "shaders", "themes", "src",
}
WALK_SKIP_DIRS = {".git", ".godot", "bin", "obj", ".vfx_staging", "node_modules"}

# C 档结构性冗余（docs/cleanup-authoring.md:82、keep-list.md 第 3 节）
STRUCTURAL_CANDIDATES = (
    "BangDreamLib.sln", "ItsCrychic.sln", "BangDreamInSpire.sln",
)

SIDECAR_EXTS = (".import", ".uid")

# 安全域：即便扫描把它们判成「确认无引用」，也不允许落 A 档
# 出处：docs/cleanup-authoring.md:100-107（第 7 节安全域）、references/keep-list.md 第 1 节
SAFETY_BASENAMES = {
    "project.godot", "export_presets.cfg", ".gitignore", ".gitattributes",
    "AGENTS.md", "Directory.Build.props", "Directory.Build.targets",
}
SAFETY_SUFFIXES = (".csproj", ".sln", ".sln.DotSettings")


def safety_reason(path: str) -> str | None:
    parts = path.split("/")
    base = os.path.basename(path)
    if len(parts) >= 2 and parts[0] == "BangDreamLib" and parts[1] in BODY_SYNC_DIRS:
        return "本体同步目录：禁止按孤儿规则处理（docs/cleanup-authoring.md:20）"
    if any(p in (".godot",) for p in parts):
        return "引擎缓存目录：可删但必须先迁出手写文件（docs/cleanup-authoring.md:21）"
    if path.endswith(SIDECAR_EXTS):
        return "sidecar 不可单独删：必须与源文件同批（docs/cleanup-authoring.md:14）"
    if base in SAFETY_BASENAMES or base.endswith(SAFETY_SUFFIXES):
        return "工程配置/构建入口：清理任务安全域外（docs/cleanup-authoring.md:100-107）"
    if base.startswith("spine-godot-extension"):
        return "官方 Spine 扩展（bin/）：删了要重新下载（Directory.Build.targets:10-19）"
    return None


# ---------------------------------------------------------------------------
def now_stamps() -> tuple[str, str]:
    local = _dt.datetime.now().astimezone()
    utc = _dt.datetime.now(_dt.timezone.utc)
    return utc.strftime("%Y-%m-%dT%H:%M:%SZ"), local.strftime("%Y-%m-%dT%H:%M:%S%z")


def read_state_file(path: str) -> tuple[dict[str, str], list[str]]:
    """读一份快照：返回 {路径: 首行第 2 列元数据} 与头部行（`#` 开头）。"""
    if not os.path.exists(path):
        return {}, []
    rows: dict[str, str] = {}
    head: list[str] = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line.strip():
                continue
            if line.startswith("#"):
                head.append(line)
                continue
            parts = line.split("\t")
            rows[parts[0]] = parts[1] if len(parts) > 1 else ""
    return rows, head


def read_broken(path: str) -> list[tuple[str, str, str]]:
    """读 broken.txt：列序为 `category<TAB>token<TAB>sources`（category 不唯一，不能做字典键）。"""
    if not os.path.exists(path):
        return []
    rows: list[tuple[str, str, str]] = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line.strip() or line.startswith("#"):
                continue
            parts = (line.split("\t") + ["", ""])[:3]
            rows.append((parts[0], parts[1], parts[2]))
    return rows


def file_size(root: str, rel: str) -> int:
    try:
        return os.path.getsize(os.path.join(root, rel))
    except OSError:
        return 0


def sidecars_of(root: str, rel: str) -> list[str]:
    out = []
    for ext in SIDECAR_EXTS:
        if os.path.exists(os.path.join(root, rel + ext)):
            out.append(rel + ext)
    return out


def untracked_files(root: str) -> set[str]:
    proc = subprocess.run(
        ["git", "-C", root, "ls-files", "-o", "--exclude-standard", "-z"],
        capture_output=True, check=False,
    )
    if proc.returncode != 0:
        return set()
    return {p for p in proc.stdout.decode("utf-8", "replace").split("\0") if p}


def empty_dirs(root: str) -> list[str]:
    out: list[str] = []
    for dirpath, dirnames, filenames in os.walk(root):
        rel = os.path.relpath(dirpath, root).replace(os.sep, "/")
        if rel == ".":
            continue
        parts = rel.split("/")
        if any(p in WALK_SKIP_DIRS for p in parts):
            dirnames[:] = []
            continue
        if len(parts) >= 2 and (parts[0], parts[1]) in {(r, d) for r in ("BangDreamLib",) for d in BODY_SYNC_DIRS}:
            dirnames[:] = []
            continue
        dirnames[:] = [d for d in dirnames if d not in WALK_SKIP_DIRS]
        if not dirnames and not filenames:
            out.append(rel + "/")
    return sorted(out)


# ---------------------------------------------------------------------------
def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="四态 + 死簇快照 → A/B/C 三档清理清单")
    parser.add_argument("--project-root", default=".", help="仓库根目录（默认 .）")
    parser.add_argument("--audit-dir", default=".vfx_staging/audit", help="四态 + 死簇快照目录（相对路径相对 --project-root 解析）")
    parser.add_argument("--out", default=None, help="输出目录（默认与 --audit-dir 相同；相对路径相对 --project-root 解析）")
    parser.add_argument("--keep-file", action="append", default=[],
                        help="追加保留规则文件（每行一条 glob，# 起注释）；可重复")
    args = parser.parse_args(argv)

    root = os.path.abspath(args.project_root)
    audit_dir = args.audit_dir if os.path.isabs(args.audit_dir) else os.path.join(root, args.audit_dir)
    out_dir = args.out or audit_dir
    out_dir = out_dir if os.path.isabs(out_dir) else os.path.join(root, out_dir)
    os.makedirs(out_dir, exist_ok=True)
    utc_stamp, local_stamp = now_stamps()

    # ---- 1. 读四态 + 死簇 + 辅助桶 ---------------------------------------
    snapshots: dict[str, dict[str, str]] = {}
    headers: dict[str, list[str]] = {}
    for name in FOUR_STATES + (DEAD_CLUSTER_STATE,) + SIDECAR_STATES:
        snapshots[name], headers[name] = read_state_file(os.path.join(audit_dir, name))

    confirmed = snapshots["confirmed_orphan.txt"]
    dead_cluster = snapshots[DEAD_CLUSTER_STATE]
    weak = snapshots["weak.txt"]
    referenced = snapshots["referenced.txt"]
    convention = snapshots["convention_referenced.txt"]
    sidecar_orphan = snapshots["sidecar_orphan.txt"]
    sidecar_missing = snapshots["sidecar_missing.txt"]
    broken = read_broken(os.path.join(audit_dir, "broken.txt"))

    # ---- 2. keep 规则（与 scan_orphans.py 共享同一份，见 keep_rules.py） ----
    keep_rules: list[kr.KeepRule] = list(kr.DEFAULT_KEEP_RULES)
    for kf in args.keep_file:
        kf_path = kf if os.path.isabs(kf) else os.path.join(root, kf)
        if not os.path.exists(kf_path):
            print(f"PROBLEM keep_file_missing {kf}")
            return 1
        keep_rules.extend(kr.load_keep_file(root, kf_path))

    def keep_reason(path: str) -> tuple[str, str] | None:
        rule = kr.find_keep_rule(path, keep_rules)
        return (rule.reason, rule.recheck) if rule else None

    # ---- 3. 快照过期复核（os.path.exists 逐条） --------------------------
    tracked_untracked = untracked_files(root)

    def exists(rel: str) -> bool:
        return os.path.exists(os.path.join(root, rel))

    stale: list[tuple[str, str]] = []   # (path, 来源桶)
    for name, rows in (("confirmed_orphan.txt", confirmed),
                       (DEAD_CLUSTER_STATE, dead_cluster),
                       ("weak.txt", weak)):
        for p in rows:
            if not exists(p):
                stale.append((p, name))
    for p in sidecar_orphan:
        if not exists(p):
            stale.append((p, "sidecar_orphan.txt"))
    stale.sort()

    # ---- 4. 分档 --------------------------------------------------------
    tier_a: list[dict] = []
    tier_b: list[dict] = []
    tier_c: list[dict] = []

    def add(tier: list[dict], path: str, reason: str, recheck: str = "", source: str = "") -> None:
        sidecars = sidecars_of(root, path) if os.path.isfile(os.path.join(root, path)) else []
        size = file_size(root, path) + sum(file_size(root, s) for s in sidecars)
        entry = {"path": path, "reason": reason, "recheck": recheck,
                 "sidecars": sidecars, "size": size, "source": source}
        tier.append(entry)

    # 4.1 confirmed_orphan：A 档（明确废件）
    for path, meta in sorted(confirmed.items()):
        if not exists(path):
            continue
        source = "confirmed_orphan"
        # 未跟踪 = 可能是用户 WIP，无处置权（SKILL.md 前置 2、cleanup-authoring.md:107）
        if path in tracked_untracked:
            add(tier_c, path, "未跟踪文件，可能是用户 WIP：清理任务无处置权，必须先与用户确认", "用户确认归属后", source)
            continue
        safe = safety_reason(path)
        if safe:
            add(tier_c, path, safe, "人工确认后", source)
            continue
        # 脚本类不落 A：模型类由反射实例化，无引用 ≠ 可删（cleanup-authoring.md:47-61）
        if path.endswith(".cs"):
            add(tier_c, path, "脚本类：模型类走反射+特性双轨注册，静态无引用不等于可删", "构建验证 + 逐条签字后", source)
            continue
        keep = keep_reason(path)
        if keep:
            add(tier_b, path, keep[0], keep[1], source)
            continue
        # 卡图不直接删：docs/cleanup-authoring.md:95（第 10 条）要求仍可能复用的卡图
        # 先记账到 images/card_portraits/DEPRECATED.md，等美术确认；账本格式见
        # references/deprecation-ledger.md。这是**通用规则**，不是逐文件白名单：
        # 任何进入分档流程的卡图（= 无同名类/无共享图匹配）都落 C。
        if "/images/card_portraits/" in path:
            add(tier_c, path,
                "无对应卡类的卡图：不直接删除，按 docs/cleanup-authoring.md:95 记入 "
                "images/card_portraits/DEPRECATED.md（格式见 references/deprecation-ledger.md），等美术确认",
                "美术确认「可删」并记账后再删", source)
            continue
        add(tier_a, path, f"引用图无命中（快照判定：{meta}）", "", source)

    # 4.1b dead_cluster（v1.2.0 新增桶）：引用者全部是孤儿（且引用者非「活跃保留」）。
    # 判定强度弱于 confirmed_orphan，**默认落 B，不得落 A**
    # （docs/cleanup-authoring.md:81「等实装的死簇」）。
    # 但安全域/未跟踪/卡图等更强的规则仍然优先。
    for path, meta in sorted(dead_cluster.items()):
        if not exists(path):
            continue
        source = DEAD_CLUSTER_STATE
        if path in tracked_untracked:
            add(tier_c, path, "死簇成员，但属未跟踪文件（可能是用户 WIP）：清理任务无处置权",
                "用户确认归属后", source)
            continue
        safe = safety_reason(path)
        if safe:
            add(tier_c, path, f"死簇成员；{safe}", "人工确认后", source)
            continue
        keep = keep_reason(path)
        if keep:
            add(tier_b, path, f"{keep[0]}（死簇快照：{meta}）", keep[1], source)
            continue
        if "/images/card_portraits/" in path:
            add(tier_c, path,
                "死簇成员且无对应卡类的卡图：不直接删除，按 docs/cleanup-authoring.md:95 记入 "
                "images/card_portraits/DEPRECATED.md，等美术确认",
                "美术确认「可删」并记账后再删", source)
            continue
        add(tier_b, path,
            f"死簇：引用者全部为孤儿/待实装（快照：{meta}）；判定强度弱于「确认无引用」",
            "引用链恢复或整簇被确认为废件后", source)

    # 4.2 weak：弱命中 → 默认 C；命中 keep 规则则 B
    for path, meta in sorted(weak.items()):
        if not exists(path):
            continue
        keep = keep_reason(path)
        if keep:
            add(tier_b, path, f"{keep[0]}（弱命中快照：{meta}）", keep[1], "weak")
            continue
        add(tier_c, path, f"弱命中，待人工确认：{meta}", "人工确认后可重分档", "weak")

    # 4.3 sidecar 孤儿：源已不存在 → 明确废件，A 档 + sidecar 连带
    for path in sorted(sidecar_orphan):
        if not exists(path):
            continue
        source_rel = path[: -len(".import")] if path.endswith(".import") else path[: -len(".uid")]
        add(tier_a, path, f"sidecar 孤儿：源文件 {source_rel} 已不存在（docs/cleanup-authoring.md:92）",
            "", "sidecar_orphan")

    # 4.4 sidecar 缺失：源在但副产物缺 → 缺陷单，不是清理项
    for path in sorted(sidecar_missing):
        add(tier_c, path, "sidecar 缺失：源文件在但副产物不存在，属缺陷而非废件（需重开编辑器让 Godot 生成）",
            "Godot 重新导入后", "sidecar_missing")

    # 4.5 结构性冗余 + 空目录（keep-list.md 第 3 节）
    for path in STRUCTURAL_CANDIDATES:
        if exists(path) and not any(e["path"] == path for e in tier_a + tier_b + tier_c):
            add(tier_c, path, "结构性冗余：多解决方案文件并存，删除前需确认构建入口", "确认 CI/IDE 入口后", "structural")
    for d in empty_dirs(root):
        if not any(e["path"] == d for e in tier_a + tier_b + tier_c):
            add(tier_c, d, "空目录：无内容，但删除会改变仓库结构，需人工确认", "人工确认后", "structural")

    # ---- 5. 统计 --------------------------------------------------------
    tiers = (("A", "A 可直接删（需逐条签字）", tier_a),
             ("B", "B 建议保留", tier_b),
             ("C", "C 需人工确认", tier_c))
    total_size = sum(e["size"] for e in tier_a + tier_b + tier_c)

    def mb(n: int) -> str:
        return f"{n / 1024 / 1024:.2f} MB"

    input_domain = (
        f"快照目录 {os.path.relpath(audit_dir, root)}/"
        f"{{{','.join(FOUR_STATES)},{DEAD_CLUSTER_STATE}}}（由 scan_orphans.py 生成，"
        f"头部含脚本名/版本/时间/输入域）；"
        f"逐条 os.path.exists 复核；keep 规则 {len(keep_rules)} 条"
    )

    # ---- 6. 落盘 --------------------------------------------------------
    md: list[str] = []
    md.append(f"# 过时文件清理清单（{SCRIPT_NAME} v{VERSION}）")
    md.append("")
    md.append(f"- 生成时间（UTC）：`{utc_stamp}`")
    md.append(f"- 生成时间（本地）：`{local_stamp}`")
    md.append(f"- 输入域：{input_domain}")
    md.append(f"- 快照头部：`{headers['confirmed_orphan.txt'][0] if headers['confirmed_orphan.txt'] else '缺失'}`")
    md.append("- 签字状态：**未签字**。A 档在人工逐条签字前禁止执行删除（SKILL.md S3 闸门）。")
    md.append("")
    md.append("## 汇总")
    md.append("")
    md.append("| 桶 | 项数 | 含 sidecar 连带项数 | 体积 |")
    md.append("|---|---:|---:|---:|")
    for key, title, tier in tiers:
        sidecar_count = len(tier) + sum(len(e["sidecars"]) for e in tier)
        md.append(f"| {title} | {len(tier)} | {sidecar_count} | {mb(sum(e['size'] for e in tier))} |")
    md.append(f"| 合计 | {len(tier_a) + len(tier_b) + len(tier_c)} | "
              f"{len(tier_a) + len(tier_b) + len(tier_c) + sum(len(e['sidecars']) for e in tier_a + tier_b + tier_c)} | {mb(total_size)} |")
    md.append(f"| dead_cluster（死簇成员，已按 B 档口径分派） | {len(dead_cluster)} | — | — |")
    md.append(f"| stale（快照过期，已排除） | {len(stale)} | — | — |")
    md.append(f"| broken（断链） | {len(broken)} | — | — |")
    md.append("")

    for key, title, tier in tiers:
        md.append(f"## {title} —— {len(tier)} 项 / {mb(sum(e['size'] for e in tier))}")
        md.append("")
        if key == "A":
            md.append("| # | 路径 | 判定依据 | sidecar 连带 | 体积 | 签字 |")
            md.append("|---:|---|---|---|---:|---|")
        elif key == "B":
            md.append("| # | 路径 | 保留理由 | 重新评估条件 | sidecar 连带 | 体积 |")
            md.append("|---:|---|---|---|---|---:|")
        else:
            md.append("| # | 路径 | 挂起原因 | 解除条件 | 体积 |")
            md.append("|---:|---|---|---|---:|")
        for i, e in enumerate(tier, 1):
            sc = "<br>".join(e["sidecars"]) if e["sidecars"] else "—"
            if key == "A":
                md.append(f"| {i} | `{e['path']}` | {e['reason']} | {sc} | {mb(e['size'])} | ☐ |")
            elif key == "B":
                md.append(f"| {i} | `{e['path']}` | {e['reason']} | {e['recheck']} | {sc} | {mb(e['size'])} |")
            else:
                md.append(f"| {i} | `{e['path']}` | {e['reason']} | {e['recheck']} | {mb(e['size'])} |")
        md.append("")

    md.append(f"## stale —— 快照过期项（{len(stale)} 项）")
    md.append("")
    md.append("以下条目在快照里存在、复核时已不在磁盘上。**过期快照不得作为证据**，必须重跑 `scan_orphans.py`。")
    md.append("")
    if stale:
        md.append("| 路径 | 来源桶 |")
        md.append("|---|---|")
        for p, src in stale:
            md.append(f"| `{p}` | {src} |")
    else:
        md.append("（无）")
    md.append("")

    md.append("## broken —— 断链（{0} 项，来自 broken.txt）".format(len(broken)))
    md.append("")
    if broken:
        md.append("| 类别 | token | 引用点 |")
        md.append("|---|---|---|")
        for category, token, sources in broken:
            md.append(f"| {category} | `{token}` | {sources} |")
    else:
        md.append("（无）")
    md.append("")

    with open(os.path.join(out_dir, "cleanup_manifest.md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(md) + "\n")

    txt: list[str] = []
    txt.append(f"# {SCRIPT_NAME} v{VERSION}")
    txt.append(f"# generated_at_utc: {utc_stamp}")
    txt.append(f"# generated_at_local: {local_stamp}")
    txt.append(f"# input_domain: {input_domain}")
    txt.append("# signature: UNVERIFIED — A 档需人工逐条签字")
    txt.append("#")
    for key, title, tier in tiers:
        txt.append(f"## {title} —— {len(tier)} 项 / {mb(sum(e['size'] for e in tier))}")
        for e in tier:
            sc = ("\t[" + ",".join(e["sidecars"]) + "]") if e["sidecars"] else ""
            txt.append(f"{e['path']}\t{e['reason']}{sc}")
        txt.append("")
    txt.append(f"## stale —— {len(stale)} 项")
    for p, src in stale:
        txt.append(f"{p}\t[{src}]")
    txt.append("")
    txt.append(f"## broken —— {len(broken)} 项")
    for category, token, sources in broken:
        txt.append(f"{token}\t{category}\t{sources}")
    with open(os.path.join(out_dir, "cleanup_manifest.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(txt) + "\n")

    # ---- 7. stdout -------------------------------------------------------
    print(f"snapshot_dir {os.path.relpath(audit_dir, root)}")
    print(f"tier_a {len(tier_a)}")
    print(f"tier_b {len(tier_b)}")
    print(f"tier_c {len(tier_c)}")
    print(f"tier_a_bytes {sum(e['size'] for e in tier_a)}")
    print(f"tier_b_bytes {sum(e['size'] for e in tier_b)}")
    print(f"tier_c_bytes {sum(e['size'] for e in tier_c)}")
    print(f"total_items {len(tier_a) + len(tier_b) + len(tier_c)}")
    print(f"dead_cluster {len(dead_cluster)}")
    print(f"stale {len(stale)}")
    print(f"broken {len(broken)}")
    print(f"keep_rules {len(keep_rules)}")
    print(f"output_dir {os.path.relpath(out_dir, root)}")

    warnings = 0
    problems = 0
    for p, src in stale:
        problems += 1
        print(f"PROBLEM stale {p} (from {src})")
    for category, path, _sources in broken:
        if category.startswith(("本体程序集路径", "本体资源")):
            warnings += 1
            print(f"WARN broken(body) {category} {path}")
        else:
            problems += 1
            print(f"PROBLEM broken {category} {path}")
    for e in tier_a:
        warnings += 1
        print(f"WARN tier_a_pending_signature {e['path']}")
    print(f"warnings {warnings}")
    print(f"problems {problems}")
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
