# 机制模块与角色接入指南

BangDreamInSpire 的玩法机制被拆分为若干**机制模块**（mechanic）。每个模块自行声明它需要注册的内容、注入的补丁、玩家状态模型与战斗生命周期钩子；核心初始化流程（`BangDreamInSpireCore`）不再感知任何具体机制。角色只需实现对应**能力接口**即可自动接入机制，库代码中不出现任何具体角色名。

## 1. 现有机制模块

| 模块 | Id | 顺序 | 依赖 | 启用条件（角色接口） | 承载内容 |
|---|---|---|---|---|---|
| 卡面覆盖层 | `overlay_factories` | 10 | — | （全局） | 向 `CardOverlayRegistry` 注册和弦角标 / 休止提示的 UI 工厂 |
| 额外卡组 | `extra_deck` | 100 | — | `IExtraDeckSupportCharacter` | 额外卡池/额外卡组/额外抽牌堆、小祥商人、额外卡组删牌、音乐牌奖励、额外卡组升级休息选项、战斗开始克隆额外抽牌堆 |
| 演奏 | `perform` | 200 | `extra_deck` | `IPerformableCharacter` | 歌单容量与入队/重排/溢出、演奏触发、音乐卡牌类型铭牌、手动落位目标类型（AnySlot / RequestGroup）、卡牌和弦（`ChordCapability`） |
| 演奏区 | `perform_area` | 210 | `perform` | `IPerformableCharacter` | 演奏区节点创建与摆放（实现 `IPerformArea`），就绪后回填给 `PerformManager` |
| 傀儡师 | `puppeteer` | 250 | `perform` | `IPuppeteerCharacter` | 双生命池的跨战斗保存、本体/傀儡形态切换（死亡拦截）、傀儡形态下的歌单冻结与分组激活 |
| 余音 | `lingered` | 300 | — | `ILingeredResourceCharacter` | 余音二级资源与卡面费用 UI、休止结算与驱动、环绕资源预览 |
| 异步伤害 | `async_damage` | 350 | — | （全局） | 「有动画的伤害不卡手」的公共底座：批次发射、命中信号驱动结算、独立同步结算动作、回合结束屏障；音符只是它的一个实现 |
| 视觉动画 | `visuals_anim` | 360 | `async_damage` | （全局） | 「有位移的表现不卡手」：按生物串行、脱手推进的角色演出时间线；视觉代理（隐藏原体 + 克隆视觉）与 `VfxSpawnPosition` 重定向 |
| 音符 | `music_note` | 400 | `async_damage` | （全局） | 音符发射/弹跳、命中同拍结算、音符伤害追踪、VFX 容器 |
| 破绽 | `break` | 600 | — | （全局） | 破绽层数上限与「弱点击破」状态、发现/进入/击破三个 Hook 广播、常驻 VFX、击破效果触发规则 |

`Order` 越小越先装配；`Dependencies` 中的机制必须存在，否则装配阶段抛异常。

> **余音上限是硬上限「7」**：`LingeredResourcesRule.HardMaxAmount = 7` 同时用于溢出转换与兜底扣减，
> 任何提升二级资源上限的效果对余音无效（动态上限只能把它压得更低）。不要在其它位置再引入第二套上限口径。

机制按 `Assembly.GetTypes()` 自动发现（`IBangDreamMechanic` 的实现），因此新增模块只需新建类，无需登记。
**机制层不得直接依赖 `Scripts/Nodes/**`**：需要 UI 时按 `perform_area` / `overlay_factories` 的范式，
把节点创建留在 `Nodes` 侧，机制只依赖 `Scripts/Interfaces/Mechanics/**` 里的契约。边界由
`python3 tools/check-architecture.py` 检查（见 `docs/architecture/coupling-boundaries.md`）。

## 2. 角色如何接入机制

角色继承 `BandMemberModel<TCardPool, TRelicPool, TPotionPool>` 后，只需实现目标能力接口：

```csharp
public sealed class TogawaSakiko()
    : BandMemberModel<SakikoStandardCardPool, SakikoRelicPool, SakikoPotionPool>(...),
      IPerformableCharacter, ILingeredResourceCharacter
{
    public IPerformScheme CreatePerformScheme() => new SakikoPerformScheme(); // IPerformableCharacter：歌单分组与容量
    public bool AutoGenerateSubsideResource => true;    // ILingeredResourceCharacter：自动生成余音
    public CardPoolModel ExtraCardPool => ModelDb.CardPool<SakikoMusicalCardPool>(); // IExtraDeckSupportCharacter
    public bool ShouldAlwaysShowExtraDeck => true;
    public bool ShouldAlwaysShowExtraPile => true;
}
```

- `IPerformableCharacter` 继承 `IExtraDeckSupportCharacter`，因此实现演奏能力的角色自动获得额外卡组能力。
- 演奏方案实现由角色模组提供（本仓库示例见 `ItsCrychic/Scripts/Character/PerformSchemes/`）；核心只保留
  `EmptyPerformScheme` 与 `IPerformScheme` 契约，不包含角色名。
- `BandMemberModel` 已自带 `ISkinSupportCharacter`（皮肤）、`IGroupableCharacter`（角色选择分组）、`IBangDreamMateData`（主菜单资源）。
- 卡牌侧：`IPerformCard`（音乐牌）、`ISubsideCard`（休止牌）决定卡牌参与哪些机制。

## 3. 新增一个机制模块

1. 在 `BangDreamInSpire/Scripts/Mechanics/<Name>/` 下新建类，实现 `IBangDreamMechanic`：

```csharp
public sealed class MyMechanic : IBangDreamMechanic
{
    public string Id => "my_mechanic";
    public int Order => 500;
    // RequiredCharacterCapabilities 是预留字段，当前核心装配不读取；能力判定写在 manager 内。

    public void RegisterContent(BangDreamMechanicContext context)
    {
        // 关键字 / 牌堆 / 二级资源 / 奖励 / 节点附加 / Run 数据 / 能力注册
        // 生命周期：context.SubscribeLifecycle<ModelRegistryInitializedEvent>(...)
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatch<MyPatch>();
    }

    // 可选：玩家状态模型（通常为单例模型的克隆）
    public IEnumerable<AbstractModel> InstantiatePlayerState(Player player)
    {
        var manager = (MyManager)ModelDb.Singleton<MyManager>().MutableClone();
        manager.Player = player;
        yield return manager;
    }

    // 可选：战斗期间需要被原版 hook 迭代的模型
    public IEnumerable<AbstractModel> GetCombatHookModels(Player player)
    {
        yield return player.AttachedData().MyManager;
    }

    // 可选：战斗开始/结束的每玩家订阅
    public void SubmitCombatState(Player player) => player.AttachedData().MyManager.SubmitCombatState();
    public void UnsubscribeCombatState(Player player) => player.AttachedData().MyManager.UnsubscribeCombatState();
}
```

