#!/usr/bin/env bash
#
# sync-sts2-assets.sh —— 把《杀戮尖塔 2》本体的解包资源同步进模组工程。
#
# 背景：模组的场景/主题会引用本体资源（如 res://images/...、res://themes/...、
# res://addons/mega_text/MegaLabel.cs）。这些路径在【运行时】由游戏本体的 pck
# 提供，模组 pck 不打包它们（见 export_presets.cfg 的 exclude_filter）；但【编辑器】
# 预览/编辑时工程里必须真实存在这些文件，否则场景会显示资源丢失、保存时可能丢引用。
# 本脚本负责把统一解包源里的这些目录同步进工程。
#
# 用法：
#   tools/sync-sts2-assets.sh                        # 同步到 BangDreamLib
#   tools/sync-sts2-assets.sh -n                     # dry-run，只打印会做什么
#   tools/sync-sts2-assets.sh --check                # 只检查源是否可用，不落盘
#   tools/sync-sts2-assets.sh -s /path/to/unpack     # 指定解包源
#   tools/sync-sts2-assets.sh --version 0.112.0      # 指定版本目录
#   tools/sync-sts2-assets.sh BangDreamLib ItsCrychic
#
# 源解析顺序：-s/--source > $STS2_UNPACKED > 仓库同级的 ../sts2unpack。
# 源可以是两种结构：
#   A) 直接是解包根：<源>/{addons,animations,fonts,images,materials,models,scenes,
#      shaders,themes}/...
#   B) 按版本分目录：<源>/0.111.0/...、<源>/0.112.0/...  —— 游戏更新后新增一个
#      版本目录即可，脚本会自动选**版本号最大**的那个（可用 --version 固定）。
#
# 同步清单（GAME_DIRS）只取编辑器真正需要的内容：
#   addons/mega_text  本体场景大量使用 MegaLabel，模组会读/引用这些场景
#   animations fonts images materials models scenes shaders themes  本体资源
# 其余 addons 不同步：
#   addons/spine   解包工具把可执行文件旁的 runtime 库改名填进了 editor/debug 槽位
#                  （那份"editor"库没有导入器），且会与 bin/ 里官方 editor 构建的
#                  Spine 扩展重复注册 → Spine 继续用 bin/ 那份
#   addons/fmod、addons/sentry、atlas_generator、dev_tools、megacontentcreator
#                  本体自用的扩展/编辑器插件，模组用不到，加载只会有插件噪声
#
# 如需额外同步或跳过某个子路径，用 STS2_SYNC_SKIP（空格分隔，覆盖默认值）：
#   STS2_SYNC_SKIP="addons/mega_text/ThemeConstants.cs" tools/sync-sts2-assets.sh
#   STS2_SYNC_SKIP="" tools/sync-sts2-assets.sh        # 不跳过任何东西
#
# 同步方式为 `cp -a --reflink=auto`：btrfs/XFS 上与源共享数据块（几乎不占额外
# 空间，实测 120MB → 0MB），其它文件系统自动退化为普通复制。
#
# 【不要】用软链接或 bind mount 指向 Steam 安装目录：
#   1) Godot 编辑器不扫描目录软链接（实测），资源等于不存在；
#   2) 编辑器会往源目录写 .import，与 Steam 文件校验互相干扰，反而不稳。
#
# 【重要】源里必须带原版 .import 文件（uid 与本体一致）：工程里数十处按 uid://
# 引用本体资源，若让 Godot 重新导入生成新 uid，编辑器会刷
# "invalid UID ... using text path instead" 警告。源缺少 .import 时脚本会告警。
#
# 同步 addons 需要工程侧三项配套设置（脚本会检查并在缺失时告警，但不代改）：
#   1) <工程>.csproj 有 <Compile Remove="addons/**"/>（否则 addons 下的 .cs 会被编译）
#   2) export_presets.cfg 的 exclude_filter 含 addons/*（否则会被打进模组 pck）
#   3) .gitignore 含 addons 规则（否则 git status 刷出上千个文件）
#
set -euo pipefail

REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
readonly REPO_ROOT

# 需要同步的本体资源目录（与 export_presets.cfg 的 exclude_filter / .gitignore 对应）
readonly -a GAME_DIRS=(addons/mega_text animations fonts images materials models scenes shaders themes src)
# 缺失时会明确告警的“核心”目录（其余缺失只打印跳过）
readonly -a CORE_DIRS=(addons/mega_text images fonts animations scenes themes shaders)
SOURCE="${STS2_UNPACKED:-$(dirname -- "$REPO_ROOT")/sts2unpack}"
VERSION=""
DRY_RUN=0
CHECK_ONLY=0
declare -a PROJECTS=()

if [[ -n "${STS2_SYNC_SKIP+set}" ]]; then
    if [[ -z "$STS2_SYNC_SKIP" ]]; then SKIP=(); else read -r -a SKIP <<<"$STS2_SYNC_SKIP"; fi
else
    SKIP=()
fi

die() { printf '错误：%s\n' "$*" >&2; exit 1; }
info() { printf '%s\n' "$*"; }
warn() { printf '警告：%s\n' "$*" >&2; }

usage() {
    # 打印文件头部的注释块，随注释自动更新
    awk 'NR>2 && /^#/ { sub(/^#[ ]?/, ""); print; next } NR>2 { exit }' "${BASH_SOURCE[0]}"
}

# ---------- 参数 ----------
while (($#)); do
    case "$1" in
        -s|--source) [[ $# -ge 2 ]] || die "--source 需要一个目录参数"; SOURCE="$2"; shift 2 ;;
        --version) [[ $# -ge 2 ]] || die "--version 需要一个版本目录名"; VERSION="$2"; shift 2 ;;
        -n|--dry-run) DRY_RUN=1; shift ;;
        --check) CHECK_ONLY=1; shift ;;
        -h|--help) usage; exit 0 ;;
        -*) die "未知选项：$1（用 --help 查看用法）" ;;
        *) PROJECTS+=("$1"); shift ;;
    esac
