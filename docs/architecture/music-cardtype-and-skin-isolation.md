# 新增 CardType「Music」与皮肤纹理按角色隔离

- 状态：已实现（2026-11-02，两个项目构建通过；实机验收待做）
- 方案落盘：2026-11-02（动手改码前）
- 日期：2026-11-02
- 目标分支：`main`（本体 0.111.0，RitsuLib 0.6.10）

## 1. 结论

1. 本体 `CardType` 是封闭枚举，无法从模组扩展。音乐牌改用**运行时值 `(CardType)7`**（下称 `Music`），通过 `CardModel.Type` 取值即得到该值（`CardModel.Type` 是 `virtual`，本体亦有 `MockQuestCard` 先例）。
2. 类型值本身没有序列化/联机成本：`SerializableCard` 不含 `CardType`，`MaxEnumValueCache` 只被 `PacketReader/PacketWriter` 的泛型枚举写入使用。
3. 真正会炸的只有**卡面纹理 getter**与**类型铭牌本地化**两条渲染路径，且都可以用 RitsuLib 官方钩子 + 一处小 patch 接管，不需要替换本体私有 switch。
4. 皮肤替换当前以 `Model.Owner`（**当前持有者**）为唯一权威；改为以**来源角色**（由卡池反查）为权威：A 的卡被 B 取得后仍解析 A 在本局的皮肤配置，A 不在本局时回退 A 的默认皮肤（`CharacterSkinList[0]`），再回退本体卡框。

## 2. 现状

### 2.1 音乐牌

- `BangDreamInSpire/Scripts/Cards/MusicCardModel.cs:10-16`：`MusicCardModel(...) : BandCardModel(baseCost, CardType.Quest, rarity, target, showInCardLibrary), IPerformCard`。
- 角色基类按角色注册卡池：`ItsCrychic/Scripts/Cards/Saki/AbstractSakikoMusicCard.cs:11`（`SakikoMusicalCardPool`）、`.../Mutsumi/AbstractMutsumiMusicCard.cs:8`（`MutsumiMusicalCardPool`）；Token 牌注册 `TokenCardPool`（`Scripts/Cards/Token/BasicScale.cs:15`、`MelodyFragments.cs:17`）。
- 类型铭牌靠 `BangDreamInSpire/Scripts/Mechanics/Perform/MusicCardTypePatch.cs` 覆盖 `NCard.UpdateTypePlaque` 的文本（PatchId `replace_music_card_type`）。
- 音乐牌经 `CardPileCmd.Add(card, BangDreamConst.ExtraDeck)` 常驻额外卡组/演奏堆（`Mechanics/ExtraDeck/MusicCardReward.cs:205-209`），**不会进入 Deck**。

### 2.2 皮肤替换（不跟随角色）

`BangDreamInSpire/Scripts/Patches/SkinVisualSupportPatches.cs`：

- `MusicCardFramePatch`（PatchId `create_music_card_frame_by_skin_info`，target `NCard.UpdatePortrait`）：条件 `Model is IPerformCard && Model is { IsMutable: true, Owner.Character: ISkinSupportCharacter }`，路径取 `BangDreamConst.PlayerSkin.Get(Model.Owner)`——即**持有者**。
- `MusicCardFrameMaterialPatch`（PatchId `clear_music_card_frame_material_by_skin_info`，target `NCard.Reload`）：同一条件，把 `_frame.Material` 置空。
- 皮肤配置本身以 JSON 资源路径为键（`Utils/SkinManager.cs`），与角色无绑定；每个角色的可选皮肤列表在 `CharacterSkinList`（`BandMemberModel`）。

因此现状：B 拿到 A 的卡后显示的是 **B 的皮肤**；且 canonical/卡牌库（`IsMutable == false`）整段跳过，恒显示本体 quest 卡框。

### 2.3 相关本体事实（证据）

