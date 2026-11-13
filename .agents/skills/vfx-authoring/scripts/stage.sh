#!/usr/bin/env bash
#
# stage.sh —— 逐层阶段验证：build → 录帧 → 裁中心 → 3x3 接触表 → 机器校验 →（可选）与上一轮对比。
#
# 把「改一层 → 编译 → 录帧 → 裁图 → 目视」这四步压成一条命令，让「每加一层就录一帧看看」变便宜。
# 录制复用 record.sh（PNG 序列 + mp4），校验复用 verify_frames.py，本脚本不重写这两段逻辑。
#
# 用法：
#   bash .agents/skills/vfx-authoring/scripts/stage.sh -s "res://BangDreamInSpire/scenes/vfx/staff_ring_vfx.tscn"
#   bash .agents/skills/vfx-authoring/scripts/stage.sh -s "<场景>" -n 12 --no-build
#   bash .agents/skills/vfx-authoring/scripts/stage.sh -s "<场景>" --compare .scratch/vfx/stage/<上一轮>
#   bash .agents/skills/vfx-authoring/scripts/stage.sh -s "<场景>" --dry-run
#
# 选项：
#   -s, --scene    场景 res:// 路径（必填）
#   -n, --frames   帧数，默认 90
#   -f, --fps      帧率，默认 60
#   -c, --crop     ffmpeg crop 参数，默认 "760:760:580:160"（1920x1080 的中心区域）
#       --no-build 跳过 build（默认先 build）
#       --compare  与上一轮对比；传上一轮的 stage 输出目录（读它的 crop/，没有则读 rec/）
#       --out      输出根目录，默认 .scratch/vfx/stage/<场景名>-<时间戳>
#       --godot    Godot 可执行文件路径；透传给 record.sh
#       --dry-run  只打印将执行的命令与解析出的路径，不 build 也不录制
#   -h, --help     打印本帮助
#
# 产物（<out>/ 下）：
#   rec/             record.sh 的原始帧序列 + mp4（+ godot.log）
#   crop/            按 --crop 裁好的帧
#   sheet.png        9 帧等间隔 3x3 接触表（每格带帧号与秒数）
#   compare.png      与上一轮的三行对照（左上一轮 / 右本轮，0%・50%・100%）——仅 --compare
#   build.log        编译日志
#   verify.txt       verify_frames.py 的完整输出
#   compare_table.txt 逐帧平均亮度差表——仅 --compare
#
set -euo pipefail

SCENE=""
FRAMES=90
FPS=60
CROP="760:760:580:160"
DO_BUILD=1
COMPARE=""
OUT=""
GODOT_ARG=""
DRY_RUN=0

while (($#)); do
    case "$1" in
        -s|--scene)   SCENE="${2:?--scene 需要一个参数}"; shift 2 ;;
        -n|--frames)  FRAMES="${2:?--frames 需要一个参数}"; shift 2 ;;
        -f|--fps)     FPS="${2:?--fps 需要一个参数}"; shift 2 ;;
        -c|--crop)    CROP="${2:?--crop 需要一个参数}"; shift 2 ;;
        --no-build)   DO_BUILD=0; shift ;;
        --compare)    COMPARE="${2:?--compare 需要一个目录}"; shift 2 ;;
        --out)        OUT="${2:?--out 需要一个目录}"; shift 2 ;;
        --godot)      GODOT_ARG="${2:?--godot 需要一个路径}"; shift 2 ;;
        --dry-run)    DRY_RUN=1; shift ;;
        -h|--help)    awk 'NR>1 && /^#/ { sub(/^# ?/, ""); print; next } NR>1 { exit }' "${BASH_SOURCE[0]}"; exit 0 ;;
        *)            echo "未知选项：$1（-h 看用法）" >&2; exit 2 ;;
    esac
done

die()  { printf '错误：%s\n' "$*" >&2; exit 1; }
warn() { printf '警告：%s\n' "$*" >&2; }
# 中文标签按「显示列宽」对齐：CJK 每字占 2 列，printf %-Ns 按字符数补空格会错位
display_width() {
    local s="$1" chars="${#1}" bytes
    bytes="$(LC_ALL=C; printf '%s' "$s" | wc -c)"
    printf '%s' $(( chars + (bytes - chars) / 2 ))
}
info() {
    local label="$1" msg="$2" pad
    pad=$(( 10 - $(display_width "$label") ))
    ((pad < 1)) && pad=1
    printf '[stage] %s%*s%s\n' "$label" "$pad" '' "$msg"
}

