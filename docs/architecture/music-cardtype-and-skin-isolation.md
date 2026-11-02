# 新增 CardType「Music」与皮肤纹理按角色隔离

- 状态：已实现（简化版，2026-11-02；实机验收待做）
- 方案落盘：2026-11-02（动手改码前）；同日按评审意见简化，见 §3.4
- 日期：2026-11-02
- 目标分支：`feature/music-cardtype-skin-isolation`（基于 `main`，本体 0.111.0，RitsuLib 0.6.10）

## 1. 结论

1. 本体 `CardType` 是封闭枚举，无法从模组扩展。音乐牌改用 RitsuLib 的动态枚举工具分配运行时值：
   `RitsuLibFramework.RegisterDynamicEnumValue<CardType>(ModId, "Music")`（值落在 `0x4000_0000+`，与本体/其它模组不冲突，
   同一 id 跨进程/跨存档稳定）。**代码里不写死任何数值。**
2. 类型值本身没有序列化/联机成本：`SerializableCard` 不含 `CardType`，`MaxEnumValueCache` 只被 `PacketReader/PacketWriter` 的泛型枚举写入使用。
3. 真正会爆的只有**卡面纹理 getter** 与**类型铭牌本地化**两条渲染路径，都可以用 RitsuLib 官方钩子 + 一处小 patch 接管，
   不需要替换本体私有 switch。
4. 默认值由**卡牌自己的 asset profile** 保证（`IPerformCard.PerformCardAssets`，`MusicCardModel` 默认给本体 quest 卡框）；
   皮肤系统只负责"验证来源角色 + 皮肤配置，命中则动态替换纹理"，不参与兜底。
5. 皮肤替换当前以 `Model.Owner`（**当前持有者**）为唯一权威；改为以**来源角色**（`OriginCharacter`，否则卡池反查）为权威：
   A 的卡被 B 取得后仍解析 A 在本局的皮肤配置；A 不在本局时回退 A 的默认皮肤（`CharacterSkinList` 首个已注册皮肤），
   再回退卡牌自身的 asset profile（本体 quest 卡框）。

## 2. 现状

### 2.1 音乐牌

- `BangDreamInSpire/Scripts/Cards/MusicCardModel.cs`：`MusicCardModel(...) : BandCardModel(baseCost, BangDreamConst.MusicCardType, rarity, target, showInCardLibrary), IPerformCard`。
- 角色基类按角色注册卡池：`ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs:11`（`SakikoMusicalCardPool`）、
  `ItsCrychic/Scripts/Cards/Mutsumi/AbstractMutsumiMusicCard.cs:8`（`MutsumiMusicalCardPool`）；
  Token 牌注册 `TokenCardPool`（`ItsCrychic/Scripts/Cards/Token/BasicScale.cs:15`、`MelodyFragments.cs:17`）。
- 音乐牌经 `CardPileCmd.Add(card, BangDreamConst.ExtraDeck)` 常驻额外卡组/演奏堆（`Mechanics/ExtraDeck/MusicCardReward.cs:205-209`），**不会进入 Deck**。

### 2.2 皮肤替换（不跟随角色）

改动前 `SkinVisualSupportPatches.MusicCardFramePatch` 按 `Model.Owner`（**持有者**）的 `PlayerSkin` 取 `Ui.MusicCardFrame`，
`MusicCardFrameMaterialPatch` 清空 `_frame.Material`。因此：

- B 拿到 A 的卡后显示的是 **B 的皮肤**（需要隔离）；
- canonical/卡牌库（`IsMutable == false`）整段跳过，始终显示本体 quest 卡框。

### 2.3 相关本体事实（证据）

