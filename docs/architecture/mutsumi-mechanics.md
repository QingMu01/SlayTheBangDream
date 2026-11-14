# 睦的三项机制：和弦书签 / 傀儡师 / 破绽

- 状态：进行中（2026-11-14 方案落盘，同日开工）
- 目标分支：`feature/mutsumi-mechanics`（基于 `beta`，本体 **0.113.0**，RitsuLib 0.6.11）
- 本体源码以 `../sts2unpack/0.113.0/src` 为准（`beta` 已适配 0.113，勿再读 0.111）
- 本轮范围：**机制代码 + 占位特效**。三套正式特效（歌单冻结 / 分组激活 / 破绽与弱点击破）另开一轮走 `vfx-authoring` 流程出方案图，再替换占位。

## 0. 需求摘要（来自用户）

1. **和弦**：卡牌叠加层的「拐角包角带」观感不好，改为**卡牌右侧的箭头型书签、垂直排列**。
2. **傀儡师**：双生命池跨战斗保存（小睦 20/20、墨缇丝 50/50，进阶减血一条作废）；战斗开始必以小睦登场，胜利切回小睦；小睦战斗中死亡 → 切墨缇丝继续打，只有墨缇丝死亡才失败；战斗前/胜利后若小睦已死 → 强制复活并恢复 1 点。
   墨缇丝形态下歌单**冻结**（不能进也不能移除，需要视觉表现）；墨缇丝「试图演奏」不发动效果，而是**激活对应分组**（另一种视觉表现）；四个分组全部激活 → 小睦复活、切回、歌单解冻，复活生命值 = 歌单容量×2（默认 4×2=8）。
   形态视觉：Spine 里 `Doll_*` 动作即墨缇丝（`Doll_Switchin` / `Doll_SwitchOut` / `Doll_Idle`）。
3. **破绽**：可对任意单位施加，每人最多 4 个；发现后给目标挂**常驻 VFX**；到 4 个时进入「弱点击破」1 回合，期间不能再被发现破绽，VFX 换形态；退出后破绽清空、VFX 淡出。玩家对弱点击破目标出牌（指向/随机/AOE，只要目标被选中），且该牌带击破效果 → 触发击破效果。发现破绽必须经 **Hook 广播**；击破触发方式与祥子余音（Subside）同构。
   - 破绽**不做 Power 图标**，只做常驻 VFX；VFX 被 Hover 时弹出**能力同款 tips 框**。

## 1. 和弦书签

### 1.1 为什么改
现有 `NChordCornerBadge` 是右上角 L 形包角带（沿上边+右边各 120px，斜切均分）。实测在卡面拐角被标题横幅压住、与大卡框边缘混在一起，辨识度差。

### 1.2 目标形态
- 卡牌**右边缘**外挂一组**箭头型书签**（ribbon bookmark），**垂直排列**：
  - 单个和弦 = 1 枚书签；多和弦（掩码）= 竖直堆叠 n 枚，枚与枚之间留 4px 缝。
  - 书签本体：矩形 + 右端 `>` 形箭头缺口（外凸 16px 左右），左端贴卡框右边缘内侧 4px，避免整枚漂在卡外。
  - 配色仍取 `PerformChordColors.ColorOf`，与演奏区槽位同源。
  - 书签垂直居中于卡框右边缘（整体居中，n 枚时为整组的中心），避开标题横幅高度。
- 新节点 `NChordBookmark : Control`（`BangDreamInSpire/Scripts/Nodes/Cards/`），删除 `NChordCornerBadge`，更新 3 处引用：
  `ChordCapability.GetCardOverlays`、`NChordDesignScreen`（结果预览与残留清理）。
- 覆盖层仍走 `ICardOverlayContributor` + `CardOverlayContribution.FromFactory(..., fullRect: false)`，节点 `MouseFilter = Ignore`，不加 `ZIndex`（与旧角标同样的层级约定：不抢前景）。
- 美术契约：书签形状**先用程序化多边形**（`Polygon2D`，无贴图）交付；保留可选贴图通道（`TexturePath` 常量，文件缺失时退化为纯色多边形），后续若要贴图美术按「书签画布」导出即可，不改代码。
- 交付时附一张 **2× 静态预览 PNG**（Python/Pillow 按同一套几何绘制，落 `.scratch/chord-bookmark/`），供快速确认排版；最终观感由用户在实机确认。

