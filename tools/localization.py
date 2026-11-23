#!/usr/bin/env python3
"""校验本地化表、生成语言骨架并抽取术语表。

游戏只加载与本体同名的表，未知文件名会被静默忽略；其他语言也不会回退到模组的
zhs 表，因此各语言必须保持完整键集合。校验 JSON 结构、重复键、参考语言键集合、
占位符签名与 BBCode 配对；换行差异和 BOM 仅警告。占位符比较变量名及括号参数，
不比较需要翻译的展示文本。参考语言与译文的空值必须一致，刻意的空串不得补写。

用法：
    python3 tools/localization.py check [--lang eng --lang jpn] [--strict-warnings]
    python3 tools/localization.py stub --lang eng [--dry-run]
    python3 tools/localization.py glossary --lang eng [--out PATH]

本体资源默认取自 ../sts2unpack 的最高版本目录，可用 --base-locales 或
STS2_UNPACKED 覆盖；check 找不到时退回内置表名，glossary 则报错。
退出码：0 = 通过；1 = 有错误（strict 下含警告）；2 = 参数或环境错误。
"""

from __future__ import annotations

import argparse
import codecs
import json
import os
import re
import sys
from collections import Counter, OrderedDict
from dataclasses import dataclass, field
from pathlib import Path

# 本体 localization/<lang>/ 下的 46 张表名（0.111.0 / 0.113.0 一致）。
# 文件名不在此列会被游戏静默忽略，所以这里同时是 check 的白名单和 stub 的产出范围。
BASE_TABLES: tuple[str, ...] = (
    "achievements",
    "acts",
    "afflictions",
    "ancients",
    "ascension",
    "badges",
    "bestiary",
    "card_keywords",
    "card_library",
    "card_reward_ui",
    "card_selection",
    "cards",
    "characters",
    "combat_messages",
    "credits",
    "enchantments",
    "encounters",
    "epochs",
    "eras",
    "events",
    "extensions",
    "ftues",
    "game_modes",
    "game_over_screen",
    "gameplay_ui",
    "inspect_relic_screen",
    "intents",
    "main_menu_ui",
    "map",
    "merchant_room",
    "modifiers",
    "monsters",
    "orbs",
    "potion_lab",
    "potions",
    "powers",
    "relic_collection",
    "relics",
    "rest_site_ui",
    "rich_presence",
    "run_history",
    "settings_ui",
    "static_hover_tips",
    "stats_screen",
    "timeline",
    "vfx",
)

# 本体支持的语言代码（LocManager._supportedLanguages）。
SUPPORTED_LANGS: tuple[str, ...] = (
    "eng",
    "zhs",
    "zht",
    "deu",
    "esp",
    "fra",
    "ind",
    "ita",
    "jpn",
    "kor",
    "pol",
    "ptb",
    "rus",
    "spa",
    "tha",
    "tur",
)

# [tag] / [/tag] / [color=#7799cc]；标签名与属性都不允许出现方括号。
TAG_RE = re.compile(r"\[(/?)([A-Za-z_][A-Za-z0-9_]*)(=[^\[\]]*)?\]")


# --------------------------------------------------------------------------- #
# 数据模型
# --------------------------------------------------------------------------- #


@dataclass
class Issue:
    """一条校验结果。keys 只存问题键名，避免把整份 JSON 打出来。"""

    severity: str  # "error" | "warn"
    code: str
    where: str
    message: str
    keys: list[str] = field(default_factory=list)


@dataclass
class Mod:
    id: str
    loc_dir: Path

    def languages(self) -> list[str]:
        if not self.loc_dir.is_dir():
            return []
        return sorted(p.name for p in self.loc_dir.iterdir() if p.is_dir())


@dataclass
class Table:
    """一张已加载的本地化表。"""

    path: Path
    entries: dict[str, str]
    had_bom: bool
    duplicate_keys: list[str]


# --------------------------------------------------------------------------- #
# 解析
# --------------------------------------------------------------------------- #


def repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def find_mods(root: Path) -> list[Mod]:
    """按 `<mod>/<mod>.json` 清单 + `<mod>/<mod>/localization` 目录识别模组。

    与游戏侧一致：本地化表的资源路径是 `res://{mod.id}/localization/...`，
    mod.id 取自清单文件名，不是项目目录名（当前两者同名，但不依赖这个巧合）。
    """
    mods: list[Mod] = []
    for project in sorted(root.iterdir()):
        if not project.is_dir() or project.name.startswith("."):
            continue
        manifests = sorted(project.glob("*.json"))
        for manifest in manifests:
            mod_id = manifest.stem
            loc_dir = project / mod_id / "localization"
            if loc_dir.is_dir():
                mods.append(Mod(id=mod_id, loc_dir=loc_dir))
                break
    return mods


