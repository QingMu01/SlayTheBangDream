#!/usr/bin/env bash
#
# record.sh —— 录帧核对：把特效场景渲染成 PNG 序列并合成 mp4 / gif。
#
# 调观感的唯一可靠手段。--headless 只能验「有没有报错」，验不了「看起来对不对」。
# 本脚本刻意【不】使用 --headless：headless 无渲染，录不到画面。
#
# 用法：
#   bash .agents/skills/vfx-authoring/scripts/record.sh -s "res://BangDreamLib/scenes/vfx/staff_ring_vfx.tscn"
#   bash .agents/skills/vfx-authoring/scripts/record.sh -s "<场景>" -n 130 -c "760:760:580:160" --gif
#
# 选项：
#   -s, --scene   场景 res:// 路径（必填）
#   -n, --frames  帧数，默认 130
#   -f, --fps     帧率，默认 60
#   -o, --out     输出 mp4 路径，默认 .vfx_staging/out/<场景名>.mp4
#   -c, --crop    ffmpeg crop 参数，如 "760:760:580:160"
#       --gif     额外合成 gif
#       --godot   Godot 可执行文件路径；默认读 Directory.Build.props 的 <GodotExe>
#       --encoder 视频编码器；默认按 libx264 → libopenh264 → mpeg4 挑第一个可用的
#
set -euo pipefail

SCENE=""
FRAMES=130
FPS=60
OUT=""
CROP=""
GIF=0
GODOT=""
ENCODER=""

while (($#)); do
    case "$1" in
        -s|--scene)  SCENE="${2:?--scene 需要一个参数}"; shift 2 ;;
        -n|--frames) FRAMES="${2:?}"; shift 2 ;;
        -f|--fps)    FPS="${2:?}"; shift 2 ;;
        -o|--out)    OUT="${2:?}"; shift 2 ;;
        -c|--crop)   CROP="${2:?}"; shift 2 ;;
        --gif)       GIF=1; shift ;;
        --godot)     GODOT="${2:?}"; shift 2 ;;
        --encoder)   ENCODER="${2:?}"; shift 2 ;;
        -h|--help)   sed -n '2,21p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "未知选项：$1" >&2; exit 2 ;;
    esac
done

die() { printf '错误：%s\n' "$*" >&2; exit 1; }
[[ -n "$SCENE" ]] || die "必须指定 -s/--scene"

REPO="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../../.." && pwd)"
PROJECT_DIR="$REPO/BangDreamLib"
[[ -d "$PROJECT_DIR" ]] || die "找不到工程目录：$PROJECT_DIR"

# Godot 路径：--godot > $GODOT 环境变量 > Directory.Build.props 的 <GodotExe> > PATH
if [[ -z "$GODOT" ]]; then
    GODOT="${GODOT:-}"
fi
if [[ -z "$GODOT" && -f "$REPO/Directory.Build.props" ]]; then
    GODOT="$(sed -n 's:.*<GodotExe>\(.*\)</GodotExe>.*:\1:p' "$REPO/Directory.Build.props" | head -n1)"
fi
if [[ -z "$GODOT" ]]; then
    GODOT="$(command -v godot || true)"
fi
[[ -n "$GODOT" && -x "$GODOT" ]] || die "找不到 Godot（用 --godot 指定，或在 Directory.Build.props 配 <GodotExe>）"

SLUG="$(basename "$SCENE" .tscn)"
STAMP="$(date +%Y%m%d-%H%M%S)"
REC="$REPO/.vfx_staging/rec/$SLUG-$STAMP"
mkdir -p "$REC"

echo "[record] 工程   $PROJECT_DIR"
echo "[record] 场景   $SCENE"
echo "[record] 帧数   $FRAMES @ $FPS fps（约 $(awk "BEGIN{printf \"%.3f\", $FRAMES/$FPS}") 秒）"
echo "[record] Godot  $GODOT"
echo "[record] 输出   $REC"

# 注意：--write-movie 与 --headless 不能同时使用。
# --log-file 指向工作区，绕开受限环境写不了 Godot 日志目录的失败。
set +e
"$GODOT" \
    --path "$PROJECT_DIR" \
    --write-movie "$REC/frame.png" \
    --fixed-fps "$FPS" \
    --quit-after "$FRAMES" \
    --log-file "$REC/godot.log" \
    "$SCENE"
GODOT_EXIT=$?
set -e

FRAME_COUNT=$(find "$REC" -maxdepth 1 -name 'frame*.png' | wc -l)
echo "[record] Godot 退出码 $GODOT_EXIT，产出 $FRAME_COUNT 帧"

if ((FRAME_COUNT == 0)); then
    echo "[record] 没有产出任何帧。检查 $REC/godot.log" >&2
    [[ -f "$REC/godot.log" ]] && tail -n 40 "$REC/godot.log" >&2 || true
    exit 1
fi
if ((FRAME_COUNT != FRAMES)); then
    echo "[record] 警告：帧数 $FRAME_COUNT 与请求的 $FRAMES 不一致（场景可能提前自毁或退出）" >&2
fi

command -v ffmpeg >/dev/null || die "PATH 里找不到 ffmpeg"

# 挑一个真实存在的视频编码器：libx264 最通用，但部分发行版（如 Fedora 的 ffmpeg）不打包它
pick_encoder() {
    local e
    for e in ${1:-libx264 libopenh264 mpeg4}; do
        if ffmpeg -hide_banner -encoders 2>/dev/null | awk -v e="$e" '$2 == e { found = 1 } END { exit !found }'; then
            printf '%s' "$e"
            return 0
        fi
    done
    return 1
}
ENC="$(pick_encoder "$ENCODER")" || die "ffmpeg 里找不到可用的 H.264 编码器（可用 --encoder 指定）"
case "$ENC" in
    libx264)     VOPTS=(-crf 18) ;;
    libopenh264) VOPTS=(-b:v 20M) ;;
    mpeg4)       VOPTS=(-qscale:v 3) ;;
    *)           VOPTS=() ;;
esac

mkdir -p "$REPO/.vfx_staging/out"
[[ -n "$OUT" ]] || OUT="$REPO/.vfx_staging/out/$SLUG.mp4"

ENCODE=(-y -loglevel error -framerate "$FPS" -i "$REC/frame%08d.png")
if [[ -n "$CROP" ]]; then
    ENCODE+=(-vf "crop=$CROP")
fi
ENCODE+=(-c:v "$ENC" "${VOPTS[@]}" -pix_fmt yuv420p "$OUT")
ffmpeg "${ENCODE[@]}" || die "ffmpeg 合成 mp4 失败"
echo "[record] mp4    $OUT（编码器 $ENC）"

if ((GIF)); then
    GIF_PATH="${OUT%.*}.gif"
    PALETTE="$REC/palette.png"
    CHAIN="fps=30,scale=720:-1:flags=lanczos"
    ffmpeg -y -loglevel error -framerate "$FPS" -i "$REC/frame%08d.png" \
        -vf "$CHAIN,palettegen" "$PALETTE"
    ffmpeg -y -loglevel error -framerate "$FPS" -i "$REC/frame%08d.png" -i "$PALETTE" \
        -lavfi "$CHAIN[x];[x][1:v]paletteuse" "$GIF_PATH"
    echo "[record] gif    $GIF_PATH"
fi

echo ""
echo "[record] 下一步：抽帧目视 + 机器校验"
echo "  python .agents/skills/vfx-authoring/scripts/verify_frames.py \"$REC\" --fps $FPS"