abspath() {
    local p="$1"
    case "$p" in
        /*) ;;
        *)  p="$PWD/$p" ;;
    esac
    # 顺手 normalize："//" / "/./" / 末尾 "/." / 末尾 "/"，让摘要里不出现 /home/x/./out 这种
    while [[ "$p" =~ // ]]; do p="${p//\/\//\/}"; done
    while [[ "$p" == *"/./"* ]]; do p="${p//\/.\//\/}"; done
    [[ "$p" == *"/." ]] && p="${p%/.}"
    [[ "$p" == "/" ]] || p="${p%/}"
    printf '%s' "$p"
}

# ---------- 参数校验 ----------
[[ -n "$SCENE" ]] || die "必须指定 -s/--scene（如 res://BangDreamInSpire/scenes/vfx/staff_ring_vfx.tscn）"
[[ "$SCENE" == res://* ]] || die "-s/--scene 需要 res:// 路径，收到：$SCENE"
[[ "$SCENE" == *.tscn ]] || warn "场景路径不以 .tscn 结尾：$SCENE"
[[ "$FRAMES" =~ ^[0-9]+$ ]] || die "-n/--frames 需要正整数，收到：$FRAMES"
((FRAMES > 0)) || die "-n/--frames 必须大于 0"
[[ "$FPS" =~ ^[0-9]+$ ]] || die "-f/--fps 需要正整数（Godot 的 --fixed-fps 只接受整数帧率），收到：$FPS"
((FPS > 0)) || die "-f/--fps 必须大于 0"

# ---------- 仓库与工程路径（照 record.sh：从脚本自身位置回推） ----------
REPO="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../../.." && pwd)"
PROJECT_DIR="$REPO/BangDreamInSpire"
CSPROJ="$PROJECT_DIR/BangDreamInSpire.csproj"
SCRIPTS_DIR="$REPO/.agents/skills/vfx-authoring/scripts"
RECORD="$SCRIPTS_DIR/record.sh"
VERIFY="$SCRIPTS_DIR/verify_frames.py"

[[ -d "$PROJECT_DIR" ]] || die "找不到工程目录：$PROJECT_DIR"
[[ -f "$CSPROJ" ]] || die "找不到工程文件：$CSPROJ"
[[ -f "$RECORD" ]] || die "找不到 record.sh：$RECORD"
[[ -f "$VERIFY" ]] || die "找不到 verify_frames.py：$VERIFY"

SCENE_FILE="$PROJECT_DIR/${SCENE#res://}"
[[ -f "$SCENE_FILE" ]] || die "场景文件不存在：$SCENE_FILE
       用 ls BangDreamInSpire/BangDreamInSpire/scenes/vfx/ 看实际文件名（--scene 写的是 res:// 后的路径）"

# 场景名规范化成安全文件名：basename 去扩展名，非 [A-Za-z0-9._-] 一律换成 -
SLUG="$(basename -- "$SCENE" .tscn)"
SLUG="${SLUG//[^A-Za-z0-9._-]/-}"
[[ -n "$SLUG" ]] || die "无法从场景路径推出文件名：$SCENE"
STAMP="$(date +%Y%m%d-%H%M%S)"

# ---------- Godot 路径（--godot > $GODOT_EXE > $GODOT > props 的 <GodotExe> > PATH） ----------
resolve_godot() {
    if [[ -n "$GODOT_ARG" ]]; then printf '%s' "$GODOT_ARG"; return 0; fi
    if [[ -n "${GODOT_EXE:-}" ]]; then printf '%s' "$GODOT_EXE"; return 0; fi
    if [[ -n "${GODOT:-}" ]]; then printf '%s' "$GODOT"; return 0; fi
    # 与 docs §3.1 一致：单属性查询直接打印值（会自动带上 local props 与环境变量的覆盖结果）
    if command -v dotnet >/dev/null 2>&1 && [[ -f "$CSPROJ" ]]; then
        local from_msbuild
        from_msbuild="$(dotnet msbuild "$CSPROJ" -getProperty:GodotExe 2>/dev/null | tail -n1 | tr -d '\r' || true)"
        if [[ -n "$from_msbuild" && "$from_msbuild" != *'$('* ]]; then
            printf '%s' "$from_msbuild"; return 0
        fi
    fi
    # 兜底：直接读 props 文本（允许属性带 Condition="..."）
    if [[ -f "$REPO/Directory.Build.props" ]]; then
        local from_props
        from_props="$(sed -n 's:.*<GodotExe[^>]*>\(.*\)</GodotExe>.*:\1:p' "$REPO/Directory.Build.props" | head -n1)"
        if [[ -n "$from_props" ]]; then printf '%s' "$from_props"; return 0; fi
    fi
    if command -v godot >/dev/null 2>&1; then command -v godot; return 0; fi
    return 1
}
GODOT="$(resolve_godot || true)"
if [[ -n "$GODOT" && ! -x "$GODOT" ]]; then
    # props 里的路径可能存在但不可执行（或已挪走）：只有在 PATH 里真找到才降级
    if command -v godot >/dev/null 2>&1; then
        warn "--godot/Directory.Build.props 指向的 Godot 不可执行（$GODOT），改用 PATH 里的 $(command -v godot)"
        GODOT="$(command -v godot)"
    else
        GODOT=""
    fi
fi

# ---------- 输出目录 ----------
if [[ -n "$OUT" ]]; then
    OUT="$(abspath "$OUT")"
else
    OUT="$REPO/.scratch/vfx/stage/$SLUG-$STAMP"
fi

# ---------- --compare 上一轮目录 ----------
PREV_FRAMES=""
if [[ -n "$COMPARE" ]]; then
    PREV_DIR="$(abspath "$COMPARE")"
    [[ "$PREV_DIR" == /* ]] || PREV_DIR="$PWD/$PREV_DIR"
    [[ -d "$PREV_DIR" ]] || die "--compare 目录不存在：$PREV_DIR"
    if [[ -d "$PREV_DIR/crop" ]]; then
        PREV_FRAMES="$PREV_DIR/crop"
    elif [[ -d "$PREV_DIR/rec" ]]; then
        PREV_FRAMES="$PREV_DIR/rec"
    else
        die "--compare 目录里既没有 crop/ 也没有 rec/：$PREV_DIR"
    fi
    PREV_COUNT="$(find "$PREV_FRAMES" -maxdepth 1 -name 'frame*.png' 2>/dev/null | wc -l || true)"
    ((PREV_COUNT > 0)) || die "$PREV_FRAMES 里没有 frame*.png，没法对比"
fi

# ---------- 依赖检查（缺了给可执行提示，不静默降级） ----------
MISSING=()
require_cmd() { command -v "$1" >/dev/null 2>&1 || MISSING+=("$1：$2"); }
# dotnet 只在真的要编译、或要靠它查 GodotExe 时才必需（--no-build + 已解析到 Godot 时用不到）
if ((DO_BUILD)) || [[ -z "$GODOT" ]]; then
    require_cmd dotnet "编译与 dotnet msbuild 查询都需要它（装 .NET SDK；只想跳过编译可加 --no-build 并用 --godot 指定 Godot）"
elif ! command -v dotnet >/dev/null 2>&1; then
    info "dotnet" "未安装，但 --no-build 且 Godot 已解析，本轮不需要它"
fi
require_cmd ffmpeg "裁帧需要它（apt install ffmpeg / dnf install ffmpeg）"
require_cmd python3 "生成接触表与对比图需要它"
if command -v python3 >/dev/null 2>&1; then
    python3 -c 'from PIL import Image, ImageDraw, ImageFont' >/dev/null 2>&1 \
        || MISSING+=("Pillow：python3 缺 Pillow（pip install Pillow）")
fi
if ((${#MISSING[@]})); then
    printf '错误：依赖不完整，先补上：\n' >&2
    for item in "${MISSING[@]}"; do printf '  - %s\n' "$item" >&2; done
    if ((DRY_RUN)); then warn "--dry-run 仍继续（只打印命令），真正运行会失败"
    else exit 1; fi
fi
if [[ -z "$GODOT" ]]; then
    if ((DRY_RUN)); then
        warn "找不到 Godot（--godot / Directory.Build.props 的 <GodotExe> / PATH 都没有），真正运行会在录制阶段失败"
    else
        die "找不到 Godot（用 --godot 指定，或在 Directory.Build.props 配 <GodotExe>）"
    fi
fi

# ---------- 中文字体（接触表/对比图上的标签要写中文；找不到就退 ASCII，绝不画豆腐块） ----------
CJK_FONT=""
for candidate in \
    /usr/share/fonts/noto-cjk/NotoSansCJK-Bold.ttc \
    /usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc \
    /usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc \
    /usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc \
    /usr/share/fonts/truetype/wqy/wqy-zenhei.ttc \
    /usr/share/fonts/wenquanyi/wqy-zenhei/wqy-zenhei.ttc \
    /usr/share/fonts/sarasa-gothic/Sarasa-Bold.ttc; do
    if [[ -f "$candidate" ]]; then
        CJK_FONT="$candidate"
        break
    fi
done
if [[ -n "$CJK_FONT" ]]; then
    info "字体" "$CJK_FONT"
else
    warn "没找到中文字体（Noto CJK / wqy / Sarasa 都没有），图上的标签改用 ASCII"
fi

# ---------- build 用的 Sts2DataDir（文档 §5：从 props 查，不猜路径） ----------
STS2_DATA=""
if command -v dotnet >/dev/null 2>&1 && ((DO_BUILD)); then
    STS2_DATA="$(dotnet msbuild "$CSPROJ" -getProperty:Sts2DataDir 2>/dev/null | tail -n1 | tr -d '\r' || true)"
    [[ -n "$STS2_DATA" ]] || die "dotnet msbuild -getProperty:Sts2DataDir 没返回路径（检查 Directory.Build.props / Sts2Dir）"
fi

build_cmd_display() {
    printf 'dotnet build "%s" -c Debug -p:Sts2Dir="%s" -p:Sts2DataDir="%s"' \
        "$CSPROJ" "$REPO/bin/BuildDeploy" "${STS2_DATA:-<Sts2DataDir>}"
}

# ---------- --dry-run：只打印 ----------
if ((DRY_RUN)); then
    info "仓库"     "$REPO"
    info "工程"     "$PROJECT_DIR"
    info "场景"     "$SCENE"
    info "场景文件" "$SCENE_FILE"
    info "文件名"   "$SLUG（输出目录名用）"
    info "帧数"     "$FRAMES @ $FPS fps（约 $(awk "BEGIN{printf \"%.3f\", $FRAMES/$FPS}") 秒）"
    info "裁切"     "crop=$CROP"
    info "Godot"    "${GODOT:-<未找到>}"
    info "输出"     "$OUT"
    info "对比"     "${PREV_FRAMES:-（未指定 --compare）}"
    info "编译"     "$( ((DO_BUILD)) && build_cmd_display || printf '%s' '跳过（--no-build）' )"
    echo ""
    echo "[stage] 将要执行的命令（未执行）："
    if ((DO_BUILD)); then
        echo "  1) $(build_cmd_display)"
        echo "     # 日志 → $OUT/build.log"
    else
        echo "  1) （跳过编译）"
    fi
    echo "  2) bash $RECORD -s \"$SCENE\" -n $FRAMES -f $FPS -o \"$OUT/rec/$SLUG.mp4\"${GODOT:+ --godot \"$GODOT\"}"
    echo "     # record.sh 会把 PNG 序列写进 $REPO/.scratch/vfx/rec/$SLUG-<它自己的时间戳>/"
    echo "     # 之后本脚本把帧移进 $OUT/rec/"
    echo "  3) ffmpeg -y -loglevel error -framerate $FPS -i \"$OUT/rec/frame%08d.png\" -vf \"crop=$CROP\" \"$OUT/crop/frame%08d.png\""
    echo "     # 帧号不连续时退化为逐帧裁切"
    echo "  4) python3 <内联 Pillow 脚本> → $OUT/sheet.png（9 帧等间隔 3x3，每格写 \"#帧号  t=秒\"）"
    echo "  5) python3 $VERIFY \"$OUT/rec\" --fps $FPS"
    if [[ -n "$PREV_FRAMES" ]]; then
        echo "  6) python3 <内联 Pillow 脚本> 对比 $PREV_FRAMES → $OUT/compare_table.txt + $OUT/compare.png"
    fi
    echo ""
    echo "[stage] --dry-run 结束：没有 build、没有录制、没有写任何文件。"
    exit 0
fi

# ---------- 真正执行 ----------
mkdir -p -- "$OUT/rec" "$OUT/crop"
info "输出" "$OUT"

# 1) build
if ((DO_BUILD)); then
    info "编译" "$CSPROJ"
    # stderr 里的 error CS 行必须原样透出：整段 tee 进日志并直接显示，失败时再摘一次错误行
    if ! dotnet build "$CSPROJ" -c Debug \
        -p:Sts2Dir="$REPO/bin/BuildDeploy" \
        -p:Sts2DataDir="$STS2_DATA" 2>&1 | tee "$OUT/build.log"; then
        printf '\n' >&2
        grep -E ': error |error (CS|MSB|NETSDK|NU)[0-9]+' "$OUT/build.log" >&2 || true
        die "本轮这一层的编译没过，先修代码（完整日志：$OUT/build.log）"
    fi
else
    info "编译" "跳过（--no-build）"
fi

# 2) 录制（复用 record.sh；record.sh 自己决定帧目录名，所以先快照再 diff 出本次目录）
info "录制" "$SCENE（$FRAMES 帧 @ $FPS fps）"
REC_ROOT="$REPO/.scratch/vfx/rec"
BEFORE_FILE="$OUT/.rec_dirs_before"
find "$REC_ROOT" -maxdepth 1 -mindepth 1 -type d -name "$SLUG-*" 2>/dev/null | sort > "$BEFORE_FILE" || true

RECORD_ARGS=(-s "$SCENE" -n "$FRAMES" -f "$FPS" -o "$OUT/rec/$SLUG.mp4")
if [[ -n "$GODOT" ]]; then RECORD_ARGS+=(--godot "$GODOT"); fi
if ! bash "$RECORD" "${RECORD_ARGS[@]}"; then
    die "record.sh 录制失败（Godot 启动/渲染问题）。先单独跑一遍看它自己的报错"
fi

AFTER_FILE="$OUT/.rec_dirs_after"
find "$REC_ROOT" -maxdepth 1 -mindepth 1 -type d -name "$SLUG-*" 2>/dev/null | sort > "$AFTER_FILE" || true
REC_DIR="$(comm -13 "$BEFORE_FILE" "$AFTER_FILE" | head -n1 || true)"
if [[ -z "$REC_DIR" ]]; then
    REC_DIR="$(ls -dt "$REC_ROOT/$SLUG"-* 2>/dev/null | head -n1 || true)"
    [[ -n "$REC_DIR" ]] && warn "没识别出本次录制目录，按最新目录取：$REC_DIR"
fi
rm -f -- "$BEFORE_FILE" "$AFTER_FILE"
[[ -n "$REC_DIR" && -d "$REC_DIR" ]] || die "record.sh 跑完了，但找不到它的帧目录（应在 $REC_ROOT）"

# 把 record.sh 的产物搬进 <out>/rec/（帧名 frame%08d.png 保持不变，供后面的 ffmpeg/verify 复用）
shopt -s nullglob
moved=("$REC_DIR"/frame*.png)
shopt -u nullglob
((${#moved[@]})) || die "record.sh 的帧目录里没有 frame*.png：$REC_DIR"
mv -- "${moved[@]}" "$OUT/rec/"
for extra in frame.wav godot.log palette.png; do
    if [[ -f "$REC_DIR/$extra" ]]; then mv -- "$REC_DIR/$extra" "$OUT/rec/"; fi
done
rmdir -- "$REC_DIR" 2>/dev/null || true

FRAME_COUNT="$(find "$OUT/rec" -maxdepth 1 -name 'frame*.png' 2>/dev/null | wc -l || true)"
((FRAME_COUNT > 0)) || die "搬帧后 $OUT/rec 里没有 PNG"
info "帧数" "$FRAME_COUNT（请求 $FRAMES）"
((FRAME_COUNT == FRAMES)) || warn "实际帧数 $FRAME_COUNT 与请求的 $FRAMES 不一致（场景可能提前自毁/退出）"
[[ -f "$OUT/rec/$SLUG.mp4" ]] || warn "没有看到 mp4：$OUT/rec/$SLUG.mp4（record.sh 合成阶段可能跳过了）"

# 3) 裁中心
info "裁切" "crop=$CROP"
if ! ffmpeg -y -loglevel error -framerate "$FPS" -i "$OUT/rec/frame%08d.png" \
    -vf "crop=$CROP" "$OUT/crop/frame%08d.png"; then
    die "ffmpeg 裁切失败：确认 --crop \"$CROP\" 落在画面尺寸内（默认 760:760:580:160 对应 1920x1080）"
fi
CROP_COUNT="$(find "$OUT/crop" -maxdepth 1 -name 'frame*.png' 2>/dev/null | wc -l || true)"
if ((CROP_COUNT != FRAME_COUNT)); then
    warn "批量裁切只出 $CROP_COUNT/$FRAME_COUNT 帧（帧号不连续），改用逐帧裁切"
    rm -f -- "$OUT"/crop/frame*.png
    while IFS= read -r src; do
        ffmpeg -y -loglevel error -i "$src" -vf "crop=$CROP" "$OUT/crop/$(basename -- "$src")" \
            || die "裁切失败：$src"
    done < <(find "$OUT/rec" -maxdepth 1 -name 'frame*.png' | sort)
    CROP_COUNT="$(find "$OUT/crop" -maxdepth 1 -name 'frame*.png' 2>/dev/null | wc -l || true)"
fi
((CROP_COUNT > 0)) || die "裁切后 $OUT/crop 里没有 PNG"
info "裁切帧" "$CROP_COUNT"

# 4) 3x3 接触表（python3 + Pillow；纯内联，不落第二个脚本文件）
SRC_CROP="$OUT/crop" SHEET_OUT="$OUT/sheet.png" SHEET_FPS="$FPS" STAGE_CJK_FONT="$CJK_FONT" python3 - <<'PY'
import os
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

src = Path(os.environ["SRC_CROP"])
dst = Path(os.environ["SHEET_OUT"])
fps = float(os.environ["SHEET_FPS"])

CELL = 360          # 单格边长：3x3 → 1080x1080，够看清节奏又不至于太大
COLS, ROWS = 3, 3
SLOTS = COLS * ROWS

frames = sorted(src.glob("frame*.png"))
if not frames:
    sys.exit("没有可用的裁切帧")
last = len(frames) - 1
picks = [frames[round(i * last / (SLOTS - 1))] for i in range(SLOTS)]


def load_font(size: int):
    cjk = os.environ.get("STAGE_CJK_FONT", "")
    for path in ((cjk,) if cjk else ()) + (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/liberation/LiberationSans-Bold.ttf",
    ):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    try:
        return ImageFont.load_default(size=size)
    except TypeError:          # Pillow < 9.2 没有 size 参数
        return ImageFont.load_default()


def frame_index(path: Path, fallback: int) -> int:
    digits = "".join(ch for ch in path.stem if ch.isdigit())
    return int(digits) if digits else fallback


font = load_font(20)
canvas = Image.new("RGB", (COLS * CELL, ROWS * CELL), (24, 24, 24))
draw = ImageDraw.Draw(canvas)

for slot, path in enumerate(picks):
    with Image.open(path) as image:
        tile = image.convert("RGB").resize((CELL, CELL), Image.LANCZOS)
    x = (slot % COLS) * CELL
    y = (slot // COLS) * CELL
    canvas.paste(tile, (x, y))
    idx = frame_index(path, slot)
    draw.text((x + 6, y + 4), f"#{idx}  t={idx / fps:.3f}s",
              font=font, fill="white", stroke_width=2, stroke_fill="black")

canvas.save(dst)
PY
info "接触表" "$OUT/sheet.png（9 格 / 共 $CROP_COUNT 帧，等间隔）"

# 5) 机器校验（复用 verify_frames.py；退出码非 0 只记录不中断——逐层阶段常常只有 1 层、画面很淡）
info "校验" "$VERIFY"
if python3 "$VERIFY" "$OUT/rec" --fps "$FPS" | tee "$OUT/verify.txt"; then
    VERIFY_EXIT=0
else
    VERIFY_EXIT=$?
fi
if ((VERIFY_EXIT != 0)); then
    warn "verify_frames.py 退出码 $VERIFY_EXIT —— 逐层阶段不视为失败（层数少/画面淡很常见）；完整输出见 $OUT/verify.txt"
fi

# 6) 与上一轮对比（可选）
COMPARE_SUMMARY="（未指定 --compare，本轮是第一轮）"
if [[ -n "$PREV_FRAMES" ]]; then
    info "对比" "上一轮 $PREV_FRAMES"
    PREV_DIR_FRAMES="$PREV_FRAMES" CUR_DIR_FRAMES="$OUT/crop" CMP_PNG="$OUT/compare.png" \
        CMP_FPS="$FPS" CMP_TABLE="$OUT/compare_table.txt" STAGE_CJK_FONT="$CJK_FONT" python3 - <<'PY'
import os
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat

prev_dir = Path(os.environ["PREV_DIR_FRAMES"])
cur_dir = Path(os.environ["CUR_DIR_FRAMES"])
png = Path(os.environ["CMP_PNG"])
fps = float(os.environ["CMP_FPS"])
table_path = Path(os.environ["CMP_TABLE"])

CELL = 360
THRESHOLD = 2.0          # 平均绝对亮度差（灰阶 0..255）超过它才算「肉眼可见」

prev_frames = sorted(prev_dir.glob("frame*.png"))
cur_frames = sorted(cur_dir.glob("frame*.png"))
if not prev_frames:
    sys.exit(f"上一轮目录里没有 frame*.png：{prev_dir}")
if not cur_frames:
    sys.exit(f"本轮没有 crop 帧：{cur_dir}")


def frame_index(path: Path, fallback: int) -> int:
    digits = "".join(ch for ch in path.stem if ch.isdigit())
    return int(digits) if digits else fallback


def gray(path: Path, size: tuple[int, int] | None = None) -> Image.Image:
    with Image.open(path) as image:
        out = image.convert("L")
        if size is not None and out.size != size:
            out = out.resize(size, Image.LANCZOS)
        # 有些 PNG 带调色板/alpha，convert("L") 后仍需保证可逐像素比较
        return out.copy()


pairs = list(zip(prev_frames, cur_frames))
size = gray(cur_frames[0]).size

rows: list[tuple[int, float, float]] = []
for position, (pf, cf) in enumerate(pairs):
    a = gray(pf, size)
    b = gray(cf, size)
    # ImageChops.difference 是 C 实现，比逐像素 Python 循环快两个数量级；
    # ImageStat.mean 给的就是平均绝对亮度差（灰阶 0..255）
    diff = ImageStat.Stat(ImageChops.difference(a, b)).mean[0]
    rows.append((frame_index(cf, position), frame_index(cf, position) / fps, diff))

lines: list[str] = []
lines.append(f"[compare] 上一轮   {prev_dir}（{len(prev_frames)} 帧）")
lines.append(f"[compare] 本轮     {cur_dir}（{len(cur_frames)} 帧）")
lines.append(f"[compare] 可比帧数 {len(rows)}（取两边较少的一个）")
if len(prev_frames) != len(cur_frames):
    lines.append(f"[compare] 注意     帧数不同，只比较前 {len(rows)} 帧")
lines.append(f"[compare] 判定阈值 {THRESHOLD:.1f} 灰阶（平均绝对亮度差）")
lines.append("")
lines.append(" 帧     t(s)     差值")
lines.append(" ----  -----   -------")

onset = None
peak_idx, peak_t, peak_diff = rows[0] if rows else (0, 0.0, 0.0)
above = 0
for idx, t, diff in rows:
    marked = diff >= THRESHOLD
    if marked:
        above += 1
        if onset is None:
            onset = (idx, t)
    if diff > peak_diff:
        peak_idx, peak_t, peak_diff = idx, t, diff
    lines.append(f" {idx:>4}  {t:6.3f}   {diff:7.3f} {'*' if marked else ' '}")

lines.append("")
lines.append(f"[compare] 最大差值 第 {peak_idx} 帧（t={peak_t:.3f}s）差值 {peak_diff:.3f}")
lines.append(f"[compare] 超阈值帧 {above}/{len(rows)}")

if onset is not None:
    lines.append(f"[compare] 新增可见变化 第 {onset[0]} 帧（t={onset[1]:.3f}s）起首次越过阈值，此前各帧都在阈值以下")
    lines.append(f"[compare] 结论  本轮新增的可见变化出现在第 {onset[0]} 帧（t={onset[1]:.3f}s）之后")
elif peak_diff > 0.05:
    lines.append("[compare] 新增可见变化 没有一帧越过阈值，但差值非零：属于亚阈值差异（抗锯齿/抖动/极淡元素）")
    lines.append("[compare] 结论  本轮与上一轮肉眼基本一致，没有新增可见层")
else:
    lines.append("[compare] 新增可见变化 全帧差值 ≈ 0")
    lines.append("[compare] 结论  两轮画面完全一致（本轮这一层没有生效，或渲染没有变化）")

# 三行对照图：左=上一轮，右=本轮；位置取 0% / 50% / 100%
positions = [0, len(rows) // 2, len(rows) - 1] if rows else []
canvas = Image.new("RGB", (2 * CELL, 3 * CELL), (24, 24, 24))


def load_font(size: int):
    cjk = os.environ.get("STAGE_CJK_FONT", "")
    for path in ((cjk,) if cjk else ()) + (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/liberation/LiberationSans-Bold.ttf",
    ):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


font = load_font(18)
draw = ImageDraw.Draw(canvas)
# 有中文字体就用中文标签，否则退 ASCII（避免豆腐块）
prev_tag, cur_tag = ("上一轮", "本轮") if os.environ.get("STAGE_CJK_FONT") else ("PREV", "CUR")
for row, position in enumerate(positions):
    pf = prev_frames[position]
    cf = cur_frames[position]
    for col, (path, tag) in enumerate(((pf, prev_tag), (cf, cur_tag))):
        with Image.open(path) as image:
            tile = image.convert("RGB").resize((CELL, CELL), Image.LANCZOS)
        x = col * CELL
        y = row * CELL
        canvas.paste(tile, (x, y))
        idx = frame_index(path, position)
        draw.text((x + 6, y + 4), f"{tag} #{idx}  t={idx / fps:.3f}s",
                  font=font, fill="white", stroke_width=2, stroke_fill="black")
canvas.save(png)

text = "\n".join(lines)
table_path.write_text(text + "\n", encoding="utf-8")
print(text)
print(f"[compare] 对照图    {png}")
PY
    COMPARE_SUMMARY="$(grep -m1 '^\[compare\] 结论' "$OUT/compare_table.txt" | sed 's/^\[compare\] 结论[[:space:]]*//' || true)"
fi

# 7) 摘要
PEAK_LINE="$(grep -m1 '峰值' "$OUT/verify.txt" | sed 's/^\[verify\][[:space:]]*//; s/^峰值[[:space:]]*//' || true)"
if [[ -z "$PEAK_LINE" ]]; then
    PEAK_LINE="无（verify 判定全部帧为空白：先确认画面里真的有东西）"
fi

echo ""
echo "[stage] ────────── 摘要 ──────────"
info "输出目录" "$OUT"
info "帧数"     "$FRAME_COUNT 帧 @ $FPS fps（约 $(awk "BEGIN{printf \"%.3f\", $FRAME_COUNT/$FPS}") 秒）"
info "峰值"     "$PEAK_LINE"
info "接触表"   "$OUT/sheet.png"
info "录制"     "$OUT/rec/（PNG 序列 + $SLUG.mp4）"
info "裁切"     "$OUT/crop/（crop=$CROP）"
info "校验"     "exit=$VERIFY_EXIT（$OUT/verify.txt）"
if [[ -n "$PREV_FRAMES" ]]; then
    info "对比图" "$OUT/compare.png"
    info "对比表" "$OUT/compare_table.txt（$COMPARE_SUMMARY）"
fi
if ((VERIFY_EXIT == 1)); then
    info "下一步" "画面是空的：先排除「脚本没挂上/场景没渲染」，再谈观感（看 $OUT/rec/godot.log 与 sheet.png）"
elif [[ -n "$PREV_FRAMES" ]]; then
    info "下一步" "只盯 compare.png 里本轮新增的那一层，别重新审一遍旧层；确认真好了再加下一层并 --compare \"$OUT\""
else
    info "下一步" "先看 sheet.png：第 1 层是否按时出现、按时消失；确认后加第 2 层，再以 --compare \"$OUT\" 跑一次"
fi
echo ""