def load_table(path: Path) -> Table:
    """读取一张表；容忍 BOM，但把重复键抓出来（json 会静默取后者）。"""
    raw = path.read_bytes()
    had_bom = raw.startswith(codecs.BOM_UTF8)
    try:
        text = raw.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise ValueError(f"不是合法 UTF-8：{exc}") from exc

    duplicates: list[str] = []

    def hook(pairs: list[tuple[str, object]]) -> dict[str, object]:
        seen: set[str] = set()
        for key, _ in pairs:
            if key in seen:
                duplicates.append(key)
            seen.add(key)
        return dict(pairs)

    try:
        data = json.loads(text, object_pairs_hook=hook)
    except json.JSONDecodeError as exc:
        raise ValueError(f"JSON 解析失败：{exc.msg}（第 {exc.lineno} 行第 {exc.colno} 列）") from exc

    if not isinstance(data, dict):
        raise ValueError("根节点必须是对象（键 → 字符串）")
    for key, value in data.items():
        if not isinstance(value, str):
            raise ValueError(f"键 {key!r} 的值不是字符串")
    return Table(path=path, entries=data, had_bom=had_bom, duplicate_keys=duplicates)


def placeholders(text: str) -> tuple[list[str], list[str]]:
    """按花括号配对抽出 SmartFormat 占位符。

    占位符内部允许出现 `[gold]...[/gold]`、`|` 与换行（仓库里确实存在
    `{IfUpgraded:show:\\n变化出的牌…|}` 这种写法），所以不能用正则截到第一个 `}`。
    `{{` 是 SmartFormat 的字面量转义，跳过。
    """
    found: list[str] = []
    errors: list[str] = []
    depth = 0
    start = 0
    i = 0
    length = len(text)
    while i < length:
        if text.startswith("{{", i):
            i += 2
            continue
        char = text[i]
        if char == "{":
            if depth == 0:
                start = i
            depth += 1
        elif char == "}":
            depth -= 1
            if depth < 0:
                errors.append(f"第 {i} 个字符处有多余的 '}}'")
                depth = 0
            elif depth == 0:
                found.append(text[start : i + 1])
        i += 1
    if depth > 0:
        errors.append(f"占位符未闭合（从第 {start} 个字符开始）")
    return found, errors


def placeholder_signature(placeholder: str) -> str:
    """占位符的「不可翻译部分」签名：变量名 + 括号参数。

    SmartFormat 里 `{IfUpgraded:show:演奏2次|演奏}` 的 `show:` 之后是**展示文本**，翻译时
    必须改；而 `{Damage:diff()}` 的变量名、`{Energy:energyIcons(1)}` 的括号参数必须原样保留。
    所以签名取「第一个冒号前的变量名 + 全部 `(...)` 参数」，裸文本参数不参与比对。
    """
    inner = placeholder[1:-1]
    head = inner.split(":", 1)[0].strip()
    args = "".join(m.group(0) for m in re.finditer(r"\([^()]*\)", inner))
    return f"{head}{args}"


def tag_profile(text: str) -> tuple[Counter[str], list[str]]:
    """返回 (开闭标签计数, 配对错误)。

    计数用 `+name` / `-name` 两个方向，方便与参考语言做集合比较。
    """
    counts: Counter[str] = Counter()
    errors: list[str] = []
    stack: list[str] = []
    for match in TAG_RE.finditer(text):
        closing, name = match.group(1), match.group(2).lower()
        if closing:
            counts[f"-{name}"] += 1
            if stack and stack[-1] == name:
                stack.pop()
            elif name in stack:
                errors.append(f"[/{name}] 交叉闭合（当前未闭合：{', '.join(stack)}）")
            else:
                errors.append(f"[/{name}] 没有对应的开标签")
        else:
            counts[f"+{name}"] += 1
            stack.append(name)
    if stack:
        errors.append(f"未闭合的开标签：{', '.join(stack)}")
    return counts, errors


# --------------------------------------------------------------------------- #
# 本体解包源
# --------------------------------------------------------------------------- #


