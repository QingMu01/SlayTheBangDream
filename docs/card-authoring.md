# 卡牌开发规范

本文是**卡牌实现方式的唯一事实来源**：新增、修改、重构卡牌都必须满足这里的约束。
执行流程（按什么顺序做、跑哪些校验、什么算完成）见 `.codex/skills/card-development/SKILL.md`；本文只回答「应该长什么样」。

相关文档：机制接入见 `docs/mechanic-authoring.md`，特效见 `docs/vfx-authoring.md`。

## 1. 卡牌解剖

### 1.1 位置与命名空间

```
<ModId>/Scripts/Cards/<角色>/<类型目录>/<ClassName>.cs
<ModId>/Scripts/Cards/<角色>/<类型目录>/<ClassName>.cs.uid
```

- 命名空间与 `Scripts/` 下目录结构完全对应（`ItsCrychic.Scripts.Cards.Saki.Skill`）。
- **命名空间第一段必须等于 Mod 目录名**（`ItsCrychic` / `BangDreamLib`）：卡图路径由 `PathExtensions.GetCardImg` 用 `type.Namespace.Split(".")[0]` 拼出，写错会静默回退默认立绘。
- 角色目录：`Saki`、`Mutsumi`。类型目录：`Attack`、`Skill`、`Power`、`Music`（音乐牌）。跨角色共用的 Token 放在 `Scripts/Cards/Token/`。
- 抽象基类以 `Abstract` 前缀命名。
- 每个 `.cs` 必须有同名 `.cs.uid`。UID 由 Godot 导入生成，**不要手写或复制伪造**。

### 1.2 基类与卡池注册

| 卡牌类别 | 基类 | 注册卡池 |
|---|---|---|
| 角色标准卡 | `AbstractSakikoCard` / `AbstractMutsumiCard` | `SakikoStandardCardPool` / `MutsumiStandardCardPool` |
| 角色音乐牌 | `AbstractSakikoMusicCard` / `AbstractMutsumiMusicCard` | `SakikoMusicalCardPool` / `MutsumiMusicalCardPool` |
| Token | 直接派生 `MusicCardModel` 或 `BandCardModel` | `TokenCardPool` |

- 角色抽象基类带 `[RegisterCard(typeof(XxxPool), Inherit = true)]`，子类**自动注册**，不要在具体卡类上重复标注。
- Token 卡不走角色基类，必须自己标 `[RegisterCard(typeof(TokenCardPool))]`。
- 基础契约：`BandCardModel(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true)`。
- 音乐牌：`MusicCardModel(int baseCost, CardRarity rarity, TargetType target, ...)`，类型固定为 `CardType.Quest`，实现 `IPerformCard`，效果写在 `OnPerform`；`GetResultLocationForCardPlay` 已被 sealed 为进入 `PerformPile`。

### 1.3 抽象基类职责

角色抽象基类只做两件事：注册卡池、按 `Type` 提供卡框（`FramePath`）。
新增角色时照抄同角色现有抽象基类的形状，不要在这一层写玩法逻辑。

## 2. 元数据

### 2.1 私有常量约定

每个具体卡类用私有 `const` 声明元数据并传给基类，**不要内联字面量**（静态审计脚本依赖此形态）：

```csharp
public class BandPractice() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;
}
```

音乐牌没有 `CustomType`（基类已固定 `Quest`）。

### 2.2 取值与语义

- 稀有度：`Basic`（初始牌）、`Common` / `Uncommon` / `Rare`（**标准奖励卡**）、`Token`（派生牌）。
  只有 `Common`/`Uncommon`/`Rare` 计入卡池奖励分布；`Basic` 与 `Token` 不参与规格表的标准卡集合。
- X 费：`protected override bool HasEnergyCostX => true;`，同时 `CustomCost` 传 `0`。
- 产生格挡的卡覆写 `protected override bool GainsBlock => true;`。
- `TargetType`、`CardType` 的完整取值查依赖库 XML 文档（见 `AGENTS.md`）。
- 音乐牌的手动落位目标类型（`BangDreamLib.Scripts.Utils.Infos.PerformTargetTypes`）：
  - `AnySlot`：出牌时把牌拖到**任意已激活槽位**上松开，强制进入该槽位并顶掉其中原有的牌。
  - `RequestGroup`：出牌时把牌拖到**任意分组**上松开，按卡牌自身的入队策略进入该分组（和弦与其余规则不变）。
  - 交互与原版单体目标一致：按住卡牌拖到候选槽位松手即选定（松开未指向则转入点击模式继续等待）；点击卡牌后点击候选槽位即选定，点空/右键/拖到底部取消区都取消本次出牌（不耗能、牌回手牌）。容量为 0、手柄方向导航与自动出牌不进入选择流程，按常规规则入队。
