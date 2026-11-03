#!/usr/bin/env bash
#
# perf-linux-ab.sh —— Linux present 路径 A/B 对照实验启动器（《杀戮尖塔 2》）。
#
# 目的：把「换一个环境变量 / 换一个合成器路径」变成一条可复现的命令：
#   脚本负责启动游戏 → 等它退出 → 找到本次新写出的 perf 报告（schema
#   bangdream.perf.v1）→ 复制归档 → 打印关键指标 → 往 results.md 追加一行。
#   这样跑 baseline / fifo / mailbox / immediate / wayland / gamescope 等模式时，
#   唯一的差异就是注入的 env 与参数，避免手工启动导致的流程不一致。
#
# 报告来源：<日志根目录>/bangdream/perf-*.json 与同名 .md；
#           归档目录：<日志根目录>/bangdream/ab/<mode>-<时间戳>/
#           结果表：  <日志根目录>/bangdream/ab/results.md
#
# 只在本机写文件（日志根目录），不删除任何东西、不修改游戏安装目录。
#
# 用法见 --help。自检入口：
#   bash -n tools/perf-linux-ab.sh
#   bash tools/perf-linux-ab.sh list
#   bash tools/perf-linux-ab.sh show fifo
#   bash tools/perf-linux-ab.sh run fifo --dry-run
#
set -euo pipefail

readonly SCRIPT_NAME="${0##*/}"
readonly STEAM_APPID=2868840
readonly GAME_BIN_NAME="SlayTheSpire2"
readonly DEFAULT_GAME_DIR="${HOME}/.local/share/Steam/steamapps/common/Slay the Spire 2"
readonly DEFAULT_LOGS_DIR="${HOME}/.local/share/SlayTheSpire2/logs"
readonly -a MODES=(baseline fifo mailbox immediate wayland gamescope mangohud nocache psocache)

# 运行参数（可被命令行覆盖）
GAME_DIR="$DEFAULT_GAME_DIR"
LOGS_DIR="$DEFAULT_LOGS_DIR"
SUBCOMMAND=""
MODE=""
DRY_RUN=0
FORCE=0
HELP=0

# 当前模式的解析结果（每次 load_mode 重填）
MODE_DESC=""
MODE_HINT=""
MODE_ENV=()
MODE_ARGS=()
MODE_WRAPPER=()