2. 完成。`BangDreamInSpireCore` 会通过反射自动发现并装配该模块，**无需修改核心或其它机制**。

## 4. 契约参考

### `IBangDreamMechanic`

| 成员 | 说明 |
|---|---|
| `Id` | 唯一标识，用作补丁分组名与去重键 |
| `Order` | 装配排序权重，小者先装配 |
| `Dependencies` | 其它机制的 `Id`；缺失或成环抛异常 |
| `RequiredCharacterCapabilities` | 预留字段：当前核心装配不读取；能力判定在各 manager 内完成 |
| `RegisterContent(ctx)` | 注册静态内容 |
| `RegisterPatches(patcher)` | 注册本机制的 Harmony 补丁（全量注入，时机与核心一致） |
| `InstantiatePlayerState(player)` | 为该玩家实例化机制状态模型 |
| `GetCombatHookModels(player)` | 战斗中需被订阅的模型 |
| `SubmitCombatState` / `UnsubscribeCombatState` | 战斗开始 / 结束的每玩家钩子 |

### `BangDreamMechanicContext`

`ModId`、`RunData`、`Content`、`Keywords`、`CardTags`、`CardPiles`、`Rewards`、`SecondaryResources`、`NodeAttachments`、`SubscribeLifecycle<T>()`、`RegisterCardKeyword()`、`RegisterCardTag()`。

### `BangDreamCapabilities`

能力**粗判**的统一入口，只服务装配 / UI / 补丁的粗粒度判定；核心内部需要访问接口成员（读方案、取配置等）时仍可直接 `is IXxx`，不要求全面收口：

`HasExtraDeck`、`HasPerformArea`（实现演奏接口且 `CreatePerformScheme().TotalSlotCount > 0`，裸 `is IPerformableCharacter` 不等于它）、`HasLingered` 均有 `CharacterModel` / `Player` 重载；`IsPerformCard`、`IsSubsideCard` 接收 `CardModel`。

## 5. 兼容性约束

- 下列类型是面向第三方角色 mod 的稳定 API，**不随机制内部迁移而改变命名空间**：
  `Scripts/Interfaces/**`、`Scripts/Utils/Infos/*`（`CardPerform`、`PerformContext`、`PerformEnqueueRequest`、`PerformTargetTypes`、`VfxContext`、`SkinInfo` 等）、
  `Scripts/Cards/BandCardModel`、`MusicCardModel`、`Scripts/Powers/BandPowerModel`、`Scripts/Relics/BandRelicModel`、
  `Scripts/Character/BandMemberModel`、`Scripts/Utils/BangDreamConst`、`Scripts/Utils/BangDreamHook`、`Scripts/Extensions/**`。
- 机制实现类（`PerformManager`、`MusicNoteCmd`、`ExtraPileCmd`、`MusicCardReward`、`TechnicalPracticeOption` 等）
  仍位于 `Scripts/Mechanics/<Name>/`，但**不再作为内容模组的公开入口**：
  - 演奏状态通过 `AttachedData().Perform`（`IPerformService`）访问；槽位表现通过 `IPerformSlot`。
  - 音符发射用 `MusicNoteExtensions.ShootMusicNotes`；额外抽牌用 `ExtraDeckExtensions.DrawExtraCards`。
  - 和弦读写用 `ChordExtensions.GetChord/SetChord`；音乐牌奖励与休息处选项用 `ExtraDeckRewardExtensions`。
  - 音符伤害统计用 `AttachedData().MusicNoteTracker`（`IMusicNoteTracker`）。
- 关键字与卡牌标签的数值由 `XxHash32(id)` 确定性生成，与注册顺序无关，因此模块装配顺序调整不影响存档与网络兼容性。

## 6. 已知边界

- 游戏本体不支持运行时卸载 mod（`Mod.Unloadable` 写死为 `false`），因此「热插拔」在运行期体现为**按角色能力自动启用**，而非卸载 DLL。
- 补丁仍在初始化阶段全量注入；机制是否生效由运行时的角色能力判定控制。

## 7. 异步伤害结算时序约定

### 7.1 定位

`async_damage` 解决的是**「有动画的伤害会卡手」**：动画本身必须播，但播的这段时间玩家还要能继续出牌。
它的契约与具体表现类型无关——**音符只是它的一个实现**，任何「先播动画、命中时刻才结算」的伤害
（冲刺斩击、下落打击、光束、飞刀……）都通过同一个 `IAsyncDamageEffect` 接入。

判定标准：这个伤害的**表现时长**会让出牌手感变差吗？会，就用异步伤害；不会（< 0.3s 的普通攻击），
走本体的 `AttackCommand` 即可。

### 7.2 契约

`Scripts/Mechanics/AsyncDamage/IAsyncDamageEffect.cs`，实现者不继承任何 VFX 类型：

| 成员 | 时机 | 语义 |
|---|---|---|
| `BeforeSpawnAsync` | 每个根动画启动前 | 起手动作 / 批次内节奏。跑在脱手泵里，不占同步 Action |
| `GetLockedDamage` | 发射时刻 | 计算并**锁定**本次伤害；同时用于目标预留 |
| `StartAnimationAsync` | 发射时刻 | 启动动画并尽快返回句柄，**不得等待动画结束** |
| `ResolveDamageAsync` | 命中后 | 在独立同步结算 Action 内施加 `LockedDamage` |
| `AfterBatchAsync` | 批次全部落地后 | 收尾（如角色归位） |
| `GetTargetEffectiveHealth` | 发射时刻 | 目标预留用的有效血量口径 |

`AsyncDamageAnimationHandle.Landing` **必须**在动画抵达目标时完成；`Completion` 只用于观察生命周期，不驱动玩法。

