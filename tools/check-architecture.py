#!/usr/bin/env python3
"""架构边界检查（构建前门禁）。

规则来自耦合治理计划 P2：核心机制不得反向依赖 UI/内容层，内容模组只允许依赖稳定契约。

退出码：HARD 违规 > 0 时 1；``--strict`` 时 WARN 也计为失败；否则 0。
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path

CORE = "BangDreamInSpire"
CONTENT = "ItsCrychic"
CORE_SCRIPTS = f"{CORE}/Scripts"

# 允许的例外：相对路径 -> 原因。逐项删除时先从这里移除，脚本会提示过期条目。
ALLOWLIST: dict[str, str] = {
    # --- 内容模组对核心 Nodes 的残留（P3：需要 VFX/商人 UI 契约） ---
    "ItsCrychic/Scripts/Cards/Saki/Attack/StrikeSakiko.cs": "P3: 依赖核心 StrikeSakikoVfx 场景",
    "ItsCrychic/Scripts/Cards/Saki/Attack/Oscillation.cs": "P3: 依赖核心 VFX 节点类型",
    "ItsCrychic/Scripts/Nodes/MegaScripts/CrychicMerchant.cs": "P3: 继承核心商人 UI 基类",
    # --- 核心机制对 Nodes 的残留（P3：需要 VFX 工厂/UI 桥） ---
    "BangDreamInSpire/Scripts/Mechanics/ExtraDeck/ExtraCardMerchantManager.cs": "P3: 商人库存 UI 直接操作节点",
    "BangDreamInSpire/Scripts/Mechanics/Lingered/LingeredOrbitManager.cs": "P3: 环绕资源预览 VFX",
    "BangDreamInSpire/Scripts/Mechanics/Lingered/LingeredMechanic.cs": "P3: 环绕资源预览 VFX 注册",
    "BangDreamInSpire/Scripts/Mechanics/MusicNote/MusicNoteAsyncDamageEffect.cs": "P3: 音符飞行 VFX",
    "BangDreamInSpire/Scripts/Mechanics/MusicNote/VfxManagerPatches.cs": "P3: VFX 容器补丁",
    # --- 核心代码中的角色专属标识符（P3：VFX 迁移） ---
    "BangDreamInSpire/Scripts/Nodes/VFX/StrikeSakikoVfx.cs": "P3: 角色专属 VFX 待迁回内容模组",
}

# 不用 \b：角色名常嵌在标识符中间（StrikeSakikoVfx），那正是要抓的专属命名。
CHARACTER_NAME = re.compile(r"(Sakiko|Mutsumi|Togawa|Wakaba)")


@dataclass(frozen=True)
class Rule:
    rule_id: str
    severity: str  # "HARD" | "WARN"
    scope: str  # 目录前缀（仓库相对）
    needles: tuple[str, ...]  # 违规命名空间片段
    description: str
    allowlist_applies: bool


RULES: tuple[Rule, ...] = (
    Rule("H1", "HARD", f"{CONTENT}/", ("BangDreamInSpire.Scripts.Mechanics",),
         "内容模组不得引用核心机制实现", False),
    Rule("H2", "HARD", f"{CORE_SCRIPTS}/Utils/", ("BangDreamInSpire.Scripts.Mechanics", "BangDreamInSpire.Scripts.Nodes"),
         "核心 Utils 层不得反向引用机制/UI 层", False),
    Rule("H3", "HARD", f"{CORE_SCRIPTS}/Mechanics/Perform/", ("BangDreamInSpire.Scripts.Nodes",),
         "演奏机制不得直接引用 UI 节点", False),
    Rule("W1", "WARN", f"{CONTENT}/", ("BangDreamInSpire.Scripts.Nodes",),
         "内容模组引用核心 UI 节点（待抽契约）", True),
    Rule("W2", "WARN", f"{CORE_SCRIPTS}/Mechanics/", ("BangDreamInSpire.Scripts.Nodes",),
         "核心机制引用 UI 节点（待抽 VFX/UI 契约）", True),
)


def strip_comments_and_strings(source: str) -> str:
    """把注释与字符串字面量替换为空白，保留换行与位置，便于按行报告。"""
    out: list[str] = []
    i, n = 0, len(source)
    while i < n:
        ch = source[i]
        nxt = source[i + 1] if i + 1 < n else ""
        if ch == "/" and nxt == "/":
            while i < n and source[i] != "\n":
                out.append(" ")
                i += 1
            continue
        if ch == "/" and nxt == "*":
            out.append("  ")
            i += 2
            while i < n and not (source[i] == "*" and i + 1 < n and source[i + 1] == "/"):
                out.append("\n" if source[i] == "\n" else " ")
                i += 1
            out.append("  ")
            i = min(i + 2, n)
            continue
        # 逐字字符串 @"..." / $@"..." / @$"..."
        if ch in "@$":
            j = i
            prefix = ""
            while j < n and source[j] in "@$" and len(prefix) < 2:
                prefix += source[j]
                j += 1
            if j < n and source[j] == '"':
                verbatim = "@" in prefix
                out.append(" " * (j - i + 1))
                i = j + 1
                while i < n:
                    c = source[i]
                    if verbatim:
                        if c == '"' and i + 1 < n and source[i + 1] == '"':
                            out.append("  ")
                            i += 2
                            continue
                        if c == '"':
                            out.append(" ")
                            i += 1
                            break
                    else:
                        if c == "\\" and i + 1 < n:
                            out.append("\n" if source[i + 1] == "\n" else "  ")
                            i += 2
                            continue
                        if c == '"':
                            out.append(" ")
                            i += 1
                            break
                    out.append("\n" if c == "\n" else " ")
                    i += 1
                continue
        if ch == '"':
            out.append(" ")
            i += 1
            while i < n:
                c = source[i]
                if c == "\\" and i + 1 < n:
                    out.append("  ")
                    i += 2
                    continue
                if c == '"':
                    out.append(" ")
                    i += 1
                    break
                out.append("\n" if c == "\n" else " ")
                i += 1
            continue
        if ch == "'":
            # 字符字面量；把 "Togawa" 这类误判排除掉，但 '\'' 与 '\n' 也要正确处理
            m = re.match(r"'(?:\\.|[^'\\\n])'", source[i:])
            if m:
                out.append(" " * len(m.group(0)))
                i += len(m.group(0))
                continue
        out.append(ch)
        i += 1
    return "".join(out)


def iter_cs_files(root: Path, scope: str):
    base = root / scope
    if not base.exists():
        return
    for path in sorted(base.rglob("*.cs")):
        if ".godot" in path.parts or "/obj/" in str(path) or "/bin/" in str(path):
            continue
        yield path


def rel(root: Path, path: Path) -> str:
    return path.relative_to(root).as_posix()


def check_using_rule(root: Path, rule: Rule, findings: list[tuple[str, str, str, str, bool, str]]):
    pattern = re.compile(r"^\s*using\s+(?:static\s+)?(?:[A-Za-z_]\w*\s*=\s*)?"
                         r"(" + "|".join(re.escape(x) for x in rule.needles) + r")(?=[.;\s])")
    for path in iter_cs_files(root, rule.scope):
        text = path.read_text(encoding="utf-8", errors="replace").lstrip("\ufeff")
        stripped = strip_comments_and_strings(text)
        for idx, line in enumerate(stripped.splitlines(), start=1):
            m = pattern.match(line)
            if not m:
                continue
            relpath = rel(root, path)
            allowed = rule.allowlist_applies and relpath in ALLOWLIST
            reason = ALLOWLIST.get(relpath, "")
            findings.append((rule.rule_id, rule.severity, relpath, f"{idx}: {m.group(1)}", allowed, reason))


def check_character_names(root: Path, findings: list[tuple[str, str, str, str, bool, str]]):
    for path in iter_cs_files(root, f"{CORE_SCRIPTS}/"):
        text = path.read_text(encoding="utf-8", errors="replace").lstrip("\ufeff")
        stripped = strip_comments_and_strings(text)
        relpath = rel(root, path)
        allowed = relpath in ALLOWLIST
        reason = ALLOWLIST.get(relpath, "")
        for idx, line in enumerate(stripped.splitlines(), start=1):
            m = CHARACTER_NAME.search(line)
            if m:
                findings.append(("W3", "WARN", relpath, f"{idx}: {m.group(1)}", allowed, reason))
                break


def main() -> int:
    parser = argparse.ArgumentParser(description="架构边界检查")
    parser.add_argument("--root", default=None, help="仓库根目录（默认脚本上一级）")
    parser.add_argument("--strict", action="store_true", help="WARN 也视为失败")
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parent.parent

    findings: list[tuple[str, str, str, str, bool, str]] = []
    for rule in RULES:
        check_using_rule(root, rule, findings)
    check_character_names(root, findings)

    hard = [f for f in findings if f[1] == "HARD"]
    warns = [f for f in findings if f[1] == "WARN" and not f[4]]
    allowed = [f for f in findings if f[4]]

    print(f"架构边界检查：{root}")
    for rule in RULES:
        print(f"  {rule.severity:<4} {rule.rule_id}  {rule.description}  [{rule.scope}]")
    print("  WARN W3    核心代码不得出现角色专属标识符  [BangDreamInSpire/Scripts/**]")
    print()

    if findings:
        print("| 级别 | 规则 | 文件 | 命中 | 状态 |")
        print("|---|---|---|---|---|")
        for rule_id, severity, relpath, detail, is_allowed, reason in findings:
            status = f"allowlist：{reason}" if is_allowed else "违规"
            print(f"| {severity} | {rule_id} | {relpath} | {detail} | {status} |")
        print()

    seen = {f[2] for f in findings}
    stale = sorted(p for p in ALLOWLIST if p not in seen)
    if stale:
        print("过期 allowlist 条目（已不再命中，请删除）：")
        for path in stale:
            print(f"  - {path}")
        print()

    print(f"汇总：HARD {len(hard)} / WARN {len(warns)} / allowlist {len(allowed)}")

    if hard:
        return 1
    if args.strict and warns:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