def _version_key(name: str) -> tuple[int, ...]:
    parts = re.findall(r"\d+", name)
    return tuple(int(p) for p in parts) if parts else (0,)


def resolve_base_locales(root: Path, override: str | None) -> Path | None:
    """定位本体 `localization/` 目录（其下应有 eng/、zhs/ 等语言目录）。"""
    candidates: list[Path] = []
    if override:
        candidates.append(Path(override).expanduser())
    env = os.environ.get("STS2_UNPACKED")
    if env:
        candidates.append(Path(env).expanduser())
    candidates.append(root.parent / "sts2unpack")

    for candidate in candidates:
        if not candidate.is_dir():
            continue
        direct = candidate / "localization"
        if (direct / "eng").is_dir():
            return direct
        versioned = [p for p in candidate.iterdir() if p.is_dir() and (p / "localization" / "eng").is_dir()]
        if versioned:
            return max(versioned, key=lambda p: _version_key(p.name)) / "localization"
    return None


# --------------------------------------------------------------------------- #
# check
# --------------------------------------------------------------------------- #


def check_reference_table(table: Table, where: str) -> list[Issue]:
    """参考语言也要过一遍结构检查：它坏了，其他语言的比对就失去意义。"""
    issues: list[Issue] = []
    if table.had_bom:
        issues.append(Issue("warn", "bom", where, "文件带 UTF-8 BOM"))
    if table.duplicate_keys:
        issues.append(
            Issue("error", "duplicate-key", where, "同文件重复键（后者覆盖前者）", table.duplicate_keys)
        )
    broken: list[str] = []
    unbalanced: list[str] = []
    for key, value in table.entries.items():
        _, ph_errors = placeholders(value)
        if ph_errors:
            broken.append(f"{key}: {ph_errors[0]}")
        _, tag_errors = tag_profile(value)
        if tag_errors:
            unbalanced.append(f"{key}: {tag_errors[0]}")
    if broken:
        issues.append(Issue("error", "placeholder-syntax", where, "占位符语法错误", broken))
    if unbalanced:
        issues.append(Issue("error", "tag-unbalanced", where, "BBCode 标签不配对", unbalanced))
    return issues


def check_translation_table(
    reference: Table,
    target: Table,
    where: str,
    ref_label: str,
) -> list[Issue]:
    """把一张译文表与参考语言逐键比对。"""
    issues: list[Issue] = []

    if target.had_bom:
        issues.append(Issue("warn", "bom", where, "文件带 UTF-8 BOM"))
    if target.duplicate_keys:
        issues.append(
            Issue("error", "duplicate-key", where, "同文件重复键（后者覆盖前者）", target.duplicate_keys)
        )

    missing = [k for k in reference.entries if k not in target.entries]
    extra = [k for k in target.entries if k not in reference.entries]
    if missing:
        issues.append(
            Issue(
                "error",
                "missing-key",
                where,
                f"缺 {len(missing)} 个键（{ref_label} 有、本语言没有）——运行时会抛 LocException",
                missing,
            )
        )
    if extra:
        issues.append(
            Issue("error", "extra-key", where, f"多 {len(extra)} 个键（{ref_label} 里没有）", extra)
        )

    syntax: list[str] = []
    unbalanced: list[str] = []
    ph_diff: list[str] = []
    tag_diff: list[str] = []
    newline_diff: list[str] = []
    empty_untranslated: list[str] = []
    empty_invented: list[str] = []

    for key, ref_value in reference.entries.items():
        if key not in target.entries:
            continue
        value = target.entries[key]

        # 空值先判，判完就跳过结构比对：骨架表整片为空时，否则每个键都会再报一遍
        # 「占位符缺」——同一个根因重复计费，清单会失去可读性。
        ref_blank = not ref_value.strip()
        val_blank = not value.strip()
        if not ref_blank and val_blank:
            empty_untranslated.append(key)
            continue
        if ref_blank and not val_blank:
            empty_invented.append(key)
            continue
        if ref_blank and val_blank:
            continue

        ref_ph, _ = placeholders(ref_value)
        val_ph, val_ph_errors = placeholders(value)
        if val_ph_errors:
            syntax.append(f"{key}: {val_ph_errors[0]}")
        else:
            ref_sigs = sorted(placeholder_signature(p) for p in ref_ph)
            val_sigs = sorted(placeholder_signature(p) for p in val_ph)
            if ref_sigs != val_sigs:
                ph_diff.append(f"{key}: {_describe_diff(ref_sigs, val_sigs)}")

        ref_tags, _ = tag_profile(ref_value)
        val_tags, val_tag_errors = tag_profile(value)
        if val_tag_errors:
            unbalanced.append(f"{key}: {val_tag_errors[0]}")
        elif ref_tags != val_tags:
            tag_diff.append(f"{key}: {_describe_diff(sorted(ref_tags), sorted(val_tags), tag=True)}")

        if ref_value.count("\n") != value.count("\n"):
            newline_diff.append(f"{key}: 参考 {ref_value.count(chr(10))} 行 → 译文 {value.count(chr(10))} 行")

    if syntax:
        issues.append(Issue("error", "placeholder-syntax", where, "占位符语法错误", syntax))
    if unbalanced:
        issues.append(Issue("error", "tag-unbalanced", where, "BBCode 标签不配对", unbalanced))
    if ph_diff:
        issues.append(Issue("error", "placeholder-mismatch", where, "占位符与参考语言不一致", ph_diff))
    if tag_diff:
        issues.append(Issue("error", "tag-mismatch", where, "BBCode 标签与参考语言不一致", tag_diff))
    if empty_untranslated:
        issues.append(
            Issue("error", "empty-untranslated", where, f"未翻译（{ref_label} 有文本，这里为空）", empty_untranslated)
        )
    if empty_invented:
        issues.append(
            Issue(
                "error",
                "empty-invented",
                where,
                f"{ref_label} 刻意留空，译文却填了内容（哨兵键 / 音乐牌 .description / 待写 .flavor）",
                empty_invented,
            )
        )
    if newline_diff:
        issues.append(Issue("warn", "newline-mismatch", where, "换行数与参考语言不一致", newline_diff))
    return issues