# 摘要 jq 程序（见 print_summary）
read -r -d '' JQ_SUMMARY <<'JQ' || true
"session_ms: \(.session_ms // 0)",
"spike_count: \(.frames.spike_count // 0)",
"p50/p95/p99 (ms): \(.frames.p50_ms // 0) / \(.frames.p95_ms // 0) / \(.frames.p99_ms // 0)",
"max_ms: \(.frames.max_ms // 0)   frames.total: \(.frames.total // 0)   avg_ms: \(.frames.avg_ms // 0)",
"",
"各 phase（frames / avg_ms / max_ms）：",
( (.frames.phases // {}) | to_entries[] | "  \(.key): \(.value.frames // 0) / \(.value.avg_ms // 0) / \(.value.max_ms // 0)" ),
"",
"Top5 spike（按 ms 降序；at_ms / ms / present_wait_ms / render_submit_ms / phase）：",
( (.frames.spikes // [])
  | (if type == "array" then . else [] end)
  | sort_by(-(.ms // 0))
  | .[0:5][]
  | "  \(.at_ms // 0) / \(.ms // 0) / \(.present_wait_ms // 0) / \(.render_submit_ms // 0) / \(.phase_after // "?")" )
JQ

read -r -d '' JQ_VALUES <<'JQ' || true
[
  .session_ms // 0,
  .frames.spike_count // 0,
  .frames.p50_ms // 0,
  .frames.p95_ms // 0,
  .frames.p99_ms // 0,
  .frames.max_ms // 0,
  .frames.total // 0
] | @tsv
JQ

die() { printf '错误：%s\n' "$*" >&2; exit 1; }
warn() { printf '警告：%s\n' "$*" >&2; }
info() { printf '%s\n' "$*"; }

usage() {
    cat <<EOF
${SCRIPT_NAME} —— Linux present 路径 A/B 对照实验启动器

用法：
  ${SCRIPT_NAME} list
  ${SCRIPT_NAME} show <mode>
  ${SCRIPT_NAME} run <mode> [--dry-run] [--force] [--game-dir DIR] [--logs-dir DIR]
  ${SCRIPT_NAME} --help

子命令：
  list              列出全部模式（注入的 env / 附加参数 / 说明 / 判定提示）
  show <mode>       打印该模式说明、脚本直跑命令，以及可粘贴进 Steam
                    「属性 → 通用 → 启动选项」的 %command% 字符串
  run <mode>        真正启动游戏，等它退出后收集本次 perf 报告、打印摘要，
                    并把一行结果追加到 results.md
                    --dry-run：只打印完整命令，不启动游戏、不写任何文件

模式（run/show 的 <mode>）：
  baseline          不注入任何 env/参数，作为对照组
  fifo              MESA_VK_WSI_PRESENT_MODE=fifo（强制 Vulkan FIFO present）
  mailbox           MESA_VK_WSI_PRESENT_MODE=mailbox
  immediate         MESA_VK_WSI_PRESENT_MODE=immediate
  wayland           GODOT_WAYLAND_DISABLE_LIBDECOR=1 + --display-driver wayland
  gamescope         gamescope -W 1920 -H 1080 -f -r 90 -- <游戏>
  mangohud          MANGOHUD=1（只观测，不改变 present 路径）
  nocache           MESA_SHADER_CACHE_DISABLE=true（反向对照）
  psocache          RADV_DEBUG=psocachestats（只输出 PSO cache 统计）

选项：
  --dry-run         只打印命令：不启动游戏、不创建目录、不写文件
  --force           跳过「已有 ${GAME_BIN_NAME} 进程在运行」的预检（默认会拒绝，
                    以免两个实例抢同一份 perf 报告）
  --game-dir DIR    游戏安装目录（默认：${DEFAULT_GAME_DIR}）
  --logs-dir DIR    日志根目录，perf 报告位于 <DIR>/bangdream（默认：${DEFAULT_LOGS_DIR}）
  -h, --help        显示本帮助

输出（真跑时）：
  <logs-dir>/bangdream/ab/<mode>-<时间戳>/   复制的 perf-*.json / perf-*.md / meta.txt
  <logs-dir>/bangdream/ab/results.md          每个模式一行的累积结果表

说明：
  * 直接启动（不经 Steam 客户端）不会加载 Steam 覆盖层/Steam Input，日志里出现
    "Steamworks is not initialized" 属预期，不影响 perf 报告。
  * 游戏退出时写出的 perf 报告可能比进程退出晚几百毫秒，脚本会最多等 10 秒。
  * 找不到新报告时仍会保留 meta.txt，并以非零状态退出，方便批处理发现问题。
EOF
}

valid_mode() {
    local m
    for m in "${MODES[@]}"; do
        [[ "$m" == "$1" ]] && return 0
    done
    return 1
}

# 载入模式定义：说明 / 判定提示 / env 前缀 / 追加参数 / 包装命令
load_mode() {
    MODE_ENV=()
    MODE_ARGS=()
    MODE_WRAPPER=()
    MODE_DESC=""
    MODE_HINT=""
    case "$1" in
        baseline)
            MODE_DESC="不注入任何 env/参数（对照组）"
            MODE_HINT="基准。与 fifo/mailbox 对比时若 present_wait_ms 高而 render_submit_ms 低，说明引擎已提交、CPU 卡在 present 等待。"
            ;;
        fifo)
            MODE_ENV=(MESA_VK_WSI_PRESENT_MODE=fifo)
            MODE_DESC="强制 Vulkan FIFO present（等价传统 vsync）"
            MODE_HINT="帧率被 refresh 限制属预期；若长帧 spike 明显减少，说明默认 present 模式在排队上出问题。"
            ;;
        mailbox)
            MODE_ENV=(MESA_VK_WSI_PRESENT_MODE=mailbox)
            MODE_DESC="Vulkan mailbox（三缓冲/丢旧帧式 present）"
            MODE_HINT="理论上有更低阻塞；XWayland 下 mailbox 支持有限，可能被驱动回退，需看日志确认。"
            ;;
        immediate)
            MODE_ENV=(MESA_VK_WSI_PRESENT_MODE=immediate)
            MODE_DESC="Vulkan immediate（不排队、可能撕裂）"
            MODE_HINT="present_wait_ms 应显著下降；若 spike 随之消失但出现撕裂，说明阻塞来自 present 排队/等待。"
            ;;
        wayland)
            MODE_ENV=(GODOT_WAYLAND_DISABLE_LIBDECOR=1)
            MODE_ARGS=(--display-driver wayland)
            MODE_DESC="绕过 XWayland，直接走 Godot 的 Wayland 后端"
            MODE_HINT="若 spike 基本消失 → 根因在 XWayland / Mutter frame callback 路径；若启动失败，说明该构建/环境不支持直连 Wayland。"
            ;;
        gamescope)
            MODE_WRAPPER=(gamescope -W 1920 -H 1080 -f -r 90 --)
            MODE_DESC="把游戏放进 gamescope 嵌套合成器（自带 present 调度）"
            MODE_HINT="若 spike 消失 → 问题出在 Mutter/XWayland 合成器一侧；注意 gamescope 自己也会限帧。"
            ;;
        mangohud)
            MODE_ENV=(MANGOHUD=1)
            MODE_DESC="叠 MangoHud（只观测）"
            MODE_HINT="不改变 present 路径。看 GPU 利用率/频率/CPU 单核是否拉满；与 baseline 对比可确认叠加本身未显著扰动结果。"
            ;;
        nocache)
            MODE_ENV=(MESA_SHADER_CACHE_DISABLE=true)
            MODE_DESC="禁用 Mesa 磁盘 shader cache（反向对照）"
            MODE_HINT="若卡顿显著变多 → 说明现有 shader cache 正在救场，spike 不是 cache miss 造成；若几乎无变化 → shader cache 与本次卡顿无关。"
            ;;
        psocache)
            MODE_ENV=(RADV_DEBUG=psocachestats)
            MODE_DESC="只让 RADV 输出 pipeline (PSO) cache 统计到 stderr"
            MODE_HINT="不改善卡顿，用于确认启动/战斗切换处是否有 PSO 未命中（stdout/stderr 会多出 cache 统计）。"
            ;;
        *)
            die "未知模式：$1（可用：${MODES[*]}）"
            ;;
    esac
}

