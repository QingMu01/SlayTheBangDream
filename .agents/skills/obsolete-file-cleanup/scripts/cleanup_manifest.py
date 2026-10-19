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
  python3 cleanup_manifest.py [--audit-dir .vfx_staging/audit] [--keep-file <path>]
                              [--out .vfx_staging/audit] [--format {md,tsv,both}]

产出（默认 `both`）：
  * `cleanup_manifest.md` —— **人看**：每项一行的紧凑清单（`- [ ]` 勾选框 + 路径 + 理由短码），
    末尾附理由码表；不再使用宽表格（宽表格在渲染器里会把路径挤成一字一行）。
  * `cleanup_decisions.tsv` —— **人改**：签字表，列 `id bucket path sidecar bytes reason action note`。
    人只需要填 `action`（delete / keep / defer）与必要的 `note`，然后交给 `sign_off.py`。

⚠️ 临时目录语义：上述两份产物默认落在 `.vfx_staging/` —— 特效设计用的临时目录，
**随时可能被整体清理**。必须在同一次会话内完成「扫描 → 签字 → dry-run → apply」；
**长期留档靠 `docs/cleanup-log.md`**（由 `sign_off.py --apply` 追加）。

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

VERSION = "1.3.0"
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
    "BangDreamInSpire.sln", "ItsCrychic.sln", "BangDreamInSpire.sln",
)

SIDECAR_EXTS = (".import", ".uid")

DECISIONS_NAME = "cleanup_decisions.tsv"

# 理由**稳定短码**：md 里只印短码（避免宽表格被长理由撑爆），码表在文末。
# 码首字母 = 该条落地的档位，便于人眼对齐。
REASON_CODES: dict[str, str] = {
    "A1": "引用图无命中",
    "A2": "sidecar 孤儿（源已删）",
    "B1": "预留待实装（keep，不阻断级联）",
    "B2": "活跃保留（keep：预览件/模板）",
    "B3": "死簇（仅被孤儿引用）",
    "C1": "结构性冗余 / 空目录",
    "C2": "卡图待美术确认",
    "C3": "脚本类（反射+特性双轨注册）",
    "C4": "未跟踪 WIP（无处置权）",
    "C5": "安全域（本体同步/缓存/配置）",
    "C6": "弱命中待人工确认",
    "C7": "sidecar 缺失（缺陷）",
}

TEMPDIR_NOTICE = (
    "⚠️ 临时目录提醒：`--out` 默认落在 .vfx_staging/ （特效设计的临时目录，**随时可能被整体清理**）。"
    "必须在同一次会话内完成「扫描 → 签字 → dry-run → apply」；"
    "长期留档靠 `docs/cleanup-log.md`（由 sign_off.py 追加）。"
)


