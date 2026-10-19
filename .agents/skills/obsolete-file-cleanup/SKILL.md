---
name: obsolete-file-cleanup
description: 清理 Slay the Spire 2 模组仓库中过时文件（孤儿资源、废弃脚本、僵尸本地化条目、多余 sidecar）的标准工作流。以引用图为唯一判据，分「被引用/约定引用/弱命中/确认无引用」四态落盘，输出「可直接删/建议保留/需人工确认」三档清单，人工签字后才执行删除，并以残留搜索、JSON 校验与完整构建收口。适用于本 Godot.NET/Harmony 模组的资源与代码瘦身、重构后的清尾、以及误删事故的复盘。
---

# 过时文件清理工作流

**第 0 步（强制）**：读 `docs/cleanup-authoring.md`。它是「某个文件能不能删」的唯一事实来源，本文件只规定流程与证据，不重复判定规则。

## 通用前置

1. 完整读取仓库 `AGENTS.md` 与 `docs/cleanup-authoring.md`；涉及卡牌或特效时加读 `docs/card-authoring.md`、`docs/vfx-authoring.md`。
2. **先跑 `git status --short`**，识别并绕开用户未提交、未跟踪的 WIP 文件。本仓库有未跟踪文件在 rebase/reset 中静默消失的前科。
3. **在删除前给出方案**（`AGENTS.md` 要求），方案里必须含三档清单与每条的依据。
4. 判定不确定时，宁可落 C 档，不要落 A 档。

## 铁律：签字之前不删任何文件

工具只能产出**证据**，不能产出**决定**。「确认无引用」不等于「可删」——模型类会被游戏无条件反射实例化（见 `docs/cleanup-authoring.md` 第 4 节），约定式引用 grep 不到（第 3 节第 3 种形态）。**必须有人对 A 档逐条签字，才允许进入 S4。**

## 阶段状态机

```
S0 盘点 ──> S1 判定 ──> S2 分档 ──> S3 人工签字（闸门）──> S4 删除 ──> S5 残留验证 ──> S6 证据化交付
```

任何阶段发现「判定依据不足」，退回 S1，不得带疑点前进。

## S0 盘点

1. `git status --short`，记录本次任务**不允许碰**的文件集合（用户 WIP）。
2. 确认任务边界：是「某次重构后的清尾」还是「全仓库瘦身」。前者应能给出变更范围（如某次提交删掉的类/场景），后者才全量扫描。
3. 记录基线：受跟踪文件数与分类计数，便于交付时对比。

```bash
git ls-files | wc -l
git status --short
```

## S1 判定

跑扫描脚本，得到四态分文件结果。**禁止**用「grep 无命中」代替这一步。

```bash
python3 .agents/skills/obsolete-file-cleanup/scripts/scan_orphans.py --project-root .
```

- `exit 0` 表示无「确认无引用」项；`exit 1` 表示有。两者都要读输出文件，不要只看退出码。
- 输出文件：`referenced.txt`、`convention_referenced.txt`、`weak.txt`、`confirmed_orphan.txt`、`dead_cluster.txt`、`broken.txt`、`sidecar_orphan.txt`、`sidecar_missing.txt`（默认落 `.vfx_staging/audit/`，该目录已被 gitignore）。
- **死簇单独成桶，不属于确认孤儿**：它的引用者本身也在孤儿堆里。级联必须迭代到不动点，且引用者若命中保留规则就不得级联。死簇默认落 B 档。
- 每份文件头部含「脚本名 + 版本 + 时间 + 输入域」。**没有头部的快照不得作为证据**。

补两道必做的独立检查：

```bash
# 1) 卡图双向：类→图（另一向由图→类由扫描脚本覆盖）
python3 .agents/skills/card-development/scripts/audit_cards.py --project-root .

# 2) 本体归属：候选里任何图片先判是不是本体资源
python3 .agents/skills/vfx-authoring/scripts/check_asset_origin.py <导出目录>
```

命中 `BODY` 的**不在本流程内删除**：正确动作是改为引用本体 uid（属 `vfx-authoring` 的范围）。

## S2 分档

```bash
python3 .agents/skills/obsolete-file-cleanup/scripts/cleanup_manifest.py
```

产出两份东西（默认都在 `.vfx_staging/audit/`，**该目录是临时目录，随时可能被清理**）：

- `cleanup_manifest.md` —— **人看**：三档紧凑清单（每项一行，带 sidecar 数与体积、理由短码），`stale` / `broken` 各自成节；
- `cleanup_decisions.tsv` —— **人改**：签字用，列 `id/bucket/path/sidecar/bytes/reason/action/note`，`action` 填 `delete` / `keep` / `defer`。

分档口径与默认保留规则见 `references/keep-list.md`。

**退出码语义**：`exit 0` = 清单生成成功；`exit 1` = 存在 `stale`（快照已过期）或工程内本地断链，**不代表 A 档为空**——不要用退出码判断「有没有待删项」，要看清单。A 档待签字只打 `WARN`。

## S3 人工签字（闸门）