# 空格连接的 env 赋值串（仅模式 env，不含 SteamAppId）
env_prefix_string() {
    local out="" e
    for e in "${MODE_ENV[@]}"; do
        out+="${out:+ }$e"
    done
    printf '%s' "$out"
}

# 空格连接的附加参数串
args_string() {
    local out="" a
    for a in "${MODE_ARGS[@]}"; do
        out+="${out:+ }$a"
    done
    printf '%s' "$out"
}

# shell 安全引用，用于打印可复制命令
shell_join() {
    local out="" a
    for a in "$@"; do
        out+="${out:+ }$(printf '%q' "$a")"
    done
    printf '%s' "$out"
}

# 构建真实执行用的环境与命令数组
build_arrays() {
    ENV_ARR=("SteamAppId=$STEAM_APPID" "SteamGameId=$STEAM_APPID")
    local e
    for e in "${MODE_ENV[@]}"; do
        ENV_ARR+=("$e")
    done

    local bin="$GAME_DIR/$GAME_BIN_NAME"
    CMD_ARR=()
    local w
    for w in "${MODE_WRAPPER[@]}"; do
        CMD_ARR+=("$w")
    done
    CMD_ARR+=("$bin")
    local a
    for a in "${MODE_ARGS[@]}"; do
        CMD_ARR+=("$a")
    done
}

