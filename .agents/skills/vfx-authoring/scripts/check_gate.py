#!/usr/bin/env python3
"""计划闸门：把「用户批准过什么」变成可判定的指纹。

设计原则（v2）：**只锁契约，不锁措辞。**
指纹只覆盖计划正文里的「契约块」——素材集合、层结构、验收标准、授权包络。
契约块以外的正文（设计意图、素材预演、决策与默认值、假设与风险、交付物）
随便改，不会使批准失效；契约块内任何改动（换素材、改层表、改验收、改授权范围）
都会使批准失效，必须重新确认。

用法：
    python check_gate.py --plan <plan.md>            # 校验，exit 0 才允许写代码
    python check_gate.py --plan <plan.md> --approve --approved-by "用户原话"
    python check_gate.py --plan <plan.md> --reopen   # 退回 draft（契约有变动时）

契约块写法（在计划正文里，标记之间的内容参与指纹）：

    <!-- gate:begin -->
    ## 2. 素材清单
    ...
    ## 3. 层结构
    ...
    ## 4. 验收标准
    ...
    ## 5. 授权包络
    ...
    <!-- gate:end -->

退出码：
    0  已批准且指纹一致 → 放行
    1  尚未批准，或 --approve 缺 --approved-by
    2  契约在批准后被改动，原批准失效
    3  文件格式错误（含新计划缺契约块）
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from datetime import datetime
from pathlib import Path

GATE_BEGIN = "<!-- gate:begin -->"
GATE_END = "<!-- gate:end -->"

# 不参与指纹的 frontmatter 字段：它们描述"批准这件事本身"或纯路由信息，
# 覆盖进去会导致写完批准就被自己判失效，也会让改个 slug 就要重新批准。
FINGERPRINT_EXEMPT = {"status", "fingerprint", "approved_by", "approved_at", "mode"}


def split_plan(text: str) -> tuple[list[str], list[str]]:
    """拆出 frontmatter 行与正文行。"""
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        raise ValueError("plan.md 必须以 '---' 开头的 frontmatter 起始")

    for index in range(1, len(lines)):
        if lines[index].strip() == "---":
            return lines[1:index], lines[index + 1 :]

    raise ValueError("frontmatter 未闭合（缺少第二个 '---'）")


def normalize(lines: list[str]) -> str:
    """规范化：去掉行尾空白、压缩连续空行、去掉首尾空行。"""
    out: list[str] = []
    for line in lines:
        stripped = line.rstrip()
        if not stripped:
            if out and out[-1] == "":
                continue
            out.append("")
        else:
            out.append(stripped)
    while out and out[0] == "":
        out.pop(0)
    while out and out[-1] == "":
        out.pop()
    return "\n".join(out) + "\n"


def contract_lines(body_lines: list[str]) -> tuple[list[str] | None, bool]:
    """返回 (契约块行, 是否找到契约块)。找不到时返回 (None, False)。"""
    begin = end = -1
    for index, line in enumerate(body_lines):
        if begin < 0 and line.strip() == GATE_BEGIN:
            begin = index
        elif begin >= 0 and line.strip() == GATE_END:
            end = index
            break
    if begin < 0 or end < 0 or end == begin + 1:
        return None, False
    return body_lines[begin + 1 : end], True


def fingerprint_of(lines: list[str]) -> str:
    return hashlib.sha256(normalize(lines).encode("utf-8")).hexdigest()[:16]


def body_text(body_lines: list[str]) -> str:
    return "\n".join(body_lines).strip() + "\n"


def fm_text(fm_lines: list[str]) -> str:
    """旧算法的一部分：剔除批准字段后的 frontmatter 文本，仅用于识别历史计划。"""
    kept: list[str] = []
    for line in fm_lines:
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        key = stripped.split(":", 1)[0].strip()
        if key in FINGERPRINT_EXEMPT:
            continue
        kept.append(stripped)
    return "\n".join(sorted(kept))


def legacy_fingerprint(fm_lines: list[str], body_lines: list[str]) -> str:
    """v1 指纹（frontmatter + 全文）。只为让仍在实现的旧计划继续可校验。"""
    payload = fm_text(fm_lines) + "\n--body--\n" + body_text(body_lines)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()[:16]


def read_field(fm_lines: list[str], key: str) -> str:
    prefix = key + ":"
    for line in fm_lines:
        stripped = line.strip()
        if stripped.startswith(prefix):
            value = stripped[len(prefix) :].strip()
            if value.startswith(('"', "'")):
                return value.strip("\"'")
            # 模板里允许写 `status: draft  # draft | approved` 这类行内注释
            return value.split(" #", 1)[0].strip()
    return ""


def write_field(fm_lines: list[str], key: str, value: str) -> list[str]:
    prefix = key + ":"
    for index, line in enumerate(fm_lines):
        if line.strip().startswith(prefix):
            fm_lines[index] = f"{key}: {value}"
            return fm_lines
    fm_lines.append(f"{key}: {value}")
    return fm_lines


def save(path: Path, fm_lines: list[str], body_lines: list[str]) -> None:
    content = "---\n" + "\n".join(fm_lines).rstrip() + "\n---\n" + "\n".join(body_lines).lstrip("\n")
    if not content.endswith("\n"):
        content += "\n"
    path.write_text(content, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="特效计划闸门（契约级指纹）")
    parser.add_argument("--plan", required=True, help="plan.md 路径")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--approve", action="store_true", help="标记为已批准并记录契约指纹")
    group.add_argument("--reopen", action="store_true", help="退回 draft 并清除指纹")
    parser.add_argument(
        "--approved-by",
        help="批准人标识：填用户的原话或账号。--approve 时必填，用于事后审计",
    )
    parser.add_argument(
        "--allow-self-approve",
        action="store_true",
        help="允许不带 --approved-by 的批准（会在记录里标注为自批准）",
    )
    args = parser.parse_args()

    path = Path(args.plan)
    if not path.is_file():
        print(f"[gate] 找不到计划文件：{path}", file=sys.stderr)
        return 3

    try:
        fm_lines, body_lines = split_plan(path.read_text(encoding="utf-8"))
    except ValueError as error:
        print(f"[gate] 计划格式错误：{error}", file=sys.stderr)
        return 3

    contract, has_contract = contract_lines(body_lines)

    if args.reopen:
        write_field(fm_lines, "status", "draft")
        write_field(fm_lines, "fingerprint", '""')
        save(path, fm_lines, body_lines)
        print("[gate] 已退回 draft。契约若有变动，须重新获得用户批准。")
        return 0

    if args.approve:
        if not has_contract:
            print(
                "[gate] 拒绝批准：正文里没有契约块（" + GATE_BEGIN + " … " + GATE_END + "）。\n"
                "[gate]   契约块必须包住：素材清单 / 层结构 / 验收标准 / 授权包络。\n"
                "[gate]   模板见 references/plan-template.md。",
                file=sys.stderr,
            )
            return 3

        current = fingerprint_of(contract or [])

        approver = (args.approved_by or "").strip()
        if not approver:
            if not args.allow_self_approve:
                print(
                    "[gate] 拒绝批准：缺少 --approved-by。\n"
                    "[gate]   请填入用户的原话或标识，例如：\n"
                    '[gate]     --approved-by "批准，开始实现"',
                    file=sys.stderr,
                )
                return 1
            approver = "SELF-APPROVED（未提供用户批准凭据）"

        write_field(fm_lines, "status", "approved")
        write_field(fm_lines, "fingerprint", current)
        write_field(fm_lines, "approved_by", f'"{approver}"')
        write_field(fm_lines, "approved_at", datetime.now().astimezone().isoformat(timespec="seconds"))
        save(path, fm_lines, body_lines)

        print(f"[gate] 已标记为 approved，契约指纹 {current}")
        print(f"[gate] 批准人：{approver}")
        if approver.startswith("SELF-APPROVED"):
            print("[gate] 注意：这条记录表明**没有**用户批准凭据，事后审计会看到。")
        print("[gate] 契约块以外的正文可自由修改；契约块内任何改动都会使批准失效。")
        return 0

    status = read_field(fm_lines, "status").lower()
    recorded = read_field(fm_lines, "fingerprint")
    approver = read_field(fm_lines, "approved_by")

    if status != "approved":
        print(f"[gate] 拒绝：status = {status or '(空)'}，尚未获得用户批准。", file=sys.stderr)
        print("[gate] 批准前允许：读文件、检索/预览素材、在 .scratch/vfx/ 内导出与预演、写计划；不得改动正式特效资源。", file=sys.stderr)
        return 1

    if not has_contract:
        # 旧计划（v1）：用旧算法校验，尽量不让在途实现卡死，但提示迁移。
        current = legacy_fingerprint(fm_lines, body_lines)
        if recorded and recorded == current:
            print("[gate] 放行（旧格式计划，全文指纹）。建议迁移到契约块格式。")
            print(f"[gate] 批准人：{approver}")
            return 0
        print("[gate] 拒绝：旧格式计划无法匹配指纹，且没有契约块。", file=sys.stderr)
        print(f"[gate]   记录指纹 {recorded or '(空)'} / 当前 {current}", file=sys.stderr)
        return 2

    current = fingerprint_of(contract or [])
    if recorded != current:
        print("[gate] 拒绝：契约块在批准后被改动，原批准失效。", file=sys.stderr)
        print(f"[gate]   批准时指纹 {recorded or '(空)'}", file=sys.stderr)
        print(f"[gate]   当前指纹   {current}", file=sys.stderr)
        print("[gate] 若改动在授权包络内，说明包络未覆盖这类变更——把包络写宽一点再批准。", file=sys.stderr)
        return 2

    print(f"[gate] 放行：契约未变，指纹 {current}")
    print(f"[gate] 批准人：{approver}")
    print("[gate] 本脚本只校验契约指纹，不校验实现参数；范围内调参记入 tuning-log.md 即可。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
