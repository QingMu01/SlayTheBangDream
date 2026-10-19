# 选材口径对照：Polaris 核心 ↔ 本 skill 预演脚本

**为什么有这份文档**：选材结论要在两处产生——Polaris 库内（Rust 特征与 facet）和本 skill 的
预演脚本（Python，`scripts/preview_candidates.py`）。两边若各用一套阈值，就会出现
「库里说填充率 0.31、预演说 0.22」这类**无法交叉校验**的分歧，agent 只能凭感觉选一边。

约定：**一份规则定义，两边共用**。数值的权威来源是 Polaris 核心常量，
机读镜像在 `PolarisManager/contracts/selection-rules.v1.json` 的 `thresholds` 块。

## 怎么读到权威取值

| 途径 | 用途 |
|---|---|
| `get_selection_spec`（MCP 工具） | 结构化规范：facet、角色、组织方式、阈值、维度、未知策略语义 |
| resource `polaris://spec/selection.json` | 同上（客户端可缓存，不占对话） |
| resource `polaris://spec/selection` | 同一份规范的人读 markdown |
| `PolarisManager/contracts/selection-rules.v1.json` | 机读契约 + `thresholds` 数值镜像 |

## 共用阈值对照表

| 项目 | 核心（权威） | 预演脚本 | 是否必须一致 |
|---|---|---|---|
| 内容像素阈值（alpha ≥ 即"看得见") | `ALPHA_TRANSPARENT = 32` | `CONTENT_ALPHA = 32` | ✅ 必须一致——决定 fill_ratio / 连通 / 洞 / 内容框 |
| 分析降采样边长 | `WORK_SIZE = 256` | `ANALYSIS_MAX = 256` | ✅ 必须一致——否则连通与洞不可比 |
| 放大倍数上限 | `selection::MAX_UPSCALE = 3.0` | `MAX_UPSCALE = 3.0` | ✅ 必须一致 |
| 参考框长边 | `selection::REFERENCE_BOX_PX = 384` | 文档/csharp-recipes 的 384px | ✅ 必须一致 |
| 深色描边占比阈值 | `selection::DARK_OUTLINE_THRESHOLD = 0.15` | 预演**不检测**描边（需 MCP/目视） | 不适用（预演只提示"未检测"） |
| 「边缘实心」阈值 | `DARK_OUTLINE_MIN_ALPHA = 128` | `SOLID_EDGE_ALPHA = 128`（仅作常量保留） | ✅ 语义一致（**不是主体阈值**） |
| 小区域下限 | `REGION_MIN_RATIO = 0.01`（1% 画布） | 预演的 `count_regions` **不设下限**（逐像素连通） | ⚠️ 有意不同：预演看的是显示细节，核心看的是可检索判据；差异在预期内，但**不要拿预演的连通数去反驳核心的洞数**——核心会忽略 <1% 画布的噪点孔 |
| 中心孔判据 | 质心落在主体框中心区(`0.25`) + 覆盖中心像素 + 面积 ≥2% 画布 | 预演只给 `holes`，无"中心孔"概念 | ⚠️ 有意不同：**不得**用预演的 `holes>0` 推断"中心孔" |
| 填充率角色否决 | 核心**不做**填充率角色否决（只给 `fill_ratio` 值与 `single/multiple_component`） | `min_fill`：burst/flash 0.25、spark 0.3 | ⚠️ 预演侧启发式：只能当**提示**，不能当"库里筛掉了"的依据 |

**历史修正**：预演脚本原用 `SOLID = 128` 作为主体阈值。这会让半透明柔光（如 α=80 的光斑板）
被算成"没有主体"，与库内 `fill_ratio` 口径不一致；已统一为 `CONTENT_ALPHA = 32`
（`SOLID_EDGE_ALPHA = 128` 只保留"边缘实心"含义）。

## 角色集合（用途）

核心 8 个：`burst` / `glow` / `flash` / `ring` / `smoke` / `spark` / `trail` / `debris`。

- 预演脚本的 `VETO` 表与之逐项一致（已补 `trail` / `debris`）。
- **`atlas` 已弃用**：图集是**组织方式**，不是用途。改用
  `representation=grid_animation|packed_atlas|image_sequence` + 相应角色。
  旧规格仍可跑，但会打弃用告警并按 `spark` 判据继续。

## 黑底素材：库里怎么给结论（修正旧说法）

旧文档说「黑底素材不可用，包围盒恒为 `x0.00-1.00 y0.00-1.00`」——**这已不成立**。

v4 特征会检测**贴边均匀背景**（`border_uniform`）：识别到黑底/白底时，主体框是
**剔除背景后的内容框**，框会收紧，尺寸结论可用。只有在"未识别到任何背景、整幅即主体"
（掩码方法 `full_frame`）时框才等于满幅——此时才不得据框定缩放。

因此选材时：

1. 读 `features` 的「背景」与掩码方法，而不是假设黑底一定满幅；
2. 黑底素材**可以**用于 `crush_add` 层，按素材实测 `black_point/softness`；
3. 带 alpha 的素材不要走压黑（会丢 alpha，见 csharp-recipes §3 的说明）。

## 交叉校验流程（推荐）

1. `get_selection_spec` 取权威取值；
2. `search_assets` 主体级检索（`role`/`facets`/`dimensions`/`target_px`/`unknown_features`）拿到候选，
   关注 `risks` 与 `required_review`：**未知证据不会被当作通过**；
3. `render_contact_sheet` 粗筛 + 接近原分辨率目视；
4. 预演脚本对候选做形态/混合近似，用上面的对照表解释差异；
5. 结论写回时：结构化语义用 `update_asset_description(semantics=...)`，
   负面观察（「不是星芒」「中心有洞」）写 `selection_note`，不要写进描述字段。

## 漂移防护（两边都有测试钉住）

| 方向 | 检查 |
|---|---|
| 核心 ↔ 契约 JSON | `cargo test -p polaris-manager --lib contract_thresholds_match_core` |
| 契约 JSON ↔ 预演脚本 | `python PolarisManager/scripts/baseline/check_skill_alignment.py` |
| 预演脚本自身 | `python scripts/test_preview_candidates.py`（含 `test_contract_constants_match_polaris_core`） |

任何一边单独改数值都会在这三者之一失败。