def _describe_diff(expected: list[str], actual: list[str], tag: bool = False) -> str:
    only_ref = [x for x in expected if x not in actual]
    only_val = [x for x in actual if x not in expected]
    parts: list[str] = []
    if only_ref:
        parts.append("缺 " + "、".join(_render(item, tag) for item in only_ref[:4]))
    if only_val:
        parts.append("多 " + "、".join(_render(item, tag) for item in only_val[:4]))
    return "；".join(parts) if parts else "顺序不同"


def _render(item: str, tag: bool) -> str:
    """标签计数用 `+name` / `-name` 编码方向，打印时翻成人话。"""
    if tag and item[:1] in "+-":
        return f"{'开' if item[0] == '+' else '闭'} {item[1:]}"
    return _shorten(item)


def _shorten(text: str, limit: int = 40) -> str:
    flat = text.replace("\n", "\\n")
    return flat if len(flat) <= limit else flat[: limit - 1] + "…"


def cmd_check(args: argparse.Namespace) -> int:
    root = repo_root() if args.repo is None else Path(args.repo).expanduser().resolve()
    mods = find_mods(root)
    if not mods:
        print(f"错误：在 {root} 下没找到任何带 localization/ 的模组", file=sys.stderr)
        return 2

    base = resolve_base_locales(root, args.base_locales)
    whitelist = set(BASE_TABLES)
    if base is not None:
        discovered = {p.stem for p in (base / "eng").glob("*.json")}
        if discovered:
            whitelist = discovered
    if base is None:
        print(
            "提示：未找到本体解包源，表名白名单使用内置 46 张（可用 --base-locales 指定）",
            file=sys.stderr,
        )

    issues: list[Issue] = []
    scanned: list[str] = []

    for mod in mods:
        languages = mod.languages()
        if not languages:
            continue
        if args.reference not in languages:
            print(f"错误：{mod.id} 没有参考语言目录 {args.reference}", file=sys.stderr)
            return 2
        targets = [lang for lang in languages if lang != args.reference]
        if args.lang:
            unknown = [lang for lang in args.lang if lang not in languages]
            if unknown:
                print(f"错误：{mod.id} 没有语言目录 {', '.join(unknown)}", file=sys.stderr)
                return 2
            targets = [lang for lang in targets if lang in args.lang]

        ref_dir = mod.loc_dir / args.reference
        ref_tables: dict[str, Table] = {}
        for path in sorted(ref_dir.glob("*.json")):
            where = f"{mod.id}/{args.reference}/{path.name}"
            if path.stem not in whitelist:
                issues.append(
                    Issue(
                        "error",
                        "table-name",
                        where,
                        f"文件名不在本体表名内（{len(whitelist)} 张）——整文件会被游戏静默忽略",
                    )
                )
                continue
            try:
                table = load_table(path)
            except ValueError as exc:
                issues.append(Issue("error", "json", where, str(exc)))
                continue
            ref_tables[path.stem] = table
            issues.extend(check_reference_table(table, where))
            scanned.append(f"{mod.id}/{args.reference}/{path.stem}（{len(table.entries)} 键）")

        for lang in targets:
            target_dir = mod.loc_dir / lang
            # 整张表缺失比缺键更隐蔽：该表一条都不会合并，运行时同样是 LocException。
            for table_name in sorted(ref_tables):
                if not (target_dir / f"{table_name}.json").is_file():
                    issues.append(
                        Issue(
                            "error",
                            "missing-table",
                            f"{mod.id}/{lang}/{table_name}.json",
                            f"整张表缺失（{args.reference} 有 {len(ref_tables[table_name].entries)} 键）",
                        )
                    )
            for path in sorted(target_dir.glob("*.json")):
                where = f"{mod.id}/{lang}/{path.name}"
                if path.stem not in whitelist:
                    issues.append(
                        Issue(
                            "error",
                            "table-name",
                            where,
                            f"文件名不在本体表名内（{len(whitelist)} 张）——整文件会被游戏静默忽略",
                        )
                    )
                    continue
                try:
                    target = load_table(path)
                except ValueError as exc:
                    issues.append(Issue("error", "json", where, str(exc)))
                    continue
                reference = ref_tables.get(path.stem)
                if reference is None:
                    issues.append(
                        Issue(
                            "error",
                            "missing-table",
                            where,
                            f"{args.reference} 里没有同名表，无法比对",
                        )
                    )
                    continue
                issues.extend(check_translation_table(reference, target, where, args.reference))
                scanned.append(f"{mod.id}/{lang}/{path.stem}（{len(target.entries)} 键）")

    errors = [i for i in issues if i.severity == "error"]
    warns = [i for i in issues if i.severity == "warn"]

    if args.json:
        payload = {
            "ok": not errors and not (args.strict_warnings and warns),
            "scanned": scanned,
            "issues": [
                {
                    "severity": i.severity,
                    "code": i.code,
                    "where": i.where,
                    "message": i.message,
                    "keys": i.keys,
                }
                for i in issues
            ],
        }
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        for issue in issues:
            print(f"[{'错误' if issue.severity == 'error' else '警告'}] {issue.where}  {issue.message}")
            for key in issue.keys[: args.max_keys]:
                print(f"         - {key}")
            if len(issue.keys) > args.max_keys:
                print(f"         …… 另有 {len(issue.keys) - args.max_keys} 条（--max-keys 调整）")
        langs_note = "、".join(args.lang) if args.lang else "全部语言"
        print()
        print(f"扫描 {len(scanned)} 张表（参考语言 {args.reference}，{langs_note}）")
        print(f"汇总：{len(errors)} 错误 / {len(warns)} 警告")

    if errors:
        return 1
    if warns and args.strict_warnings:
        return 1
    return 0