| 事实 | 位置 |
|---|---|
| `enum CardType { None, Attack, Skill, Power, Status, Curse, Quest }` | `sts2unpack/0.111.0/src/Core/Entities/Cards/CardType.cs` |
| `CardModel.Type` 为 `virtual` | `src/Core/Models/CardModel.cs:289` |
| `FramePath / PortraitBorderPath / AncientTextBgPath` 三处私有 switch 穷举，`default: throw ArgumentOutOfRangeException` | `src/Core/Models/CardModel.cs:161-240` |
| `CardTypeExtensions.ToLocString()` 是 switch 表达式，**无 default** → 新值抛 `SwitchExpressionException` | `src/Core/Entities/Cards/CardTypeExtensions.cs:7-25` |
| `ToLocString` 的展示调用点只有 `NCard.UpdateTypePlaque`（`src/Core/Nodes/Cards/NCard.cs:822`）与 `TheFutureOfPotions`（非卡牌展示） | 同上 |
| `CardType.Quest` 的行为判定只有 3 处：变换候选/选牌过滤/镜子遗物（均在 Deck 语义内） | `CardSelectCmd.cs:591`、`CardFactory.cs:172`、`DollysMirror.cs:28` |
| `CardType` 不进存档/网络包 | `src/Core/Saves/Runs/SerializableCard.cs:13-53`；`MaxEnumValueCache` 仅用于 packet 泛型枚举 |
| RitsuLib 以 Prefix 短路 `CardModel.Frame/PortraitBorder/EnergyIcon/Ancient*` 与 `FrameMaterial` getter：找到覆盖则不跑原版 | `STS2-RitsuLib/src/Scaffolding/Content/Patches/ContentAssetModelVisualOverridePatches.cs` |
| `IModCardAssetOverrides.CustomFramePath / CustomPortraitBorderPath` 可被 `ModCardTemplate` 子类 override | `STS2-RitsuLib/src/Scaffolding/Content/ModCardTemplate.cs` |
| `CustomFramePath` 为空 → RitsuLib 回退原版 `CardModel.Frame`（对 Music 会抛） | 同上 + `ContentAssetOverridePatches.cs` |
| `GiveToAnotherPlayer` 只改 `_owner`，不触发节点重绘 | `src/Core/Models/CardModel.cs:2206` |
| 皮肤数据为每玩家 run 数据，联机全员可读 | `Multiplayer/RunData/PlayerSkinData.cs`、`BangDreamInSpireCore.cs:96-108` |

## 3. 新增 CardType「Music」要处理的内容

### 3.1 必改（不接管会崩/错）

1. **类型取值**：`MusicCardModel` 基类 ctor 改传 `BangDreamConst.MusicCardType`（`(CardType)7`）。
2. **铭牌本地化**：新增 `CardTypeExtensions.ToLocString` 的 Postfix，把 `Music` 映射到 `LocString("gameplay_ui", "CARD_TYPE.MUSIC")`，并在 `gameplay_ui.json` 增加 `"CARD_TYPE.MUSIC": "音乐"`。
   - 原 `MusicCardTypePatch` 的 `NCard.UpdateTypePlaque` 覆盖被此机制取代（当前 `IPerformCard` 实现者只有 `MusicCardModel`，不会漏掉任何牌）。
   - 如果保留旧 patch 与新版并存，等于两套权威；选择**只保留一套**，旧 PatchId 复用、目标换成 `ToLocString`。
3. **卡面纹理**：`Music` 会命中原版三处 switch 的 `default` throw。在 `MusicCardModel` 上 override
   - `CustomFramePath`：优先返回皮肤解析结果，否则返回本体 quest 卡框路径（`atlases/ui_atlas.sprites/card/card_frame_quest_s.tres`）；
   - `CustomPortraitBorderPath`：返回本体 skill 卡图边框路径（quest 在原版 switch 里映射到 skill，`atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres`）。
   - RitsuLib 的 getter Prefix 会在原版 switch 之前短路，所以只要保证**永远返回非空**就不会抛。
   - `AncientTextBgPath` 只对 `Rarity == Ancient` 生效；音乐牌稀有度只有 Common/Uncommon/Rare/Token，不可达。
4. **类型颜色**：`BangDreamInSpire/Scripts/Nodes/BangDreamCharacterDetailsPopup.cs` 的 `GetTypeColor` 增加 `Music` 分支（否则落到灰色兜底）。

### 3.2 记录但不改（行为不可达/已由框架兜底）

| 点 | 结论 |
|---|---|
| `CardSelectCmd.cs:591`、`CardFactory.cs:172`、`DollysMirror.cs:28` 的 `Type != Quest` | 都是 Deck 内变换语义；音乐牌永不进 Deck，不可达。若未来支持"从额外卡组变换"，再补 `!= Music` 判定 |
| `EnchantmentModel.CanEnchant` 的 `(uint)(type - 4) <= 2u` | 只排除 Status/Curse/Quest；Music 会变成可附魔——同样仅当音乐牌进 Deck 才可达 |
| `HiddenGem.cs` 的类型过滤 | 同上 |
| 卡牌库"NCardLibrary"的 攻击/技能/能力/其他 筛选 | Music 落入"其他"，与改造前（Quest）一致 |
| `CardPoolModel` / `NTinyCard` / `NDeckViewScreen` 的 FrameMaterial 消费 | 音乐牌不进 Deck；且材质已由 3.3 统一处理 |