### 1.3 验收
- 1/2/3/4 个和弦时书签数量、堆叠位置、颜色正确；横幅不遮挡；卡牌库、奖励预览、和弦设计面板三处一致。
- 无和弦时不产生可见节点；旧类无残留引用（`grep NChordCornerBadge` 为空）。

## 2. 傀儡师（双形态双生命池）

### 2.1 数据结构（跨战斗）
新增 core 运行数据 `PuppeteerRunData`（`PlayerRunSavedData<T>`，注册 key `PuppeteerState`，`SyncLobbyOnChange`）：

| 字段 | 含义 |
|---|---|
| `HostHp` / `HostMaxHp` | 小睦形态当前/最大生命（初始 20/20） |
| `PuppetHp` / `PuppetMaxHp` | 墨缇丝形态当前/最大生命（初始 50/50） |
| `ActiveForm` | `Host` / `Puppet`（战斗外只允许 `Host`） |

- 初始值：角色 `StartingHp`（小睦）= 20，墨缇丝上限由角色能力接口给出（50）。进阶减血作废，不再有额外扣减。
- 角色能力接口（core，`Scripts/Interfaces/CharacterAugment/IPuppeteerCharacter.cs`）：
  ```csharp
  public interface IPuppeteerCharacter : IPerformableCharacter
  {
      int PuppetMaxHp { get; }            // 墨缇丝上限，睦 = 50
      string PuppetSwitchInTrigger { get; }   // "Doll_Switchin"
      string PuppetSwitchOutTrigger { get; }  // "Doll_SwitchOut"
      string PuppetIdleTrigger { get; }       // "Doll_Idle"
  }
  ```
  库代码不出现任何角色名；睦在 `WakabaMutsumi` 实现。

### 2.2 机制模块
`Scripts/Mechanics/Puppeteer/PuppeteerMechanic.cs`，`Id = "puppeteer"`，`Order = 250`，`Dependencies = ["perform"]`（冻结与激活都操作 `PerformManager`），`RequiredCharacterCapabilities = [typeof(IPuppeteerCharacter)]`。
每玩家状态模型 `PuppeteerFormManager : HookedSingletonModel(HookType.Combat)`（克隆 + 绑定 player，沿用 `PerformManager` 的做法）。

职责：
1. **战斗开始**（`SubmitCombatState`）：把 `ActiveForm` 归位为 `Host`；`HostHp <= 0` → `HostHp = 1`；把该形态的 HP 写入 `Creature`（`SetMaxHpInternal` / `SetCurrentHpInternal`，两者在 0.113 为 `public`）；清空本场分组激活位。
2. **死亡拦截**：`override bool ShouldDie(Creature)`（`AbstractModel`）——仅当目标是本玩家、当前 `Host` 形态时返回 `false`；`AfterPreventingDeath(creature)` 里执行**形态切换**：
   - 把当前（小睦）HP 写回 `HostHp`（此时为 0）；
   - `ActiveForm = Puppet`，`SetMaxHpInternal(PuppetMaxHp)`、`SetCurrentHpInternal(PuppetHp)`（`PuppetHp <= 0` 时按 1 兜底，避免零血复活死循环）；写回运行数据；
   - 播放 `Doll_Switchin`（`NCreature.SpineAnimation` / `CreatureAnimator.SetTrigger`），把 `PerformManager` 置为冻结并刷新演奏区占位表现。
3. **战斗胜利 / 结束**（`AfterCombatVictory`，以及 `AfterCombatEnd` 兜底）：若 `ActiveForm == Puppet` → 切回 `Host`；`HostHp <= 0` → `HostHp = 1`；把 `Host` HP 写回 `Creature`；解冻歌单；写回运行数据。战斗外始终 `Host`。
4. **分组激活**：`PerformManager` 处于冻结时，所有「演奏」入口（`PerformSlot` / `PerformChordSlots` / `RequestChordPerform` / 打出音乐牌的入队）改为**激活分组**：
   - 分组来源：卡牌 `RequestGroup` 目标 → 该分组；卡牌和弦掩码 → 掩码覆盖的全部分组；否则默认分组（C）。
   - 激活是**幂等**的，记在 `PuppeteerFormManager.ActivatedGroups`（位掩码，仅本场战斗有效）。
   - 四个分组全部激活 → 按 §2.3 复活。