**`BeforeSpawnAsync` 必须真的 `await`**（时序前提，不是风格问题）：表现泵是 `_ = PumpAsync(...)` 启动的，
而异步方法会**同步执行到第一个真正未完成的 await**。若 `BeforeSpawnAsync`（或紧随其后的
`StartAnimationAsync`）同步完成，整批会在发射方那个同步 Action 里一路推进到结束——「不占同步 Action」
对同步实现**不成立**。需要真正脱手的实现应在首个 await 处显式让出（`await Task.Yield()` 或等一帧）
再开始起手动作。

### 7.3 时序

1. 发起方（卡片 / 能力 / 遗物）把请求交给原版动作队列（`RitsuLibManagedNetActions.Request`），
   **立即返回，不阻塞出牌**。单机同样经由该队列。
2. 请求 Action 在各端执行 `LaunchAsync`：按注册顺序为整批根动画**一次性锁定**目标与伤害
   （`ReserveTarget` / `GetLockedDamage`）。飞行期间不再读取任何可变状态，故多端一致。
3. 表现层脱手：`CombatAsyncDamageManager` 用一条**表现泵**（`PumpAsync`）按注册顺序启动动画，
   不占用动作队列；`BeforeSpawnAsync` 负责批次内节奏。
4. **命中时刻由表现信号驱动**：`NBangDreamFlyingVfx.HitTriggered` → `VfxHandle.Arrived`
   → `AsyncDamageAnimationHandle.Landing`。**不用「距离 / 速度」推算**，避免与表现层的 Speed / 缓动耦合。
5. 命中时由**发射者**（`IsLocalDealer`）请求一条独立的同步结算 Action（`AsyncDamageResolveCmd`），
   各端在**同一队列位置**执行 `ResolveAsync` 并施加已锁定伤害。
6. 该结算 Action 被 `AsyncResolveNotPlayerDrivenPatch` 标记为「非玩家驱动」，
   使其在回合结束阶段仍能执行，不与回合结束屏障互相死锁。
7. 弹跳：上一目标命中后才登记并发射下一枚；`ExcludePreviousTargetFromChain` 决定是否排除上一目标。
8. 目标在命中前已不可命中 → 该次伤害**丢弃且不重定向**（确定性最强）。
9. 批次全部落地后执行 `AfterBatchAsync`，随后 `CheckWinCondition`。

**关键点：发射与结算被拆成两条独立的同步 Action，飞行发生在它们之间且不占队列。**

### 7.4 屏障与回合结束

`CombatResolutionBarrier` 按战斗登记尚未完成的批次（每批一个租赁，批次结束时释放）。
`WaitForAsyncDamageResolutionPatch` 让 `WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction`
在队列空后继续等待屏障，避免回合切换时在飞伤害被丢弃。
战斗结束由 `AsyncDamageMechanic` 订阅 `CombatEndedEvent` 调 `OnCombatEnded` 强制完成，
防止租赁悬挂让回合永远推进不了。

**覆盖范围：当前只有「玩家结束回合」这一个转场**。等待点挂在
`CombatManager.WaitUntilQueueIsEmptyOrWaitingOnNonPlayerDrivenAction` 上，而本体**只**在玩家结束回合的转场里
调用它。敌方回合结束、强制结束战斗等其它转场**不会**等在飞伤害：那里的批次要等命中信号到达才继续，
观感上结算可能落到下一个阶段之后；若该转场里战斗直接结束，`OnCombatEnded` 会清掉在飞记录并强制完成屏障，
**这部分伤害不再产生**。扩大覆盖范围（新增补丁目标）属于产品语义决策——每多挂一个转场就多一次跨端同步等待，
不要顺手加。

### 7.5 退化路径

| 场景 | 行为 |
|---|---|
| `NonInteractiveMode`（测试 / 无渲染） | `LaunchAsync` 走 `ResolveInlineAsync`：仍调一次 `StartAnimationAsync` 拿上下文，但不等待命中，按注册顺序就地结算 |
| 动画启动失败 / 战斗状态失效 | `FailNoteAsync` 兜底落地：释放预留、推进批次，保证屏障最终完成 |
| 表现泵异常 | `PumpAsync` 的 `finally` 调 `DrainQueueAsync`，把队列中剩余音符全部兜底落地 |
| 节点被移除 / 战斗结束 | `BangDreamVfxManager` 在 `TreeExited` / `VfxFinished` 时完成句柄，避免 `await` 永久挂起 |

> 注意：最后一条由**表现实现方**（`NBangDreamFlyingVfx` 系）提供，核心不保证。新接入其它表现类型时，
> 必须自己提供完成路径（或退回 `FailNoteAsync` 的兜底），不能假设 `BangDreamVfxManager` 会替你收口。

### 7.6 已知边界（尚未实现）

- **拿不到批次句柄**：交互模式下 `LaunchAsync` 返回 `Task.CompletedTask`（非交互模式返回 inline 任务），
  调用方无法「等这批落地再发下一批」。
  需要跨批次串联的编排（演出 → 伤害 → 演出 → 伤害）目前只能靠 `Cmd.Wait` 估算，不精确。
- **批次内多次命中无法串行**：`PumpAsync` 只等到 `StartAnimationAsync` 返回就启动下一枚，**不等 `Landing`**。
  这对音符是对的（各飞各的），但对「同一角色连续两次冲刺」是错的——两段位移会叠在一起。
- `FlushPendingAsync` 当前**没有调用点**，是预留的「就地落地」入口（注释里提到的 Finale / Moonlight 未接入）。

## 8. 音乐牌的手动落位目标类型

`PerformTargetTypes` 提供两个供音乐牌使用的 `TargetType`：

| 目标类型 | 语义 | 落位 |
|---|---|---|
| `AnySlot` | 指定任意已激活槽位（可跨分组） | 强制进入该槽位，槽位原有卡牌离开歌单（等价 `PerformEnqueueStrategy.Fixed`） |
| `RequestGroup` | 指定一个和弦分组，其他规则不变 | 只把分组换成所选分组，组内仍按卡牌自身的入队策略落位；卡牌和弦不改写（和弦不参与入组，见 §9） |

实现要点：