### 3.3 素材与配置

- 本地化：`BangDreamInSpire/BangDreamInSpire/localization/zhs/gameplay_ui.json` 新增 `CARD_TYPE.MUSIC`，删除已无引用的 `BANG_DREAM_IN_SPIRE_MUSIC_TYPE`。
- 皮肤：`Ui.MusicCardFrame` 语义不变（每个角色/皮肤一条路径），无需改 `SkinInfo.cs`。
- 已知数据缺口：睦的两套皮肤 `skins/mutsumi/*.json:18` 的 `MusicCardFrame` 仍指向祥子的 `default_music_bg_sakiko.png`，且睦的美术目录 `images/charui/mutsumi/` 没有音乐背景图。本次只保证解析正确，美术补齐后改 JSON 即可生效。

## 4. 皮肤纹理按角色隔离

### 4.1 解析规则（唯一权威 = 卡牌来源角色）

```
ResolveOriginCharacter(card):
    1. card is MusicCardModel { OriginCharacter: not null } → 用它（Token 牌显式声明来源）
    2. ModelDb.AllCharacters 中 CardPool == card.Pool 的角色
    3. ModelDb.AllCharacters 中 ExtraCardPool == card.VisualCardPool 的角色
    4. null（按"无来源角色"处理，回退本体卡框）
ResolveSkinInfo(card):
    origin = ResolveOriginCharacter(card)
    origin == null → null
    player = (card 可变且 Owner.RunState 是 RunState) ? runState.Players 中 Character == origin 的玩家 : null
    player != null 且 PlayerSkin.Get(player).GetSkin().Ui.MusicCardFrame != null → 用它   // A 在本局的实时配置
    否则 → SkinManager.GetDefaultSkinInfo(origin)                                        // A 的默认皮肤兜底
ResolveFramePath(card):
    path = skinInfo?.Ui.MusicCardFrame
    if path != null && ResourceLoader.Exists(path) → path
    else → null（调用方回退本体卡框）
```

> 资源存在性必须在这里消化：RitsuLib 的贴图覆盖 Prefix 加载失败时会退回原版 `CardModel.Frame`，
> 而那条路径对 Music 运行时值会抛 `ArgumentOutOfRangeException`。

- 关键点：**持有者只用于拿到 `RunState`，不参与选皮肤**。A 的卡在 B 手中时，`runState.Players` 里仍能找到 A（角色唯一），于是取 A 的 `PlayerSkin`。
- 页面/库存等无 `RunState` 的 canonical 卡（卡牌库、奖励预览）→ 走 A 的默认皮肤；这使音乐牌在卡牌库中也能显示角色主题卡框（原为 quest 卡框）。
- 单机时"来源角色玩家"就是自己，行为与现状一致。

### 4.2 实现点

| 位置 | 改动 |
|---|---|
| `Utils/BangDreamConst.cs` | 新增 `public const CardType MusicCardType = (CardType)7;`；新增 `internal static PlayerRunSavedData<PlayerSkinData>? PlayerSkinOrNull`（未初始化时安全返回 null） |
| `Utils/SkinManager.cs` | 新增 `GetDefaultSkinInfo(ISkinSupportCharacter)`：按 `CharacterSkinList` 顺序返回第一条已注册皮肤 |
| `Utils/SkinManager.cs`（追加 `MusicCardSkinResolver` 类） | 4.1 的解析实现 + `DefaultFramePath`/`DefaultPortraitBorderPath` 常量；来源角色按 `ModelId.Entry` 记忆化。放在既有文件里，避免新增 `.cs` 需要 Godot 生成 `.cs.uid` |
| `Cards/MusicCardModel.cs` | ctor 传 `BangDreamConst.MusicCardType`；新增 `public virtual ISkinSupportCharacter? OriginCharacter => null;`；override `CustomFramePath`/`CustomPortraitBorderPath` |
| `Mechanics/Perform/MusicCardTypePatch.cs` | 目标改为 `CardTypeExtensions.ToLocString` 的 Postfix（Music → `CARD_TYPE.MUSIC`） |
| `Patches/SkinVisualSupportPatches.cs` | 删除 `MusicCardFramePatch`（纹理改由 `CustomFramePath` 输出）；`MusicCardFrameMaterialPatch` 目标改为 `CardModel.FrameMaterial` 的 getter Postfix：`ResolveFramePath(card) != null → __result = null`，保证纹理与材质在同一条解析链上，避免 postfix 顺序竞争 |
| `Nodes/BangDreamCharacterDetailsPopup.cs` | `GetTypeColor` 增加 `Music` 分支 |
| `ItsCrychic/Scripts/Cards/Token/BasicScale.cs` | `OriginCharacter => ModelDb.Character<TogawaSakiko>()`（`TokenCardPool` 无法反查来源；该牌只由祥子牌生成） |
| 本地化 / 文档 | `gameplay_ui.json`、`docs/card-authoring.md` 同步 |