5. **复活**：`HostHp = min(HostMaxHp, PerformManager.Capacity * 2)`（默认 4×2=8），`ActiveForm = Host`，写回 `Creature`，播放 `Doll_SwitchOut` 后回到 `Idle`，解冻歌单、清空激活位、刷新表现。
6. **播放列表冻结**（`PerformManager` 改动）：
   - 新增 `bool IsPlaylistFrozen`（默认 `false`）与 `SetPlaylistFrozen(bool)`；
   - 冻结时 `ModifyCardPlayResultLocation` **不再**把音乐牌送进歌单（落回原版去向），`OnCardAdded` / `HandleCardAddedInternal` 直接拒绝并说明，`Clean()` / `RemoveOverflowItems()` / 溢出移出全部跳过；
   - `NPerformArea` 增加 `SetFrozen(bool)` 与 `SetActivatedGroups(mask)`：冻结 = 槽位降饱和 + 冰霜覆盖层；激活 = 分组角标点亮/边框高亮。**本轮为占位表现**（纯色/程序化），预留正式特效挂点（节点名与信号稳定，后续只换内层视觉效果）。

### 2.3 复活生命值口径
「歌单容量」= 当前**已激活槽位总数**（`PerformManager.Capacity`）。默认四组各 1 → 4 → 复活 8 点。若容量被削减则按当前值计算，上限不超过 `HostMaxHp`。

### 2.4 边界
- 多人：`PlayerRunSavedData` 同步；形态切换只走 Hook/命令路径，不直接改远端状态。
- 墨缇丝形态下若歌单为空（没有牌可演奏）→ 仍然可以通过「打出带和弦的牌」激活分组（不要求分组里有牌）。
- 形态切换期间不得触发「死亡结算」：`ShouldDie` 只在 `Host` 返回 `false`，`Puppet` 正常死亡。
- 战斗中 `Host` 复活后，本场战斗再次死亡 → 还能再切墨缇丝（`Puppet` 形态仍存活时），规则不做额外限制，按「谁活着谁在场上」执行。
- 读档：`ActiveForm` 在战斗外强制 `Host`，因此读档后不会卡在墨缇丝。

## 3. 破绽 / 弱点击破

### 3.1 模型
core 机制模块 `Scripts/Mechanics/Break/BreakMechanic.cs`，`Id = "break"`，`Order = 600`，**全局启用**（不绑定角色：任何角色/敌人都会用到）。

- `WeaknessBreakPower`（破绽）：`PowerType.Debuff`，`PowerStackType.Counter`，上限 4（自定义上限，超出截断）。附着在**任意单位**上；死亡时随原版一起移除。
- `WeakPointBreakPower`（弱点击破，1 回合）：标记状态；在目标一侧回合结束时递减/移除；移除时清空 `WeaknessBreakPower` 并让 VFX 淡出。
- 规则常量集中在 `BreakConst`：`MaxStacks = 4`、`BreakDurationTurns = 1`。

### 3.2 发现与广播
`BreakCmd`（静态命令入口）：
```csharp
Task<int> Discover(PlayerChoiceContext ctx, Creature target, int amount, AbstractModel? source);
```
- 目标已死 / 已有 `WeakPointBreakPower` → 不生效（返回 0）。
- 层数 `min(Max, current + amount)`；层数到 4 → 施加 `WeakPointBreakPower(1)` 并切换 VFX 形态。
- **每次实际生效都要广播** `BangDreamHook.AfterWeaknessBreakDiscovered(ctx, combatState, target, amount, source)`（新增 `IWeaknessBreakHookListener`，放在 `Scripts/Interfaces/GameHook/`）。
- 进入弱点击破时额外广播 `AfterWeakPointBroken`（同一接口的第二个方法），供卡牌/遗物订阅。

### 3.3 击破效果触发（与余音同构）
- 卡牌侧接口 `IWeaknessBreakCard`（`Scripts/Interfaces/CardAugment/`）：
  ```csharp
  public interface IWeaknessBreakCard
  {
      bool HasBreakEffect { get; }   // 是否带「击破」效果
      Task OnWeakPointBreak(PlayerChoiceContext ctx, CardPlay play, Creature target);
  }
  ```