| 事实 | 位置 |
|---|---|
| `enum CardType { None, Attack, Skill, Power, Status, Curse, Quest }` | `sts2unpack/0.111.0/src/Core/Entities/Cards/CardType.cs` |
| `CardModel.Type` 为 `virtual` | `src/Core/Models/CardModel.cs:289` |
| `FramePath / PortraitBorderPath / AncientTextBgPath` 三处私有 switch 穷举，`default: throw ArgumentOutOfRangeException` | `src/Core/Models/CardModel.cs:161-240` |
| `CardTypeExtensions.ToLocString()` 是 switch 表达式，**无 default** → 新值抛 `SwitchExpressionException` | `src/Core/Entities/Cards/CardTypeExtensions.cs:7-25` |
| `ToLocString` 展示调用点只有 `NCard.UpdateTypePlaque`（`src/Core/Nodes/Cards/NCard.cs:822`）与 `TheFutureOfPotions`（非卡牌展示） | 同上 |
| `CardType.Quest` 的行为判定只有 3 处：选牌过滤/变换候选/镜子遗物（都在 Deck 语义内） | `CardSelectCmd.cs:591`、`CardFactory.cs:172`、`DollysMirror.cs:28` |
| `CardType` 不进存档/网络包 | `src/Core/Saves/Runs/SerializableCard.cs:13-53`；`MaxEnumValueCache` 仅用于 packet 泛型枚举 |
| RitsuLib 以 Prefix 短路 `CardModel.Frame/PortraitBorder/EnergyIcon/Ancient*` 与 `FrameMaterial` getter：找到覆盖则不跑原版；路径为空或加载失败会回退原版 | `STS2-RitsuLib/src/Scaffolding/Content/Patches/ContentAssetModelVisualOverridePatches.cs`、`ContentAssetOverridePatches.cs:37` |
| `ModCardTemplate.CustomFramePath => AssetProfile.FramePath`，空路径会回退原版 getter | `STS2-RitsuLib/src/Scaffolding/Content/ModCardTemplate.cs` |
| 动态枚举 API：`RitsuLibFramework.RegisterDynamicEnumValue<TEnum>(string modId, string localStem)` | `STS2-RitsuLib/src/RitsuLibFramework.cs:818`、`src/Utils/DynamicEnumValueMinter.cs` |
| `GiveToAnotherPlayer` 只改 `_owner`，不触发节点重绘 | `src/Core/Models/CardModel.cs:2206` |
| 皮肤数据为每玩家 run 数据，联机全员可读 | `Multiplayer/RunData/PlayerSkinData.cs`、`BangDreamInSpireCore.cs:96-108` |

## 3. 新增 CardType「Music」要处理的内容

### 3.1 必改（不接管会崩/错）

1. **类型取值**：`PerformMechanic.RegisterContent` 中
   `BangDreamConst.MusicCardType = RitsuLibFramework.RegisterDynamicEnumValue<CardType>(context.ModId, "Music");`
   `BangDreamConst.MusicCardType` 采用与自定义 `PileType` 相同的"初始化后读取"模式（`Init` + 未初始化抛异常），
   另提供 `BangDreamConst.IsMusicCardType(CardType)` 供渲染早期安全判断。
2. **铭牌本地化**：新增 `CardTypeExtensions.ToLocString` 的 Postfix，把 Music 映射到
   `LocString("gameplay_ui", "CARD_TYPE.MUSIC")`，并在 `gameplay_ui.json` 增加 `"CARD_TYPE.MUSIC": "音乐"`。
   - 原"覆盖 `NCard.UpdateTypePlaque` 文本"的做法被取代；`IPerformCard` 的唯一实现者是 `MusicCardModel`，不会漏牌。
   - RitsuLib 的 `CardTypeTextHook` 只能替换 `GetFormattedText` 之后的结果，`ToLocString` 仍先执行，救不了这个异常。
3. **卡面纹理**：Music 会命中原版三处 switch 的 `default` throw。默认值由卡牌 asset profile 保证：
   - `IPerformCard` 新增契约 `CardAssetProfile PerformCardAssets { get; }`；
   - `MusicCardModel` 默认返回本体 quest 卡框（`res://images/atlases/ui_atlas.sprites/card/card_frame_quest_s.tres`）与
     本体 skill 卡图边框（`.../card_portrait_border_skill_s.tres`），并并入 `AssetProfile`；
   - RitsuLib 的 getter Prefix 拿到非空 `CustomFramePath` 后直接短路原版 switch，canonical 卡同样生效。
   - `AncientTextBgPath` 只对 `Rarity == Ancient` 生效；音乐牌稀有度只有 Common/Uncommon/Rare/Token，不可达。
4. **类型颜色**：`Nodes/BangDreamCharacterDetailsPopup.cs` 的 `GetTypeColor` 增加 Music 分支。
   由于值是运行时动态分配的（非编译期常量），用 `if (BangDreamConst.IsMusicCardType(type))` 前置判断，不能写进 switch 分支。

### 3.2 记录但不改（行为不可达/已由框架兜底）

| 点 | 结论 |
|---|---|
| `CardSelectCmd.cs:591`、`CardFactory.cs:172`、`DollysMirror.cs:28` 的 `Type != Quest` | 都是 Deck 内变换语义；音乐牌永不进 Deck，不可达。若将来支持"从额外卡组变换"，再补判定 |
| `EnchantmentModel.CanEnchant` 的 `(uint)(type - 4) <= 2u` | 只排除 Status/Curse/Quest；Music 会变成可附魔——同样仅当音乐牌进 Deck 才可达 |
| `HiddenGem.cs` 的类型过滤 | 同上 |
| `NCardLibrary` 的攻击/技能/能力/其他筛选 | Music 落入"其他"，与改造前（Quest）一致 |
| `CardPoolModel` / `NTinyCard` / `NDeckViewScreen` 的 FrameMaterial 消费 | 音乐牌不进 Deck；且材质已由 §4.2 统一处理 |