done
((${#PROJECTS[@]})) || PROJECTS=(BangDreamLib)

# ---------- 解析源目录 ----------
[[ -d "$SOURCE" ]] || die "解包源不存在：$SOURCE
请先解包游戏本体资源，或用 -s/--source（或环境变量 \$STS2_UNPACKED）指定路径。
期望结构：<源>/<版本>/ 或 <源>/{addons,images,fonts,animations,...}/...
解包时务必保留原版 .import 文件（uid 与本体一致）。"
SOURCE="$(cd -- "$SOURCE" && pwd)"

has_game_dir() { [[ -d "$1/images" || -d "$1/fonts" || -d "$1/scenes" ]]; }

if [[ -n "$VERSION" ]]; then
    [[ -d "$SOURCE/$VERSION" ]] || die "源下没有版本目录：$SOURCE/$VERSION"
    SOURCE="$SOURCE/$VERSION"
    info "使用指定版本目录：${SOURCE##*/}"
fi

if ! has_game_dir "$SOURCE"; then
    collect() { # $1 = 深度
        local depth="$1" d
        while IFS= read -r -d '' d; do has_game_dir "$d" && printf '%s\0' "$d"; done \
            < <(find "$SOURCE" -mindepth "$depth" -maxdepth "$depth" -type d -print0 2>/dev/null || true)
    }
    mapfile -d '' -t matches < <(collect 1)
    if ((${#matches[@]} > 1)); then
        latest="$(printf '%s\n' "${matches[@]}" | sort -V | tail -n1)"
        info "发现多个解包版本：$(printf '%s ' "${matches[@]##*/}")→ 使用版本号最大的：${latest##*/}"
        info "（可用 --version 指定其它版本）"
        SOURCE="$latest"
    elif ((${#matches[@]} == 1)); then
        info "源根未直接包含资源目录，自动下钻一层：${matches[0]##*/}"
        SOURCE="${matches[0]}"
    else
        mapfile -d '' -t matches < <(collect 2)
        if ((${#matches[@]} == 1)); then
            info "源根未直接包含资源目录，自动下钻两层：${matches[0]#"$SOURCE"/}"
            SOURCE="${matches[0]}"
        else
            die "目录 $SOURCE 看起来不是解包根（找不到 images/ fonts/ scenes/ 等）。
若解包结果多套了一层目录，请用 -s 明确指向内层，例如：
  tools/sync-sts2-assets.sh -s \"$SOURCE/<解包出的目录名>\""
        fi
    fi
fi

# uid 一致性检查
imports=$(find "$SOURCE" -name '*.import' 2>/dev/null | wc -l)
if ((imports == 0)); then
    warn "源里没有找到任何 .import 文件 —— 编辑器会为这些资源生成【新的 uid】，"
    warn "工程里按 uid:// 引用本体资源的地方会出现 \"invalid UID\" 警告。"
    warn "请用能保留原始 .import 的解包方式重新解包（gdre_tools 等）。"
elif ((imports < 500)); then
    warn "源里只有 $imports 个 .import，数量偏少，请确认解包是否完整。"
fi

# 核心目录缺失时给出明确提示（解包工具常把目录名缩写成 anima/font 之类，
# 那样脚本会静默跳过、只剩部分资源，必须让用户看见）
missing_core=()
for d in "${CORE_DIRS[@]}"; do [[ -e "$SOURCE/$d" ]] || missing_core+=("$d"); done
if ((${#missing_core[@]})); then
    warn "源中缺少这些常用目录：${missing_core[*]}"
    warn "源根下实际存在的目录：$(find "$SOURCE" -mindepth 1 -maxdepth 1 -type d -printf '%f ' 2>/dev/null)"
    warn "若解包工具的命名不同（例如 anima/font），请先改名或建目录软链接，"
    warn "否则对应资源不会被同步（工程里会缺少这些本体资源）。"
fi

# ---------- 工程侧配套检查（只提示，不代改） ----------
check_addons_integration() {
    local dest="$1" csproj="$2" ok=1
    if [[ -z "$csproj" ]]; then
        warn "${dest#"$REPO_ROOT"/} 下找不到 .csproj，无法确认 addons 的 C# 排除设置。"
        ok=0
    elif ! grep -q 'Compile Remove="addons/\*\*"' "$csproj" 2>/dev/null; then
        warn "${csproj##*/} 缺少 <Compile Remove=\"addons/**\"/>：addons 下的 .cs 会被编进模组程序集，构建很可能失败。"
        ok=0
    fi
    grep -q 'addons/\*' "$dest/export_presets.cfg" 2>/dev/null || {
        warn "${dest#"$REPO_ROOT"/}/export_presets.cfg 的 exclude_filter 不含 addons/*：这些文件会被打进模组 pck。"
        ok=0
    }
    grep -qE '^/?addons/?$|addons' "$REPO_ROOT/.gitignore" 2>/dev/null || {
        warn ".gitignore 缺少 addons 规则：git status 会刷出上千个文件。"
        ok=0
    }
    local n
    n=$(find "$dest" -name 'spine_godot_extension.gdextension' 2>/dev/null | wc -l)
    if ((n > 1)); then
        warn "工程里有 $n 个 spine_godot_extension.gdextension（addons/spine 与 bin/ 各一份）："
        warn "  entry_symbol 相同会重复注册 Spine 类；官方 bin/ 那份带导入器，建议只保留它。"
        ok=0
    fi
    ((ok)) || warn "（以上配套设置脚本不代改，确认后再同步。）"
}

# ---------- 逐工程同步 ----------
human() { du -shL "$1" 2>/dev/null | cut -f1; }

total_files=0
total_dirs=0

for proj in "${PROJECTS[@]}"; do
    if [[ -d "$proj" ]]; then
        dest="$(cd -- "$proj" && pwd)"
    else
        dest="$REPO_ROOT/$proj"
    fi
    [[ -f "$dest/project.godot" ]] || die "不是 Godot 工程（缺 project.godot）：$dest"

    # 防呆：源与目标不能互相包含，否则 cp/rm 会伤到工程或源
    case "$SOURCE/" in "$dest"/*) die "解包源位于工程目录内：$SOURCE" ;; esac
    case "$dest/" in "$SOURCE"/*) die "工程目录位于解包源内：$dest" ;; esac

    info ""
    info "==> ${dest#"$REPO_ROOT"/}"
    if printf '%s\n' "${GAME_DIRS[@]}" | grep -q '^addons'; then
        csproj="$(find "$dest" -maxdepth 1 -name '*.csproj' 2>/dev/null | head -n1)"
        check_addons_integration "$dest" "$csproj"
    fi

    used_before=$(df --output=used --block-size=1M "$dest" | tail -1)

    for d in "${GAME_DIRS[@]}"; do
        skip_all=0
        for s in "${SKIP[@]}"; do [[ "$s" == "$d" ]] && skip_all=1; done
        if ((skip_all)); then
            printf '  %-11s 跳过（跳过列表）\n' "$d"
            continue
        fi
        if [[ ! -e "$SOURCE/$d" ]]; then
            printf '  %-11s 跳过（源中不存在）\n' "$d"
            continue
        fi

        sub_skips=()
        for s in "${SKIP[@]}"; do [[ "$s" == "$d/"* ]] && sub_skips+=("$s"); done
        note=""
        ((${#sub_skips[@]})) && note="   跳过：${sub_skips[*]}"

        n=$(find -L "$SOURCE/$d" -type f 2>/dev/null | wc -l)
        printf '  %-11s %6s  %6s 个文件%s' "$d" "$(human "$SOURCE/$d")" "$n" "$note"
        if ((DRY_RUN || CHECK_ONLY)); then
            printf '   （目标现有 %s 个）\n' "$(find -L "$dest/$d" -type f 2>/dev/null | wc -l)"
        else
            printf '\n'
        fi
        ((total_dirs++)) || true
        total_files=$((total_files + n))

        ((DRY_RUN || CHECK_ONLY)) && continue
        [[ -L "$dest/$d" ]] && warn "$dest/$d 是软链接，将删除链接本身（不会动链接目标）"
        rm -rf -- "$dest/$d"
        mkdir -p -- "$dest/$d"
        cp -a --reflink=auto -- "$SOURCE/$d/." "$dest/$d/"
        for s in "${sub_skips[@]}"; do rm -rf -- "$dest/$s"; done
    done

    # 提醒目标里“同步清单之外”的同级残留（脚本不自动删，避免误删你自己的东西）
    declare -A synced=()
    for d in "${GAME_DIRS[@]}"; do synced["$d"]=1; done
    for d in "${GAME_DIRS[@]}"; do
        [[ "$d" == */* ]] || continue
        parent="${d%%/*}"
        [[ -d "$dest/$parent" ]] || continue
        for p in "$dest/$parent"/*/; do
            [[ -d "$p" ]] || continue
            rel="$parent/$(basename -- "$p")"
            [[ -n "${synced[$rel]:-}" ]] || printf '  提醒：%s/%s 不在同步清单里（脚本不删），不需要可自行删除\n' "${dest#"$REPO_ROOT"/}" "$rel"
        done
        break
    done

    used_after=$(df --output=used --block-size=1M "$dest" | tail -1)
    ((DRY_RUN || CHECK_ONLY)) || \
        info "  磁盘实际占用变化：$((used_after - used_before)) MB（reflink 生效时接近 0）"
done

info ""
if ((DRY_RUN)); then
    info "dry-run 结束：共会同步 $total_dirs 个目录 / $total_files 个文件，未做任何修改。"
elif ((CHECK_ONLY)); then
    info "检查结束：源 ${SOURCE#"$REPO_ROOT"/} 可用，共 $total_dirs 个目录 / $total_files 个文件可同步。"
else
    info "完成：源 ${SOURCE#"$REPO_ROOT"/}，已同步 $total_dirs 个目录 / $total_files 个文件。"
    info "提醒：这些目录已被 export_presets.cfg 的 exclude_filter 排除，不会进模组 pck。"
fi
