# 机制模块与角色接入指南

BangDreamInSpire 的玩法机制被拆分为若干**机制模块**（mechanic）。每个模块自行声明它需要注册的内容、注入的补丁、玩家状态模型与战斗生命周期钩子；核心初始化流程（`BangDreamInSpireCore`）不再感知任何具体机制。角色只需实现对应**能力接口**即可自动接入机制，库代码中不出现任何具体角色名。

## 1. 现有机制模块

| 模块 | Id | 顺序 | 依赖 | 启用条件（角色接口） | 承载内容 |
|---|---|---|---|---|---|
| 额外卡组 | `extra_deck` | 100 | — | `IExtraDeckSupportCharacter` | 额外卡池/额外卡组/额外抽牌堆、小祥商人、额外卡组删牌、音乐牌奖励、额外卡组升级休息选项、战斗开始克隆额外抽牌堆 |
| 演奏 | `perform` | 200 | `extra_deck` | `IPerformableCharacter` | 歌单容量与入队/重排/溢出、演奏触发、演奏区节点、音乐卡牌类型铭牌、手动落位目标类型（AnySlot / RequestGroup）、卡牌和弦（`ChordCapability`） |
| 余音 | `lingered` | 300 | — | `ILingeredResourceCharacter` | 余音二级资源与卡面费用 UI、休止结算与驱动、环绕资源预览 |
| 音符 | `music_note` | 400 | — | （全局） | 音符发射/弹跳与伤害、命中同拍结算、音符伤害追踪、VFX 容器 |

`Order` 越小越先装配；`Dependencies` 中的机制必须存在，否则装配阶段抛异常。

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
- `BandMemberModel` 已自带 `ISkinSupportCharacter`（皮肤）、`IGroupableCharacter`（角色选择分组）、`IBangDreamMateData`（主菜单资源）。
- 卡牌侧：`IPerformCard`（音乐牌）、`ISubsideCard`（休止牌）决定卡牌参与哪些机制。

## 3. 新增一个机制模块

1. 在 `BangDreamInSpire/Scripts/Mechanics/<Name>/` 下新建类，实现 `IBangDreamMechanic`：

