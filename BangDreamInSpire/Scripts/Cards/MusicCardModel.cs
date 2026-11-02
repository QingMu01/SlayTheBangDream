using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamInSpire.Scripts.Cards;

public abstract class MusicCardModel(
    int baseCost,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : BandCardModel(baseCost, BangDreamConst.MusicCardType, rarity, target, showInCardLibrary),
    IPerformCard
{
    /// <summary>
    ///     卡牌的来源角色。默认为 null，由卡池反查；<c>TokenCardPool</c> 等无法从卡池识别来源的牌可覆写，
    ///     以便皮肤纹理仍按"来源角色"而不是"当前持有者"解析。
    /// </summary>
    public virtual ISkinSupportCharacter? OriginCharacter => null;

    /// <summary>音乐牌卡框：来源角色的皮肤配置优先，未配置时回退本体 quest 卡框。</summary>
    public override string? CustomFramePath =>
        MusicCardSkinResolver.ResolveFramePath(this) ?? MusicCardSkinResolver.DefaultFramePath;

    /// <summary>音乐牌卡图边框：类型在牌匾上已由"音乐"独立表达，固定使用本体 skill 边框。</summary>
    public override string? CustomPortraitBorderPath => MusicCardSkinResolver.DefaultPortraitBorderPath;

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