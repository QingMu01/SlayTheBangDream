#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""scan_orphans.py —— 过时文件「引用图」扫描，四态分文件落盘。

本脚本只证明「静态引用关系」，**不产出删除决定**。删除决策见
`docs/cleanup-authoring.md`（唯一事实来源）与 `.agents/skills/obsolete-file-cleanup/SKILL.md`。

判定模型出处：
  * 四态模型 ......... docs/cleanup-authoring.md:63-74（被引用 / 约定引用 / 弱命中 / 确认无引用）
  * 硬性条款 ......... docs/cleanup-authoring.md:84-98（搜索域、自引用剥离、uid 优先、盲区声明 …）
  * 引用形态与实现出处 .agents/skills/obsolete-file-cleanup/references/reference-graph.md:5-28
  * 输出契约 ......... .agents/skills/card-development/scripts/audit_cards.py:526-546
                       （`key value` 统计行 + `PROBLEM `/`WARN ` 前缀 + 有问题 exit 1）
  * 参考实现 ......... .vfx_staging/audit/orphan_scan3.py（历史存档，只读）
                       本脚本修掉它三个已证实缺陷：
                         (1) 死簇不迭代到不动点 —— 本脚本用 while 循环迭代到不动点（见 dead_cluster_pass）
                         (2) 卡图 startswith 前缀近似过宽 —— 本脚本改为「类名精确相等」+「Sakiko 精确后缀」
                             两条强规则，并把任何近似匹配单独标成「弱约定」（见 convention_hit）
                         (3) 带空格路径被非引号正则截断 —— 本脚本先取引号内完整串再取非引号兜底
                             （见 extract_tokens）
                   v1.1.0 另修一个同类自引用缺陷：
                         (4) 清理自述文档（本 skill 自身与 docs/cleanup-authoring.md）不得作为
                             引用证据 —— 它们列举候选文件名，属自引用（见 SELF_DOC_PREFIXES）
                   v1.2.0：
                         (5) 死簇级联必须咨询 keep 规则（见 keep_rules.py 的 cascade_blocks）：
                             引用者若是「活跃保留」项，其被引用者不得因级联被判死簇；
                             死簇单列 dead_cluster.txt，不再并入 confirmed_orphan.txt
                         (6) 动态排除本次 --out 目录与工具产物，避免「清单自污染判定」

CLI:
  python3 scan_orphans.py [--project-root .] [--out .vfx_staging/audit] [--json]

退出码：1 = 存在 confirmed_orphan（有「确认无引用」项，需分档与人工签字）；
        0 = 无。注意 exit 1 只表示「有东西要人看」，不表示脚本失败。