- 触发规则模型 `BreakRules : HookedSingletonModel(HookType.Combat)`（与 `LingeredResourcesRule` 同款单例）：
  - `BeforeCardPlayed` 压栈一张「本次出牌的命中集合」；`AfterDamageReceived` / `AfterPowerAmountChanged` 里 `cardSource == 本次出牌的牌` 即把 `target` 记入集合（覆盖随机/AOE/非伤害效果）；`CardPlay.Target` 也直接记入（覆盖纯指向牌）。
  - `AfterCardPlayedLate`（发现破绽一定已结算完）出栈，对集合里**处于弱点击破**的目标逐个调用 `((IWeaknessBreakCard)card).OnWeakPointBreak(...)`，再广播 `BangDreamHook.AfterWeakPointBreakTriggered`。
  - 同一张牌对同一目标只触发一次；不同目标各触发一次。
  - 嵌套出牌用栈处理（外层出牌的效果触发内层出牌时，各自结算各自的集合）。

### 3.4 常驻 VFX（本轮占位）
`NWeaknessBreakVfx : Control`（`Scripts/Mechanics/Break/`），挂到目标 `NCreature` 的 VFX 容器：
- 显示：破绽 1–3 层 = n 枚指示点（沿目标命中框上缘排列）；4 层/弱点击破 = 换「破裂」形态（不同配色 + 轻微脉冲）。
- `MouseFilter = Stop` + 命中区域；`MouseEntered` → `NHoverTipSet.CreateAndShow(this, HoverTipFactory.FromPower(power))`，`MouseExited` → `NHoverTipSet.Remove(this)`，即**能力同款 tips 框**。
- 生命周期：power 被添加/层数变化/被移除时刷新；移除后 `Tween` 淡出再 `QueueFree`。
- 正式特效替换点：`NWeaknessBreakVfx` 内部的形状绘制方法与其子节点命名；外部只依赖 `Refresh(int stacks, bool broken)` 与 `FadeOut()`。

### 3.5 视觉/文本
- 关键字：`破绽`、`弱点击破`、`击破`（core `card_keywords.json` + `static_hover_tips.json`）。Power 文本：`powers.json`（core 新建，键前缀 `BANG_DREAM_IN_SPIRE_*`）。
- 描述约定：卡牌文案用 `击破：……` 句式，由卡牌自身 `ICardDescriptionContributor` 实现（本轮无卡牌实现，先只交付接口与关键字）。

## 4. 关键决策与默认值（[自定]）

| 项 | 决定 | 理由 |
|---|---|---|
| 形态视觉 | 同一 Spine 换 `Doll_*` 动作，不换场景/不换皮肤 | Spine 内已有 `Doll_Idle/Doll_Switchin/Doll_SwitchOut/Doll_Die`，两道 skin 共用同一骨架 |
| 进阶减血 | 作废（用户确认记错塔1） | — |
| 破绽 Power 图标 | 不做 Power，只常驻 VFX + Hover tips | 用户指定 |
| 击破触发时机 | `AfterCardPlayedLate` | 保证「发现破绽」先结算，再触发击破 |
| 击破触发粒度 | 每目标一次/每张牌 | AOE 命中多个破绽目标时各自触发 |
| 复活生命 | `min(HostMaxHp, Capacity × 2)` | 需求原文 4×2=8 |
| 墨缇丝期间能否继续入组 | 不能（冻结），入组被拒绝的牌按原版去向 | 需求原文 |
| 书签美术 | 程序化多边形先行 + 可选贴图通道 | 不阻塞机制，后续可换贴图 |

## 5. 验收清单

- [ ] 构建通过：`flock .scratch/build/lock dotnet build ItsCrychic/ItsCrychic.csproj -p:Sts2Dir=$PWD/.scratch/sts2dir -p:Sts2DataDir="<本体 data 目录>"`（0 error）
- [ ] `grep NChordCornerBadge` 无残留；旧节点类已删除且引用清理完整
- [ ] 书签 1–4 个和弦的排版预览图 + 实机观感确认
- [ ] 傀儡师：小睦死亡→墨缇丝→（四组激活）→小睦复活血量=容量×2；胜利切回小睦且 1 血兜底；双池跨战斗保存
- [ ] 冻结期间音乐牌不入歌单、歌单不可移除、演奏改为激活分组
- [ ] 破绽：4 层进弱点击破、期间发现无效、1 回合后清空、VFX 形态切换与淡出、Hover 出 tips
- [ ] 击破：指向/随机/AOE 三种来源都能触发；发现→进入状态→再触发击破的顺序正确
- [ ] 本地化 JSON 合法（`python3 -m json.tool`），中英文键齐全
- [ ] 改动按单元提交，`docs/` 与本文件同步更新