- **枚举值来源**：`CustomTargetType.RegisterMultiTargetType(BangDreamConst.ModId, "<stem>", _ => false)`，ID 形如 `BANG_DREAM_IN_SPIRE_TARGETTYPE_ANY_SLOT`，数值由 ID 确定性生成。词干（`any_slot` / `request_group`）**冻结**：改名会破坏存档与联机一致性。
- **注册为「群体目标类型 + 恒假谓词」**：RitsuLib 的目标类型只表达生物目标，无法表达槽位目标；注册为群体且不选任何生物，可让原版与 RitsuLib 的目标判定（`IsValidTarget(null)`、`CanPlayTargeting(null)`、目标指示器）都按「无需生物目标」处理。
- **出牌交互**：`PerformSlotTargetingPatches` 接管 `NMouseCardPlay.TargetSelection`、`NMouseCardPlay.IsCardInPlayZone` 与 `get_PlayZoneThreshold`、`NCardPlay.TryPlayCard`、`NCardPlay.Cleanup`，以 `NHandCardHolder.BeginDrag` 兜底；主入口是 `TargetSelection`（与 RitsuLib 处理自定义目标类型的方式一致）。任一钩子未生效时按常规规则出牌，不会出现打不出去的情况。
- **选择态 UI**：`NPerformArea` 在候选槽位上叠加高亮，指向判定优先用 `NPerformItem` 的 `MouseEntered/MouseExited`（Godot 自身拾取），再用「卡片容器全局中心 + 全局变换基向量长度换算半尺寸」兜底。半尺寸必须用**全局变换的基向量长度**而非节点自身的 `Scale`，否则父级带缩放时命中框会被算小。
- **原版交互**：接管后先 `CenterCard()` 归位，再复刻原版单体目标的判定——拖拽模式松开时指向候选槽位即选定，未指向则转入点击模式；点击模式点候选槽位即选定，点空取消；右键、Esc、手牌快捷键、拖到底部取消区都取消本次出牌（不耗能）。指示箭头复用 `NTargetManager` 的 `TargetingArrow`，不复用 `MultiCreatureTargeting`（它每帧把卡牌位置设为鼠标位置，会让箭头退化）。
- **回退规则**：容量为 0、无候选槽位、手柄方向导航、自动出牌（`CardCmd.AutoPlay` / `--autoslay`）、以及 `RequestGroup` 只有一个可达分组时**不进入选择态**，按卡牌自身规则出牌；一旦进入选择态，则完全按原版单体目标的判定收尾。每次进入/跳过/模式切换/收尾都写日志（`Manual slot selection ...` / `Manual slot targeting ...`）。
- **联机**：落位随 `Enter` 生命周期动作下发（`PerformNetworkActionPayload.RequestSlotIndex` / `RequestGroupMask`），各端在 `HandleCardAddedInternal` 内使用同一请求计算落位；缺字段（旧版本 payload）等价于无覆盖。
- **一次性语义**：请求写在卡牌的 `PerformContext.Request` 上，入队规划消费后即清空；出牌被取消时一并清空（`CancelManualSlotSelection`）。

## 9. 卡牌和弦

和弦是**卡牌**的属性，由 RitsuLib 模型能力 `ChordCapability` 承载：随卡牌一同克隆、随卡牌实例存档。

| 项 | 约定 |
|---|---|
| 用途 | **只**用于卡牌打出后激活对应分组中音乐牌的奏响（角色规则经 `IPerformTriggerListener` 读取，如 `MutsumiSpecialRules`）：该分组内**全部**已落位卡牌一起奏响；多和弦卡牌会同时激活其全部和弦分组。 |
| 不参与入组 | 音乐牌进入哪个分组由 `PerformTargetTypes.RequestGroup` 指定，未指定时落回 `IPerformScheme.DefaultGroup`（`PerformCapacityState.ResolveDefaultGroup()`，不读取和弦）。 |
| 赋值 | `card.GetOrCreateCapability<ChordCapability>().SetChord(chord)`；传 `PerformChord.None` 会移除该能力。 |
| 赋值时机 | 当前只有两处赋值：控制台命令，以及**休息处选项「和弦设计」**（仅睦）让玩家手动给卡组里的一张牌指定和弦（见 §9.2）。卡牌奖励流程尚未接入和弦。 |
| 读取 | `card.TryGetCapability<ChordCapability>(out var capability)`，没有该能力即无和弦。 |

- 能力类型由 `[RegisterModelCapability]` 自动注册，无需手动初始化。
- 和弦不参与入组判定，因此与 `RequestGroup` 目标类型正交：手动落位只换分组，不改写和弦。
- 奏响由 `PerformManager.PerformChordSlots(mask)` 执行：逐个奏响掩码覆盖分组内**已落位卡牌所在的槽位**（按槽位升序，跳过即兴牌），并输出 `Chord perform <mask> : slots [...]` 日志便于核对。

### 9.1 卡面书签

和弦通过能力实现的**卡面覆盖层**（`ICardOverlayContributor`）画在卡牌**右边缘**：`ChordCapability` 返回一个
`CardOverlayContribution`（`FromFactory`），节点是 `NChordBookmark`；RitsuLib 在 `NCard.ReloadOverlay()` 之后
把能力覆盖层挂到 `%OverlayContainer` 下并重建，所以卡牌模型刷新时书签自动跟随，无需自己订阅事件。

