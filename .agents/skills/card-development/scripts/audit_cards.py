#!/usr/bin/env python3
"""Audit C# card implementations against project conventions and an optional spec table.

Always checked
--------------
- every card class has a sibling '.cs.uid'
- every card class resolves to exactly one localization '.title' key
- required localization subkeys exist ('.description' / '.subside' / '.instant')
- metadata parses from the base-class constructor arguments: rarity, type, cost and target are
  read either from the conventional 'Custom*' fields or from the literal written at the call site
- no duplicate class names, no duplicate localization titles
- every localization JSON parses and has no duplicate keys
- card portrait png exists (WARN by default, problem with --strict-portrait)

Warnings
--------
- rarity, type, cost or target written inline at the base call instead of a 'Custom*' field
  ('CustomRarity' / 'CustomType' / 'CustomCost' / 'CustomTarget')
- a 'Custom*' field that is declared but never passed to the base constructor
- a music card whose target is not one of 'PerformTargetTypes'

With --spec
-----------
Bidirectional comparison between a Markdown spec table and the standard-rarity
card set, plus rarity / type / cost equality.

This script proves set membership and static metadata only. It does **not**
prove effect semantics; use references/effect-audit-checklist.md for that.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

RARITY_MAP = {"普通": "Common", "罕见": "Uncommon", "稀有": "Rare"}
TYPE_MAP = {"攻击": "Attack", "技能": "Skill", "能力": "Power"}
STANDARD_RARITIES = set(RARITY_MAP.values())

CLASS_RE = re.compile(
    r"public\s+(?:sealed\s+)?class\s+(\w+)\s*(?:\([^)]*\))?\s*:\s*([A-Za-z_][\w.]*)"
)
NAMESPACE_RE = re.compile(r"^\s*namespace\s+([\w.]+)\s*;", re.MULTILINE)
CARD_BASE_RE = re.compile(r"Abstract\w*Card|BandCardModel|MusicCardModel")
RARITY_LITERAL_RE = re.compile(r"CardRarity\.(\w+)")
TYPE_LITERAL_RE = re.compile(r"CardType\.(\w+)")
TARGET_LITERAL_RE = re.compile(r"(?:TargetType|PerformTargetTypes)\.(\w+)")
RARITY_CONST_RE = re.compile(r"private\s+const\s+CardRarity\s+CustomRarity")
XCOST_RE = re.compile(r"HasEnergyCostX\s*=>\s*true")

METADATA_FIELDS = ("CustomCost", "CustomType", "CustomRarity", "CustomTarget")


@dataclass
class Card:
    class_name: str
    rarity: str | None
    rarity_source: str
    card_type: str
    type_source: str
    cost: str
    cost_source: str
    target: str | None
    target_literal: str | None
    target_source: str
    warnings: list[str]
    path: Path
    is_music: bool
    is_subside: bool
    is_instant: bool
    scope: str
    title: str | None
    key: str | None


@dataclass
class SpecCard:
    rarity: str
    card_type: str
    title: str
    cost: str
    line: int


def find_project_root(start: Path) -> Path:
    for candidate in [start, *start.parents]:
        if (candidate / "AGENTS.md").is_file():
            return candidate
    raise SystemExit("ERROR: cannot locate project root (no AGENTS.md found above cwd)")


def pascal_to_upper_snake(value: str) -> str:
    value = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", value)
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)
    return value.upper()


def reject_duplicate_pairs(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_localization(paths: list[Path]) -> dict[str, object]:
    data: dict[str, object] = {}
    for path in paths:
        parsed = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=reject_duplicate_pairs)
        if not isinstance(parsed, dict):
            raise ValueError(f"{path}: top level is not an object")
        for key, value in parsed.items():
            if key in data:
                raise ValueError(f"{path}: key also defined in another file: {key}")
            data[key] = value
    return data


def discover_localization_files(project_root: Path) -> list[Path]:
    return sorted(project_root.glob("*/*/localization/zhs/cards.json"))


def discover_card_roots(project_root: Path) -> list[Path]:
    return sorted(p for p in project_root.glob("*/Scripts/Cards") if p.is_dir())


def extract_base_arguments(text: str, class_match: re.Match[str]) -> str:
    """Return the text inside the base type's constructor call, or an empty string.

    The call may span several lines, so the closing bracket is found by balancing brackets.
    """
    rest = text[class_match.end(2):]
    offset = len(rest) - len(rest.lstrip())
    if rest[offset:offset + 1] != "(":
        return ""
    open_index = class_match.end(2) + offset
    depth = 0
    for index in range(open_index, len(text)):
        character = text[index]
        if character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0:
                return text[open_index + 1:index]
    return ""


def split_arguments(arguments: str) -> list[str]:
    parts: list[str] = []
    current: list[str] = []
    depth = 0
    for character in arguments:
        if character in "([{":
            depth += 1
        elif character in ")]}":
            depth -= 1
        if character == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(character)
    if "".join(current).strip():
        parts.append("".join(current).strip())
    return parts


def resolve_field(text: str, field_name: str) -> str | None:
    """Return the right-hand side of a 'Custom*' field declaration."""
    match = re.search(rf"(?<![\w.]){re.escape(field_name)}\s*=(?!=)\s*([^;]+);", text)
    return match.group(1).strip() if match else None


def field_is_passed(arguments: str, field_name: str) -> bool:
    return re.search(rf"(?<![\w.]){re.escape(field_name)}(?![\w])", arguments) is not None


def resolve_metadata(
    arguments: str, field_name: str, literal_re: re.Pattern[str], text: str
) -> tuple[str | None, str, str | None]:
    """Resolve one metadata value.

    Returns (value, source, literal). Source is one of 'field', 'inline', 'missing' or
    'unresolved-field'; literal is the matched literal such as 'PerformTargetTypes.AnySlot'.
    """
    if field_is_passed(arguments, field_name):
        right_hand_side = resolve_field(text, field_name)
        if right_hand_side is None:
            return None, "unresolved-field", None
        match = literal_re.search(right_hand_side)
        if match is None:
            return None, "field", None
        return match.group(1), "field", match.group(0)
    match = literal_re.search(arguments)
    if match is None:
        return None, "missing", None
    return match.group(1), "inline", match.group(0)


def resolve_cost(arguments: list[str], text: str, is_music: bool) -> tuple[str, str]:
    """Resolve the base cost. Music cards may use the two-argument overload, which keeps cost 0."""
    if arguments and field_is_passed(arguments[0], "CustomCost"):
        right_hand_side = resolve_field(text, "CustomCost")
        if right_hand_side is not None:
            match = re.search(r"-?\d+", right_hand_side)
            if match:
                return match.group(0), "field"
        return "?", "unresolved-field"
    if arguments and re.fullmatch(r"-?\d+", arguments[0]):
        return arguments[0], "inline"
    if is_music:
        return "0", "implicit"
    return "?", "missing"


def collect_convention_warnings(
    class_name: str,
    arguments: str,
    text: str,
    rarity: str | None,
    rarity_source: str,
    card_type: str | None,
    type_source: str,
    cost: str,
    cost_source: str,
    target_literal: str | None,
    target_source: str,
    is_music_folder: bool,
) -> list[str]:
    """Report metadata that is correct but written against the documented convention."""
    warnings: list[str] = []
    if rarity_source == "inline":
        warnings.append(
            f"{class_name}: rarity written inline at the base call; declare "
            f"'private const CardRarity CustomRarity = CardRarity.{rarity};' and pass CustomRarity"
        )
    if type_source == "inline":
        warnings.append(
            f"{class_name}: type written inline at the base call; declare "
            f"'private const CardType CustomType = CardType.{card_type};' and pass CustomType"
        )
    if cost_source == "inline":
        warnings.append(
            f"{class_name}: cost written inline at the base call; declare "
            f"'private const int CustomCost = {cost};' and pass CustomCost"
        )
    if target_source == "inline":
        warnings.append(
            f"{class_name}: target written inline at the base call; declare "
            f"'private static readonly TargetType CustomTarget = {target_literal};' and pass CustomTarget"
        )
    if is_music_folder and target_literal and not target_literal.startswith("PerformTargetTypes."):
        warnings.append(
            f"{class_name}: music card target is '{target_literal}'; music cards normally use one of "
            f"PerformTargetTypes so the player can choose the slot"
        )
    for field_name in METADATA_FIELDS:
        if resolve_field(text, field_name) is not None and not field_is_passed(arguments, field_name):
            warnings.append(
                f"{class_name}: '{field_name}' is declared but never passed to the base constructor"
            )
    return warnings


def parse_card(path: Path) -> Card | None:
    text = path.read_text(encoding="utf-8-sig")
    match = CLASS_RE.search(text)
    if not match:
        return None
    class_name, base = match.group(1), match.group(2).split(".")[-1]
    if not CARD_BASE_RE.fullmatch(base):
        return None

    is_music = "Music" in base
    # Tokens derive from MusicCardModel too; the manual-placement convention only covers the
    # character Music type directory (Scripts/Cards/<character>/Music).
    is_music_folder = "Music" in path.parts
    arguments = extract_base_arguments(text, match)
    arguments_list = split_arguments(arguments)

    rarity, rarity_source, _ = resolve_metadata(arguments, "CustomRarity", RARITY_LITERAL_RE, text)
    card_type, type_source, _ = resolve_metadata(arguments, "CustomType", TYPE_LITERAL_RE, text)
    target, target_source, target_literal = resolve_metadata(
        arguments, "CustomTarget", TARGET_LITERAL_RE, text
    )
    cost, cost_source = resolve_cost(arguments_list, text, is_music)

    # X-cost cards declare HasEnergyCostX and still pass a numeric CustomCost; report X.
    if XCOST_RE.search(text):
        cost, cost_source = "X", "x-cost"

    if type_source == "missing" and is_music:
        card_type, type_source = "Quest", "implicit"

    warnings = collect_convention_warnings(
        class_name,
        arguments,
        text,
        rarity,
        rarity_source,
        card_type,
        type_source,
        cost,
        cost_source,
        target_literal,
        target_source,
        is_music_folder,
    )

    return Card(
        class_name=class_name,
        rarity=rarity,
        rarity_source=rarity_source,
        card_type=card_type if card_type is not None else "?",
        type_source=type_source,
        cost=cost,
        cost_source=cost_source,
        target=target,
        target_literal=target_literal,
        target_source=target_source,
        warnings=warnings,
        path=path,
        is_music=is_music,
        is_subside="ISubsideCard" in text,
        is_instant=bool(re.search(r"IsInstant\s*=>\s*true\s*;", text)),
        scope="",
        title=None,
        key=None,
    )


def find_localization_key(class_name: str, keys: list[str]) -> tuple[str | None, str | None]:
    suffix = "_" + pascal_to_upper_snake(class_name)
    matches = [key for key in keys if key.endswith(suffix)]
    if len(matches) == 1:
        return matches[0], None
    if not matches:
        return None, "no localization key"
    return None, f"ambiguous localization keys: {sorted(matches)}"


def parse_spec(path: Path) -> list[SpecCard]:
    cards: list[SpecCard] = []
    for line_no, raw in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = raw.strip()
        if not line.startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if len(cells) < 4 or cells[0] not in RARITY_MAP or cells[1] not in TYPE_MAP:
            continue
        title = re.sub(r"\s+", " ", re.sub(r"~~.*?~~", "", cells[2])).strip()
        if not title:
            raise ValueError(f"{path}:{line_no}: empty card title")
        cards.append(
            SpecCard(RARITY_MAP[cells[0]], TYPE_MAP[cells[1]], title, cells[3].upper(), line_no)
        )
    return cards


def duplicates(values: list[str]) -> list[str]:
    counts: dict[str, int] = {}
    for value in values:
        counts[value] = counts.get(value, 0) + 1
    return sorted(value for value, count in counts.items() if count > 1)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--project-root", type=Path, default=None)
    parser.add_argument("--card-root", type=Path, action="append", default=[])
    parser.add_argument("--localization", type=Path, action="append", default=[])
    parser.add_argument("--spec", type=Path, default=None, help="Markdown spec table (branch C)")
    parser.add_argument("--class", dest="class_filter", action="append", default=[],
                        help="restrict per-card checks to these class names (branch A/B)")
    parser.add_argument("--strict-portrait", action="store_true",
                        help="treat a missing card portrait as a problem instead of a warning")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = (args.project_root or find_project_root(Path.cwd())).resolve()

    card_roots = [p.resolve() for p in args.card_root] or discover_card_roots(project_root)
    if not card_roots:
        print(f"ERROR: no card root found under {project_root}", file=sys.stderr)
        return 2

    loc_files = [p.resolve() for p in args.localization] or discover_localization_files(project_root)
    if not loc_files:
        print(f"ERROR: no localization cards.json found under {project_root}", file=sys.stderr)
        return 2

    try:
        data = load_localization(loc_files)
    except (OSError, UnicodeError, ValueError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    keys = sorted(data)
    base_keys = sorted({key.rsplit(".", 1)[0] if "." in key else key for key in keys})
    titles_by_key = {key[: -len(".title")]: data[key] for key in keys if key.endswith(".title")}

    per_class_problems: dict[str, list[str]] = defaultdict(list)
    per_class_warnings: dict[str, list[str]] = defaultdict(list)
    global_problems: list[str] = []
    global_warnings: list[str] = []
    cards: list[Card] = []

    for root in card_roots:
        for path in sorted(root.rglob("*.cs")):
            text = path.read_text(encoding="utf-8-sig")
            card = parse_card(path)
            if card is None:
                if RARITY_CONST_RE.search(text):
                    global_warnings.append(f"unparsed card class (unknown base): {path}")
                continue
            cards.append(card)
            for message in card.warnings:
                per_class_warnings[card.class_name].append(message)
            relative = path.relative_to(root).parts
            card.scope = f"{root.name}/{relative[0]}" if len(relative) > 1 else root.name

            if not path.with_suffix(".cs.uid").is_file():
                per_class_problems[card.class_name].append(f"missing .cs.uid: {path}")

            namespace_match = NAMESPACE_RE.search(text)
            if namespace_match:
                mod = namespace_match.group(1).split(".")[0]
                portrait = project_root / mod / mod / "images" / "card_portraits" / f"{card.class_name}.png"
                if not portrait.is_file():
                    message = f"missing portrait: {card.class_name} ({portrait})"
                    bucket = per_class_problems if args.strict_portrait else per_class_warnings
                    bucket[card.class_name].append(message)

            key, error = find_localization_key(card.class_name, base_keys)
            if error:
                per_class_problems[card.class_name].append(f"{card.class_name}: {error} ({card.path})")
                continue
            card.key = key
            assert key is not None
            card.title = titles_by_key.get(key)
            if key not in titles_by_key:
                per_class_problems[card.class_name].append(f"{card.class_name}: missing {key}.title")
                continue
            if f"{key}.description" not in data:
                per_class_problems[card.class_name].append(f"{card.class_name}: missing {key}.description")
            if card.is_instant and f"{key}.instant" not in data:
                per_class_problems[card.class_name].append(f"{card.class_name}: instant card missing {key}.instant")
            if card.is_subside and f"{key}.subside" not in data:
                per_class_problems[card.class_name].append(f"{card.class_name}: subside card missing {key}.subside")
            if card.rarity is None:
                per_class_problems[card.class_name].append(f"{card.class_name}: cannot parse CustomRarity ({card.path})")
            if card.card_type == "?":
                per_class_problems[card.class_name].append(f"{card.class_name}: cannot parse CustomType ({card.path})")
            if card.cost == "?":
                per_class_problems[card.class_name].append(f"{card.class_name}: cannot parse CustomCost ({card.path})")

    for name in duplicates([card.class_name for card in cards]):
        global_problems.append(f"duplicate card class: {name}")
    titles_by_scope: dict[str, list[str]] = defaultdict(list)
    for card in cards:
        if card.title:
            titles_by_scope[card.scope].append(card.title)
    for scope, titles in sorted(titles_by_scope.items()):
        for title in duplicates(titles):
            global_problems.append(f"duplicate localization title in {scope}: {title}")

    # Music cards are Quest-type support cards; they never appear as standard
    # reward cards in a spec table.
    standard = [
        card
        for card in cards
        if card.rarity in STANDARD_RARITIES and card.card_type in {"Attack", "Skill", "Power"}
    ]

    selected = cards
    if args.class_filter:
        wanted = set(args.class_filter)
        present = {card.class_name for card in cards}
        selected = [card for card in cards if card.class_name in wanted]
        global_problems.extend(
            f"class filter matched no card: {name}" for name in sorted(wanted - present)
        )

    if args.spec:
        if args.class_filter:
            global_problems.append("--spec cannot be combined with --class")
        else:
            try:
                specs = parse_spec(args.spec.resolve())
            except (OSError, UnicodeError, ValueError) as exc:
                print(f"ERROR: {exc}", file=sys.stderr)
                return 2

            spec_by_title = {card.title: card for card in specs}
            code_by_title = {card.title: card for card in standard if card.title}
            for title in duplicates([card.title for card in specs]):
                global_problems.append(f"duplicate spec title: {title}")
            for title in sorted(set(spec_by_title) - set(code_by_title)):
                global_problems.append(f"missing implementation: {title} (spec line {spec_by_title[title].line})")
            for title in sorted(set(code_by_title) - set(spec_by_title)):
                global_problems.append(f"extra standard implementation: {title} ({code_by_title[title].path})")
            for title in sorted(set(spec_by_title) & set(code_by_title)):
                spec, code = spec_by_title[title], code_by_title[title]
                if spec.rarity != code.rarity:
                    global_problems.append(f"{title}: rarity spec={spec.rarity} code={code.rarity}")
                if spec.card_type != code.card_type:
                    global_problems.append(f"{title}: type spec={spec.card_type} code={code.card_type}")
                if spec.cost != code.cost:
                    global_problems.append(f"{title}: cost spec={spec.cost} code={code.cost}")
            print(f"spec_rows {len(specs)}")
            print(f"set_equal {set(spec_by_title) == set(code_by_title)}")

    problems = list(global_problems)
    warnings = list(global_warnings)
    for card in selected:
        problems.extend(per_class_problems.get(card.class_name, []))
        warnings.extend(per_class_warnings.get(card.class_name, []))

    print(f"card_roots {len(card_roots)}")
    print(f"localization_files {len(loc_files)}")
    print(f"cards_total {len(cards)}")
    print(f"standard_cards {len(standard)}")
    distribution: dict[tuple[str, str], int] = {}
    for card in standard:
        bucket = (card.rarity or "?", card.card_type)
        distribution[bucket] = distribution.get(bucket, 0) + 1
    for (rarity, card_type), count in sorted(distribution.items()):
        print(f"distribution {rarity}/{card_type} {count}")
    if args.class_filter:
        names = ", ".join(sorted(card.class_name for card in selected)) or "none"
        print(f"selected {len(selected)} ({names})")

    for message in sorted(set(warnings)):
        print(f"WARN {message}")
    for message in problems:
        print(f"PROBLEM {message}")
    print(f"warnings {len(set(warnings))}")
    print(f"problems {len(problems)}")
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
