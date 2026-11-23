using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamInSpire.Scripts.Cards;

public abstract class MusicCardModel(
    int baseCost,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : BandCardModel(baseCost, RequireMusicCardType(), rarity, target, showInCardLibrary),
    IPerformCard
{
    /// <summary>读取注册阶段写入的音乐卡类型，过早使用时给出明确错误。</summary>
    private static CardType RequireMusicCardType()
    {
        try
        {
            return BangDreamConst.MusicCardType;
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                "音乐卡必须在核心内容注册（PerformMechanic.RegisterContent）之后构造：" +
                "BangDreamConst.MusicCardType 尚未初始化。请等核心 mod 初始化完成后再实例化音乐卡。", ex);
        }
    }

    /// <summary>使用存在的本体 quest 卡框作为默认。</summary>
    private const string DefaultFramePath = "res://images/atlases/ui_atlas.sprites/card/card_frame_quest_s.tres";

    /// <summary>沿用本体 skill 卡图边框。</summary>
    private const string DefaultPortraitBorderPath = "res://images/atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres";

    private CardAssetProfile? _assetProfile;

    /// <summary>默认音乐卡资源，皮肤仅按需替换卡框。</summary>
    public virtual CardAssetProfile PerformCardAssets => new()
    {
        FramePath = DefaultFramePath,
        PortraitBorderPath = DefaultPortraitBorderPath
    };

    /// <summary>提供非空默认卡框，避免进入本体新增类型默认分支。</summary>
    public override CardAssetProfile AssetProfile => _assetProfile ??= base.AssetProfile with
    {
        FramePath = PerformCardAssets.FramePath,
        PortraitBorderPath = PerformCardAssets.PortraitBorderPath
    };

    /// <summary>可选来源角色，供无法从卡池反查的牌使用。</summary>
    public virtual ISkinSupportCharacter? OriginCharacter => null;

    /// <summary>优先使用声明的来源角色，否则按卡池反查。</summary>
    public static ISkinSupportCharacter? ResolveOriginCharacter(CardModel card)
    {
        return (card as MusicCardModel)?.OriginCharacter ?? FindCharacterByPool(card.VisualCardPool);
    }

    private static ISkinSupportCharacter? FindCharacterByPool(CardPoolModel? pool)
    {
        if (pool == null)
        {
            return null;
        }

        foreach (var character in ModelDb.AllCharacters)
        {
            if (character is not ISkinSupportCharacter skinSupport)
            {
                continue;
            }

            if (ReferenceEquals(character.CardPool, pool))
            {
                return skinSupport;
            }

            if (character is IExtraDeckSupportCharacter extraDeckSupport &&
                ReferenceEquals(extraDeckSupport.ExtraCardPool, pool))
            {
                return skinSupport;
            }
        }

        return null;
    }

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