- 卡牌和弦（`BangDreamLib.Scripts.Mechanics.Perform.Chord.ChordCapability`）：
  - 和弦由 `ChordCapability` 承载，随卡牌实例存档；**只**用于卡牌打出后激活对应分组中音乐牌的奏响，不参与音乐牌的入组判定。
  - 和弦在**卡牌奖励随机生成**时赋予（`card.GetOrCreateCapability<ChordCapability>().SetChord(chord)`），没有设计期或静态赋值入口。详见 `mechanic-authoring.md` §9。

### 2.3 卡图与卡框

`BandCardModel.AssetProfile` 自动解析：

| 资源 | 路径 | 必需 |
|---|---|---|
| 立绘 | `res://<ModId>/images/card_portraits/<ClassName>.png` | 是（缺失回退 `BangDreamLib/images/sceneui/default_portrait.png`，即缺陷） |
| 升级立绘 | `res://<ModId>/images/card_portraits/<ClassName>_Bate.png` | 否 |
| 卡框 | 由角色抽象基类按 `Type` 给出 | 由基类负责 |

- 磁盘路径为 `<ModId>/<ModId>/images/card_portraits/<ClassName>.png`（`res://` 第二段是 Mod 目录）。
- 新增 PNG 后必须经 Godot 导入生成 `.import`，不要只把文件拷进仓库。
- 卡图文件名与类名严格一致，改名时同步重命名资源。

## 3. 本地化

### 3.1 键规则

| 对象 | 键 |
|---|---|
| 卡牌 | `<MOD_ID_UPPER_SNAKE>_CARD_<CLASS_NAME_UPPER_SNAKE>` |
| Power | `<MOD_ID_UPPER_SNAKE>_POWER_<CLASS_NAME_UPPER_SNAKE>` |

`BandPractice` → `ITS_CRYCHIC_CARD_BAND_PRACTICE`；`AbsoluteAuthorityPower` → `ITS_CRYCHIC_POWER_ABSOLUTE_AUTHORITY_POWER`。

文件：`<ModId>/<ModId>/localization/zhs/cards.json`、`powers.json`。

### 3.2 卡牌子键

| 子键 | 用途 | 必需条件 |
|---|---|---|
| `.title` | 卡名 | 始终 |
| `.description` | 基础/升级描述 | 非音乐牌 |
| `.subside` | 休止文本（`ISubsideCard`） | 实现 `ISubsideCard` 时 |
| `.instant` | 即兴文本（`IsInstant => true`） | 音乐牌 |

Power 子键：`.title`、`.description`、`.smartDescription`（带数值的高亮描述）。

### 3.3 动态变量与格式器

- 描述里的 `{Xxx:diff()}` / `{Xxx}` **必须**对应 `CardVars` 中真实存在的变量名，否则运行时显示异常。
- 升级差异用 `diff()`；升级分支用 `{IfUpgraded:show:升级文本|基础文本}`。
- 图标用格式器：`{LingeredResource:secondaryResourceIcons()}`、`{energyPrefix:energyIcons(1)}` 等。

### 3.4 关键字

`固有`、`消耗`、`虚无`、`保留` 由游戏自动追加说明，**不要**写进本地化。
模组关键字（`音乐`、`音符`、`歌单` 等）定义在 `BangDreamLib/BangDreamLib/localization/zhs/card_keywords.json`，引用即可。

### 3.5 同步要求

修改 Card、Power 或 Relic 时必须同步其本地化文件（`AGENTS.md`）。JSON 为运行时资源，重复键会导致加载异常，改完必须校验可解析且无重复键。

## 4. 效果实现

### 4.1 入口

| 场景 | 入口 |
|---|---|
| 普通卡打出 | `protected override async Task OnPlay(PlayerChoiceContext, CardPlay)` |
| 音乐牌演奏 | `public override async Task OnPerform(PlayerChoiceContext, CardPerform)` |
| 休止结算 | `public async Task OnSubside(PlayerChoiceContext, CardPlay)`（`ISubsideCard`） |
| 升级 | `protected override void OnUpgrade()` —— 只改动态变量与关键字，**不改描述文本** |

### 4.2 动态变量

- 声明：`protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Damage.Create(6)];`
- 读取：`DynamicVars.Block` 或 `QuickVar.Buff.GetVar(this).IntValue`
- 同名多变量：`QuickVar.Block.Create("ExtraBlock", 3)`，读取 `(BlockVar)DynamicVars["ExtraBlock"]`
- 升级：`DynamicVars.Block.UpgradeValueBy(2m)` / `QuickVar.Buff.GetVar(this).UpgradeValueBy(1m)`

### 4.3 关键字