⚠️ 临时目录语义：本脚本的快照默认落在 `.vfx_staging/audit/` —— `.vfx_staging/` 是特效设计用的
临时目录，**随时可能被整体清理**。因此必须在同一次会话内完成
「扫描 → 分档 → 签字 → dry-run → apply」这条链；**长期留档靠 `docs/cleanup-log.md`**
（由 `sign_off.py --apply` 追加）。
"""

from __future__ import annotations

import argparse
import collections
import datetime as _dt
import fnmatch
import json
import os
import re
import subprocess
import sys

# keep 规则与 cleanup_manifest.py 共享同一份定义（见 keep_rules.py 头部注释）：
# 级联时必须咨询它，否则会把「被有意保留的预览件所引用」的文件误判为死簇。
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import keep_rules  # noqa: E402  (路径就绪后再导入)

VERSION = "1.3.0"
SCRIPT_NAME = "scan_orphans.py"

# ---------------------------------------------------------------------------
# 搜索域（docs/cleanup-authoring.md:86 硬性条款 1）
# ---------------------------------------------------------------------------
EXCLUDED_DIRS = {".git", ".godot", "bin", "obj", ".vfx_staging"}
# 「清理自述文档」自引用剥离（v1.1.0 新增）。
#
# **描述候选的文档不能构成「候选被使用」的证据** —— 与 `.import` 里 `source_file=`
# 自引用是同一类错误（docs/cleanup-authoring.md:87）：不剥离时，清理工具自己的
# keep-list / 规范正文只要列举了候选文件名，就会把候选从「确认无引用」洗成
# `weak(doc-only)`，并连带让整条死簇消失。
# 已知事故：`references/keep-list.md` 正文列举 `anon01.tscn`/`anon02.tscn`，导致
# anon 死簇（2 场景 + 32 张 charui 贴图）整条从 confirmed_orphan 消失。
#
# 只排除**本 skill 自身**（它描述的是候选与判定，不是使用关系）。
# 其余 `docs/**` 与 `.agents/skills/{card-development,vfx-authoring}/**` **不得**排除：
# 它们对 `MusicFlashVfx` 一类文件的提及是合法的「意图保留」证据
# （references/reference-graph.md:49、references/keep-list.md 第 2 节）。
SELF_DOC_PREFIXES = (
    ".agents/skills/obsolete-file-cleanup/",
    "docs/cleanup-authoring.md",
)
# 工具产物文件名（v1.2.0）：清单类产物可能被写到任意目录（如
# `cleanup_manifest.py --out .` 落在工程根），此时它描述候选 ≠ 候选被使用，
# 必须按文件名兜底排除，否则真孤儿会被自己的清单洗成 weak(doc-only)。
GENERATED_ARTIFACT_NAMES = frozenset({
    "cleanup_manifest.md", "cleanup_manifest.txt", "scan_orphans.json",
    "referenced.txt", "convention_referenced.txt", "weak.txt",
    "confirmed_orphan.txt", "dead_cluster.txt", "broken.txt",
    "sidecar_orphan.txt", "sidecar_missing.txt",
})
# 本体同步目录：BangDreamInSpire/{addons,animations,fonts,images,materials,models,
# scenes,shaders,themes} 由 tools/sync-sts2-assets.sh 生成；外加解包游戏源码 src。
# 出处：docs/cleanup-authoring.md:20、tools/sync-sts2-assets.sh:62、AGENTS.md 项目结构节。
BODY_SYNC_DIRS = {
    "addons", "animations", "fonts", "images", "materials",
    "models", "scenes", "shaders", "themes", "src",
}
BODY_SYNC_ROOTS = [("BangDreamInSpire", d) for d in sorted(BODY_SYNC_DIRS)]
MOD_ROOTS = ("BangDreamInSpire", "ItsCrychic")

# 语料里可被读作文本的文件后缀（token 提取 / 弱命中搜索 / uid 索引）
TEXT_EXTS = {
    ".cs", ".gd", ".tscn", ".tres", ".gdshader", ".json", ".cfg", ".import",
    ".uid", ".atlas", ".skel", ".gltf", ".md", ".txt", ".xml", ".html",
    ".yml", ".yaml", ".sh", ".godot", ".csproj", ".sln", ".editorconfig",
    ".gitignore", ".gitattributes",
}
# 仅文档/IDE 命中必须单列（references/reference-graph.md:49，误报来源 #7）
DOC_SOURCE_EXTS = {".md", ".txt", ".html", ".xml", ".yml", ".yaml"}
DOC_SOURCE_DIR_PARTS = {".idea"}

# 资源型候选：除脚本/副产物/工程元数据以外的 mod 自有文件
NON_CANDIDATE_EXTS = {".import", ".uid", ".cs", ".csproj", ".sln", ".godot"}

# 图片资源（需要 .import 副产物），docs/cleanup-authoring.md:14
IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp", ".svg", ".bmp", ".tga", ".exr", ".hdr"}
# uid 副产物的拥有者
UID_OWNER_EXTS = {".cs", ".gd", ".gdshader", ".shader"}

# 反射/特性注册保护名单：docs/cleanup-authoring.md:47-61（§4）、
# references/reference-graph.md:39-53（§4 误报来源 1/2/3/5/10）
REFLECTION_TOKENS = {
    # 游戏无条件反射注入（ModelDb.ReflectionHelper.GetSubtypesInMods<AbstractModel>）
    "AbstractModel",
    # 模组自定义发现入口
    "IBangDreamMechanic", "IPatchMethod", "AbstractConsoleCmd",
    # RitsuLib / 本体模板基类
    "ModCardTemplate", "ModRelicTemplate", "ModCharacterTemplate",
    "ModCustomReward", "ModRestSiteOptionTemplate", "ModPotionTemplate",
    "TypeListCardPoolModel", "TypeListRelicPoolModel", "TypeListPotionPoolModel",
    # 模型/单例族
    "SingletonModel", "HookedSingletonModel", "DynamicVar", "MerchantEntry",
    "CardCapability", "CardAugment",
    # Godot 脚本类型（由 .tscn 或动态路径实例化）
    "Node", "Node2D", "Node3D", "Control", "CanvasLayer", "Button", "Label",
    "Resource", "RefCounted", "GodotObject", "Sprite2D",
    # mod 自有基类（其子类由 Inherit 注册，见 §4 第 1 点）
    "BandCardModel", "MusicCardModel", "BandPowerModel", "BandRelicModel",
    "BandTemporaryPowerModel", "HiddenRelic", "BandMemberModel",
}


# ---------------------------------------------------------------------------
# 基础工具
# ---------------------------------------------------------------------------
def git_ls(project_root: str, *args: str) -> list[str]:
    """跑 `git ls-files`；用 -z 分隔以正确处理带空格的路径。"""
    proc = subprocess.run(
        ["git", "-C", project_root, "ls-files", *args, "-z"],
        capture_output=True, check=False,
    )
    if proc.returncode != 0:
        sys.stderr.write(proc.stderr.decode("utf-8", "replace"))
        raise SystemExit(f"git ls-files {' '.join(args)} 失败（returncode={proc.returncode}）")
    raw = proc.stdout.decode("utf-8", "replace")
    return [p for p in raw.split("\0") if p]


def is_excluded(path: str) -> bool:
    parts = path.split("/")
    if any(p in EXCLUDED_DIRS for p in parts):
        return True
    if len(parts) >= 2 and (parts[0], parts[1]) in set(BODY_SYNC_ROOTS):
        return True
    return False


def is_self_doc(path: str) -> bool:
    """清理自述文档：只描述候选，不构成使用证据。见 SELF_DOC_PREFIXES 注释。"""
    return any(path == prefix or path.startswith(prefix) for prefix in SELF_DOC_PREFIXES)


def read_text(project_root: str, rel: str) -> str:
    try:
        with open(os.path.join(project_root, rel), encoding="utf-8", errors="ignore") as fh:
            return fh.read()
    except OSError:
        return ""


def now_stamps() -> tuple[str, str]:
    local = _dt.datetime.now().astimezone()
    utc = _dt.datetime.now(_dt.timezone.utc)
    return (
        utc.strftime("%Y-%m-%dT%H:%M:%SZ"),
        local.strftime("%Y-%m-%dT%H:%M:%S%z"),
    )


# ---------------------------------------------------------------------------
# uid 索引（三来源：*.uid 内容 / *.import 的 uid= / *.tscn|*.tres|*.gdshader 头部）
# 出处：docs/cleanup-authoring.md:88（硬性条款 3）、references/reference-graph.md:11
# ---------------------------------------------------------------------------
UID_RE = re.compile(r"uid://[A-Za-z0-9_]+")


def header_uid(text: str) -> str | None:
    """取 .tscn/.tres/.gdshader 首行自声明的 uid。

    只认首行：`[gd_scene ... uid="uid://xxx"]` / `[gd_resource ... uid="uid://xxx"]`。
    放宽到整个文件会把后面的 `ext_resource uid="..."` 当成自己的 uid，
    使「A 引用了 B 的 uid」被误读成「B 自引用」，进而让死簇判定全线失效
    （参考 orphan_scan3.py:47-49 的 400 字符窗口就有这个缺陷）。
    """
    first = text.split("\n", 1)[0]
    m = re.search(r'uid="(uid://[A-Za-z0-9_]+)"', first)
    return m.group(1) if m else None


def index_uids(project_root: str, corpus: list[str], body_files: list[str]) -> dict[str, str]:
    uid_to_path: dict[str, str] = {}

    def add(uid: str, path: str) -> None:
        uid_to_path.setdefault(uid, path)

    def scan(p: str, text: str) -> None:
        ext = os.path.splitext(p)[1]
        if ext == ".uid":
            m = UID_RE.search(text)
            if m:
                add(m.group(0), p[: -len(".uid")])
        elif ext == ".import":
            m = re.search(r'^uid="([^"]+)"', text, re.M)
            if m:
                add(m.group(1), p[: -len(".import")])
        elif ext in (".tscn", ".tres", ".gdshader"):
            uid = header_uid(text)
            if uid:
                add(uid, p)

    for p in corpus:
        if os.path.splitext(p)[1] in (".uid", ".import", ".tscn", ".tres", ".gdshader"):
            scan(p, read_text(project_root, p))

    # 本体同步目录只作为「uid→磁盘路径」字典的来源，**不作为引用证据**：
    # 场景里大量 ext_resource 用本体 uid（references/reference-graph.md:13）。
    for p in body_files:
        if os.path.splitext(p)[1] in (".uid", ".import", ".tscn", ".tres", ".gdshader"):
            scan(p, read_text(project_root, p))
    return uid_to_path


def walk_body_dirs(project_root: str) -> list[str]:
    """列出本体同步目录下的磁盘文件（仅用于 uid 解析与存在性复核）。"""
    out: list[str] = []
    for root_dir, sub in BODY_SYNC_ROOTS:
        base = os.path.join(project_root, root_dir, sub)
        if not os.path.isdir(base):
            continue
        for dirpath, _dirnames, filenames in os.walk(base):
            for name in filenames:
                full = os.path.join(dirpath, name)
                out.append(os.path.relpath(full, project_root).replace(os.sep, "/"))
    return out


# ---------------------------------------------------------------------------
# 引用 token 提取
# ---------------------------------------------------------------------------
IMPORT_SELF_LINE = re.compile(r"^\s*(source_file|dest_files|path|path\.[a-z0-9_.]+)\s*=", re.I)
QUOTED_RES = re.compile(r"""["'](res://[^"']*)["']""")
# 非引号兜底：只收 ASCII 路径字符，避免把中文标点/说明文字吃进 token
BARE_RES = re.compile(r"""res://[^\s"'()\[\]{},;<>…、。，（）【】「」]+""")


