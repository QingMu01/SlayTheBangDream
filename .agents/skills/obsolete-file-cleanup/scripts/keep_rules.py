#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""keep_rules.py —— 「建议保留」规则的**唯一**共享定义（scan_orphans.py 与 cleanup_manifest.py 共用）。

为什么必须共享：
  * `cleanup_manifest.py` 用它决定三档归属（B 档「建议保留」）；
  * `scan_orphans.py` 用它决定**死簇级联的边界** —— 若某引用者命中 keep 规则，
    它就不是「确认孤儿」，其被引用者不得因级联被判死簇
    （缺陷样本：`MusicCrystalHitVfx.cs` 被 `music_crystal_hit_vfx.tscn` 硬引用，
     而该场景是「有意保留的预览件」→ 级联前提不成立）。
  两份规则若漂移，扫描与分档就会各说各话，故抽成同一个模块。

规则出处：`docs/cleanup-authoring.md` 第 5.1 节、`references/keep-list.md` 第 2/5 节。

`cascade_blocks` 语义（v1.2.0 新增，是「该保留意图是否意味着这条引用边是活的」）：
  * True  = **活跃保留**：该文件是在用的预览件/模板/参考实现，出边是活引用，级联到此为止；
  * False = **预留待实装**：该文件本身就是「无引用但先别删」的典型死簇形态，
            keep 只影响分档（落 B），不阻断级联 —— 否则 anon 预留素材（28 张贴图 + 2 个场景）
           会整条从死簇里消失，失去「已无引用、但因未实装角色而保留」这一唯一的信号。
"""

from __future__ import annotations

import fnmatch
import re
from typing import Iterable, NamedTuple


class KeepRule(NamedTuple):
    pattern: str
    reason: str
    recheck: str
    cascade_blocks: bool = True


# 默认保留规则：与 references/keep-list.md 第 5 节逐条一致。
DEFAULT_KEEP_RULES: tuple[KeepRule, ...] = (
    KeepRule(
        "images/charui/anon/**",
        "未实装角色（anon）的预留素材，不属于过时",
        "角色实装并接线后",
        cascade_blocks=False,
    ),
    KeepRule(
        "**/anon0*.tscn",
        "未实装角色（anon）的预留场景，不属于过时",
        "角色实装并接线后",
        cascade_blocks=False,
    ),
    KeepRule(
        "images/charui/img_{soyo,taki,tomori}*.webp",
        "未实装角色的立绘预留",
        "对应角色实装后",
        cascade_blocks=False,
    ),
    KeepRule(
        "scenes/vfx/music_crystal_hit_vfx.tscn",
        "有意保留的预览件：有 .vfx_staging/rec/music_crystal_hit_vfx-* 录帧记录，等接入卡牌",
        "接入完成后",
        cascade_blocks=True,
    ),
    KeepRule(
        "skins/skin_template.json",
        "皮肤模板，供新增角色复制",
        "模板机制变更时",
        cascade_blocks=True,
    ),
)

# `--keep-file` 追加的外部规则：默认按「活跃保留」处理（阻断级联 = 保守方向：
# 不因为一个来历不明的保留规则去指控别的文件已死）。
EXTERNAL_RULE_CASCADE_BLOCKS = True


def expand_braces(pattern: str) -> list[str]:
    """把 `{a,b,c}` 展开成多条 glob（fnmatch 不认花括号）。"""
    m = re.search(r"\{([^{}]*)\}", pattern)
    if not m:
        return [pattern]
    out: list[str] = []
    for alt in m.group(1).split(","):
        out.extend(expand_braces(pattern[: m.start()] + alt + pattern[m.end():]))
    return out


def match_pattern(path: str, pattern: str) -> bool:
    """仓库相对路径 vs keep 规则。

    规则写法不带前导 mod 目录（如 `images/charui/anon/**`），而实际路径形如
    `ItsCrychic/ItsCrychic/images/charui/anon/X.png`，故按「后缀段匹配」处理，
    并保留 `**` 跨段语义。
    """
    for pat in expand_braces(pattern):
        if fnmatch.fnmatch(path, pat) or fnmatch.fnmatch(path, "*/" + pat):
            return True
        pp, sp = pat.split("/"), path.split("/")
        if len(pp) <= len(sp) and all(
            fnmatch.fnmatch(s, p) for s, p in zip(sp[-len(pp):], pp)
        ):
            return True
        if "**" in pp:
            idx = pp.index("**")
            head, tail = pp[:idx], pp[idx + 1:]
            if len(sp) >= len(head) + len(tail):
                head_ok = all(fnmatch.fnmatch(s, p) for s, p in zip(sp[: len(head)], head))
                tail_ok = all(
                    fnmatch.fnmatch(s, p) for s, p in zip(sp[-len(tail):] if tail else [], tail)
                )
                if head_ok and tail_ok:
                    return True
    return False


def load_keep_file(project_root: str, path: str) -> list[KeepRule]:
    """读 `--keep-file`：每行一条 glob，`#` 起注释。不存在的文件抛 FileNotFoundError。"""
    import os

    full = path if os.path.isabs(path) else os.path.join(project_root, path)
    rules: list[KeepRule] = []
    with open(full, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            rules.append(KeepRule(
                line,
                f"外部 keep 规则（{os.path.basename(path)}）",
                "规则移除时",
                EXTERNAL_RULE_CASCADE_BLOCKS,
            ))
    return rules


def find_keep_rule(path: str, rules: Iterable[KeepRule]) -> KeepRule | None:
    for rule in rules:
        if match_pattern(path, rule.pattern):
            return rule
    return None