# 可直接粘进 Steam「启动选项」的 %command% 字符串
steam_string() {
    local out=""
    local envs
    envs="$(env_prefix_string)"
    if [[ -n "$envs" ]]; then
        out+="$envs "
    fi
    if ((${#MODE_WRAPPER[@]})); then
        out+="$(shell_join "${MODE_WRAPPER[@]}") %command%"
    else
        out+="%command%"
    fi
    local args
    args="$(args_string)"
    if [[ -n "$args" ]]; then
        out+=" $args"
    fi
    printf '%s' "$out"
}

# Steam 启动选项里 %command% 的说明（用于 show / dry-run）
steam_note() {
    if ((${#MODE_WRAPPER[@]})); then
        printf '%s\n' "Steam 的 %command% 是游戏本体命令行；环境变量写在 %command% 前面，gamescope 用 '-- %command%' 包裹。"
    elif ((${#MODE_ARGS[@]})); then
        printf '%s\n' "Steam 的 %command% 是游戏本体命令行；env 写在前面、参数写在后面。"
    else
        printf '%s\n' "Steam 的 %command% 是游戏本体命令行；env 直接写在它前面。"
    fi
}

# 依赖与前置环境检查（只警告，不阻断；真跑里对 gamescope 另行强校验）
check_deps() {
    if ! command -v jq >/dev/null 2>&1; then
        warn "未找到 jq：真跑时将跳过结构化摘要（报告仍会复制归档）；建议安装 jq 后重试。"
    fi
    if [[ "$MODE" == "gamescope" ]]; then
        if ! command -v gamescope >/dev/null 2>&1; then
            warn "未找到 gamescope：该模式需要 gamescope 在 PATH 中。"
        fi
    fi
    if [[ "$MODE" == "mangohud" ]]; then
        if [[ ! -f /usr/share/vulkan/implicit_layer.d/MangoHud.x86_64.json ]] \
            && [[ ! -f /usr/share/vulkan/implicit_layer.d/MangoHud.x86.json ]]; then
            warn "未找到 MangoHud 的 Vulkan implicit layer 注册文件：MANGOHUD=1 可能不生效。"
        fi
    fi
    if [[ "$MODE" == "wayland" ]] && [[ -z "${WAYLAND_DISPLAY:-}" ]]; then
        warn "WAYLAND_DISPLAY 未设置：--display-driver wayland 可能启动失败（需在 Wayland 会话内运行）。"
    fi
}

perf_dir() { printf '%s' "$LOGS_DIR/bangdream"; }

# 当前 perf 报告（不含 ab/）里最新的 mtime（秒），没有则 0
max_report_mtime() {
    local dir
    dir="$(perf_dir)"
    local max=0
    if [[ -d "$dir" ]]; then
        shopt -s nullglob
        local files=( "$dir"/perf-*.json )
        shopt -u nullglob
        local f m
        for f in "${files[@]}"; do
            m="$(stat -c %Y -- "$f" 2>/dev/null || printf '0')"
            if (( m > max )); then
                max="$m"
            fi
        done
    fi
    printf '%s' "$max"
}

# 找 mtime 严格大于 floor 的 perf 报告；优先带时间戳的 perf-YYYY...json，
# 没有则退回 perf-latest.json。找不到时输出空串。
find_new_report() {
    local floor="$1"
    local dir
    dir="$(perf_dir)"
    [[ -d "$dir" ]] || { printf ''; return 0; }

    local f m best="" best_m="$floor"
    shopt -s nullglob
    local files=( "$dir"/perf-[0-9]*.json )
    shopt -u nullglob
    for f in "${files[@]}"; do
        m="$(stat -c %Y -- "$f" 2>/dev/null || printf '0')"
        if (( m > best_m )); then
            best="$f"
            best_m="$m"
        fi
    done

    if [[ -z "$best" ]]; then
        shopt -s nullglob
        files=( "$dir"/perf-*.json )
        shopt -u nullglob
        for f in "${files[@]}"; do
            m="$(stat -c %Y -- "$f" 2>/dev/null || printf '0')"
            if (( m > best_m )); then
                best="$f"
                best_m="$m"
            fi
        done
    fi
    printf '%s' "$best"
}

# 从 JSON 报告里提取结果行需要的数值；jq 缺失或解析失败时全部 n/a
report_values() {
    local json="$1"
    if command -v jq >/dev/null 2>&1 && [[ -n "$json" ]] && [[ -f "$json" ]]; then
        local values
        values="$(jq -r "$JQ_VALUES" "$json" 2>/dev/null || true)"
        if [[ -n "$values" ]]; then
            IFS=$'\t' read -r SESSION SPIKES P50 P95 P99 MAXMS TOTAL <<< "$values" || true
            SESSION="${SESSION:-n/a}"
            SPIKES="${SPIKES:-n/a}"
            P50="${P50:-n/a}"
            P95="${P95:-n/a}"
            P99="${P99:-n/a}"
            MAXMS="${MAXMS:-n/a}"
            TOTAL="${TOTAL:-n/a}"
            return 0
        fi
    fi
    SESSION="n/a"; SPIKES="n/a"; P50="n/a"; P95="n/a"; P99="n/a"; MAXMS="n/a"; TOTAL="n/a"
    return 0
}

print_summary() {
    local json="$1"
    if [[ -z "$json" ]]; then
        return 0
    fi
    info ""
    info "===== perf 摘要（$(basename -- "$json")）====="
    if ! command -v jq >/dev/null 2>&1; then
        warn "未找到 jq：跳过结构化摘要。可直接查看报告：$json"
        return 0
    fi
    if ! jq -r "$JQ_SUMMARY" "$json"; then
        warn "jq 解析报告失败（结构可能与 bangdream.perf.v1 不同）：$json"
    fi
}

append_results_row() {
    local results
    results="$(perf_dir)/ab/results.md"
    if [[ ! -s "$results" ]]; then
        {
            printf '# Linux present 路径 A/B 实验结果\n\n'
            printf '> 由 `tools/perf-linux-ab.sh run` 自动追加，每次运行为一行。\n'
            printf '> 判定看 `p99_ms` / `max_ms` / `spike_count` 的变化，以及 Top5 spike\n'
            printf '> 的 `present_wait_ms`（present 侧）与 `render_submit_ms`（引擎侧）占比。\n\n'
            printf '| 时间 | 模式 | session_ms | spike_count | p50_ms | p95_ms | p99_ms | max_ms | 报告目录 |\n'
            printf '| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n'
        } > "$results"
    fi
    printf '| %s | %s | %s | %s | %s | %s | %s | %s | %s |\n' \
        "$(date '+%Y-%m-%d %H:%M:%S')" \
        "$MODE" "${SESSION:-n/a}" "${SPIKES:-n/a}" "${P50:-n/a}" \
        "${P95:-n/a}" "${P99:-n/a}" "${MAXMS:-n/a}" "${1:-n/a}" >> "$results"
    info "已追加一行到：$results"
}

# 收集、归档、汇总。参数：pre_max、退出码、开始时间、结束时间。
# 返回 0 = 找到报告；1 = 未找到报告（仍保留 meta.txt）。
finalize_run() {
    local pre_max="$1"
    local rc="$2"
    local started="$3"
    local ended="$4"

    local report_json=""
    local attempt
    if [[ -d "$(perf_dir)" ]]; then
        for attempt in $(seq 1 20); do
            report_json="$(find_new_report "$pre_max")"
            if [[ -n "$report_json" ]]; then
                break
            fi
            sleep 0.5
        done
    fi

    local ts base dir i
    ts="$(date '+%Y%m%d-%H%M%S')"
    base="$(perf_dir)/ab/${MODE}-${ts}"
    dir="$base"
    i=2
    while [[ -e "$dir" ]]; do
        dir="${base}-${i}"
        i=$((i + 1))
    done
    mkdir -p -- "$dir"

    local copied_json="" copied_md="" report_md=""
    if [[ -n "$report_json" ]]; then
        cp -p -- "$report_json" "$dir/"
        copied_json="$dir/$(basename -- "$report_json")"
        report_md="${report_json%.json}.md"
        if [[ -f "$report_md" ]]; then
            cp -p -- "$report_md" "$dir/"
            copied_md="$dir/$(basename -- "$report_md")"
        else
            report_md=""
        fi
    fi

    {
        printf 'mode: %s\n' "$MODE"
        printf 'description: %s\n' "$MODE_DESC"
        printf 'game_dir: %s\n' "$GAME_DIR"
        printf 'logs_dir: %s\n' "$LOGS_DIR"
        printf 'perf_dir: %s\n' "$(perf_dir)"
        printf 'started_at: %s\n' "$started"
        printf 'ended_at: %s\n' "$ended"
        printf 'exit_code: %s\n' "$rc"
        printf 'env: %s\n' "$(env_prefix_string)"
        printf 'args: %s\n' "$(args_string)"
        printf 'command: cd %s && env %s %s\n' \
            "$(printf '%q' "$GAME_DIR")" "$(shell_join "${ENV_ARR[@]}")" "$(shell_join "${CMD_ARR[@]}")"
        if [[ -n "$report_json" ]]; then
            printf 'report_json: %s\n' "$(basename -- "$report_json")"
            if [[ -n "$copied_md" ]]; then
                printf 'report_md: %s\n' "$(basename -- "$copied_md")"
            else
                printf 'report_md: (无同名 .md)\n'
            fi
        else
            printf 'report_json: (未找到)\n'
            printf 'note: 本次没有找到比启动时刻更新的 perf 报告\n'
        fi
        printf 'note: 直接启动无 Steam 覆盖层/Steam Input，属预期\n'
    } > "$dir/meta.txt"

    if [[ -n "$copied_json" ]]; then
        info ""
        info "已归档：$dir"
        report_values "$copied_json"
        print_summary "$copied_json"
        append_results_row "$dir"
        return 0
    fi

    warn "未找到比启动时刻更新的 perf 报告（目录：$(perf_dir)）"
    info "已保留本次运行信息：$dir/meta.txt"
    info "可能原因："
    info "  * 游戏内/模组设置没有启用 perf 报告（BDS-Perf）"
    info "  * 游戏在报告写出前被强制结束（崩溃/被 kill）"
    info "  * 日志目录不是 <logs-dir>/bangdream（可用 --logs-dir 指定正确的日志根目录）"
    info "  * 已有另一个游戏实例在跑，报告写到了别处（可用 pgrep -f $GAME_BIN_NAME 检查）"
    report_values ""
    append_results_row "未找到报告"
    return 1
}

cmd_list() {
    local m
    info "可用模式（${SCRIPT_NAME} show <mode> 查看单个模式的 Steam 启动选项）："
    info ""
    for m in "${MODES[@]}"; do
        load_mode "$m"
        printf '%s\n' "  $m"
        printf '%s\n' "      env  : $( [[ -n "$(env_prefix_string)" ]] && printf '%s' "$(env_prefix_string)" || printf '(无)' )"
        printf '%s\n' "      参数 : $( [[ -n "$(args_string)" ]] && printf '%s' "$(args_string)" || printf '(无)' )"
        if ((${#MODE_WRAPPER[@]})); then
            printf '%s\n' "      包装 : $(shell_join "${MODE_WRAPPER[@]}") <游戏>"
        fi
        printf '%s\n' "      说明 : $MODE_DESC"
        printf '%s\n' "      判定 : $MODE_HINT"
    done
    info ""
    info "默认游戏目录：$DEFAULT_GAME_DIR"
    info "默认日志根目录：$DEFAULT_LOGS_DIR（报告在其下 bangdream/）"
    info ""
    local dep
    for dep in jq gamescope mangohud pgrep; do
        if command -v "$dep" >/dev/null 2>&1; then
            printf '%s\n' "依赖 $dep：$(command -v "$dep")"
        else
            printf '%s\n' "依赖 $dep：未安装（相关模式会降级/提示）"
        fi
    done
}

cmd_show() {
    [[ -n "$MODE" ]] || die "show 需要一个模式名（可用：${MODES[*]}）"
    valid_mode "$MODE" || die "未知模式：$MODE（可用：${MODES[*]}）"
    load_mode "$MODE"
    build_arrays

    info "模式：$MODE"
    info "说明：$MODE_DESC"
    local envs args
    envs="$(env_prefix_string)"
    args="$(args_string)"
    info "env：$( [[ -n "$envs" ]] && printf '%s' "$envs" || printf '(无)' )"
    info "附加参数：$( [[ -n "$args" ]] && printf '%s' "$args" || printf '(无)' )"
    if ((${#MODE_WRAPPER[@]})); then
        info "包装命令：$(shell_join "${MODE_WRAPPER[@]}") <游戏>"
    fi
    info ""
    info "Steam 启动选项（Steam 库 → 右键游戏 → 属性 → 通用 → 启动选项）："
    info "  $(steam_string)"
    steam_note
    info ""
    info "脚本等价直跑命令（会额外设置 SteamAppId/SteamGameId）："
    info "  cd $(printf '%q' "$GAME_DIR") && env $(shell_join "${ENV_ARR[@]}") $(shell_join "${CMD_ARR[@]}")"
    info ""
    info "判定提示：$MODE_HINT"
    info ""
    info "注意：Steam 启动选项里只需粘贴上面那一行；不要再叠加已经设置过的其它 env，"
    info "      否则本次实验就不再是单变量对照。"
}

cmd_run() {
    [[ -n "$MODE" ]] || die "run 需要一个模式名（可用：${MODES[*]}）"
    valid_mode "$MODE" || die "未知模式：$MODE（可用：${MODES[*]}）"
    load_mode "$MODE"
    build_arrays
    check_deps

    local bin="$GAME_DIR/$GAME_BIN_NAME"
    local perf
    perf="$(perf_dir)"

    if ((DRY_RUN)); then
        if [[ ! -d "$GAME_DIR" ]]; then
            warn "游戏目录不存在：$GAME_DIR（dry-run 仍然只打印命令；可用 --game-dir 指定）"
        elif [[ ! -x "$bin" ]]; then
            warn "找不到可执行文件或不可执行：$bin（dry-run 仍然只打印命令）"
        fi
        info "[dry-run] 只打印命令：不会启动游戏，也不会创建/修改任何文件。"
        info ""
        info "模式：$MODE"
        info "说明：$MODE_DESC"
        info "env：$( [[ -n "$(env_prefix_string)" ]] && printf '%s' "$(env_prefix_string)" || printf '(无)' )"
        info "附加参数：$( [[ -n "$(args_string)" ]] && printf '%s' "$(args_string)" || printf '(无)' )"
        if ((${#MODE_WRAPPER[@]})); then
            info "包装命令：$(shell_join "${MODE_WRAPPER[@]}") <游戏>"
        fi
        info ""
        info "完整命令（可直接粘贴到终端）："
        info "  cd $(printf '%q' "$GAME_DIR") && env $(shell_join "${ENV_ARR[@]}") $(shell_join "${CMD_ARR[@]}")"
        info ""
        info "Steam 启动选项（属性 → 通用 → 启动选项）："
        info "  $(steam_string)"
        info ""
        info "真跑时会：等待游戏退出 → 从 $perf 取本次新报告 → 复制到 $perf/ab/$MODE-<时间戳>/ → 打印摘要并追加 results.md"
        return 0
    fi

    [[ -d "$GAME_DIR" ]] || die "游戏目录不存在：$GAME_DIR（可用 --game-dir 指定）"
    [[ -x "$bin" ]] || die "找不到可执行文件或不可执行：$bin（可用 --game-dir 指定）"
    if [[ "$MODE" == "gamescope" ]] && ! command -v gamescope >/dev/null 2>&1; then
        die "gamescope 模式需要 gamescope 在 PATH 中（当前未找到）"
    fi
    if command -v pgrep >/dev/null 2>&1 && pgrep -x "$GAME_BIN_NAME" >/dev/null 2>&1; then
        if ((FORCE)); then
            warn "检测到已有 $GAME_BIN_NAME 进程在运行，--force：继续（可能产生并发/报告归属混淆）"
        else
            die "检测到 $GAME_BIN_NAME 已在运行：请先正常退出游戏，或加 --force 强制继续"
        fi
    fi

    mkdir -p -- "$perf"
    local pre_max started ended rc
    pre_max="$(max_report_mtime)"
    started="$(date '+%Y-%m-%d %H:%M:%S%z')"
    info "模式：$MODE —— $MODE_DESC"
    info "启动：$(shell_join "${CMD_ARR[@]}")"
    info "（等待游戏退出；直接启动没有 Steam 覆盖层/Steam Input 属预期）"

    rc=0
    ( cd "$GAME_DIR" && exec env "${ENV_ARR[@]}" "${CMD_ARR[@]}" ) || rc=$?
    ended="$(date '+%Y-%m-%d %H:%M:%S%z')"
    info ""
    info "游戏已退出（exit_code=$rc）"

    if finalize_run "$pre_max" "$rc" "$started" "$ended"; then
        info ""
        info "完成。对照下一个模式时保持相同场景与操作流程，结果才有可比性。"
        return 0
    fi
    return 1
}

parse_args() {
    while (($#)); do
        case "$1" in
            -h|--help)
                HELP=1
                shift
                ;;
            --dry-run)
                DRY_RUN=1
                shift
                ;;
            --force)
                FORCE=1
                shift
                ;;
            --game-dir)
                [[ $# -ge 2 ]] || die "--game-dir 需要一个目录参数"
                GAME_DIR="$2"
                shift 2
                ;;
            --game-dir=*)
                GAME_DIR="${1#*=}"
                shift
                ;;
            --logs-dir)
                [[ $# -ge 2 ]] || die "--logs-dir 需要一个目录参数"
                LOGS_DIR="$2"
                shift 2
                ;;
            --logs-dir=*)
                LOGS_DIR="${1#*=}"
                shift
                ;;
            --*)
                die "未知选项：$1（见 --help）"
                ;;
            -*)
                die "未知选项：$1（见 --help）"
                ;;
            *)
                if [[ -z "$SUBCOMMAND" ]]; then
                    SUBCOMMAND="$1"
                elif [[ -z "$MODE" ]]; then
                    MODE="$1"
                else
                    die "多余的参数：$1（见 --help）"
                fi
                shift
                ;;
        esac
    done
    [[ -n "$GAME_DIR" ]] || die "--game-dir 不能为空"
    [[ -n "$LOGS_DIR" ]] || die "--logs-dir 不能为空"
}

main() {
    parse_args "$@"
    if ((HELP)); then
        usage
        return 0
    fi
    case "$SUBCOMMAND" in
        ""|help)
            usage
            [[ -z "$SUBCOMMAND" ]] && return 2
            return 0
            ;;
        list)
            cmd_list
            ;;
        show)
            cmd_show
            ;;
        run)
            cmd_run
            ;;
        *)
            die "未知子命令：$SUBCOMMAND（可用：list / show / run / --help）"
            ;;
    esac
}

# 允许被其它脚本 source 以复用其函数（直接执行时才进入 main）
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main "$@"
fi