def shorten_path(path: str, universe: set[str]) -> str:
    """人看的短路径：`<mod>/<mod>/rest` → `<mod>/rest`；有歧义时保留全路径。"""
    for mod in ("BangDreamInSpire", "ItsCrychic"):
        prefix = f"{mod}/{mod}/"
        if path.startswith(prefix):
            cand = f"{mod}/{path[len(prefix):]}"
            if cand not in universe:
                return cand
    return path

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
    if len(parts) >= 2 and parts[0] == "BangDreamInSpire" and parts[1] in BODY_SYNC_DIRS:
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
        if len(parts) >= 2 and (parts[0], parts[1]) in {(r, d) for r in ("BangDreamInSpire",) for d in BODY_SYNC_DIRS}:
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
    parser.add_argument("--out", default=None,
                        help="输出目录（默认与 --audit-dir 相同，是临时目录；相对路径相对 --project-root 解析）")
    parser.add_argument("--keep-file", action="append", default=[],
                        help="追加保留规则文件（每行一条 glob，# 起注释）；可重复")
    parser.add_argument("--format", choices=("md", "tsv", "both"), default="both",
                        help="md=只看人看的紧凑清单；tsv=只出签字表；both=两份都出（默认）")
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

    def keep_rule(path: str) -> "kr.KeepRule | None":
        return kr.find_keep_rule(path, keep_rules)

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

    def add(tier: list[dict], path: str, reason: str, recheck: str = "",
            source: str = "", code: str = "C6") -> None:
        sidecars = sidecars_of(root, path) if os.path.isfile(os.path.join(root, path)) else []
        size = file_size(root, path) + sum(file_size(root, s) for s in sidecars)
        entry = {"path": path, "reason": reason, "recheck": recheck,
                 "sidecars": sidecars, "size": size, "source": source, "code": code}
        tier.append(entry)

    def keep_code(rule: "kr.KeepRule") -> str:
        """活跃保留（预览件/模板）→ B2；预留待实装 → B1。与 keep_rules.cascade_blocks 同源。"""
        return "B2" if rule.cascade_blocks else "B1"

    # 4.1 confirmed_orphan：A 档（明确废件）
    for path, meta in sorted(confirmed.items()):
        if not exists(path):
            continue
        source = "confirmed_orphan"
        # 未跟踪 = 可能是用户 WIP，无处置权（SKILL.md 前置 2、cleanup-authoring.md:107）
        if path in tracked_untracked:
            add(tier_c, path, "未跟踪文件，可能是用户 WIP：清理任务无处置权，必须先与用户确认",
                "用户确认归属后", source, code="C4")
            continue
        safe = safety_reason(path)
        if safe:
            add(tier_c, path, safe, "人工确认后", source, code="C5")
            continue
        # 脚本类不落 A：模型类由反射实例化，无引用 ≠ 可删（cleanup-authoring.md:47-61）
        if path.endswith(".cs"):
            add(tier_c, path, "脚本类：模型类走反射+特性双轨注册，静态无引用不等于可删",
                "构建验证 + 逐条签字后", source, code="C3")
            continue
        rule = keep_rule(path)
        if rule:
            add(tier_b, path, rule.reason, rule.recheck, source, code=keep_code(rule))
            continue
        # 卡图不直接删：docs/cleanup-authoring.md:95（第 10 条）要求仍可能复用的卡图
        # 先记账到 images/card_portraits/DEPRECATED.md，等美术确认；账本格式见
        # references/deprecation-ledger.md。这是**通用规则**，不是逐文件白名单：
        # 任何进入分档流程的卡图（= 无同名类/无共享图匹配）都落 C。
        if "/images/card_portraits/" in path:
            add(tier_c, path,
                "无对应卡类的卡图：不直接删除，按 docs/cleanup-authoring.md:95 记入 "
                "images/card_portraits/DEPRECATED.md（格式见 references/deprecation-ledger.md），等美术确认",
                "美术确认「可删」并记账后再删", source, code="C2")
            continue
        add(tier_a, path, f"引用图无命中（快照判定：{meta}）", "", source, code="A1")

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
                "用户确认归属后", source, code="C4")
            continue
        safe = safety_reason(path)
        if safe:
            add(tier_c, path, f"死簇成员；{safe}", "人工确认后", source, code="C5")
            continue
        rule = keep_rule(path)
        if rule:
            add(tier_b, path, f"{rule.reason}（死簇快照：{meta}）", rule.recheck, source, code=keep_code(rule))
            continue
        if "/images/card_portraits/" in path:
            add(tier_c, path,
                "死簇成员且无对应卡类的卡图：不直接删除，按 docs/cleanup-authoring.md:95 记入 "
                "images/card_portraits/DEPRECATED.md，等美术确认",
                "美术确认「可删」并记账后再删", source, code="C2")
            continue
        add(tier_b, path,
            f"死簇：引用者全部为孤儿/待实装（快照：{meta}）；判定强度弱于「确认无引用」",
            "引用链恢复或整簇被确认为废件后", source, code="B3")

    # 4.2 weak：弱命中 → 默认 C；命中 keep 规则则 B
    for path, meta in sorted(weak.items()):
        if not exists(path):
            continue
        rule = keep_rule(path)
        if rule:
            add(tier_b, path, f"{rule.reason}（弱命中快照：{meta}）", rule.recheck, "weak", code=keep_code(rule))
            continue
        add(tier_c, path, f"弱命中，待人工确认：{meta}", "人工确认后可重分档", "weak", code="C6")

    # 4.3 sidecar 孤儿：源已不存在 → 明确废件，A 档 + sidecar 连带
    for path in sorted(sidecar_orphan):
        if not exists(path):
            continue
        source_rel = path[: -len(".import")] if path.endswith(".import") else path[: -len(".uid")]
        add(tier_a, path, f"sidecar 孤儿：源文件 {source_rel} 已不存在（docs/cleanup-authoring.md:92）",
            "", "sidecar_orphan", code="A2")

    # 4.4 sidecar 缺失：源在但副产物缺 → 缺陷单，不是清理项
    for path in sorted(sidecar_missing):
        add(tier_c, path, "sidecar 缺失：源文件在但副产物不存在，属缺陷而非废件（需重开编辑器让 Godot 生成）",
            "Godot 重新导入后", "sidecar_missing", code="C7")

    # 4.5 结构性冗余 + 空目录（keep-list.md 第 3 节）
    for path in STRUCTURAL_CANDIDATES:
        if exists(path) and not any(e["path"] == path for e in tier_a + tier_b + tier_c):
            add(tier_c, path, "结构性冗余：多解决方案文件并存，删除前需确认构建入口",
                "确认 CI/IDE 入口后", "structural", code="C1")
    for d in empty_dirs(root):
        if not any(e["path"] == d for e in tier_a + tier_b + tier_c):
            add(tier_c, d, "空目录：无内容，但删除会改变仓库结构，需人工确认",
                "人工确认后", "structural", code="C1")

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

    # ---- 6. 落盘（v1.3.0：紧凑 md + 可编辑 decisions.tsv） -----------------
    all_paths = {e["path"] for e in tier_a + tier_b + tier_c}

    def display_path(p: str) -> str:
        return shorten_path(p, all_paths)

    def describe(e: dict) -> str:
        text = REASON_CODES.get(e["code"], e["code"])
        if e["recheck"]:
            text += f"（复核：{e['recheck']}）"
        return text

    def list_line(e: dict) -> str:
        n = len(e["sidecars"])
        sc = f"sidecar×{n}；" if n else ""
        return f"- [ ] `{display_path(e['path'])}` — {describe(e)}；{sc}{mb(e['size'])}；`{e['code']}`"

    decisions_rel = os.path.join(os.path.relpath(out_dir, root), DECISIONS_NAME).replace(os.sep, "/")
    manifest_rel = os.path.join(os.path.relpath(out_dir, root), "cleanup_manifest.md").replace(os.sep, "/")

    md: list[str] = []
    md.append(f"# 过时文件清理清单（{SCRIPT_NAME} v{VERSION}）")
    md.append("")
    md.append(f"- 生成时间（UTC）：`{utc_stamp}`")
    md.append(f"- 生成时间（本地）：`{local_stamp}`")
    md.append(f"- 输入域：{input_domain}")
    md.append(f"- 快照头部：`{headers['confirmed_orphan.txt'][0] if headers['confirmed_orphan.txt'] else '缺失'}`")
    md.append(f"- 签字方式：编辑 `{decisions_rel}` 的 `action` 列 → "
              f"`python3 sign_off.py --decisions {decisions_rel} --dry-run` → 确认后 `--apply`")
    md.append(f"- {TEMPDIR_NOTICE}")
    md.append("")
    md.append("## 汇总")
    md.append("")
    for key, title, tier in tiers:
        sidecar_count = len(tier) + sum(len(e["sidecars"]) for e in tier)
        md.append(f"- **{title}**：{len(tier)} 项 / {mb(sum(e['size'] for e in tier))}"
                  f"（含 sidecar 连带共 {sidecar_count} 项）")
    md.append(f"- **合计**：{len(tier_a) + len(tier_b) + len(tier_c)} 项 / {mb(total_size)}")
    md.append(f"- dead_cluster {len(dead_cluster)} 项 ／ stale {len(stale)} 项 ／ broken {len(broken)} 项")
    md.append("")

    for key, title, tier in tiers:
        md.append(f"## {title} —— {len(tier)} 项 / {mb(sum(e['size'] for e in tier))}")
        md.append("")
        if key == "A":
            md.append("> A 档每一条都必须在 `" + DECISIONS_NAME + "` 里填 `action`，未填则 `sign_off.py` 拒绝执行。")
            md.append("")
        for e in tier:
            md.append(list_line(e))
        if not tier:
            md.append("（无）")
        md.append("")

    md.append("## 理由码表")
    md.append("")
    for code in sorted(REASON_CODES):
        md.append(f"- `{code}` {REASON_CODES[code]}")
    md.append("")

    md.append(f"## stale —— 快照过期项（{len(stale)} 项）")
    md.append("")
    md.append("以下条目在快照里存在、复核时已不在磁盘上。**过期快照不得作为证据**，必须重跑 `scan_orphans.py`。")
    md.append("")
    if stale:
        for p, src in stale:
            md.append(f"- `{p}` — 来自 `{src}`")
    else:
        md.append("（无）")
    md.append("")

    md.append(f"## broken —— 断链（{len(broken)} 项，来自 broken.txt）")
    md.append("")
    if broken:
        for category, token, sources in broken:
            md.append(f"- `{token}` — {category}；引用点：{sources}")
    else:
        md.append("（无）")
    md.append("")

    tsv: list[str] = []
    tsv.append(f"# {SCRIPT_NAME} v{VERSION} — 人工签字表（制表符分隔，请用编辑器改 action/note 列）")
    tsv.append(f"# generated_at_utc: {utc_stamp}")
    tsv.append(f"# generated_at_local: {local_stamp}")
    tsv.append(f"# input_domain: {input_domain}")
    tsv.append("# 用法：")
    tsv.append("#   1) A 档每行必须填 action；B/C 档留空 = 不动。")
    tsv.append("#   2) action 取值 delete / keep / defer；填 keep|defer 时必须在 note 列写理由（--force 可跳过）。")
    tsv.append("#   3) 填完先 dry-run 核对：python3 sign_off.py --decisions <本文件> --dry-run")
    tsv.append("#      确认无误再执行：  python3 sign_off.py --decisions <本文件> --apply")
    tsv.append("#   4) 安全域（本体同步目录/.godot/bin/obj/.vfx_staging/未跟踪 WIP）即使 --force 也拒删。")
    tsv.append(f"#   5) {TEMPDIR_NOTICE}")
    tsv.append("# 图例（reason 列）：" + "  ".join(f"{c}={REASON_CODES[c]}" for c in sorted(REASON_CODES)))
    tsv.append("# sidecar 列 = 逗号分隔的相对仓库根路径（源文件应连带删除的 .import/.uid）")
    tsv.append("# bucket: A=可直接删 / B=建议保留 / C=需人工确认    bytes: 源+sidecar 字节数")
    tsv.append("# 列： id  bucket  path  sidecar  bytes  reason  action  note")
    tsv.append("\t".join(("id", "bucket", "path", "sidecar", "bytes", "reason", "action", "note")))
    seq = {"A": 0, "B": 0, "C": 0}
    for key, _title, tier in tiers:
        for e in tier:
            seq[key] += 1
            tsv.append("\t".join((
                f"{key}{seq[key]:03d}", key, e["path"], ",".join(e["sidecars"]),
                str(e["size"]), f"{e['code']} {REASON_CODES.get(e['code'], '')}", "", "",
            )))

    written: list[str] = []
    if args.format in ("md", "both"):
        with open(os.path.join(out_dir, "cleanup_manifest.md"), "w", encoding="utf-8") as fh:
            fh.write("\n".join(md) + "\n")
        written.append("cleanup_manifest.md")
    if args.format in ("tsv", "both"):
        with open(os.path.join(out_dir, DECISIONS_NAME), "w", encoding="utf-8") as fh:
            fh.write("\n".join(tsv) + "\n")
        written.append(DECISIONS_NAME)
    # v1.3.0 起不再产出 cleanup_manifest.txt；发现旧版残留就清掉，避免拿旧证据当新证据
    legacy = os.path.join(out_dir, "cleanup_manifest.txt")
    legacy_removed = False
    if os.path.exists(legacy):
        try:
            with open(legacy, encoding="utf-8") as fh:
                first = fh.readline()
            if SCRIPT_NAME in first:
                os.remove(legacy)
                legacy_removed = True
        except OSError:
            pass

    # ---- 7. stdout -------------------------------------------------------
    print(f"snapshot_dir {os.path.relpath(audit_dir, root)}")
    print(f"format {args.format}")
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
    for name in written:
        print(f"written {name}")
    if legacy_removed:
        print("removed legacy cleanup_manifest.txt")
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