| 项 | 约定 |
|---|---|
| 书签几何 | 卡框**右边缘**的竖直色带 + 右端向右凸出的 `>` 形箭头尖端：矩形段 `x∈[146,156]`（左端压入卡框 4px、右端伸出 6px），尖端 `x=166`（相对右边缘外凸 16px），单枚高 20px。卡框 `x±150`、`y±211`（卡中心坐标）。 |
| 堆叠 | n 个和弦 = n 枚书签按 `C→D→F→G` 自上而下堆叠，枚间缝 4px，整组在卡框右边缘**垂直居中**（组高 `n×20+(n−1)×4`，1 枚时组顶 −10、4 枚时 −46）；组顶另有 −160 的硬下限，确保 1~4 枚都不会顶到标题横幅（横幅占 `y∈[−207,−124]`）。实现见 `NChordBookmark.BuildBookmarkRects` / `BuildBookmarkShape`。 |
| 渲染 | 纯程序化 `Polygon2D` 五边形 + 同色系加深的 `Line2D` 描边；可选贴图通道 `res://BangDreamInSpire/images/sceneui/chord_bookmark.png`（单枚画布 20×20，按 2× 导出，顶点 UV 映射），贴图缺失时退化为纯色多边形（形状与成品一致，可直接验收排版）。`images/**` 不入库，新克隆缺图会自动走占位。 |
| 配色 | `PerformChordColors` 是唯一来源：C `#7799CC`（祥子）、D `#FFDD88`（素世）、F `#77BBDD`（灯）、G `#7777AA`（立希）。`MutsumiPerformScheme.GetSlotColor`（演奏区四组槽位）与 `CrychicMemberEnum`（角色色）都引用这张表。 |
| 层级 | 能力覆盖层挂在 `%OverlayContainer` 下（排在 `TitleBanner`/`TitleLabel` 之前），书签是「贴在卡框侧边的书签」，不抢卡面前景。**不要**用 `ZIndex` 抬高它：Godot 的 z_index 在同一 canvas 内全局排序，会把书签抬到面板、HUD 等所有界面之上；也**不要**重排 `OverlayContainer` 的画布顺序（卡牌入场时父节点正处在 AddChild 构建阶段，同步 `MoveChild` 会被 Godot 拒绝并报 `Parent node is busy setting up children, move_child() failed`，结果只会让不同界面表现不一致）。 |
| 层级风险 | 书签尖端外凸 16px 依赖父级不裁剪。`card.tscn` 的 `%OverlayContainer` / `CardContainer` 均未开 `clip_contents`；若后续宿主容器开启裁剪，把 `BookmarkTipRight` 收到 ≤150 或改为「贴右缘竖直条 + 内凹箭头」即可，不影响其它逻辑。 |
| 调试 | 控制台 `chord CDF` / `chord 13` / `chord none`：给手牌设和弦并强制重建覆盖层，用于预览 1~4 枚书签的堆叠（`ChordConsoleCmd`）。 |

### 9.2 和弦设计（休息处选项）

睦的休息处多出「和弦设计」选项（`ChordDesignOption : ModRestSiteOptionTemplate`，由
`MutsumiSpecialRules.TryModifyRestSiteOptions` 在 `player.Character is WakabaMutsumi` 时注入）。
选择后打开自绘覆盖层面板 `NChordDesignScreen`（`BangDreamInSpire/Scripts/Nodes/Screens/`）：

| 项 | 约定 |
|---|---|
| 布局 | 三行居中排布（场景 `BangDreamInSpire/BangDreamInSpire/scenes/screens/chord_design_screen.tscn`，代码只填内容）：顶部和弦筹码（C/D/F/G，**始终同一行，不拆行**）→ 中央箭头 → 卡组卡牌行（**一格一张牌、始终同一行**）→ 箭头 → 居中结果预览卡。点筹码时**整行水平平移**，让选中筹码对齐到中央箭头正上方（其余筹码按原顺序让位、不交换；取消选中后整行回正）；点卡牌同样**整行水平平移**，让那张牌对齐下方箭头并保持常驻行内，结果区同步显示它的预览与和弦。打开时默认 C 和弦 + 第一张牌，两者都停在中轴上。左右分别是原版 `NBackButton` / `NConfirmButton`（取消返回、确定应用）。卡牌带高 406px（y294–700），行内容再乘 `CardLayerScale = 0.9`（静止 0.8×0.9 = 0.72 倍、悬停 1.0×0.9 = 0.9 倍），余量按「悬停放大后不被 `clip_contents` 裁切」定；箭头在 274–310 / 748–784，三行在 1080 高内紧凑分布。 |
| 结果 | `ChordDesignResult(CardIndex, Chord, Additive)`；面板返回后由选项在 `Player.Deck.Cards` 上 `GetOrCreateCapability<ChordCapability>().SetChord(...)`——**确定之前卡组模型完全不变**。面板内的卡牌只是 `NCard` 视图（`NodePool` 复用节点）；结果预览先隐掉卡面自带的能力书签，再把按「结果和弦」构建的书签挂到**预览卡的 `%OverlayContainer`**（继承卡的缩放/变换，和真实卡牌一样位于卡面前景之下）。预览卡回池前与新建卡后都会 `RemoveStaleChordBookmarks` 清掉直挂的残留书签，避免旧书签随节点复用被带到别的卡上。 |
| 收尾 | 确定后在 `DoLocalPostSelectVfx` 复用本体 `NCardUpgradeVfx`（与升级卡牌同一套表现）：屏幕中央放大展示（此时书签已是新和弦）→ 停留片刻 → `NCardFlyVfx` 飞入顶部卡组图标；选项 `AssetPaths` 追加 `NCardUpgradeVfx.AssetPaths` 以便预载，`DoLocalPostSelectVfx` 只等展示段结束（约 1.4–1.9s），飞行交给后场继续。 |
| 增加 / 覆盖 | 结果右侧开关默认「覆盖」，`Additive=true` 时与现有和弦取并集。开关默认隐藏（`NChordDesignScreen.ModeToggleUnlocked = false`），留给后续事件遗物解锁。 |
| 联机 | `OnSelect()` 在每个 peer 执行：本地玩家用 `ReserveChoiceId` + 面板结果 + `SyncLocalChoice(PlayerChoiceResult.FromIndexes([cardIndex, mask, additive]))`，其余 peer `WaitForRemoteChoice` 后以相同参数改卡；取消时同步空列表并 `return false`。 |
| 滚动 | 本体滚动容器只有纵向，面板自绘 `NChordScrollbar`（横向轨道）：滚轮按格步进、拖条吸附到最近的牌、键盘/手柄焦点进入某张牌也会把整行带到它上面；滑块位置 = 选中牌在卡组里的序号比例，可视比例按 `1/牌数` 计算（内容不足一屏时隐藏）。 |
| 接线 | 注入依赖玩家**拥有** `MutsumiSpecialRules`（`Hook.ModifyRestSiteOptions` 只遍历 `runState.IterateHookListeners` 里的已拥有模型）。该遗物已加 `[RegisterRelic(typeof(DeprecatedRelicPool))]`，两个睦皮肤 `skins/mutsumi/*.json` 的 `StartingRelics` 追加了 `ITS_CRYCHIC_RELIC_MUTSUMI_SPECIAL_RULES`（保留 `ITS_CRYCHIC_RELIC_SAKIKO_SPECIAL_RULES` 以复用音乐牌奖励逻辑）。 |
| 素材 | 选项图标 `res://BangDreamInSpire/images/sceneui/music_draw.png`（`images/**` 不入库，缺图时图标缺失但不影响逻辑）。 |

