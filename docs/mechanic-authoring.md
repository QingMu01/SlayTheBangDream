# 机制模块与角色接入指南

BangDreamLib 的玩法机制被拆分为若干**机制模块**（mechanic）。每个模块自行声明它需要注册的内容、注入的补丁、玩家状态模型与战斗生命周期钩子；核心初始化流程（`BangDreamLibCore`）不再感知任何具体机制。角色只需实现对应**能力接口**即可自动接入机制，库代码中不出现任何具体角色名。

## 1. 现有机制模块

| 模块 | Id | 顺序 | 依赖 | 启用条件（角色接口） | 承载内容 |
|---|---|---|---|---|---|
| 额外卡组 | `extra_deck` | 100 | — | `IExtraDeckSupportCharacter` | 额外卡池/额外卡组/额外抽牌堆、小祥商人、额外卡组删牌、音乐牌奖励、额外卡组升级休息选项、战斗开始克隆额外抽牌堆 |
| 演奏 | `perform` | 200 | `extra_deck` | `IPerformableCharacter` | 歌单容量与入队/重排/溢出、演奏触发、演奏区节点、音乐卡牌类型铭牌 |
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
    public int GetDefaultCapacity => 3;                 // IPerformableCharacter：歌单容量
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

1. 在 `BangDreamLib/Scripts/Mechanics/<Name>/` 下新建类，实现 `IBangDreamMechanic`：

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

2. 完成。`BangDreamLibCore` 会通过反射自动发现并装配该模块，**无需修改核心或其它机制**。

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

`HasExtraDeck`、`HasPerform`（含 `GetDefaultCapacity > 0`）、`HasLingered`、`IsPerformCard`、`IsSubsideCard`（均有 `CharacterModel`/`Player`/`CardModel` 重载）。

## 5. 兼容性约束

- 下列类型是面向第三方角色 mod 的稳定 API，**不随机制内部迁移而改变命名空间**：
  `Scripts/Interfaces/**`、`Scripts/Utils/Infos/*`（`CardPerform`、`PerformContext`、`VfxContext`、`SkinInfo` 等）、
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

