# 模块耦合边界

本文记录两个模组之间、以及核心内部各层之间的**可执行边界**，由 `tools/check-architecture.py` 检查。
规则不是审美偏好：它们对应「内容模组只依赖稳定契约」与「机制层不直接驱动 UI」两条既有约定
（见 `docs/mechanic-authoring.md` 第 5 节、`AGENTS.md` 开发约定）。

## 1. 依赖方向

```
ItsCrychic（内容）  ──→  BangDreamInSpire（核心）       单向，禁止反向
核心内的方向约束（对应下表 H1–H3 三条硬规则）：
  Scripts/Interfaces/**   ← 任何层都可以依赖（契约），本身不依赖实现
  Scripts/Utils/Infos/**  ← 任何层都可以依赖（共享值类型 / 配色 / 注册表）
  Scripts/Extensions/**   ← 内容模组调用机制的稳定门面
  Scripts/Utils/**        → 不得依赖 Scripts/Mechanics/**、Scripts/Nodes/**   （H2）
  Scripts/Mechanics/**    → 不得依赖 Scripts/Nodes/**；`Perform` 已强制       （H3）
  ItsCrychic/**           → 不得依赖 Scripts/Mechanics/**                     （H1）
```

不在检查范围的依赖属正常建模，不要为了"零循环"去拆：卡牌 ↔ Power 的领域互引、
`Nodes` ↔ `Nodes.SubNode` 父子节点、`Utils` ↔ `Utils.Infos` 同层拆分。

`Scripts/Mechanics` 里除 `Perform` 之外的模块对 `Nodes` 的依赖暂列 WARN（W2），逐项登记在脚本
`ALLOWLIST`，新增例外须注明原因。

## 2. 规则表

| 级别 | 规则 | 约束 | 说明 |
|---|---|---|---|
| HARD | H1 | `ItsCrychic/**` 不得 `using BangDreamInSpire.Scripts.Mechanics` | 内容模组只能用契约与扩展门面 |
| HARD | H2 | `BangDreamInSpire/Scripts/Utils/**` 不得 `using ...Mechanics` / `...Nodes` | 工具层不能反向依赖实现层 |
| HARD | H3 | `BangDreamInSpire/Scripts/Mechanics/Perform/**` 不得 `using ...Nodes` | 演奏机制经 `IPerformArea` 驱动 UI |
| WARN | W1 | `ItsCrychic/**` 引用核心 `Nodes` | 待抽契约（VFX/商人 UI） |
| WARN | W2 | 核心 `Mechanics/**` 引用 `Nodes` | 待抽 VFX/UI 契约 |
| WARN | W3 | 核心代码出现 `Sakiko/Mutsumi/Togawa/Wakaba` 标识符 | 角色专属实现应在内容模组 |

HARD 违规必须修；WARN 违规必须登记在脚本的 `ALLOWLIST`（附原因与目标阶段），且不允许静默新增。
注释与字符串字面量不参与判定；`...Mechanics/...MechanicsExtra` 这类同前缀命名空间不会误判。

## 3. 用法

```bash
python3 tools/check-architecture.py            # HARD 违规时 exit 1
python3 tools/check-architecture.py --strict   # 收口用：WARN 也 exit 1
```

脚本会列出**过期 allowlist 条目**（已不再命中的路径），逐项清理时从 `ALLOWLIST` 删除即可。

## 4. 例外登记

**当前例外只以脚本的 [`ALLOWLIST`](../../tools/check-architecture.py) 为准**，本节不复制逐文件清单，避免双份登记漂移。现有例外分为两类，另有一类已收敛项作为记录：

- **VFX 契约**：`Lingered` / `MusicNote` 的机制直接引用 `Nodes.VFX`（角色专属特效已迁出，见下条）。
- **商人 UI 契约**：`ExtraCardMerchantManager` 直接操作 `Nodes.Merchant`；
  `CrychicMerchant` 继承核心商人 UI 基类。
- **角色专属表现（已收敛）**：`StrikeSakikoVfx`、`MutsumiGroupActivationVfx`、
  `MutsumiPlaylistFreezeVfx` 及其场景/着色器已迁到内容模组；核心只保留
  `IPerformAreaFreezeVisual` / `IPerformAreaActivationVisual` 数据契约与 `PerformAreaVisuals` 注册入口，
  `NPerformArea` 不再持有任何具体角色类型。商人按钮场景、主菜单日夜贴图同理：
  路径由内容模组通过 `IExtraDeckSupportCharacter.MerchantCharacterButtonScenePath` 与
  `MainMenuEnvironmentCharacter.ConfigureEnvironmentTextures` 提供。

收敛路径与演奏区一致：抽接口 → 机制只依赖接口 → UI 侧机制注册实现。演奏区已经按此完成
（`IPerformArea` + `Nodes/PerformAreaMechanic.cs`），可作为模板。