## 10. 傀儡师（双形态双生命池）

`puppeteer` 机制由 `IPuppeteerCharacter` 启用（继承 `IPerformableCharacter`，因此自动带演奏与额外卡组）。
状态分两层：跨战斗的双池在 `PuppeteerRunData`（`RunData.RegisterPerPlayer("PuppeteerState")`，`SyncLobbyOnChange`），
本场战斗的分组激活位只在每玩家克隆 `PuppeteerFormManager` 上；`Creature` 的 HP 表示**当前登场形态**的生命。

| 项 | 约定 |
|---|---|
| 形态与生命 | 本体（`PuppeteerForm.Host`）用角色 `StartingHp`；傀儡（`Puppet`）上限由 `IPuppeteerCharacter.PuppetMaxHp` 给出。切形态时先把旧形态当前血量写回运行数据，再 `SetMaxHpInternal` → `SetCurrentHpInternal` 写入新形态（顺序不能反：写上限会把当前值压进上限）。 |
| 死亡拦截 | **本体形态吞掉一切原版防死，傀儡形态正常吃。** 只拦「战斗内 + 本体形态 + 目标是本玩家角色」：`ShouldDie` 返回 `false`，在 `AfterPreventingDeath` 里切傀儡。原版 `Hook.ShouldDie` 先遍历全部 `ShouldDie`（首个 `false` 者胜出），全部通过后才遍历 `ShouldDieLate` —— 本体返回 `false` 时两轮都跑不到，故精灵药水（`ShouldDie` 型）与蜥蜴尾巴（`ShouldDieLate` 型）**都不触发也不消耗**；傀儡形态返回 `true`，两类防死照常生效。**顺序是硬约束**：本模型必须走 `GetRunStateHookModels`（运行态订阅组，`RunState.IterateHookListeners` 中排在遗物 / 药水**之前**）；挂到 `GetCombatHookModels`（战斗组最末、排在遗物之后）会让 `ShouldDie` 型防死抢先，本体就不切形态了。**必须在 `AfterPreventingDeath` 里把血量抬到 >0**，否则原版会递归重杀（10 次后抛异常）。傀儡形态阵亡才是真正的失败结算。 |
| 战斗边界 | 战斗开始强制本体形态（本体 0 血 → 1 血）；胜利 / 战斗结束（`AfterCombatVictory` + `AfterCombatEnd` + 解除订阅，三入口幂等）切回本体，本体已死则按 1 血强制复活。战斗外恒为 `Host`，读档不会卡在傀儡形态。 |
| 歌单冻结 | `PerformManager.IsPlaylistFrozen`：冻结时音乐牌不再进歌单（`ModifyCardPlayResultLocation` 保持原版去向）、`OnCardAdded` / `HandleCardAddedInternal` 拒绝入组、`Clean` / `RemoveOverflowItems` 整段跳过（歌单内容不可移除）。 |
| 演奏→激活 | 冻结时所有「演奏」入口（`PerformSlot` / `PerformChordSlots` / `RequestSlotPerform` / `RequestChordPerform` / `PerformCard`，以及打出音乐牌本身）不再发动效果、不再排网络动作，改为 `PuppeteerFormManager.ActivateGroups(...)`。分组来源：卡牌 `RequestGroup` / 指定槽位所属分组 → 该组；和弦掩码 → 掩码覆盖的所有组；都没有 → 方案默认分组。激活幂等，四组全亮触发复活。 |
| 复活 | 生命 = `min(HostMaxHp, PerformManager.Capacity × 2)`（默认四组各 1 → 8）；切回本体、解冻、清空激活位，播 `PuppetSwitchOutTrigger`。 |
| 形态动作 | 不换场景、不换皮肤：`IPuppeteerCharacter` 给出 `PuppetSwitchInTrigger` / `PuppetSwitchOutTrigger` / `PuppetIdleTrigger` / `PuppetDieTrigger`，角色在自己的 `GenerateAnimator` 里注册（睦为 Spine 的 `Doll_Switchin` / `Doll_SwitchOut` / `Doll_Idle` / `Doll_Die`）。`Doll_Die` 的 `AddAnyState("Dead", ...)` 带条件 `IsPuppetForm(creature)`，因此只在傀儡形态阵亡时生效，条件失败落回通用死亡动作。 |
| 占位表现 | `NPerformArea.SetFrozen(bool)`：槽位冷色相乘 + `FrozenOverlay` 覆盖层；`SetActivatedGroups(PerformChord)`：`ActivatedGroupBadge_<和弦>` 角标。正式特效替换点写在 `NPerformArea` 的字段注释里，外部接口不变。 |
| 调试 | `puppet state` / `puppet kill` / `puppet activate CDFG` / `puppet freeze` / `puppet unfreeze`（`PuppeteerConsoleCmd`）。 |

## 11. 破绽 / 弱点击破

`break` 机制全局启用（不绑角色）：破绽可施加给任意单位，任何角色/敌人都能用。