def strip_self_reference_lines(path: str, text: str) -> str:
    """剥离 .import 的自引用行：source_file= / dest_files= / path=。

    出处：docs/cleanup-authoring.md:87（硬性条款 2）、
          references/reference-graph.md:9（补边 1，v1 结构性失效的根因）。
    """
    if not path.endswith(".import"):
        return text
    return "\n".join(line for line in text.splitlines() if not IMPORT_SELF_LINE.match(line))


def extract_tokens(text: str) -> tuple[set[str], set[str]]:
    """提取 res:// 路径与 uid:// 引用。

    - 先取引号内的完整串（带空格路径不被截断 —— 修 orphan_scan3.py:60 的缺陷）
    - 再把引号内的 res:// 串从文本里抹掉，剩余部分才用非引号兜底正则，
      否则 `path="res://….png"` 会被兜底正则再截出 `res://…` 前缀，
      凭空造出 3 条不存在的短路径（anon01.tscn 的实例）。
    - 丢弃含插值（`{`）的串：动态拼接不可静态解析，属声明的盲区
      出处：docs/cleanup-authoring.md:89（硬性条款 4）、reference-graph.md:55-60
    """
    res: set[str] = set()

    def _take(m: re.Match) -> str:
        token = m.group(1)
        if "{" not in token and "\n" not in token:
            res.add(token)
        return " " * len(m.group(0))

    remainder = QUOTED_RES.sub(_take, text)
    for raw in BARE_RES.findall(remainder):
        if "{" not in raw:
            res.add(raw)
    uids = {u for u in UID_RE.findall(text) if "{" not in u}
    return res, uids


# ---------------------------------------------------------------------------
# 类名索引（卡图/圣物图等命名约定 + .cs 反射保护）
# ---------------------------------------------------------------------------
DECL_RE = re.compile(r"\b(class|record|interface|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)")


def parse_decls(text: str) -> list[tuple[str, str, int]]:
    """列出 (声明种类, 类型名, 声明结束偏移)。"""
    return [(m.group(1), m.group(2), m.end()) for m in DECL_RE.finditer(text)]