# --------------------------------------------------------------------------- #
# stub
# --------------------------------------------------------------------------- #


def cmd_stub(args: argparse.Namespace) -> int:
    root = repo_root() if args.repo is None else Path(args.repo).expanduser().resolve()
    mods = find_mods(root)
    if not mods:
        print(f"错误：在 {root} 下没找到任何带 localization/ 的模组", file=sys.stderr)
        return 2

    created = 0
    skipped = 0
    for mod in mods:
        ref_dir = mod.loc_dir / args.reference
        if not ref_dir.is_dir():
            print(f"错误：{mod.id} 没有参考语言目录 {args.reference}", file=sys.stderr)
            return 2
        target_dir = mod.loc_dir / args.lang
        for path in sorted(ref_dir.glob("*.json")):
            out = target_dir / path.name
            if out.exists() and not args.force:
                skipped += 1
                print(f"跳过 {out.relative_to(root)}（已存在，--force 覆盖）")
                continue
            try:
                table = load_table(path)
            except ValueError as exc:
                print(f"错误：{path.relative_to(root)}：{exc}", file=sys.stderr)
                return 2
            payload = OrderedDict(
                (key, value if args.copy else "") for key, value in table.entries.items()
            )
            text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
            if args.dry_run:
                print(f"[预演] 将写 {out.relative_to(root)}（{len(payload)} 键）")
            else:
                target_dir.mkdir(parents=True, exist_ok=True)
                out.write_text(text, encoding="utf-8")
                print(f"已写 {out.relative_to(root)}（{len(payload)} 键）")
            created += 1

    print()
    print(f"汇总：{created} 张表{'待生成' if args.dry_run else '已生成'} / {skipped} 张跳过")
    if args.dry_run:
        return 0
    if created:
        print(
            "下一步：让 agent 逐表填值，然后 `python3 tools/localization.py check "
            f"--lang {args.lang}` 收口（占位符、标签、换行必须与参考语言一致）。"
        )
    return 0