```csharp
public sealed class MyMechanic : IBangDreamMechanic
{
    public string Id => "my_mechanic";
    public int Order => 500;
    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(IMyCharacter)];

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
| `RequiredCharacterCapabilities` | 启用所需的角色接口；空表示全局装配 |
| `RegisterContent(ctx)` | 注册静态内容 |
| `RegisterPatches(patcher)` | 注册本机制的 Harmony 补丁（全量注入，时机与核心一致） |
| `InstantiatePlayerState(player)` | 为该玩家实例化机制状态模型 |
| `GetCombatHookModels(player)` | 战斗中需被订阅的模型 |
| `SubmitCombatState` / `UnsubscribeCombatState` | 战斗开始 / 结束的每玩家钩子 |

### `BangDreamMechanicContext`

`ModId`、`RunData`、`Content`、`Keywords`、`CardTags`、`CardPiles`、`Rewards`、`SecondaryResources`、`NodeAttachments`、`SubscribeLifecycle<T>()`、`RegisterCardKeyword()`、`RegisterCardTag()`。

### `BangDreamCapabilities`

能力判定的统一入口，库内所有角色/卡牌判定都经由它，避免散落 `is IXxxCharacter`：

`HasExtraDeck`、`HasPerform`（含 `CreatePerformScheme().TotalSlotCount > 0`）、`HasLingered`、`IsPerformCard`、`IsSubsideCard`（均有 `CharacterModel`/`Player`/`CardModel` 重载）。

## 5. 兼容性约束

- 下列类型是面向第三方角色 mod 的稳定 API，**不随机制内部迁移而改变命名空间**：
  `Scripts/Interfaces/**`、`Scripts/Utils/Infos/*`（`CardPerform`、`PerformContext`、`PerformEnqueueRequest`、`PerformTargetTypes`、`VfxContext`、`SkinInfo` 等）、
  `Scripts/Cards/BandCardModel`、`MusicCardModel`、`Scripts/Powers/BandPowerModel`、`Scripts/Relics/BandRelicModel`、
  `Scripts/Character/BandMemberModel`、`Scripts/Utils/BangDreamConst`、`Scripts/Utils/BangDreamHook`、`Scripts/Extensions/**`。
- 机制实现类（`PerformManager`、`MusicNoteCmd`、`ExtraPileCmd`、`MusicCardReward`、`TechnicalPracticeOption` 等）
  已移入 `Scripts/Mechanics/<Name>/`。若曾直接引用它们，需更新 `using`。
- 关键字与卡牌标签的数值由 `XxHash32(id)` 确定性生成，与注册顺序无关，因此模块装配顺序调整不影响存档与网络兼容性。

## 6. 已知边界

- 游戏本体不支持运行时卸载 mod（`Mod.Unloadable` 写死为 `false`），因此「热插拔」在运行期体现为**按角色能力自动启用**，而非卸载 DLL。
- 补丁仍在初始化阶段全量注入；机制是否生效由运行时的角色能力判定控制。

## 7. 音符结算时序约定

音符伤害遵循「发射即锁定、命中才结算」：

- 发起卡片/能力把音符请求交给原版动作队列（`RitsuLibManagedNetActions.Request`），随后立即返回，**不阻塞出牌动作**。单机同样经由该队列（`CanSendManagedAction` 对 `Singleplayer` 返回 `true`）。
- 结算动作内先按节奏发射全部根音符，并在**发射时刻一次性锁定**目标与伤害数值；飞行期间不再读取任何可变增益，故多端一致。
- 结算动作等待音符抵达目标（`AsyncDamageAnimationHandle.Landing`，时长等于 `距离 / 速度`，与 VFX 推进公式严格等价），抵达后才施加已锁定伤害；弹跳音符在上一目标命中后才发射。
- 若目标在飞抵前已不可命中，该次伤害**丢弃且不重定向**（`TargetPolicy` 语义已移除），以保证确定性最强。
- 由于结算动作本身在队列中等待飞行，该动作完成前的后续动作会顺延；这是「伤害必须晚于飞行」与「顺序确定」的必然代价。
- 不使用 VFX 信号驱动玩法状态；`CombatResolutionBarrier` 与 `WaitForCombatResolutionPatch` 已删除（结算进入原版队列后不再需要）。

## 8. 音乐牌的手动落位目标类型

`PerformTargetTypes` 提供两个供音乐牌使用的 `TargetType`：

| 目标类型 | 语义 | 落位 |
|---|---|---|
| `AnySlot` | 指定任意已激活槽位（可跨分组） | 强制进入该槽位，槽位原有卡牌离开歌单（等价 `PerformEnqueueStrategy.Fixed`） |
| `RequestGroup` | 指定一个和弦分组，其他规则不变 | 只把分组换成所选分组，组内仍按卡牌自身的入队策略落位；卡牌和弦不改写（和弦不参与入组，见 §9） |

实现要点：

- **枚举值来源**：`CustomTargetType.RegisterMultiTargetType(BangDreamConst.ModId, "<stem>", _ => false)`，ID 形如 `BANG_DREAM_IN_SPIRE_TARGETTYPE_ANY_SLOT`，数值由 ID 确定性生成。词干（`any_slot` / `request_group`）**冻结**：改名会破坏存档与联机一致性。
- **为什么注册为「群体目标类型 + 恒假谓词」**：RitsuLib 的目标类型只表达生物目标（单体/群体），无法表达槽位目标；注册为群体且不选任何生物，可让原版与 RitsuLib 的全部目标判定（`IsValidTarget(null)`、`CanPlayTargeting(null)`、目标指示器）都按「无需生物目标」处理——出牌不会被目标校验拦住，生物指示器也不会误显示。
- **出牌交互**：`PerformSlotTargetingPatches` 接入四处——`NMouseCardPlay.TargetSelection`（进入选择态，随后沿用原版「拖拽跟随鼠标、松开结束」的群体目标循环，结束时按松手位置写入请求或取消出牌）、`NMouseCardPlay.IsCardInPlayZone` 与私有的 `get_PlayZoneThreshold`（角色旁的槽位可能位于原版出牌区阈值下方，仅在选择态激活时放宽；`TargetSelection` 内成功取到请求后还会置位原版「正在尝试出牌」标记，避免随后因不在出牌区而取消）、`NCardPlay.TryPlayCard`（读取选择结果）、`NCardPlay.Cleanup`（结束/取消选择态）；另以 `NHandCardHolder.BeginDrag` 作为兜底入口。
- **不依赖极小方法**：`BeginDrag`、`IsCardInPlayZone` 这类方法体极小，可能被 JIT 内联到调用点，Harmony 补丁在那些调用点不生效；因此选择交互的主入口是 `TargetSelection`（与 RitsuLib 处理自定义目标类型的方式一致），`BeginDrag` / 出牌区放宽只作兜底。任一钩子未生效时按常规规则出牌，不会出现打不出去的情况。
- **选择态 UI**：`NPerformArea` 在候选槽位上叠加高亮，并按两种来源确定"指向"：`NPerformItem` 的 `MouseEntered/MouseExited`（Godot 自身拾取，不受缩放/镜像/父级变换影响），以及"卡片容器全局中心 + 全局变换基向量长度换算半尺寸"的手算命中；两者都未命中时再用视口坐标兜底判定一次。半尺寸必须用**全局变换的基向量长度**而非节点自身的 `Scale`，否则父级带缩放时命中框会被算小。
- **原版交互（点击/拖拽两种模式，逐条复刻单体目标）**：接管 `NMouseCardPlay.TargetSelection` 后先 `CenterCard()` 把卡牌归位到屏幕下方中央，再按原版 `NTargetManager._Input` 的判定等待结束——① **拖拽模式**（按住卡牌开始）：松开左键时指向候选槽位即选定；未指向则与原版一致**转入点击模式继续等待**（不是结束）；② **点击模式**（点击卡牌或快捷键开始）：移动鼠标后点击候选槽位即选定，**点空则取消本次出牌**；③ 中途右键、Esc、手牌快捷键取消，或拖到底部取消区，都取消本次出牌并把牌留在手牌（不耗能）；判定复用原版 `_isLeftMouseDown` 字段与 `IsCardInCancelZone()`。指示反馈见下条。
- **原版指示器**：`PerformTargetingArrow` 复用原版目标指示箭头（`NTargetManager` 私有持有的 `TargetingArrow` 子节点）：箭头自卡牌指向鼠标、隐藏系统指针、指向候选槽位时高亮（友方配色），结束/取消时恢复指针。**不复用原版群体目标循环 `MultiCreatureTargeting`**：它每帧把卡牌位置设为鼠标位置，会让箭头起点与终点重合而退化。只驱动箭头绘制，不进入原版目标选择会话（该会话要求单体目标类型，本机制的类型是群体类型）；手柄方向导航下不绘制。
- **回退规则**：容量为 0、无候选槽位、手柄方向导航、自动出牌（`CardCmd.AutoPlay` / `--autoslay`）、以及 `RequestGroup` 只有一个可达分组时**不进入选择态**，出牌按卡牌自身规则处理：不放宽出牌区，卡面也不显示「指定分组」提示（单分组角色的 `RequestGroup` 因此等价于 `TargetType.None`）；一旦进入选择态，则完全按原版单体目标的判定收尾（选定 / 点空取消 / 取消区取消 / 右键取消，均不耗能）。每一次进入/跳过/模式切换/收尾都写日志（`Manual slot selection ...` / `Manual slot targeting ...`，含候选槽位的中心与半尺寸），便于排查。
- **联机**：落位随 `Enter` 生命周期动作下发（`PerformNetworkActionPayload.RequestSlotIndex` / `RequestGroupMask`），各端在 `HandleCardAddedInternal` 内使用同一请求计算落位；缺字段（旧版本 payload）等价于无覆盖。
- **一次性语义**：请求写在卡牌的 `PerformContext.Request` 上，入队规划消费后即清空；出牌被取消时一并清空（`CancelManualSlotSelection`）。

## 9. 卡牌和弦

和弦是**卡牌**的属性，由 RitsuLib 模型能力 `ChordCapability` 承载：随卡牌一同克隆、随卡牌实例存档。

| 项 | 约定 |
|---|---|
| 用途 | **只**用于卡牌打出后激活对应分组中音乐牌的奏响（角色规则经 `IPerformTriggerListener` 读取，如 `MutsumiSpecialRules`）：该分组内**全部**已落位卡牌一起奏响；多和弦卡牌会同时激活其全部和弦分组。 |
| 不参与入组 | 音乐牌进入哪个分组由 `PerformTargetTypes.RequestGroup` 指定，未指定时落回 `IPerformScheme.DefaultGroup`（`PerformCapacityState.ResolveDefaultGroup()`，不读取和弦）。 |
| 赋值 | `card.GetOrCreateCapability<ChordCapability>().SetChord(chord)`；传 `PerformChord.None` 会移除该能力。 |
| 赋值时机 | 卡牌奖励生成时可随机赋予（跟随卡牌实例进入卡组）；另有**休息处选项「和弦设计」**（仅睦）让玩家手动给卡组里的一张牌指定和弦（见 §9.2）。 |
| 读取 | `card.TryGetCapability<ChordCapability>(out var capability)`，没有该能力即无和弦。 |

```csharp
// 卡牌奖励生成时随机赋予和弦
rewardCard.GetOrCreateCapability<ChordCapability>().SetChord(randomChord);
```

- 能力类型由 `[RegisterModelCapability]` 自动注册，无需手动初始化。
- 和弦不参与入组判定，因此与 `RequestGroup` 目标类型正交：手动落位只换分组，不改写和弦。
- 奏响由 `PerformManager.PerformChordSlots(mask)` 执行：逐个奏响掩码覆盖分组内**已落位卡牌所在的槽位**（按槽位升序，跳过即兴牌），并输出 `Chord perform <mask> : slots [...]` 日志便于核对。

### 9.1 卡面角标

和弦通过能力实现的**卡面覆盖层**（`ICardOverlayContributor`）画在卡牌右上角：`ChordCapability` 返回一个
`CardOverlayContribution`（`FromFactory`），节点是 `NChordCornerBadge`；RitsuLib 在 `NCard.ReloadOverlay()` 之后
把能力覆盖层挂到 `%OverlayContainer` 下并重建，所以卡牌模型刷新时角标自动跟随，无需自己订阅事件。

| 项 | 约定 |
|---|---|
| 包角带几何 | 右上角的**等臂** L 形带（上臂沿卡牌上边 + 右臂沿右边），带宽 26px、两臂各从拐角量 120px：上臂 `x∈[30,150] y∈[-211,-185]`、右臂 `x∈[124,150] y∈[-211,-91]`（卡中心坐标；卡框 `x±150`、`y±211`）。角标画布 = 120×120 正方形，包角带占其上边与右边。 |
| 切分 | 所有切缝都是 `x + y = c` 的 **45° 斜线**，按**面积均分**：n 个和弦 = n 段等面积。1=整条 L；2=一条斜接缝正好落在拐角（c = -61，上臂/右臂各一段）；3=两刀（c = -96.67 / -25.33，中间段绕拐角）；4=三刀（c = -114.5 / -61 / -7.5，上臂两段 + 右臂两段）。实现见 `NChordCornerBadge.BuildSlots`。 |
| 渲染 | 单张白色包角带贴图 `res://BangDreamInSpire/images/sceneui/chord_corner.png`（画布恰好 120×120，按 2× 导出），每段作为 `Polygon2D` 的多边形（顶点 UV 映射到贴图）并 `Color` 染色；贴图缺失时退化为纯色多边形（形状与成品一致，可直接验收切分）。`images/**` 不入库，新克隆缺图会自动走占位。 |
| 配色 | `PerformChordColors` 是唯一来源：C `#7799CC`（祥子）、D `#FFDD88`（素世）、F `#77BBDD`（灯）、G `#7777AA`（立希）。`MutsumiPerformScheme.GetSlotColor`（演奏区四组槽位）与 `CrychicMemberEnum`（角色色）都引用这张表。 |
| 层级 | 能力覆盖层挂在 `%OverlayContainer` 下，而该节点在 `card.tscn` 里排在 `TitleBanner`/`TitleLabel` **之前**——角标会被标题横幅压住一角，这是**设计预期**（角标只给卡面右上角背景着色，不抢前景，类似一层「贴在卡面上的色带」）。**不要**用 `ZIndex` 抬高它：Godot 的 z_index 在同一 canvas 内全局排序，会把角标抬到面板、HUD 等所有界面之上；也**不要**重排 `OverlayContainer` 的画布顺序（卡牌入场时父节点正处在 AddChild 构建阶段，同步 `MoveChild` 会被 Godot 拒绝并报 `Parent node is busy setting up children, move_child() failed`，结果只会让不同界面表现不一致）。 |
| 调试 | 控制台 `chord CDF` / `chord 13` / `chord none`：给手牌设和弦并强制重建覆盖层，用于预览 1~4 个和弦的切分（`ChordConsoleCmd`）。 |

### 9.2 和弦设计（休息处选项）

睦的休息处多出「和弦设计」选项（`ChordDesignOption : ModRestSiteOptionTemplate`，由
`MutsumiSpecialRules.TryModifyRestSiteOptions` 在 `player.Character is WakabaMutsumi` 时注入）。
选择后打开自绘覆盖层面板 `NChordDesignScreen`（`BangDreamInSpire/Scripts/Nodes/Screens/`）：

| 项 | 约定 |
|---|---|
| 布局 | 三行居中排布（场景 `BangDreamInSpire/scenes/screens/chord_design_screen.tscn`，代码只填内容）：顶部和弦筹码（C/D/F/G，**始终同一行，不拆行**）→ 中央箭头 → 卡组卡牌行（**一格一张牌、始终同一行**）→ 箭头 → 居中结果预览卡。点筹码时**整行水平平移**，让选中筹码对齐到中央箭头正上方（其余筹码按原顺序让位、不交换；取消选中后整行回正）；点卡牌同样**整行水平平移**，让那张牌对齐下方箭头并保持常驻行内，结果区同步显示它的预览与和弦。打开时默认 C 和弦 + 第一张牌，两者都停在中轴上。左右分别是原版 `NBackButton` / `NConfirmButton`（取消返回、确定应用）。卡牌带高 406px（y294–700），行内容再乘 `CardLayerScale = 0.9`（静止 0.8×0.9 = 0.72 倍、悬停 1.0×0.9 = 0.9 倍），余量按「悬停放大后不被 `clip_contents` 裁切」定；箭头在 274–310 / 748–784，三行在 1080 高内紧凑分布。 |
| 结果 | `ChordDesignResult(CardIndex, Chord, Additive)`；面板返回后由选项在 `Player.Deck.Cards` 上 `GetOrCreateCapability<ChordCapability>().SetChord(...)`——**确定之前卡组模型完全不变**。面板内的卡牌只是 `NCard` 视图（`NodePool` 复用节点）；结果预览先隐掉卡面自带的能力角标，再把按「结果和弦」构建的角标挂到**预览卡的 `%OverlayContainer`**（继承卡的缩放/变换，和真实卡牌一样被标题横幅压住一角）。预览卡回池前与新建卡后都会 `RemoveStaleChordBadges` 清掉直挂的残留角标，避免旧角标随节点复用被带到别的卡上。 |
| 收尾 | 确定后在 `DoLocalPostSelectVfx` 复用本体 `NCardUpgradeVfx`（与升级卡牌同一套表现）：屏幕中央放大展示（此时角标已是新和弦）→ 停留片刻 → `NCardFlyVfx` 飞入顶部卡组图标；选项 `AssetPaths` 追加 `NCardUpgradeVfx.AssetPaths` 以便预载，`DoLocalPostSelectVfx` 只等展示段结束（约 1.4–1.9s），飞行交给后场继续。 |
| 增加 / 覆盖 | 结果右侧开关默认「覆盖」，`Additive=true` 时与现有和弦取并集。开关默认隐藏（`NChordDesignScreen.ModeToggleUnlocked = false`），留给后续事件遗物解锁。 |
| 联机 | `OnSelect()` 在每个 peer 执行：本地玩家用 `ReserveChoiceId` + 面板结果 + `SyncLocalChoice(PlayerChoiceResult.FromIndexes([cardIndex, mask, additive]))`，其余 peer `WaitForRemoteChoice` 后以相同参数改卡（与 `ExtraPileCmd` 同款范式）。取消时同步空列表并 `return false`（选项保留）。 |
| 滚动 | 本体滚动容器只有纵向，面板自绘 `NChordScrollbar`（横向轨道）表现「当前停在哪一格」：滚轮按格步进、拖条吸附到最近的牌、键盘/手柄焦点进入某张牌也会把整行带到它上面；滚动条滑块位置 = 选中牌在卡组里的序号比例，可视比例按 `1/牌数` 计算（内容不足一屏时隐藏）。 |
| 接线 | 注入依赖玩家**拥有** `MutsumiSpecialRules`（`Hook.ModifyRestSiteOptions` 只遍历 `runState.IterateHookListeners` 里的已拥有模型）。该遗物已加 `[RegisterRelic(typeof(DeprecatedRelicPool))]`，两个睦皮肤 `skins/mutsumi/*.json` 的 `StartingRelics` 追加了 `ITS_CRYCHIC_RELIC_MUTSUMI_SPECIAL_RULES`（保留 `ITS_CRYCHIC_RELIC_SAKIKO_SPECIAL_RULES` 以复用音乐牌奖励逻辑）。 |
| 素材 | 选项图标 `res://BangDreamInSpire/images/sceneui/music_draw.png`（`images/**` 不入库，缺图时图标缺失但不影响逻辑）。 |


