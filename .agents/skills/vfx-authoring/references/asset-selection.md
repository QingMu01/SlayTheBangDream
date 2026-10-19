# 素材选材（Polaris MCP）

## 素材的三个来源

| 来源 | 何时用 | 取法 |
|---|---|---|
| **Polaris 资产库**（主） | 默认。库里有 12000+ 素材，覆盖面最广 | 本文件全部流程 |
| **工程内已有贴图** | 想让新特效与现有特效**风格统一**时 | 见 `BangDreamInSpire/BangDreamInSpire/images/vfx/` |
| **美术新画的图** | 库和工程里都没有合适形态 | 走常规资源流程，同样要 `--headless --import` |

工程内已有素材很少（截至当前只有 `music_slash/` 与 `images/vfx/` 根下若干张），
所以**新特效基本都要从资产库取**——这也是本 skill 绑定 MCP 的原因。

## 需要 S1 时：MCP 连不上就停止侦察

**需要库内新候选时，MCP 不可用就暂停素材侦察，不伪造 uuid。** 已定工程内素材的改造按 SKILL.md 跳过 S1；工程来源记资源路径与文件 hash，新绘素材记作者/原始文件，不能伪装成库内来源。

理由：S1 的全部价值是拿到**真实库内候选**。没有候选池，需要新候选的素材题无法进行；
没有真实候选时不能靠编造 uuid 填补计划。其他已明确来源不要求伪造库内身份。

**排查步骤**（逐条给用户看，不要自己猜）：

1. 调用已暴露的 Polaris 只读工具（如 library_info）。仅缺少资源列表能力不等于断连；记录实际工具错误。
2. 检查服务是否在跑：
   ```powershell
   Get-Process -Name "polaris-mcp" -ErrorAction SilentlyContinue
   ```
3. 确认当前客户端确实通过 DSH 启动后再检查 DSH profile；其他客户端按其实际配置排查。**仅将禁用条目作为候选原因，不预设根因**：
   ```powershell
   Get-Content "$env:USERPROFILE\.dsh\profiles\<profile>\cordis.patch.yml" |
     Select-String -Pattern 'mcp' -Context 3,14
   ```
   逐项核对：`disabled: true/false`、`transport`（`stdio` 或 `streamable-http`）、
   stdio 下的 `command` 路径是否存在、http 下的 `url` 与 `Authorization` 是否正确。
4. 注意该配置块被 `# dsh-launcher mcp begin/end` 包裹，**由启动器生成**——
   手改可能被覆盖，优先从启动器界面开关。
5. MCP 客户端在**会话启动时**建立连接，改完配置需要重开会话 / 重启 DSH。

排查完把结论与可执行步骤交给用户，**然后等**。不要在同一轮里继续做 S2。

---

## 工具序列

```
library_info                  # 首次连接：库规模、已有标签、覆盖率
  ↓
search_assets                 # 结构化过滤定位候选（纯文本，最省 token）
  ↓
render_contact_sheet          # 批量目视（≤256 个，cell_size 128–256）★关键步骤
  ↓
get_assets                    # 同批 uuid 批量读文本 + features（≤64 个）
  ↓
render_asset_preview          # 只对少数候选看高清（默认 1024）
  ↓
get_asset_group_preview       # 图集/序列帧：拿帧结构（hframes/vframes 的唯一依据）
  ↓
export_assets                 # 导出到 .vfx_staging/（绝对路径，自动创建目录）
```

## `search_assets` 的结构化过滤（比关键词强得多）

全部是 SQL 级过滤，`total` 即真实命中数。VFX 选材常用的：

| 参数 | 用途 |
|---|---|
| `tags:["VFX"]` | 定位特效素材大类 |
| `alpha` | 透明占比区间。`[0.5,1.0]` 找大面积透明贴图 |
| `fill_ratio` | 剪影填充率。环形/框线偏低，实心接近 1 |
| **`holes`** | **剪影洞数区间。`[0,0]` = 只要实心的**——找爆散/光斑/闪光核心时必加，能直接排除"中心是黑洞"的素材 |
| **`components`** | **剪影连通数区间。`[1,1]` = 只要单颗主体**——直接排除散点图集，比看名字可靠 |
| `glow_range` / `glow` | 辉光强度区间 / 布尔 |
| `brightness` | 内容区平均亮度 |
| `edge_softness` | 边缘柔硬。`[0,0.1]` 找硬边贴纸（仅带 alpha 的素材有值） |
| `dark_outline` | 深色描边占比。**染色前用 `[0,0]` 排除自带描边的**（描边会残留异色） |
| `layout` | 构图：`centered` / `full` / `horizontal` / `vertical` |
| `colors` + `color_min_ratio` | 主色过滤 + 最低占比。**要找能染色的素材就用 `colors:["白"]` + `color_min_ratio:0.9`** |
| `kinds` / `sub_types` | `image` / `asset_group`；`grid_atlas` / `image_sequence` / `atlas` |
| `width` / `height` | 像素尺寸区间。**太小要放大，会糊**——见陷阱 8 |