# --------------------------------------------------------------------------- #
# glossary
# --------------------------------------------------------------------------- #


def tagged_segments(text: str, tag: str) -> list[str]:
    """抽 `[tag]...[/tag]` 之间的内容；用于从平行表里取关键词对照。"""
    pattern = re.compile(rf"\[{tag}\](.*?)\[/{tag}\]", re.S)
    return [m.group(1).strip() for m in pattern.finditer(text)]


def cmd_glossary(args: argparse.Namespace) -> int:
    root = repo_root() if args.repo is None else Path(args.repo).expanduser().resolve()
    base = resolve_base_locales(root, args.base_locales)
    if base is None:
        print(
            "错误：找不到本体解包源（../sts2unpack/<版本>/localization），"
            "无法抽术语表；用 --base-locales 或 STS2_UNPACKED 指定",
            file=sys.stderr,
        )
        return 2

    ref_dir = base / args.reference
    target_dir = base / args.lang
    if not ref_dir.is_dir() or not target_dir.is_dir():
        print(f"错误：本体缺 {args.reference} 或 {args.lang} 语言目录", file=sys.stderr)
        return 2

    pairs: Counter[tuple[str, str]] = Counter()
    tables_used: list[str] = []
    for path in sorted(ref_dir.glob("*.json")):
        target_path = target_dir / path.name
        if not target_path.is_file():
            continue
        try:
            ref_table = load_table(path)
            target_table = load_table(target_path)
        except ValueError:
            continue
        used = False
        for key in ref_table.entries.keys() & target_table.entries.keys():
            ref_segments = tagged_segments(ref_table.entries[key], args.tag)
            target_segments = tagged_segments(target_table.entries[key], args.tag)
            if not ref_segments or len(ref_segments) != len(target_segments):
                continue
            if args.align == "single" and len(ref_segments) != 1:
                continue
            for ref_text, target_text in zip(ref_segments, target_segments):
                if not ref_text or not target_text or ref_text == target_text:
                    continue
                pairs[(ref_text, target_text)] += 1
                used = True
        if used:
            tables_used.append(path.name)

    if not pairs:
        print(f"错误：本体 {args.reference}/{args.lang} 的 [{args.tag}] 段落没有可对齐的对照", file=sys.stderr)
        return 2

    by_ref: dict[str, Counter[str]] = {}
    for (ref_text, target_text), count in pairs.items():
        by_ref.setdefault(ref_text, Counter())[target_text] += count
    # 一词多译只保留「次选占比够高」的译法：index 对齐会把同一句里换位的两个 [gold]
    # 段配错（如 虚无→Hand），那类噪声是 1 次对 19 次，不该进人工定夺清单。
    conflicts: dict[str, dict[str, int]] = {}
    for ref_text, counts in by_ref.items():
        if len(counts) < 2:
            continue
        top = counts.most_common(1)[0][1]
        floor = max(2, top * args.conflict_ratio)
        kept = {text: count for text, count in counts.items() if count >= floor}
        if len(kept) > 1:
            conflicts[ref_text] = dict(sorted(kept.items(), key=lambda kv: -kv[1]))
    terms = {
        ref_text: counts.most_common(1)[0][0]
        for ref_text, counts in sorted(by_ref.items(), key=lambda kv: (-sum(kv[1].values()), kv[0]))
        if sum(counts.values()) >= args.min_count
    }

    if args.json or args.out:
        payload = {
            "reference": args.reference,
            "target": args.lang,
            "tag": args.tag,
            "base_locales": str(base),
            "source_tables": tables_used,
            "terms": terms,
            "counts": {ref: dict(counts) for ref, counts in by_ref.items()},
            "conflicts": conflicts,
        }
        text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
        if args.out:
            out = Path(args.out).expanduser()
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_text(text, encoding="utf-8")
            print(f"已写 {out}（{len(terms)} 条术语，{len(conflicts)} 条歧义）")
        else:
            print(text, end="")
        return 0

    print(f"# {args.reference} → {args.lang} 术语表（[{args.tag}] 段落对齐）")
    print(f"# 来源：{base}  表：{', '.join(tables_used)}")
    print()
    print("| 源词 | 译法 | 次数 |")
    print("|---|---|---|")
    for ref_text, target_text in terms.items():
        print(f"| {ref_text} | {target_text} | {sum(by_ref[ref_text].values())} |")
    if conflicts:
        print()
        print("## 一词多译（需人工定夺，工具取最高频）")
        print()
        for ref_text, counts in sorted(conflicts.items()):
            detail = "、".join(f"{text}({count})" for text, count in sorted(counts.items(), key=lambda kv: -kv[1]))
            print(f"- {ref_text} → {detail}")
    print()
    print(f"共 {len(terms)} 条术语；歧义 {len(conflicts)} 条。")
    return 0