补充：`MelodyFragments` 已 override `VisualCardPool => SakikoMusicalCardPool`，规则 3 自动命中，无需改。

### 4.3 边界与风险

| 风险 | 处理 |
|---|---|
| 同角色多人（若允许）| `Players` 按顺序取第一个；要严格区分需 per-instance 来源（`ModelSavedData`），本次不做 |
| 远端玩家皮肤数据尚未同步 | 回退该角色默认皮肤（允许的兜底） |
| Token 牌来源不可由卡池判定 | `OriginCharacter` 覆写；未覆写则回退本体卡框 |
| `GiveToAnotherPlayer` 不触发重绘 | 与现状一致（卡牌节点下次 `Reload/UpdateVisuals` 时按新解析绘制，解析结果此时已不受持有者影响） |
| canonical 卡牌库显示角色皮肤而非 quest 卡框 | 有意行为（与"来源角色配置"一致），若需保留静态卡框可后续加开关 |
| 皮肤单图会盖掉 attack/power/skill 三档角色卡框 | 音乐牌只有一个帧位，维持现状；如需区分需扩 `Ui` 为 MusicCardFrame×3（本次不做） |
| 性能 | 来源角色按模型 Id 缓存；每次解析只做一次 `Players` 小列表扫描 |

## 5. 验收

1. `dotnet build` 两个项目通过（0 警告 0 错误）。沙箱内构建需把部署目录重定向到工作区：
   `dotnet build SlayTheBangDream.sln --no-restore -p:Sts2Dir=$PWD/.dsh-tmp/sts2 -p:Sts2DataDir="<游戏>/data_sts2_linuxbsd_x86_64"`（`Sts2Dir` 同时是 mods 部署目录，必须单独固定 `Sts2DataDir` 否则本体引用会解析失败）。
2. 代码搜索确认：`CardType.Quest` 不再作为音乐牌类型出现；`BANG_DREAM_IN_SPIRE_MUSIC_TYPE` 无引用。
3. 实机（后续）：
   - 音乐牌铭牌显示"音乐"，卡框显示当前角色皮肤配置；
   - A/B 双人局中 B 拿到 A 的音乐牌 → 显示 A 的皮肤卡框；
   - 未配置 `MusicCardFrame` 的皮肤 → 显示本体 quest 卡框，不报错；
   - 卡牌库中音乐牌显示来源角色默认皮肤卡框。

## 6. 本次修改文件清单

| 文件 | 改动 |
|---|---|
| `BangDreamInSpire/Scripts/Utils/BangDreamConst.cs` | 新增 `MusicCardType = (CardType)7`、`PlayerSkinOrNull` |
| `BangDreamInSpire/Scripts/Utils/SkinManager.cs` | 新增 `GetDefaultSkinInfo`；追加 `MusicCardSkinResolver`（来源角色/皮肤解析、兜底路径） |
| `BangDreamInSpire/Scripts/Cards/MusicCardModel.cs` | 类型改用 `MusicCardType`；新增 `OriginCharacter`；override `CustomFramePath` / `CustomPortraitBorderPath` |
| `BangDreamInSpire/Scripts/Mechanics/Perform/MusicCardTypePatch.cs` | 目标改为 `CardTypeExtensions.ToLocString` Postfix |
| `BangDreamInSpire/Scripts/Patches/SkinVisualSupportPatches.cs` | 删除 `MusicCardFramePatch`；材质改为 `CardModel.FrameMaterial` getter Postfix，按新解析链清空 |
| `BangDreamInSpire/Scripts/Nodes/BangDreamCharacterDetailsPopup.cs` | `GetTypeColor` 增加 Music 分支 |
| `ItsCrychic/Scripts/Cards/Token/BasicScale.cs` | 显式声明 `OriginCharacter => TogawaSakiko`（`TokenCardPool` 无法反查来源） |
| `BangDreamInSpire/BangDreamInSpire/localization/zhs/gameplay_ui.json` | 新增 `CARD_TYPE.MUSIC`，删除 `BANG_DREAM_IN_SPIRE_MUSIC_TYPE` |
| `docs/card-authoring.md` | 音乐牌类型/卡框说明同步 |
| `docs/architecture/music-cardtype-and-skin-isolation.md` | 本方案与实现记录 |