def parse_type_bases(text: str) -> dict[str, list[str]]:
    """解析每个类型声明的基类/接口列表（去掉主构造参数与泛型参数）。"""
    out: dict[str, list[str]] = {}
    for _kind, name, end in parse_decls(text):
        seg = text[end: end + 800]
        brace = seg.find("{")
        head = seg[:brace] if brace >= 0 else seg
        head = head.split(";")[0]
        head = head.split("=>")[0]
        head = re.sub(r"\([^()]*\)", "", head)  # 主构造参数 / 基类构造实参
        bases: list[str] = []
        if ":" in head:
            for item in head.split(":", 1)[1].split(","):
                item = item.strip().split("<")[0].strip()
                item = item.split(".")[-1]
                if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", item):
                    bases.append(item)
        out[name] = bases
    return out


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------
def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="过时文件引用图扫描（四态分文件落盘）")
    parser.add_argument("--project-root", default=".", help="仓库根目录（默认 .）")
    parser.add_argument("--out", default=".vfx_staging/audit", help="输出目录（默认 .vfx_staging/audit，是会在清理中被整体删除的临时目录；"
                             "相对路径相对 --project-root 解析。长期留档请用 sign_off.py 的 docs/cleanup-log.md）")
    parser.add_argument("--json", action="store_true", help="额外输出机器可读 JSON")
    parser.add_argument("--keep-file", action="append", default=[],
                        help="追加保留规则文件（每行一条 glob，# 起注释）；影响死簇级联边界，"
                             "须与 cleanup_manifest.py 使用同一份规则")
    args = parser.parse_args(argv)

    project_root = os.path.abspath(args.project_root)
    out_dir = args.out if os.path.isabs(args.out) else os.path.join(project_root, args.out)
    os.makedirs(out_dir, exist_ok=True)
    utc_stamp, local_stamp = now_stamps()

    # ---- 1. 搜索域 ------------------------------------------------------
    tracked = git_ls(project_root)                                   # git ls-files
    all_visible = git_ls(project_root, "-co", "--exclude-standard")   # + 未跟踪（非 ignore）
    corpus = sorted(set(tracked) | set(all_visible))
    tracked_set = set(tracked)
    corpus = [p for p in corpus if not is_excluded(p)]
    # 清理自述文档自引用剥离（v1.1.0）：见 SELF_DOC_PREFIXES 的注释。
    self_docs = [p for p in corpus if is_self_doc(p)]
    corpus = [p for p in corpus if not is_self_doc(p)]
    # 工具产物自污染剥离（v1.2.0）：`--out` 落在搜索域内时，清单自身会被当成
    # 「文档引用」把真孤儿洗成 weak(doc-only)（实测 confirmed_orphan 1→0、exit 1→0）。
    # 两重防护：① 本次实际 out 目录（若是工程根的**真子目录**）；② 工具产物文件名
    # （清单可能被写到任意目录，例如 `cleanup_manifest.py --out .` 落在工程根，
    #  此时「排除 out 目录」会连整个仓库一起排除，故必须按文件名兜底）。
    out_rel = os.path.relpath(out_dir, project_root).replace(os.sep, "/")
    out_dir_excluded = (
        out_rel not in (".", "..")
        and not out_rel.startswith("../")
        and not os.path.isabs(out_rel)
    )
    out_artifacts = [p for p in corpus if os.path.basename(p) in GENERATED_ARTIFACT_NAMES]
    corpus = [
        p for p in corpus
        if os.path.basename(p) not in GENERATED_ARTIFACT_NAMES
        and not (out_dir_excluded and (p == out_rel or p.startswith(out_rel + "/")))
    ]
    body_files = walk_body_dirs(project_root)

    # ---- 2. 读语料 ------------------------------------------------------
    texts: dict[str, str] = {}
    for p in corpus:
        if os.path.splitext(p)[1] in TEXT_EXTS or os.path.basename(p).startswith(".git"):
            texts[p] = read_text(project_root, p)

    # ---- 3. uid 索引 ----------------------------------------------------
    uid_to_path = index_uids(project_root, corpus, body_files)

    # ---- 4. 引用 token --------------------------------------------------
    path_src: dict[str, set[str]] = collections.defaultdict(set)
    uid_src: dict[str, set[str]] = collections.defaultdict(set)
    for p, text in texts.items():
        # *.import / *.uid 只承载「自己的 uid」，不可能是别人的引用来源：
        # 若把它们计入，会让 `uids_of(X)` 里任何一个 uid 都回落命中自己的 .import，
        # 从而出现「X 被 X.import 引用」的假引用（自引用剥离的姊妹条款，
        # 出处：docs/cleanup-authoring.md:87）。
        if p.endswith((".import", ".uid")):
            continue
        text = strip_self_reference_lines(p, text)
        res_toks, uid_toks = extract_tokens(text)
        for tok in res_toks:
            path_src[tok].add(p)
        for tok in uid_toks:
            uid_src[tok].add(p)

    def is_doc_source(p: str) -> bool:
        if os.path.splitext(p)[1] in DOC_SOURCE_EXTS:
            return True
        return any(part in DOC_SOURCE_DIR_PARTS for part in p.split("/"))

    # ---- 5. 类名索引 ----------------------------------------------------
    file_types: dict[str, list[str]] = {}
    file_decls: dict[str, list[tuple[str, str]]] = {}
    type_owner: dict[str, str] = {}
    type_bases: dict[str, list[str]] = {}
    for p in corpus:
        if not p.endswith(".cs"):
            continue
        text = texts.get(p, "")
        decls = parse_decls(text)
        file_types[p] = sorted({name for _k, name, _e in decls})
        file_decls[p] = [(k, name) for k, name, _e in decls]
        bases_map = parse_type_bases(text)
        for name in file_types[p]:
            type_owner.setdefault(name, p)
            type_bases.setdefault(name, bases_map.get(name, []))

    # RitsuLib 特性继承注册：docs/cleanup-authoring.md:51
    inherit_roots: set[str] = set()
    for p in corpus:
        if not p.endswith(".cs"):
            continue
        text = texts.get(p, "")
        for m in re.finditer(r"\[Register\w*\(([^)]*)\)\]", text):
            if re.search(r"Inherit\s*=\s*true", m.group(1)):
                inherit_roots.update(file_types.get(p, []))

    # 本工程内的继承闭包（基类是本体/库类型时无法解析，由 REFLECTION_TOKENS + 基类兜底覆盖）
    inherited_protected = set(inherit_roots)
    changed = True
    while changed:
        changed = False
        for t, bases in type_bases.items():
            if t not in inherited_protected and any(b in inherited_protected for b in bases):
                inherited_protected.add(t)
                changed = True

    card_classes = {
        t for t, owner in type_owner.items() if "/Cards/" in owner
    }
    all_classes_by_mod: dict[str, set[str]] = collections.defaultdict(set)
    for t, owner in type_owner.items():
        for r in MOD_ROOTS:
            if owner.startswith(r + "/"):
                all_classes_by_mod[r].add(t)

    # ---- 6. Spine 裸贴图名（.atlas 内没有 res://） -----------------------
    # 出处：references/reference-graph.md:24（不做会让 8-33 张 Spine png 落进孤儿）
    spine_names: dict[str, set[str]] = collections.defaultdict(set)  # basename -> atlas 文件
    for p in corpus:
        if not p.endswith(".atlas"):
            continue
        for m in re.finditer(r"([A-Za-z0-9_.\- ]+\.png)", texts.get(p, "")):
            name = m.group(1).strip()
            if name:
                spine_names[os.path.basename(name)].add(p)

    # ---- 7. 候选集 ------------------------------------------------------
    own = [p for p in corpus if p.startswith(tuple(r + "/" for r in MOD_ROOTS))]
    asset_candidates = [
        p for p in own if os.path.splitext(p)[1] not in NON_CANDIDATE_EXTS
    ]
    script_candidates = [p for p in own if p.endswith(".cs")]
    candidates = sorted(asset_candidates + script_candidates)

    def res_paths(disk: str) -> list[str]:
        return [
            "res://" + disk[len(r) + 1:]
            for r in MOD_ROOTS if disk.startswith(r + "/")
        ]

    def uids_of(disk: str) -> set[str]:
        found: set[str] = set()
        for sc in (disk + ".import", disk + ".uid"):
            if sc in texts:
                found |= set(UID_RE.findall(texts[sc]))
            elif os.path.exists(os.path.join(project_root, sc)):
                found |= set(UID_RE.findall(read_text(project_root, sc)))
        if disk.endswith((".tscn", ".tres", ".gdshader")):
            uid = header_uid(texts.get(disk, ""))
            if uid:
                found.add(uid)
        return found

    def hard_hits(disk: str) -> list[dict]:
        mine = {disk, disk + ".import", disk + ".uid"}
        hits: list[dict] = []
        for rp in res_paths(disk):
            srcs = set(path_src.get(rp, ())) - mine
            if srcs:
                hits.append({"kind": "res-path", "rule": "字面 res:// 路径", "token": rp, "sources": srcs})
        for uid in uids_of(disk):
            srcs = set(uid_src.get(uid, ())) - mine
            if srcs:
                hits.append({"kind": "uid", "rule": "uid 引用", "token": uid, "sources": srcs})
        return hits

    # ---- 8. 约定规则 ----------------------------------------------------
    def convention_hit(disk: str) -> tuple[str, str] | None:
        """返回 (规则名, 强度)；强度 ∈ {strong, weak-convention}。

        每条规则都在注释里标注实现出处（file:line），与 references/reference-graph.md 同源。
        """
        base = os.path.basename(disk)
        stem, ext = os.path.splitext(base)
        parent = os.path.dirname(disk)
        parts = disk.split("/")
        mod_id = parts[0] if parts else ""

        # mod loader 入口清单必须保留：docs/cleanup-authoring.md:27
        if base in ("BangDreamInSpire.json", "ItsCrychic.json") and len(parts) == 2:
            return (f"mod入口清单 {base}", "strong")

        # 本体同步目录 —— 不应出现在候选里（防御性检查）
        if len(parts) >= 2 and (parts[0], parts[1]) in set(BODY_SYNC_ROOTS):
            return ("本体同步目录（禁止按孤儿规则处理）", "strong")

        # 本地化文件：游戏按本体表文件名遍历（reference-graph.md:25）——
        # 仓内拿不到本体 46 张表名，退化为弱约定。
        if "/localization/" in disk and ext == ".json":
            return ("弱约定:本地化表名（需本体 46 张表名核对）", "weak-convention")

        # 卡图约定：{modFolder}/images/card_portraits/{类名}.png
        # 出处：BangDreamInSpire/Scripts/Extensions/PathExtensions.cs:9-15
        # 升级图 {类名}_Bate.png：同文件 17-23
        if parent.endswith("/images/card_portraits"):
            core = stem[: -len("_Bate")] if stem.endswith("_Bate") else stem
            if core in all_classes_by_mod.get(mod_id, set()):
                label = "卡图约定 <类名>_Bate.png" if stem.endswith("_Bate") else "卡图约定 <类名>.png"
                return (f"{label} ← {core}", "strong")
            # 音乐牌共享图：类名以 Sakiko 结尾时优先 {去掉 Sakiko}.png（_Bate 同理）
            # 出处：ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs:22-39
            for cls in all_classes_by_mod.get(mod_id, set()):
                if cls.endswith("Sakiko") and cls[: -len("Sakiko")] == core:
                    return (f"音乐牌共享立绘约定 ← {cls}", "strong")
            return None

        # 圣物图约定：images/relics/{类名}.png 与 images/relics/big/{类名}.png
        # 出处：BangDreamInSpire/Scripts/Extensions/PathExtensions.cs:25-39
        if parent.endswith("/images/relics") or parent.endswith("/images/relics/big"):
            if stem in all_classes_by_mod.get(mod_id, set()):
                return (f"圣物图约定 ← {stem}", "strong")

        # mod 图标：res://{modId}/mod_image.png
        # 出处：BangDreamInSpire/src/Core/Nodes/Screens/ModdingScreen/NModInfoContainer.cs:39
        if base == "mod_image.png" and disk == f"{mod_id}/{mod_id}/mod_image.png":
            return ("mod图标约定 res://<modId>/mod_image.png", "strong")

        # Spine 贴图：.atlas 里是裸文件名，只能按 basename 补边 —— 近似匹配，标弱约定
        # 出处：references/reference-graph.md:24
        if base in spine_names and ext in IMAGE_EXTS:
            atlas = sorted(spine_names[base])[0]
            return (f"弱约定:Spine图集裸贴图名（{atlas}）", "weak-convention")

        return None

    # ---- 9. .cs 保护规则（反射/特性注册，docs/cleanup-authoring.md:47-61） ----
    def code_convention_hit(disk: str) -> str | None:
        text = texts.get(disk, "")
        types = file_types.get(disk, [])
        if re.search(r"\[Register\w*\(", text):
            return "代码约定:[Register*] 特性注册（docs/cleanup-authoring.md:51）"
        if types and any(t in inherited_protected for t in types):
            return "代码约定:继承 [Register(..., Inherit=true)] 基类（docs/cleanup-authoring.md:51）"
        if "static class" in text:
            return "代码约定:static class 扩展（按方法名调用，docs/cleanup-authoring.md:59）"
        if "HarmonyPatch" in text:
            return "代码约定:[HarmonyPatch]（Harmony 补丁）"
        if {k for k, _n in file_decls.get(disk, [])} & {"interface", "enum"}:
            return "代码约定:接口/枚举声明（被注册/反射基础设施消费）"
        if types and any(type_bases.get(t) for t in types):
            return "代码约定:有基类/接口（模型族或本体基类，docs/cleanup-authoring.md:47-61）"
        return None

    def weak_hit(disk: str) -> tuple[list[str], str]:
        """候选 basename / 类名出现在任意语料文本中（排除自身与 sidecar）。

        出处：docs/cleanup-authoring.md:71（弱命中 = 待人工确认，不得当孤儿）
        """
        holes = {disk, disk + ".import", disk + ".uid"}
        needles: list[str] = []
        if disk.endswith(".cs"):
            needles = [t for t in file_types.get(disk, []) if len(t) >= 4]
        else:
            needles = [os.path.basename(disk)]
        hits: list[str] = []
        for p, text in texts.items():
            if p in holes or p == disk:
                continue
            body = strip_self_reference_lines(p, text)
            for needle in needles:
                if needle and needle in body:
                    hits.append(p)
                    break
        return hits, ("class-name" if disk.endswith(".cs") else "basename")

    # ---- 10. 分类 -------------------------------------------------------
    referenced: list[dict] = []
    convention: list[dict] = []
    weak: list[dict] = []
    orphan: dict[str, str] = {}     # disk -> 判定理由

    for disk in candidates:
        hits = hard_hits(disk)
        strong = [h for h in hits if any(not is_doc_source(s) for s in h["sources"])]
        if strong:
            srcs = sorted({s for h in strong for s in h["sources"] if not is_doc_source(s)})
            referenced.append({
                "path": disk,
                "rule": " + ".join(sorted({h["rule"] for h in strong})),
                "detail": " + ".join(h["token"] for h in strong),
                "sources": srcs,
            })
            continue

        conv = code_convention_hit(disk) if disk.endswith(".cs") else None
        strength = "strong"
        if conv is None:
            cv = convention_hit(disk)
            if cv:
                conv, strength = cv
        if conv:
            convention.append({"path": disk, "rule": conv, "strength": strength})
            continue

        doc_only = sorted({s for h in hits for s in h["sources"] if is_doc_source(s)})
        w_hits, w_kind = weak_hit(disk)
        all_weak = sorted(set(doc_only) | set(w_hits))
        if all_weak:
            only_docs = all(is_doc_source(s) for s in all_weak)
            weak.append({
                "path": disk,
                "kind": "doc-only" if only_docs else w_kind,
                "rule": "仅文档/IDE 命中" if only_docs else "basename/类名出现在其它语料",
                "sources": all_weak,
            })
            continue

        orphan[disk] = "无字面路径/uid/约定/弱命中"

    # 死簇：某候选的所有引用者本身也在孤儿集合中；迭代到不动点。
    # 出处：docs/cleanup-authoring.md:63-74、本脚本修复 orphan_scan3.py:158-164 的「只算一次」。
    #
    # v1.2.0 追加两条边界（缺陷：MusicCrystalHitVfx.cs 级联假阳性）：
    #   ① 级联前先咨询 keep 规则：命中「活跃保留」（cascade_blocks=True）的引用者不是
    #      「确认孤儿」，它的出边是活引用 —— 被它引用的文件不得因级联被判死簇。
    #      反例：`music_crystal_hit_vfx.tscn` 是「有意保留的预览件」，它 uid+path 硬引用
    #      `MusicCrystalHitVfx.cs`，级联前提不成立（「被引用 → 不动」）。
    #   ② 死簇**单列** dead_cluster.txt，不再并入 confirmed_orphan.txt：
    #      「确认无引用」与「仅被孤儿引用」是两种判定强度，混在一起会让
    #      cleanup_manifest 的 A 档判断失去依据。
    keep_rules_all: list[keep_rules.KeepRule] = list(keep_rules.DEFAULT_KEEP_RULES)
    for kf in args.keep_file:
        try:
            keep_rules_all.extend(keep_rules.load_keep_file(project_root, kf))
        except OSError as exc:
            print(f"PROBLEM keep_file_unreadable {kf}: {exc}")
            return 1

    def keep_rule_of(path: str) -> keep_rules.KeepRule | None:
        return keep_rules.find_keep_rule(path, keep_rules_all)

    # 级联可用的「已死」集合 = 种子孤儿里未命中「活跃保留」的那些
    cascade_blocked = sorted(p for p in orphan if (keep_rule_of(p) and keep_rule_of(p).cascade_blocks))
    cascade_set = {p for p in orphan if p not in set(cascade_blocked)}
    dead_cluster: list[dict] = []
    changed, rounds = True, 0
    while changed:
        changed = False
        rounds += 1
        for item in list(referenced):
            disk = item["path"]
            srcs = set(item["sources"])
            srcs_only_dead = bool(srcs) and srcs <= cascade_set
            if srcs_only_dead:
                referenced.remove(item)
                dead_cluster.append({
                    "path": disk, "sources": sorted(srcs), "round": rounds,
                    "reason": "死簇：引用者全部为孤儿（且引用者非「活跃保留」）",
                })
                cascade_set.add(disk)
                changed = True
    # 每轮都可能让「新的孤儿（死簇成员）」成为别人的唯一引用者，while 覆盖传递闭包直到不动点

    # ---- 11. 断链 -------------------------------------------------------
    # res:// 是**工程根相对**，而仓库里有两个工程，故三种解析顺序都要试：
    #   <root>/<rel>                 —— 仓库根相对（res://src/** 等本体路径）
    #   <root>/BangDreamInSpire/<rel>    —— BangDreamInSpire 工程根（= 本体同步目录挂载点）
    #   <root>/ItsCrychic/<rel>      —— ItsCrychic 工程根
    # 出处：references/reference-graph.md:11-13（跨工程建图、本体内嵌资源合法）
    def resolve_res(rel: str) -> str | None:
        for base in ("", *MOD_ROOTS):
            candidate = os.path.join(project_root, base, rel)
            if os.path.exists(candidate):
                return os.path.join(base, rel).lstrip("/")
        return None

    def classify_missing(rel: str) -> str:
        parts = rel.split("/")
        if parts[0] == "src":
            return "本体程序集路径 res://src/**（仓库内必然不存在）"
        if parts[0] in BODY_SYNC_DIRS:
            return "本体资源（res:// 根为本体同步目录，未跑 sync-sts2-assets.sh？）"
        return "工程内缺失"

    broken: list[dict] = []
    for tok in sorted(path_src):
        srcs = sorted(path_src[tok])
        if not any(not is_doc_source(s) for s in srcs):
            continue  # 仅文档/IDE 命中：不是可解析引用，单列在 weak.txt
        rel = tok[len("res://"):]
        if not rel or resolve_res(rel):
            continue
        broken.append({"category": classify_missing(rel), "token": tok, "sources": srcs})
    for tok in sorted(uid_src):
        srcs = sorted(uid_src[tok])
        if not any(not is_doc_source(s) for s in srcs):
            continue
        target = uid_to_path.get(tok)
        if target is None:
            broken.append({"category": "未知 uid", "token": tok, "sources": srcs})
        elif not os.path.exists(os.path.join(project_root, target)):
            broken.append({"category": f"uid 指向不存在的文件（{target}）", "token": tok, "sources": srcs})

    # ---- 12. sidecar 双向 -------------------------------------------------
    # 出处：docs/cleanup-authoring.md:14（铁律三）、:92（硬性条款 7）
    sidecar_orphan: list[str] = []
    sidecar_missing: list[str] = []
    for p in own:
        if p.endswith(".import") and not os.path.exists(os.path.join(project_root, p[: -len(".import")])):
            sidecar_orphan.append(p)
        elif p.endswith(".uid") and not os.path.exists(os.path.join(project_root, p[: -len(".uid")])):
            sidecar_orphan.append(p)
    for p in own:
        if p.endswith((".import", ".uid")):
            continue
        ext = os.path.splitext(p)[1]
        need = None
        if ext in UID_OWNER_EXTS:
            need = p + ".uid"
        elif ext in IMAGE_EXTS or ext in (".atlas", ".skel"):
            need = p + ".import"
        if need and not os.path.exists(os.path.join(project_root, need)):
            sidecar_missing.append(need)

    # ---- 13. 落盘 -------------------------------------------------------
    input_domain = (
        f"git ls-files ∪ git ls-files -co --exclude-standard；"
        f"排除 {sorted(EXCLUDED_DIRS)} 与本体同步目录 BangDreamInSpire/{{{','.join(sorted(BODY_SYNC_DIRS))}}}；"
        f"候选 = {MOD_ROOTS} 下的资源文件 + .cs 脚本（排除 *.import/*.uid 副产物）；"
        f"状态 = referenced / convention_referenced / weak / confirmed_orphan + dead_cluster（单列）"
    )
    excluded_self_docs = (
        f"已从引用语料中排除清理自述文档（{len(self_docs)} 个文件）："
        f"{list(SELF_DOC_PREFIXES)} —— 它们枚举候选文件名，属自引用，"
        f"不得作为「候选被使用」的证据；其余 docs/** 与 .agents/skills/{{card-development,vfx-authoring}}/** 未排除"
    )
    excluded_out_dir = (
        f"--out 目录 {out_rel} "
        + ("在搜索域内 → 已动态排除" if out_dir_excluded else "不在搜索域内 → 无需额外处理")
        + f"；另按文件名排除工具产物 {sorted(GENERATED_ARTIFACT_NAMES)}"
        + (f"（本次命中 {len(out_artifacts)} 个：{out_artifacts[:6]}）" if out_artifacts else "（本次未命中）")
    )
    blind_spots = (
        "动态拼接路径（含 { 的插值串）不进 token；string.Format/插值构造的 res:// 路径、"
        "本体 pck 内不可见的 uid、以及运行时反射实例化均无法覆盖 —— 删除候选必须人工核对动态加载点"
    )

    def write_snapshot(name: str, columns: str, rows: list[str]) -> int:
        path = os.path.join(out_dir, name)
        head = [
            f"# generated_by: {SCRIPT_NAME}",
            f"# version: {VERSION}",
            f"# generated_at_utc: {utc_stamp}",
            f"# generated_at_local: {local_stamp}",
            f"# input_domain: {input_domain}",
            f"# excluded_self_docs: {excluded_self_docs}",
            f"# excluded_out_dir: {excluded_out_dir}",
            f"# columns: {columns}",
            f"# blind_spot: {blind_spots}",
            f"#",
        ]
        with open(path, "w", encoding="utf-8") as fh:
            fh.write("\n".join(head) + "\n")
            for row in rows:
                fh.write(row + "\n")
        return len(rows)

    def short_sources(srcs: list[str], limit: int = 4) -> str:
        if not srcs:
            return ""
        shown = srcs[:limit]
        suffix = f" (+{len(srcs) - limit})" if len(srcs) > limit else ""
        return ",".join(shown) + suffix

    counts = {
        "corpus_files": len(corpus),
        "excluded_self_docs_files": len(self_docs),
        "body_files_indexed": len(body_files),
        "candidates": len(candidates),
        "asset_candidates": len(asset_candidates),
        "script_candidates": len(script_candidates),
        "referenced": len(referenced),
        "referenced_assets": sum(1 for r in referenced if not r["path"].endswith(".cs")),
        "convention": len(convention),
        "convention_strong": sum(1 for c in convention if c["strength"] == "strong"),
        "convention_weak": sum(1 for c in convention if c["strength"] == "weak-convention"),
        "weak": len(weak),
        "weak_doc_only": sum(1 for w in weak if w["kind"] == "doc-only"),
        "weak_basename": sum(1 for w in weak if w["kind"] == "basename"),
        "weak_class_name": sum(1 for w in weak if w["kind"] == "class-name"),
        "confirmed_orphan": len(orphan),
        "confirmed_orphan_assets": sum(1 for p in orphan if not p.endswith(".cs")),
        "confirmed_orphan_scripts": sum(1 for p in orphan if p.endswith(".cs")),
        "dead_cluster": len(dead_cluster),
        "dead_cluster_assets": sum(1 for d in dead_cluster if not d["path"].endswith(".cs")),
        "dead_cluster_blocked_by_keep": len(cascade_blocked),
        "broken": len(broken),
        "broken_body": sum(1 for b in broken if b["category"].startswith(("本体程序集路径", "本体资源"))),
        "broken_local": sum(1 for b in broken if not b["category"].startswith(("本体程序集路径", "本体资源"))),
        "sidecar_orphan": len(sidecar_orphan),
        "sidecar_missing": len(sidecar_missing),
    }

    write_snapshot(
        "referenced.txt", "path<TAB>rule<TAB>token<TAB>sources",
        [f"{r['path']}\t{r['rule']}\t{r['detail']}\t{short_sources(r['sources'])}" for r in referenced],
    )
    write_snapshot(
        "convention_referenced.txt", "path<TAB>rule<TAB>strength",
        [f"{c['path']}\t{c['rule']}\t{c['strength']}" for c in convention],
    )
    write_snapshot(
        "weak.txt", "path<TAB>kind<TAB>rule<TAB>hits",
        [f"{w['path']}\t{w['kind']}\t{w['rule']}\t{short_sources(w['sources'])}" for w in weak],
    )
    write_snapshot(
        "confirmed_orphan.txt", "path<TAB>reason",
        [f"{p}\t{orphan[p]}" for p in sorted(orphan)],
    )
    write_snapshot(
        "dead_cluster.txt", "path<TAB>sources<TAB>round<TAB>reason",
        [f"{d['path']}\t{short_sources(d['sources'])}\t{d['round']}\t{d['reason']}" for d in dead_cluster],
    )
    write_snapshot(
        "broken.txt", "category<TAB>token<TAB>sources",
        [f"{b['category']}\t{b['token']}\t{short_sources(b['sources'])}" for b in broken],
    )
    write_snapshot("sidecar_orphan.txt", "path", sorted(sidecar_orphan))
    write_snapshot("sidecar_missing.txt", "path", sorted(sidecar_missing))

    if args.json:
        payload = {
            "script": SCRIPT_NAME,
            "version": VERSION,
            "generated_at_utc": utc_stamp,
            "generated_at_local": local_stamp,
            "input_domain": input_domain,
            "excluded_self_docs": excluded_self_docs,
            "excluded_self_docs_files": sorted(self_docs),
            "excluded_out_dir": excluded_out_dir,
            "excluded_out_dir_excluded": out_dir_excluded,
            "excluded_out_artifacts": sorted(out_artifacts),
            "blind_spots": blind_spots,
            "counts": counts,
            "referenced": referenced,
            "convention_referenced": convention,
            "weak": weak,
            "confirmed_orphan": [{"path": p, "reason": r} for p, r in sorted(orphan.items())],
            "dead_cluster": dead_cluster,
            "dead_cluster_blocked_by_keep": cascade_blocked,
            "broken": broken,
            "sidecar_orphan": sorted(sidecar_orphan),
            "sidecar_missing": sorted(sidecar_missing),
        }
        with open(os.path.join(out_dir, "scan_orphans.json"), "w", encoding="utf-8") as fh:
            json.dump(payload, fh, ensure_ascii=False, indent=2)

    # ---- 14. stdout 契约 -------------------------------------------------
    for key in ("corpus_files", "excluded_self_docs_files", "body_files_indexed", "candidates", "asset_candidates",
                "script_candidates", "referenced", "referenced_assets", "convention",
                "convention_strong", "convention_weak", "weak", "weak_doc_only",
                "weak_basename", "weak_class_name", "confirmed_orphan",
                "confirmed_orphan_assets", "confirmed_orphan_scripts", "dead_cluster",
                "dead_cluster_assets", "dead_cluster_blocked_by_keep",
                "broken", "broken_body", "broken_local", "sidecar_orphan", "sidecar_missing"):
        print(f"{key} {counts[key]}")
    print(f"output_dir {os.path.relpath(out_dir, project_root)}")

    problems = 0
    warnings = 0
    for b in broken:
        if b["category"].startswith(("本体程序集路径", "本体资源")):
            # 本体路径在仓库内不存在属已知形态（reference-graph.md:11-13）
            warnings += 1
            print(f"WARN broken(body) {b['category']} {b['token']} <- {short_sources(b['sources'])}")
        else:
            problems += 1
            print(f"PROBLEM broken {b['category']} {b['token']} <- {short_sources(b['sources'])}")
    for sc in sidecar_orphan:
        warnings += 1
        print(f"WARN sidecar_orphan {sc}")
    for sc in sidecar_missing:
        warnings += 1
        print(f"WARN sidecar_missing {sc}")
    for p in sorted(orphan):
        problems += 1
        print(f"PROBLEM confirmed_orphan {p}")
    # 死簇弱于「确认无引用」：只打 WARN（cleanup_manifest 默认落 B，不落 A）。
    # exit 码仍按契约只由 confirmed_orphan 决定（docs/cleanup-authoring.md:97）。
    for d in dead_cluster:
        warnings += 1
        print(f"WARN dead_cluster {d['path']} <- {short_sources(d['sources'])} (round {d['round']})")

    print(f"warnings {warnings}")
    print(f"problems {problems}")
    return 1 if orphan else 0


if __name__ == "__main__":
    raise SystemExit(main())
