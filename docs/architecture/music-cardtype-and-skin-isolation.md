# 音乐牌 CardType 与皮肤纹理按角色隔离

- 状态：已实现（2026-11-02）；构建与静态验收通过，双人 A/B 实机验收未记录
- 目标：本体 `CardType` 是封闭枚举，音乐牌需要一个新的运行时类型；皮肤卡框必须按**卡牌来源角色**解析，而不是按当前持有者

## 1. 核心决策

1. **类型取值**：用 RitsuLib 动态枚举在内容注册阶段分配
   `BangDreamConst.MusicCardType = RitsuLibFramework.RegisterDynamicEnumValue<CardType>(ModId, "Music")`。
   值落在 `0x4000_0000+`，与本体/其它模组不冲突，同一 id 跨进程/跨存档稳定。**代码里不写死任何数值。**
2. **类型铭牌**：`CardTypeExtensions.ToLocString` 是穷举 switch 表达式且**没有 default**，新值会抛
   `SwitchExpressionException`。必须用 **Prefix** 直接写 `__result` 并 `return false`；Postfix 在原方法抛异常时根本不会执行。
   本地化键：`gameplay_ui` 表的 `CARD_TYPE.MUSIC`。
3. **卡面纹理**：`FramePath` / `PortraitBorderPath` / `AncientTextBgPath` 三处私有 switch 对新值抛异常。
   默认值由卡牌自身的 asset profile 保证：`IPerformCard.PerformCardAssets` 给出本体 quest 卡框与 skill 卡图边框，
   `MusicCardModel.AssetProfile` 将其并入。RitsuLib 的 getter Prefix 拿到非空路径后直接短路原版 switch，canonical 卡同样生效。
4. **类型颜色**：`BangDreamCharacterDetailsPopup.GetTypeColor` 用 `BangDreamConst.IsMusicCardType(type)` 前置判断，
   不能写进 switch 分支（值是运行时分配的，不是编译期常量）。
5. **皮肤只做替换、不参与兜底**：`SkinVisualSupportPatches` 只有在「来源角色 + 皮肤配置 `Ui.MusicCardFrame` + 资源存在」三者都命中时才替换纹理；
   否则保留 asset profile 的默认卡框。

## 2. 来源角色解析（唯一权威）

```
ResolveOriginCharacter(card):
    1. card is MusicCardModel { OriginCharacter: not null } → 用它（Token 牌显式声明）
    2. ModelDb.AllCharacters 中 CardPool == card.VisualCardPool 的角色
    3. 同上，ExtraCardPool == card.VisualCardPool 的角色
    4. null（不替换，保留 asset profile 默认卡框）
```

- 实现：`MusicCardModel.ResolveOriginCharacter` / `FindCharacterByPool`。
- `TokenCardPool` 无法靠卡池反查来源的音乐 Token 需覆写 `OriginCharacter`（如 `BasicScale` → `TogawaSakiko`）；
  已 override `VisualCardPool` 的牌（如 `MelodyFragments`）自动命中规则 2。
- 只有 `IsMutable == true` 的音乐牌参与替换；卡牌库、奖励预览等 canonical 卡保持默认卡框。

## 3. 皮肤解析规则

```
ResolveSkin(card, origin):
    来源角色就在本局（card.Owner.RunState.Players 中存在 Character == origin 的玩家）
        → 该玩家的 PlayerSkin（A 的卡在 B 手中时仍用 A 的配置）
    否则 → 该角色 CharacterSkinList 中首个已注册皮肤（默认皮肤兜底）
TryResolveFramePath(card):
    path = ResolveSkin(...)?.Ui.MusicCardFrame
    path 非空且 ResourceLoader.Exists(path) → 替换
    否则 → false（保留 asset profile 默认卡框）
```

- 持有者只用于拿到 `RunState`，不参与选皮肤。
- 远端玩家皮肤数据尚未同步时回退角色默认皮肤；同角色多人时按 `Players` 顺序取第一个（当前不支持同角色多人）。

## 4. 涉及实现

| 位置 | 作用 |
|---|---|
| `BangDreamInSpire/Scripts/Utils/BangDreamConst.cs` | `MusicCardType` 动态值属性（`Init` 模式）+ `IsMusicCardType` |
| `BangDreamInSpire/Scripts/Mechanics/Perform/PerformMechanic.cs` | 内容注册阶段注册 Music 动态枚举值 |
| `BangDreamInSpire/Scripts/Interfaces/CardAugment/IPerformCard.cs` | `PerformCardAssets` 契约 |
| `BangDreamInSpire/Scripts/Cards/MusicCardModel.cs` | 类型、默认卡框、来源角色解析 |
| `BangDreamInSpire/Scripts/Mechanics/Perform/MusicCardTypePatch.cs` | `ToLocString` Prefix |
| `BangDreamInSpire/Scripts/Patches/SkinVisualSupportPatches.cs` | `MusicCardSkinFrames` + 纹理/材质替换 patch |
| `BangDreamInSpire/Scripts/Nodes/BangDreamCharacterDetailsPopup.cs` | Music 类型颜色 |
| `ItsCrychic/Scripts/Cards/Token/BasicScale.cs` | 显式声明 `OriginCharacter` |
| `BangDreamInSpire/BangDreamInSpire/localization/zhs/gameplay_ui.json` | `CARD_TYPE.MUSIC` |

已知数据缺口：睦的两套皮肤 `MusicCardFrame` 仍指向祥子的 `default_music_bg_sakiko.png`，且睦的美术目录没有音乐背景图；
解析逻辑正确，美术补齐后改 JSON 即可生效。
