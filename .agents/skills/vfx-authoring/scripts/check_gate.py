#!/usr/bin/env python3
"""计划闸门：只有用户批准过、且批准后未被改动的计划才放行。

用法：
    python check_gate.py --plan <plan.md>            # 校验，exit 0 才允许写代码
    python check_gate.py --plan <plan.md> --approve --approved-by "用户原话"
    python check_gate.py --plan <plan.md> --reopen   # 退回 draft（计划有改动时）

指纹覆盖 **正文 + frontmatter（除批准字段外）**：
只改 `branch` / `slug` 这类 frontmatter 字段同样会使批准失效。

退出码：
    0  已批准且指纹一致 → 放行
    1  尚未批准，或 --approve 缺 --approved-by
    2  计划已改动，原批准失效
    3  文件格式错误
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from datetime import datetime
from pathlib import Path


def split_plan(text: str) -> tuple[list[str], list[str]]:
    """拆出 frontmatter 行与正文行。正文用于计算指纹。"""
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    if not lines or lines[0].strip() != "---":
        raise ValueError("plan.md 必须以 '---' 开头的 frontmatter 起始")

    for index in range(1, len(lines)):
        if lines[index].strip() == "---":
            return lines[1:index], lines[index + 1 :]

    raise ValueError("frontmatter 未闭合（缺少第二个 '---'）")


def body_text(body_lines: list[str]) -> str:
    """规范化正文：去掉首尾空行，统一以单个换行结尾。"""
    return "\n".join(body_lines).strip() + "\n"


# 指纹不覆盖的 frontmatter 字段：它们描述的是"批准这件事本身"，
# 覆盖进去会导致写完批准就被自己判失效。
FINGERPRINT_EXEMPT = {"status", "fingerprint", "approved_by", "approved_at"}


def fm_text(fm_lines: list[str]) -> str:
    """参与指纹的 frontmatter 文本：剔除status/fingerprint/approved_* 后按行排序。

    排序是为了让字段顺序变化不触发失效，但**字段值变化一定触发**——
    只改 branch 或 slug 也会让批准作废。
    """
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


def fingerprint(fm_lines: list[str], body_lines: list[str]) -> str:
    """指纹 = 正文 + 参与指纹的 frontmatter。任一变化都使批准失效。"""
    payload = fm_text(fm_lines) + "\n--body--\n" + body_text(body_lines)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()[:16]


def read_field(fm_lines: list[str], key: str) -> str:
    prefix = key + ":"
    for line in fm_lines:
        if line.strip().startswith(prefix):
            return line.strip()[len(prefix) :].strip().strip('"').strip("'")
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
    parser = argparse.ArgumentParser(description="特效计划闸门")
    parser.add_argument("--plan", required=True, help="plan.md 路径")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--approve", action="store_true", help="标记为已批准并记录指纹")
    group.add_argument("--reopen", action="store_true", help="退回 draft 并清除指纹")
    parser.add_argument(
        "--approved-by",
        help="批准人标识：填用户的原话或账号。--approve 时建议提供，用于事后审计",
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

    if args.reopen:
        write_field(fm_lines, "status", "draft")
        write_field(fm_lines, "fingerprint", '""')
        save(path, fm_lines, body_lines)
        print("[gate] 已退回 draft。修改后须重新获得用户批准。")
        return 0

    if args.approve:
        # 先算指纹再写字段：approve 写入的字段在 FINGERPRINT_EXEMPT 内，
        # 但顺序上仍先算，避免以后调整豁免表时踩坑。
        current = fingerprint(fm_lines, body_lines)

        approver = (args.approved_by or "").strip()
        if not approver:
            if not args.allow_self_approve:
                print(
                    "[gate] 拒绝批准：缺少 --approved-by。\n"
                    "[gate]   请填入用户的原话或标识，例如：\n"
                    '[gate]     --approved-by "批准，开始实现"\n'
                    "[gate]   确实要自批准（会留下审计痕迹）时加 --allow-self-approve。",
                    file=sys.stderr,
                )
                return 1
            approver = "SELF-APPROVED（未提供用户批准凭据）"

        write_field(fm_lines, "status", "approved")
        write_field(fm_lines, "fingerprint", current)
        write_field(fm_lines, "approved_by", f'"{approver}"')
        write_field(fm_lines, "approved_at", datetime.now().astimezone().isoformat(timespec="seconds"))
        save(path, fm_lines, body_lines)

        print(f"[gate] 已标记为 approved，指纹 {current}")
        print(f"[gate] 批准人：{approver}")
        if approver.startswith("SELF-APPROVED"):
            print("[gate] 注意：这条记录表明**没有**用户批准凭据，事后审计会看到。")
        print("[gate] 此后计划正文或 frontmatter（除批准字段外）任何改动都会使批准失效。")
        return 0

    current = fingerprint(fm_lines, body_lines)
    status = read_field(fm_lines, "status").lower()
    recorded = read_field(fm_lines, "fingerprint")
    approver = read_field(fm_lines, "approved_by")

    if status != "approved":
        print(f"[gate] 拒绝：status = {status or '(空)'}，尚未获得用户批准。", file=sys.stderr)
        print("[gate] 可读文件、检索/预览素材，并在 .vfx_staging/ 内导出、写计划与预演规格、生成预演图；不得修改正式特效资源。", file=sys.stderr)
        return 1

    if recorded != current:
        print("[gate] 拒绝：计划在批准后被改动，原批准失效。", file=sys.stderr)
        print(f"[gate]   批准时指纹 {recorded or '(空)'}", file=sys.stderr)
        print(f"[gate]   当前指纹   {current}", file=sys.stderr)
        print("[gate] 请把改动告知用户并重新确认。", file=sys.stderr)
        return 2

    print(f"[gate] 放行：已批准，指纹 {current}")
    if approver:
        print(f"[gate] 批准人：{approver}")
    else:
        print("[gate] 警告：没有 approved_by 记录（旧格式计划）。")
    print("[gate] 本脚本校验批准计划指纹，不校验实现参数；范围内调参须另记 tuning-log.md 并逐项对照批准范围。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