### 3.3 素材与配置

- 本地化：`BangDreamInSpire/BangDreamInSpire/localization/zhs/gameplay_ui.json` 新增 `CARD_TYPE.MUSIC`，删除已无引用的 `BANG_DREAM_IN_SPIRE_MUSIC_TYPE`。
- 皮肤：`Ui.MusicCardFrame` 语义不变（每个角色/皮肤一条路径），无需改 `SkinInfo.cs`。
- 已知数据缺口：睦的两套皮肤 `ItsCrychic/ItsCrychic/skins/mutsumi/*.json:18` 的 `MusicCardFrame` 仍指向祥子的
  `default_music_bg_sakiko.png`，且睦的美术目录 `images/charui/mutsumi/` 没有音乐背景图。本次只保证解析正确，美术补齐后改 JSON 即可生效。

### 3.4 评审后的简化（m00528）

评审意见：① 由音乐牌接口要求提供 asset profile 保证默认值；② patch 只负责"验证角色与皮肤配置 + 动态替换"；
③ 枚举值不要写死，用 RitsuLib 的工具维护。据此：

- 删除 `SkinManager` 里的 `MusicCardSkinResolver` / `GetDefaultSkinInfo`（该文件恢复原样），解析逻辑内聚为
  `SkinVisualSupportPatches.cs` 内的 `MusicCardSkinFrames` 小助手；
- `MusicCardModel` 不再 override `CustomFramePath` / `CustomPortraitBorderPath`，默认值改由 `PerformCardAssets` 提供；
- 枚举值改为 `RegisterDynamicEnumValue<CardType>`，不写死 `7`。

## 4. 皮肤纹理按角色隔离

### 4.1 解析规则（唯一权威 = 卡牌来源角色）

```
ResolveOriginCharacter(card):
    1. card is MusicCardModel { OriginCharacter: not null } → 用它（Token 牌显式声明来源）
    2. ModelDb.AllCharacters 中 CardPool == card.VisualCardPool 的角色
    3. ModelDb.AllCharacters 中 ExtraCardPool == card.VisualCardPool 的角色
    4. null（不替换，保留 asset profile 默认卡框）
ResolveSkin(card, origin):
    card 可变且 Owner.RunState 是 RunState 时，在 runState.Players 找 Character == origin 的玩家
        → 用该玩家的 PlayerSkin（A 在本局的实时配置）
    否则 → 该角色 CharacterSkinList 中首个已注册皮肤（默认皮肤兜底）
TryResolveFramePath(card):
    path = ResolveSkin(...)?.Ui.MusicCardFrame
    if path != null && ResourceLoader.Exists(path) → path
    else → false（不替换，保留 asset profile 默认卡框）
```

- 关键点：**持有者只用于拿到 `RunState`，不参与选皮肤**。A 的卡在 B 手中时，`runState.Players` 里仍能找到 A（角色唯一），于是取 A 的 `PlayerSkin`。
- canonical 卡（卡牌库、奖励预览，`IsMutable == false`）不替换，显示 asset profile 默认卡框，与改动前一致。
- 单机时"来源角色玩家"就是自己，行为与现状一致。

### 4.2 实现点

| 位置 | 改动 |
|---|---|
| `Utils/BangDreamConst.cs` | `MusicCardType` 改为动态值属性（`Init` 模式）；新增 `IsMusicCardType(CardType)`、`PlayerSkinOrNull` |
| `Mechanics/Perform/PerformMechanic.cs` | 内容注册阶段用 `RitsuLibFramework.RegisterDynamicEnumValue<CardType>` 注册 Music 值 |
| `Interfaces/CardAugment/IPerformCard.cs` | 新增契约 `CardAssetProfile PerformCardAssets { get; }` |
| `Cards/MusicCardModel.cs` | 类型用 `BangDreamConst.MusicCardType`；`PerformCardAssets` 默认卡框并入 `AssetProfile`；保留 `OriginCharacter` |
| `Mechanics/Perform/MusicCardTypePatch.cs` | 目标改为 `CardTypeExtensions.ToLocString` Postfix（用 `IsMusicCardType` 判断） |
| `Patches/SkinVisualSupportPatches.cs` | `MusicCardSkinFrames` + 纹理两个 patch：`NCard.UpdatePortrait` 动态替换纹理；`CardModel.FrameMaterial` getter 同步清空材质 |
| `Nodes/BangDreamCharacterDetailsPopup.cs` | `GetTypeColor` 增加 Music 颜色 |
| `ItsCrychic/Scripts/Cards/Token/BasicScale.cs` | `OriginCharacter => ModelDb.Character<TogawaSakiko>()`（`TokenCardPool` 无法反查来源；该牌只由祥子牌生成） |
| 本地化 / 文档 | `gameplay_ui.json`、`docs/card-authoring.md` 同步 |