| 项 | 约定 |
|---|---|
| 层数与状态 | `WeaknessBreakPower` 上限 `BreakConst.MaxStacks` = 4（`TryModifyPowerAmountReceived` 截断 + `AfterApplied` 兜底）；满 4 层时由 `BreakCmd` 施加 `WeakPointBreakPower`（1 回合，目标一侧 `AfterSideTurnEnd` 递减，归零移除并清空破绽）。处于弱点击破时 `BreakCmd.CanDiscover` 短路，不能再被发现破绽。 |
| 唯一入口 | **必须走 `BreakCmd.Discover(ctx, target, amount, source)`**：层数截断、满层进状态、Hook 广播、VFX 刷新都在里面。`source` 传卡牌才能让这次发现计入该牌的「击破」命中集合。 |
| 广播 | `IWeaknessBreakHookListener`（`Scripts/Interfaces/GameHook/`）三个点：发现、进入弱点击破、击破触发；派发在 `BangDreamHook`，不 push `LastInvolvedModel`（与 `OnCardTriggeredPerform` 同款）。 |
| 击破触发 | 卡牌实现 `IWeaknessBreakCard`（`HasBreakEffect` + `OnWeakPointBreak(ctx, play, target)`）。`BreakRules` 在 `BeforeCardPlayed` 压帧（先记 `CardPlay.Target`），出牌中按 `cardSource` 从 `AfterDamageReceived` / `AfterPowerAmountChanged` 补记命中目标（覆盖随机 / AOE / 非伤害；给自己上的增益不算），`AfterCardPlayedLate` 出栈逐个触发——此时「发现破绽 → 满层 → 弱点击破」已结算完，因此同张牌可以「先发现凑满、再触发击破」。同一张牌对同一目标只触发一次，不同目标各一次。**触发语义写死为「本牌命中的目标此刻仍处于弱点击破」，不是「本牌造成了这次击破」**：队友或前一张牌刚打出的弱点击破同样算，后续命中该目标的牌继续触发「击破：」——这是有意设计（决策 B-08），不要按击破来源过滤。 |
| 视觉 | 不占 Power 图标（`IsVisibleInternal = false`）：常驻 `NWeaknessBreakVfx` 挂在 `NCreature` 上，1–3 层是 n 枚指示点、弱点击破换破裂形态；Hover 时 `NHoverTipSet.CreateAndShow` 弹能力同款 tips，移除后淡出。施加能力用 `silent: true`，避免无图标却有受击抖动。 |
| 调试 | `break 2` / `break 4` / `break self 3` / `break clear`（`BreakConsoleCmd`）。 |

## 12. 演奏区槽位的视觉形态（做特效/UI 前必读）

槽位不是「方形卡槽」，而是一条条**斜切平行四边形播放列表行**；设计演奏区特效（冻结、激活、落位高亮）时按下面的形态，不要按方块画。

| 项 | 事实 | 出处 |
|---|---|---|
| 单行尺寸 | `NPerformItem` 局部 **160×50**（宽度按卡名自适应，`MinimumItemWidth = 160`）；`NPerformArea.ItemScale` 默认 **0.7** → 实渲染约 **112×35** | `perform_item.tscn`、`NPerformItem.UpdateAdaptiveWidth`、`NPerformArea.ItemScale` |
| 斜切 | `parallelogram_clip.gdshader` 的 `slant_slope = 0.3` → 上下各偏移约 `0.3 × 高 / 2`（35px 高时约 5.25px） | `shaders/parallelogram_clip.gdshader` |
| 行内组成 | 卡面 portrait（同款斜切）+ 点阵叠加 + 渐变遮罩 + 白色标题（28px，右侧列整体水平镜像）；行右侧另有一条 165×25 的标题底色带 | `perform_item.tscn` |
| 分组与列 | 四组 × 3 行，贴命中框左右两侧成**两列**：左列 **C（上）/ D（下）**，右列 **F（上）/ G（下，镜像）**；同一列上下两组在竖直中段留约 5px 缝 | `MutsumiPerformScheme` 的 `PerformSlotCorner`、`NPerformArea.GetItemPosition` |
| 间距 | 行距 `ItemSpacing(70) × ItemScale` ≈ **49px**；列与命中框边缘 `SlotHitboxGap = 10px`；`PerformAreaAnchor.HitboxCenter` 表示演奏区坐标原点在命中框中心 | `NPerformArea` |
| 分组角标 | 在该组**第一行的外端**，尺寸 22px（占位实现） | `NPerformArea.TryGetGroupBadgeAnchor` |
| 行坐标获取 | 特效逐行定位请读 `_items[i].Position`（实时值），不要写死像素；右侧列用 `IsMirroredCorner` 判断镜像 | `NPerformArea.ApplyItemScale` / `GetItemPosition` |

## 13. 角色视觉动画时间线（`visuals_anim`）

### 13.1 分工

| 机制 | 解决什么 | 单位 | 串行粒度 |
|---|---|---|---|
| `async_damage` | 有动画的**伤害**不卡手 | 一次命中（动画 + 伤害） | 同一批内并行（音符各飞各的） |
| `visuals_anim` | 有**位移**的表现不卡手 | 一段编排（多步） | 按生物串行 |

两者通过 `VisualsAnimCmd.Callback` 组合：演出推进到某个位置时发射一次异步伤害。

**为什么演出必须脱手**：动作队列被占住时，`NPlayerHand` 会对**所有玩家**执行 `AnimDisable`
（手牌下移、变暗、禁用手柄导航），因为 `ActionQueueSet.AreAnyPlayerDrivenActionsBeingExecuted`
查的是全部队列。所以「把演出做成一串阻塞的 action」等于「演出期间全场不能出牌」——正是要消灭的问题。

**为什么按生物串行**：同一时刻只有一个身体，两段演出叠加会抢同一个视觉代理。
代价是连打两张演出牌时第二张整条时间线排在后面，与 STS1 `AbstractRenderer.effectList` 一致。

### 13.2 视觉代理：隐藏原体 + 克隆

演出期间把 `NCreature.Visuals` 设为不可见，用 `Creature.CreateVisuals()` 克隆一份挂在同一个
`NCreature` 下，由时间线自由驱动。要点：

- `Hitbox` / `HealthBar` / `Intents` / `SelectionReticle` 都是 `NCreature` 的**直接子节点**，
  不是 `NCreatureVisuals` 的后代 → 隐藏 `Visuals` 不影响选中、血条、意图；**不要隐藏整个 `NCreature`**，否则命中框收不到鼠标拾取。
- 血条留在布局坐标上（STS1 的 `AbstractRenderer` 偏移也只作用于精灵，血条本来就不跟着走）。
- 不用直接改 `Visuals.Position`：那个槽位被本体 `AnimShake` 占用。
- `CreateVisuals()` 是本体既有原语（`NGameOverScreen` / `NFakeMerchant` / `NUnlockCharacterScreen` 都在用），每次都是独立骨架实例。
- 克隆按生物缓存复用，演出时显示、结束时隐藏，战斗结束统一释放。