- A 档**逐条**过：每条给出「无引用的证据」与「为什么不是预留/预览/待实装」。
- B 档**逐条**核对理由是否仍然成立（等实装的角色是否真的还没实装）。
- C 档补齐判定或明确挂起，并写清挂起原因。
- 涉及模型类（Card/Power/Relic）的删除，必须准备构建验证，不能只看静态引用。
- 卡图不直接删，按 `references/deprecation-ledger.md` 记账。

签字的具体操作：

1. 打开 `cleanup_decisions.tsv`，给 **A 档每一行**填 `action`（`delete` / `keep` / `defer`）；选 `keep`/`defer` 必须在 `note` 写理由。B/C 档可留空。
2. `sign_off.py --decisions <tsv> --dry-run` 复核将要执行的 rm 清单；**有未决 A 项会 `exit 1`**。
3. 确认无误后 `sign_off.py --decisions <tsv> --apply`（进入 S4）。

**未签字不得进入 S4。** 签字结论落在 tsv 里，不在对话里；最终批次记录由 `--apply` 写进 `docs/cleanup-log.md`（持久、随提交入库）。

## S4 删除

1. **用 `sign_off.py --apply` 执行**（它内置「先 `git add` 再删」、安全域硬校验、批次记账）；只有在工具不适用的特殊情况下才手工删，且必须自己先 `git add`。
2. **成簇删除**：`A` 依赖 `B`、`B` 依赖 `C` 的链条一起删，避免留下新的断链。
3. **sidecar 同批**：删 `X.png` 时同批删 `X.png.import`；删 `X.cs` 时同批删 `X.cs.uid`；删脚本时同批处理引用它的 `.tscn`。
4. **同步硬编码清单**：删除场景/脚本后，检查 `BangDreamPreloadManager.cs` 的 `VfxAssets` 一类清单，以及 `skins/*/*.json` 的 entry 字符串。
5. 只动签字范围内的文件；与用户 WIP 重叠的一律停手并回报。

## S5 残留验证（不得跳过）

1. 旧类名、旧模型 ID、旧 entry 字符串、旧本地化键、旧资源名的残留搜索，要求命中为 0：

```bash
rg -n "<旧类名>|<旧entry>|<旧资源名>" --glob '!.godot/**' --glob '!bin/**' --glob '!obj/**'
```

2. 本地化 JSON 可解析且无重复键（`audit_cards.py` 已内置该检查）。
3. `git diff --check` 通过。
4. 完整构建受影响项目；部署目标若指向游戏目录，用工作区内临时 `Sts2Dir` 重定向：

```bash
dotnet build BangDreamLib/BangDreamLib.csproj -c Debug -p:Sts2Dir="$PWD/bin/BuildDeploy"
```

5. 删了脚本或场景的，额外跑一次无头加载，确认无 `ext_resource, invalid UID` / `Failed loading resource`：

```bash
"$GODOT" --headless --editor --path BangDreamLib --quit-after 150 2>&1 | grep -iE "error|failed"
```

出现任何未解释的残留、断链或构建错误，**不得宣布完成**。

## 反模式

- 用「grep 没有命中」作为唯一判据。
- 把弱命中（basename 出现在别处）当孤儿。
- 把 `.godot/`、`bin/`、本体同步目录扫进证据域。
- 只跑「图→类」或只跑「类→图」单向盘点。
- 用 `.import` 里的 `source_file=` 证明资源被引用（那是自引用）。
- 把未实装角色的预留素材、有意保留的预览件当成废件。
- 整目录 dump 成「未跟踪垃圾」。
- 拿着过期快照直接开删。
- 手写或复制伪造 uid。

## 验证清单（交付前逐条打勾）

- [ ] `git status --short` 里的用户 WIP 文件一个没动。
- [ ] 四态清单来自本次运行，头部有脚本名/版本/时间/输入域。
- [ ] `confirmed_orphan` 中的卡图、Spine 贴图、`mod_image.png` 已逐条排除误报。
- [ ] 本体同步目录未出现在任何桶。
- [ ] sidecar 孤儿/缺失双向都算过（当前应为 0/0，若非 0 要解释）。
- [ ] A 档逐条有依据，B 档逐条有理由，C 档有挂起原因。
- [ ] `cleanup_decisions.tsv` 的 A 档已填满；`sign_off.py --dry-run` 无未决项。
- [ ] 删除经 `sign_off.py --apply`（或手工时已 `git add`），sidecar 同批，硬编码清单已同步。
- [ ] `docs/cleanup-log.md` 已追加本次批次记录。
- [ ] 残留搜索为 0、JSON 校验通过、`git diff --check` 通过、构建通过。
- [ ] 交付含三档清单、每条依据、执行的命令与输出、修改文件表。

## 交付结果

用简洁结论报告：

- 本次清理的范围与触发原因（重构清尾 / 全仓瘦身 / 事故复盘）。
- 四态与三档的实测数字，以及从证据到决定的推理链。
- 删除、保留、挂起各多少项、共多少 MB；挂起项为什么挂起。
- 残留搜索、JSON、`git diff --check`、构建、无头加载的结果。
- 以 Markdown 表格列出本次实际修改与删除的全部文件，排除用户原有的无关工作区改动。