补充：`MelodyFragments` 已 override `VisualCardPool => SakikoMusicalCardPool`，规则 2 自动命中，无需改。

### 4.3 边界与风险

| 风险 | 处理 |
|---|---|
| 同角色多人（若允许） | `Players` 按顺序取第一个；要严格区分需 per-instance 来源（`ModelSavedData`），本次不做 |
| 远端玩家皮肤数据尚未同步 | 回退该角色默认皮肤（允许的兜底） |
| Token 牌来源不可由卡池判定 | `OriginCharacter` 覆写；未覆写则不替换、保留默认卡框 |
| `GiveToAnotherPlayer` 不触发重绘 | 与现状一致（节点下次 `Reload/UpdateVisuals` 时按新解析绘制，结果已不受持有者影响） |
| 皮肤单图会盖掉 attack/power/skill 三档角色卡框 | 音乐牌只有一个帧位，维持现状；如需区分需扩 `Ui`（本次不做） |
| 性能 | 每次解析只做一次 `Players` 小列表扫描 + `ResourceLoader.Exists`；无额外缓存 |

## 5. 验收

1. `dotnet build` 两个项目通过（0 警告 0 错误）。沙箱内构建需把部署目录重定向到工作区：
   `mkdir -p .dsh-tmp/dotnet-home .dsh-tmp/sts2 && DOTNET_CLI_HOME=$PWD/.dsh-tmp/dotnet-home dotnet build SlayTheBangDream.sln --no-restore -p:Sts2Dir=$PWD/.dsh-tmp/sts2 -p:Sts2DataDir="<游戏>/data_sts2_linuxbsd_x86_64"`
   （`Sts2Dir` 同时是 mods 部署目录，必须单独固定 `Sts2DataDir` 否则本体引用会解析失败）。
2. 代码搜索确认：`(CardType)7` 不再出现；`BANG_DREAM_IN_SPIRE_MUSIC_TYPE` 无引用；`CardType.Quest` 不再作为音乐牌类型出现。
3. 实机（后续）：
   - 音乐牌铭牌显示"音乐"，卡框显示来源角色皮肤配置；
   - A/B 双人局中 B 拿到 A 的音乐牌 → 显示 A 的皮肤卡框；
   - 未配置 `MusicCardFrame` 的皮肤 → 显示本体 quest 卡框，不报错；
   - 卡牌库中音乐牌显示本体 quest 卡框。

## 6. 本次修改文件清单

| 文件 | 改动 |
|---|---|
| `BangDreamInSpire/Scripts/Utils/BangDreamConst.cs` | `MusicCardType` 改为动态注册值属性 + `IsMusicCardType` + `PlayerSkinOrNull` |
| `BangDreamInSpire/Scripts/Mechanics/Perform/PerformMechanic.cs` | 内容注册阶段用 RitsuLib 动态枚举工具注册 Music 值 |
| `BangDreamInSpire/Scripts/Interfaces/CardAugment/IPerformCard.cs` | 新增 `PerformCardAssets` 契约（asset profile 保证默认值） |
| `BangDreamInSpire/Scripts/Cards/MusicCardModel.cs` | 类型改用 `MusicCardType`；`PerformCardAssets` 默认卡框并入 `AssetProfile`；保留 `OriginCharacter` |
| `BangDreamInSpire/Scripts/Mechanics/Perform/MusicCardTypePatch.cs` | 目标改为 `CardTypeExtensions.ToLocString` Postfix |
| `BangDreamInSpire/Scripts/Patches/SkinVisualSupportPatches.cs` | `MusicCardSkinFrames` + 纹理/材质两个替换 patch |
| `BangDreamInSpire/Scripts/Nodes/BangDreamCharacterDetailsPopup.cs` | `GetTypeColor` 增加 Music 颜色 |
| `ItsCrychic/Scripts/Cards/Token/BasicScale.cs` | 显式声明 `OriginCharacter => TogawaSakiko` |
| `BangDreamInSpire/BangDreamInSpire/localization/zhs/gameplay_ui.json` | 新增 `CARD_TYPE.MUSIC`，删除 `BANG_DREAM_IN_SPIRE_MUSIC_TYPE` |
| `docs/card-authoring.md` | 音乐牌类型/卡框/asset profile 说明同步 |
| `docs/architecture/music-cardtype-and-skin-isolation.md` | 本方案与实现记录 |