- 静态：`protected override IEnumerable<CardKeyword> CardKeywords => [CardKeyword.Exhaust];`
- 升级后增删：`AddKeyword(CardKeyword.Innate)` / `RemoveKeyword(CardKeyword.Ethereal)`
- 模组关键字常量在 `BangDreamConst`（`Music`、`MusicNote`、`PerformArea` 等）。

### 4.4 悬浮提示

自定义 Power、关键字、被引用卡牌都应提供 `CardHoverTips`：

```csharp
protected override IEnumerable<IHoverTip> CardHoverTips =>
[
    HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    HoverTipFactory.FromPower<StrengthPower>(),
    HoverTipFactory.FromCard<MelodyFragments>(IsUpgraded)
];
```

### 4.5 结算时序与机制接入

- 需要玩法结算的异步效果（音符等）必须走原版动作队列（`RitsuLibManagedNetActions.Request`），遵循「发射即锁定、命中才结算」，详见 `docs/mechanic-authoring.md` 第 7 节。
- **VFX 不得驱动玩法状态**。
- 音乐牌实现 `IPerformCard`（由 `MusicCardModel` 提供），休止牌实现 `ISubsideCard`；额外卡组 / 演奏 / 余音 / 音符机制见 `docs/mechanic-authoring.md`。
- 特效见 `docs/vfx-authoring.md`；特效基类默认继承 `Node2D`。

### 4.6 效果语义口径

元数据一致不等于效果正确。伤害口径、触发条件、状态生命周期、历史窗口、牌堆范围、联机归属、动态变量与 UI 必须逐项对照
`.codex/skills/card-development/references/effect-audit-checklist.md`。

## 5. 三种变更的影响面

| 变更 | 判定 | 必改文件 | 验证重点 |
|---|---|---|---|
| **新增** | 卡池中不存在该类名 | 卡类 `.cs` + `.cs.uid`；卡图 PNG（+ `.import`）；`cards.json` 的 `.title`/`.description`（+ `.subside`/`.instant`）；若新增 Power 则加 `Power/*.cs` + `.uid` + `powers.json` | 注册生效、本地化齐全、描述与实现一致、构建通过 |
| **修改** | 类名不变，改数值/描述/关键字/效果 | 卡类 `.cs`；同步 `cards.json`；涉及 Power 时同步 `powers.json` | 描述与实现一致、升级分支正确、旧行为无残留 |
| **重构** | 类名/稀有度/类型/目录/卡池发生迁移，或按规格表批量对齐 | 迁移涉及的全部文件 + 引用点 + 本地化 + 资源 | 双向集合相等、分布一致、无残留引用、构建通过 |

### 5.1 新增卡牌清单

1. 新建 `Scripts/Cards/<角色>/<类型>/<ClassName>.cs` 与 `.cs.uid`。
2. 放入卡图 `images/card_portraits/<ClassName>.png`（可选 `_Bate.png`），经 Godot 导入生成 `.import`。
3. 在 `cards.json` 添加 `.title` 与 `.description`（音乐牌用 `.instant`，休止牌加 `.subside`）。
4. 引入新 Power 时：`Scripts/Power/<Buff|Debuff|Temporary>/<Xxx>Power.cs` + `.uid` + `powers.json`。
5. 注册：角色卡由抽象基类 `Inherit = true` 自动完成；Token 需 `[RegisterCard(typeof(TokenCardPool))]`。
6. 构建并实机确认卡池可见、描述正确。

### 5.2 重构与清理

- 名称变化时同步标题、描述与所有代码引用，不依赖旧标题残留。
- 删除表外卡牌时删除 C#、UID、本地化条目及专属无用 Power；删除前用 `rg` 搜索类名、模型 ID、本地化键与资源名。
- **不直接删除仍可能复用的卡图**：在 `images/card_portraits/DEPRECATED.md` 记录废弃文件与原因（该文件目前不存在，首次需要时创建）。资源不存在时如实记录「无对应资源」，不要虚构文件。
- 移动资源时优先保留原 UID。
- 只改任务范围内文件；与用户已有修改重叠时采用最小补丁。

## 6. 验收

### 6.1 机器可查

1. 静态审计脚本问题数为 0（见 skill 的 `scripts/audit_cards.py`）。
2. 所有修改过的 JSON 可解析且无重复键。
3. 已删除的类名、模型 ID、本地化键在代码与运行时本地化中搜索结果为 0。
4. `git diff --check` 通过。
5. 完整构建受影响项目。**若构建目标会写入游戏目录，用工作区内临时 `Sts2Dir` 重定向输出，验证后移除临时目录。**

### 6.2 语义可查

按 `effect-audit-checklist.md` 逐卡核对，不留未解释项。编译通过不能替代语义核对。

### 6.3 交付

用 Markdown 表格列出本次实际修改的全部文件，排除用户原有的无关工作区改动。