# --------------------------------------------------------------------------- #
# CLI
# --------------------------------------------------------------------------- #


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="localization.py",
        description="模组本地化表的校验、骨架生成与术语表抽取",
    )
    parser.add_argument("--repo", help="仓库根（默认取本脚本的上级目录）")
    parser.add_argument(
        "--base-locales",
        help="本体 localization 目录或解包源根目录（默认 ../sts2unpack/<最高版本>）",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    check = sub.add_parser("check", help="校验表名、键集合、占位符、标签、空值")
    check.add_argument("--reference", default="zhs", help="参考语言（默认 zhs）")
    check.add_argument(
        "--lang",
        action="append",
        help="只校验这些语言（可重复）；缺省校验参考语言之外的全部语言",
    )
    check.add_argument("--strict-warnings", action="store_true", help="警告也算失败")
    check.add_argument("--max-keys", type=int, default=10, help="每类问题最多列出的键数（默认 10）")
    check.add_argument("--json", action="store_true", help="输出 JSON")
    check.set_defaults(func=cmd_check)

    stub = sub.add_parser("stub", help="为新语言生成键齐全的空表骨架")
    stub.add_argument("--lang", required=True, help="目标语言代码（eng / jpn / zht …）")
    stub.add_argument("--reference", default="zhs", help="参考语言（默认 zhs）")
    stub.add_argument("--copy", action="store_true", help="值先复制参考语言文本（就地改写用）")
    stub.add_argument("--force", action="store_true", help="覆盖已存在的目标表")
    stub.add_argument("--dry-run", action="store_true", help="只打印将写哪些文件")
    stub.set_defaults(func=cmd_stub)

    glossary = sub.add_parser("glossary", help="从本体平行表抽术语对照")
    glossary.add_argument("--lang", required=True, help="目标语言代码")
    glossary.add_argument("--reference", default="zhs", help="参考语言（默认 zhs）")
    glossary.add_argument("--tag", default="gold", help="用于对齐的 BBCode 标签名（默认 gold）")
    glossary.add_argument(
        "--align",
        choices=("index", "single"),
        default="index",
        help="index = 同句多段按位置配对（召回高）；single = 只用单段句（精度高）",
    )
    glossary.add_argument(
        "--conflict-ratio",
        type=float,
        default=0.25,
        help="次选占比达到多少才算一词多译（默认 0.25，低于此视为对齐噪声）",
    )
    glossary.add_argument("--min-count", type=int, default=1, help="只保留出现次数 ≥ N 的词条")
    glossary.add_argument("--out", help="写入文件（JSON）而不是打印 markdown")
    glossary.add_argument("--json", action="store_true", help="打印 JSON 而不是 markdown")
    glossary.set_defaults(func=cmd_glossary)

    return parser


def main(argv: list[str]) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if getattr(args, "lang", None) and args.command in {"stub", "glossary"}:
        if args.lang not in SUPPORTED_LANGS:
            print(
                f"错误：{args.lang} 不在本体支持的语言代码内：{', '.join(SUPPORTED_LANGS)}",
                file=sys.stderr,
            )
            return 2
    try:
        return int(args.func(args))
    except BrokenPipeError:
        return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