**节点结构**：`NCreature → Holder(Node2D) → Clone(NCreatureVisuals) → Mirror(Node)`。
位移写 `Holder.Position`，抖动与其它姿态写 `Clone` 的局部变换，二者互不覆盖。
载体加入后 `MoveChildSafely(holder, 0)` 前置——与原体 `Visuals` 一样排在最前面，
否则克隆会盖在后加的血条 / 意图图标之上。

**骨架动画必须跟随本体**。本体的 `CreatureAnimator` 绑定在 `NCreature.Visuals` 的骨架上
（`NCreature._spineAnimator`），`SetAnimationTrigger` 只转发给它；克隆是独立骨架实例，不会自动同步。
`VisualsAnimMirror` 在演出期间逐帧读本体的当前动画名、循环标记与轨道时间，镜像到克隆：

```csharp
using var sourceTrack = sourceAnim.GetCurrentTrack();
if (name != _lastAnimation) cloneAnim.SetAnimation(name, sourceTrack.IsLoop());
using var cloneTrack = cloneAnim.GetCurrentTrack();
cloneTrack?.SetTrackTime(sourceTrack.GetTrackTime());
```

这样攻击 / 施法 / 受击 / 死亡 / 待机全部自然同步，**不需要在克隆上再建一套状态机**
（那样既要复刻 `GenerateAnimator` 的状态图，又会因 `AnimState.MarkHasLooped` 之类的可变状态与本体互相干扰）。

受击抖动同理：`AnimShake` 写的是隐藏中原体的 `Visuals.Position`，`VisualsAnimShakeForwardPatch`
把它转发到克隆的局部位置，并复刻本体的守卫（正在抖动或正在播 `hurt` 时不重开）。

**命中特效的落点**由 `VisualsAnimVfxSpawnPositionPatch` 接管 `NCreature.VfxSpawnPosition` getter，
演出期间重定向到代理。本体几十个 VFX 类与模组侧都读这一个属性，所以只补这一处即可全部跟随。
这是 STS1 `EnhanceCreatureRenderPatch.Info.renderer` 那层间接的同构移植。

### 13.3 步骤与语义

`VisualsAnimCmd` 提供：`Move`（相对基准位置缓动）、`Teleport`、`Wait`、`Face`、`Spin`、`Reset`、`Callback`。
所有位移都相对 `VisualsAnimProxy.Home`（原体视觉的基准位置）表达，因此同一套编排在不同站位与分辨率下都成立。

- **`Teleport` 支持落位停留**：`Teleport(offset, holdSeconds = 0)`，对应 STS1 `TpProcess(x, y, duration)`
  的语义——到位是瞬时的，duration 是**到位之后的停留**，不是一段过渡。停留属于纯表现节奏，
  无渲染环境下不等待。
- **`Face` 与 `Spin` 不是一回事**。`Face` 是左右镜像（`Scale.X` 取符号）；`Spin` 是绕纵轴自转
  （`Scale.X = |base| × cos(弧度)`，90° 时侧面收窄为 0、越过 180° 后成背面镜像）。
  STS1 的 `FlipProcess(1, 0.1f)` 走的是 `renderer.rotateY(0→360°)`，对应的是 **`Spin` 而不是 `Face`**
  ——移植时最容易把这两个搞混。

- **主线程**：时间线全程在主线程推进（tween 信号、节点操作都在主线程）。`TweenAwaiter` 刻意不使用
  `RunContinuationsAsynchronously`，并在取消与节点退出时强制完成——`Tween.Kill()` **不会**发 `Finished`，
  直接 await 原始信号会永久挂起并卡死整条演出。
- **瞬间模式**：`FastModeType.Instant` 下视觉步骤直接落到终态，与 `Cmd.Wait` 的跳过行为一致。
- **无渲染环境**：`CreateVisuals()` 返回 null → 视觉步骤自行跳过，**`Callback` 照常执行**，
  保证 headless / 测试下玩法不缺。
- **`Move` 不是同步点**：里程碑要等真实表现时用 `Callback` 承载逻辑，不要用 `Cmd.Wait` 估算。

### 13.4 已知边界

- **回合结束时在跑的演出**（已处理）：`Callback` 里的请求走玩家驱动动作，玩家在演出中途结束回合时
  该请求会被 `StartCancellingAllPlayerDrivenCombatActions` 取消。现在 **含 `Callback` 的演出**
  在提交时会自动向 `CombatResolutionBarrier` 租一个租赁（同一战斗 key，额外一个 lease），
  时间线结束（含取消）时释放，因此回合结束会等这类演出跑完再推进。
  纯视觉演出不租，避免无谓拖住回合结束。实现见 `VisualsAnimCmd.AcquireResolutionLease`；
  该依赖使 `visuals_anim` 声明了 `Dependencies = ["async_damage"]`。
  **仍存的边界**：租赁只保证「回合结束会等」，不保证演出本身不被打断 ——
  战斗直接结束（`ResetCombat`）时演出仍会被取消。
- **`Home` 在创建时捕获**：战斗中途若布局重排（换位、召唤），基准位置会过期。
- **非骨架生物不镜像动画**：`VisualsAnimMirror` 只在 `NCreature.HasSpineAnimation` 与克隆同为真时创建。
  非 Spine（Live2D 等）的克隆只是一个静止画面，需要另行按 `Body` 实际类型驱动。
- **皮肤与恐惧症模式的体型**：克隆创建时补调 `NCreatureVisuals.SetUpSkin(Monster)`（与原体 `NCreature._Ready` 同序），
  有皮肤的怪物（拜尔丁的发色、多眼生物的瞳孔皮肤等）在演出中不会跳回默认皮肤；**玩家角色不受影响**
  （玩家的皮肤走 `GenerateAnimator`，本来就不经过 `SetUpSkin`）。
  **仍存的边界**：恐惧症模式的体型/皮肤切换（`NCreatureVisuals.UpdatePhobiaMode`）**没有复现**——
  骨架镜像只驱动 `SpineBody`（`_body`），切到 `%PhobiaModeVisuals` 后会停在 setup pose；
  加上克隆按生物缓存复用，创建之后再改该选项也不会同步。开启这一辅助选项的玩家，演出中的克隆显示普通体型。
- **拿不到异步伤害批次句柄**：演出无法「等这批命中再推进下一段」，见 §7.6。