**一次到位的常用组合**（找"干净、可染色、实心的单颗星芒"）：

```
tags: ["VFX"], kinds: ["image"],
colors: ["白"], color_min_ratio: 0.9,
holes: [0, 0], dark_outline: [0, 0],
width: [128, 2048]
```

**AND 查询无命中时会自动放宽为 OR**，并在 `relaxed` 字段说明——看到 `relaxed` 就说明你的条件是"或"的关系，别误判。

## 特征字段解读（`get_asset` / `get_assets` 的 `features`）

> **先读规范再选材**：`get_selection_spec`（或 resource `polaris://spec/selection[.json]`）
> 是受控取值与阈值的权威来源，由 Polaris 核心注册表生成。
> 与预演脚本的口径对照见 [selection-contract.md](selection-contract.md)。

| 字段 | 怎么读 |
|---|---|
| **剪影连通数** | **1 = 单颗；多 = 散点或图集** ← 最容易看错的一栏 |
| 剪影填充率 | 低（<0.3）= 细芒 / 星形；高（>0.7）= 实心光斑 |
| 透明占比 / alpha 状态 | 看「透明占比」数值 + `alpha_has_non_opaque`：**有没有 alpha 由布尔量决定**，别用透明占比的百分比反推（v4 起口径明确） |
| 辉光 | 档位直接决定能不能当"发光核心"层 |
| 亮度 | 与辉光一起决定明度档位 |
| **主体框 / 对象** | 看 v4 的「背景」与掩码方法：有 alpha → 按 alpha 事实取内容框；**识别到贴边均匀背景（黑底/白底）→ 剔除背景后取内容框，框会收紧**；只有在「未识别到背景、整幅即主体」时框才等于满幅，此时尺寸结论不可用于缩放。黑底素材**不是**不可用 |
| **洞数** | 有洞 → 圆环 / 框线。**但"洞"也可能是不该有的中心空洞**——见下方陷阱 2 |
| **中心孔** | 单独一栏（experimental）：只有"洞的质心落在主体框中心区 + 覆盖中心像素 + 面积达标"才算中心孔，**不能用任意孔洞替代** |
| **深色描边** | `> 0` 表示自带描边。染成单色时描边会残留异色（如金边在冷色下变黄边）；黑底加法贴图不判此项（边缘发暗是衰减而非描边，会显示"不适用"） |
| **逐维度状态** | `known` / `missing` / `not_applicable` / `stale` / `failed`：**「不适用」与「没测出来」含义不同**，后两者都不能当作通过 |

## 判定决策树

```
alpha 状态：alpha_has_non_opaque ?
├─ 是 → 有 alpha
│   ├─ 连通数 = 1 → 单颗 → 粒子贴图 / 单个 Sprite2D
│   ├─ 连通数多   → 散点 / 图集 → hframes/vframes 或 region_rect
│   └─ 亮度恒为 1 → 必须 alpha_add，禁止压黑
└─ 否（不透明）
    ├─ 识别到黑底 → 仍可用：crush_add；black_point/softness 按该素材实测，不固定套阈值
    │                 主体框已剔除背景（不要再用"满幅"推断尺寸）
    ├─ 连通数 = 1 → 单颗光形 → 同样按实测压黑参数
    └─ 连通数多   → 图集 → 先 get_asset_group_preview 拿行列数
```

## 已知陷阱

