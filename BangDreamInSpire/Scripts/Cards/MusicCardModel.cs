using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamInSpire.Scripts.Cards;

public abstract class MusicCardModel(
    int baseCost,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : BandCardModel(baseCost, BangDreamConst.MusicCardType, rarity, target, showInCardLibrary),
    IPerformCard
{
    /// <summary>本体 quest 卡框：Music 是运行时新增类型，没有专属卡框资源，默认值必须指向一定存在的本体资源。</summary>
    private const string DefaultFramePath = "res://images/atlases/ui_atlas.sprites/card/card_frame_quest_s.tres";

    /// <summary>本体 skill 卡图边框：类型已由"音乐"铭牌单独表达，边框沿用本体 skill 资源。</summary>
    private const string DefaultPortraitBorderPath = "res://images/atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres";

    private CardAssetProfile? _assetProfile;

    /// <summary>
    ///     音乐牌的默认视觉资源（<see cref="IPerformCard" /> 契约）。由本基类保证非空，具体角色需要
    ///     专属默认卡框时再覆写；皮肤系统只负责在配置了 <c>Ui.MusicCardFrame</c> 时动态替换纹理，不参与兜底。
    /// </summary>
    public virtual CardAssetProfile PerformCardAssets => new()
    {
        FramePath = DefaultFramePath,
        PortraitBorderPath = DefaultPortraitBorderPath
    };

    /// <summary>
    ///     把音乐牌默认卡框并入卡牌资源描述。RitsuLib 的卡框 getter 覆盖据此拿到非空路径，
    ///     从而完全跳过本体按 CardType 穷举的 default 分支（新增类型会在那里抛异常）。
    /// </summary>
    public override CardAssetProfile AssetProfile => _assetProfile ??= base.AssetProfile with
    {
        FramePath = PerformCardAssets.FramePath,
        PortraitBorderPath = PerformCardAssets.PortraitBorderPath
    };

    /// <summary>
    ///     卡牌的来源角色。默认为 null，由卡池反查；<c>TokenCardPool</c> 等无法从卡池识别来源的牌可覆写，
    ///     以便皮肤纹理仍按"来源角色"而不是"当前持有者"解析。
    /// </summary>
    public virtual ISkinSupportCharacter? OriginCharacter => null;

    public virtual bool IsInstant => false;
    public virtual int AspirationSlot => 0;
    public virtual PerformEnqueueStrategy Strategy => PerformEnqueueStrategy.Default;

    public virtual Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        return Task.CompletedTask;
    }

    public virtual CardLocation StopPerformanceNextPile()
    {
        var location = base.GetResultLocationForCardPlay();
        return location.pileType == PileType.Discard
            ? new CardLocation(Owner, BangDreamConst.ExtraDraw, CardPilePosition.Bottom)
            : location;
    }

    protected sealed override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, BangDreamConst.PerformPile, CardPilePosition.Bottom);
    }
}