1. **不要按文件名判断内容。** 实证：`T_VFX_SparklesF21` 名字像散点闪光，实际是柔焦光形；`T_VFX_exp_dissapear` 是螺旋涡流；`T_BlastUi6` 是纵向渐变。
2. **缩略图不足以判断形状，必须在接近原始分辨率下目视。** 这条踩了三次，逐级加深：
   - 96px 联络表 → 漏掉 6 处描述错误；
   - 160px 联络表 → 看不出 `flash_v2` 的中心黑洞，也看不出 `base_glow` 是不规则拱形；
   - **256px 联络表仍然看不出 `flash_v2` 中心的八边形扇贝花纹，也看不出 `star_04` 其实是实心三角形**。

   结论：**选材必须包含一步「接近原分辨率的目视」**——
   用 `export_assets` 导出后本地渲染，或用 `render_asset_preview`。
   同时读 `features` 的「洞 / 填充率 / 深色描边」作为交叉校验：
   - **洞 > 0**：中心可能是空的。真环/框线属预期，但**爆散、光斑类素材有洞默认视为风险**——
     有意留空或用核心层填补时，记录理由并检查合成结果，不将艺术选择一律视为缺陷。
   - **深色描边 > 0**：染成单色后会残留异色边。
3. **`find_similar_assets` 对本类素材失效。** dHash 把鸟居、彩虹、花朵判为与柔光光形"相似"（距离 7–14）。这批图多为近黑背景，哈希区分度太低。**不要用它选材。**
4. **批量误标要用 `list_import_batches`。** 按导入时间聚类，比手工按时间戳聚类可靠；配合 `render_contact_sheet` + `get_assets` 逐格比对。
5. **`export_assets` 不覆盖同名文件**——已存在同名文件夹的资产会报错，其余继续。
6. **不要在资产库内改名或删除。** 导出后在项目里重命名。
7. **乘算染色优先用白色或中性灰阶贴图。** `modulate` 是乘算：带色贴图（如橙色）染冷色时对应通道接近 0，
   结果发暗发脏。用 `colors:["白"]` + `color_min_ratio:0.9` 可以直接筛出纯白素材。
8. **小图放大会糊。** 72×80 的素材要放大 4.8× 才够用，插值后细节全失。
   选材时把原始尺寸一起看：**需要的显示尺寸 ÷ 原始尺寸** 超过 3× 就要预期模糊。


## 工具能力核对

这些历史问题已在 `docs/vfx-authoring.md` 更新。使用前以当前工具描述与实测为准：

| 文档说法 | 当前实际 |
|---|---|
| `render_asset_preview` 会丢 alpha | 已自动叠棋盘格底，文本块带 `background=checkerboard`，并附 features |
| `export_assets` 目标目录须预先存在 | "不存在时自动创建" |
| `search_assets` `detail="minimal"` 不可用 | 已可用（只回 `uuid/name/kind/subType`） |

> 不再把已修正文档称为过期；工具不可用不意味着所有列举来源都没有实际凭据。改造且已定素材的分支按 SKILL.md 跳过 S1。

## 导出、本体归属校验与命名

导出到仓库外的暂存目录（`.vfx_staging` 已在 `.gitignore` 内）；`export_assets` 的 `directory` 给绝对路径。

**复制进工程之前必须跑本体归属校验**（结论与判定表见 `SKILL.md` S1 第 9 步）：

```
python .agents/skills/vfx-authoring/scripts/check_asset_origin.py <导出目录>
```

- **`BODY`（是本体素材）**：**不要复制**。按脚本输出的本体 `uid` + `path` 直接引用；
  编辑器侧由 `tools/sync-sts2-assets.sh` 同步的 `BangDreamInSpire/images/**` 提供同一 uid 的文件。
- **`LOCAL`（非本体素材）**：
  1. 重命名为语义化文件名（`explosion_burst.jpg` 而非 `fx_301403_tex_002.jpg`）；
  2. 复制进 `BangDreamInSpire/BangDreamInSpire/images/vfx/<特效名>/`；
  3. 运行 `--headless --import` 生成 `.import` 与 uid。**不要手写 uid。**
  4. 场景里引用资源用**引擎生成的 uid**（从 `.import` 的 `uid=` 行读），`path=` 保留作可读性兜底。
- **`REVIEW`（疑似在本体图上改绘）**：同尺寸且感知哈希接近本体某张图，人工判定后按上面两条之一处理。

**不产出 `README.md` / `SOURCES.md`**：库内 uuid/名称、语义名、用途与校验判定结果写进 `.vfx_staging/plans/<slug>.md`。

## 回写描述（本 skill 默认不做）

本 skill 对素材库**只读**。补描述是独立任务。若确需回写：用 `update_asset_description`（字段级合并用 `fields` 只改指定字段），写完 `validate_description` 自检，`ok: true` 才算过；误写用 `revert_asset_meta` 按 `batchId` 整批回滚。
