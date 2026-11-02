using BangDreamInSpire.Scripts.Cards;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamInSpire.Scripts.Utils;

public static class SkinManager
{
    private static readonly Dictionary<string, SkinInfo> SkinsMap = new();

    public static void RegisterCharacterSkin(string path, SkinTemplate skinTemplates)
    {
        if (SkinsMap.ContainsKey(path))
        {
            BangDreamInSpireCore.Logger.Warn($"Skin({path}) already exists, will be overwrite it.");
        }

        SkinsMap[path] = new SkinInfo(skinTemplates);
    }

    public static SkinInfo? GetSkinInfo(string path)
    {
        return SkinsMap.GetValueOrDefault(path);
    }

    /// <summary>
    ///     返回角色皮肤列表中的首个已注册皮肤。皮肤列表本身没有"默认"标记，
    ///     按约定首项（<c>*_default.json</c>）作为默认皮肤，供"来源角色不在本局"等场景兜底。
    /// </summary>
    public static SkinInfo? GetDefaultSkinInfo(ISkinSupportCharacter character)
    {
        foreach (var path in character.CharacterSkinList)
        {
            if (GetSkinInfo(path) is { } skinInfo)
            {
                return skinInfo;
            }
        }

        return null;
    }
}

/// <summary>
///     解析音乐牌卡面纹理的"来源角色"。
///     <para>
///         皮肤配置以卡牌来源角色为唯一权威，而不是当前持有者：角色 A 的音乐牌被角色 B 取得后，
///         仍显示 A 在本局的皮肤配置；A 不在本局（或未配置音乐卡框）时回退 A 的默认皮肤，再无则回退本体卡框。
///     </para>
/// </summary>
public static class MusicCardSkinResolver
{
    private static readonly Dictionary<string, ISkinSupportCharacter?> OriginCharacterCache = new();

    /// <summary>未解析到皮肤配置时的兜底卡框：本体 quest 卡框（改造前音乐牌的卡框）。</summary>
    public static string DefaultFramePath =>
        ImageHelper.GetImagePath("atlases/ui_atlas.sprites/card/card_frame_quest_s.tres");

    /// <summary>
    ///     兜底卡图边框。原版 <c>CardModel.PortraitBorderPath</c> 里 Quest 映射到 Skill，
    ///     这里保持同样的视觉结果，避免新类型命中 <c>default: throw</c>。
    /// </summary>
    public static string DefaultPortraitBorderPath =>
        ImageHelper.GetImagePath("atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres");

    /// <summary>
    ///     返回来源角色配置的音乐卡框纹理；未配置或资源缺失时返回 null（调用方回退本体卡框）。
    ///     <para>
    ///         资源缺失必须在这里消化：RitsuLib 的贴图覆盖 Prefix 在加载失败时会退回原版
    ///         <c>CardModel.Frame</c>，而那条路径对 Music 运行时值会抛 <c>ArgumentOutOfRangeException</c>。
    ///     </para>
    /// </summary>
    public static string? ResolveFramePath(CardModel card)
    {
        if (ResolveSkinInfo(card)?.SkinTemplate.Ui.MusicCardFrame is not { } path)
        {
            return null;
        }

        return ResourceLoader.Exists(path) ? path : null;
    }

    /// <summary>该卡是否应由皮肤配置接管卡框（决定是否清空卡框材质）。</summary>
    public static bool UsesSkinFrame(CardModel card)
    {
        return ResolveFramePath(card) != null;
    }

    /// <summary>解析来源角色的皮肤配置：本局中有对应玩家时用该玩家的实时配置，否则用默认皮肤。</summary>
    public static SkinInfo? ResolveSkinInfo(CardModel card)
    {
        var origin = ResolveOriginCharacter(card);
        if (origin == null)
        {
            return null;
        }

        var player = ResolveOriginPlayer(card, origin);
        if (player != null)
        {
            var playerSkin = BangDreamConst.PlayerSkinOrNull?.Get(player).GetSkin();
            if (playerSkin?.SkinTemplate.Ui.MusicCardFrame != null)
            {
                return playerSkin;
            }
        }

        return SkinManager.GetDefaultSkinInfo(origin);
    }

    /// <summary>解析卡牌的来源角色：优先取卡牌自身的显式声明，其次按卡池反查角色。</summary>
    public static ISkinSupportCharacter? ResolveOriginCharacter(CardModel card)
    {
        if (card is MusicCardModel { OriginCharacter: { } declaredOrigin })
        {
            return declaredOrigin;
        }

        var key = card.Id.Entry;
        if (OriginCharacterCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var origin = FindCharacterByPool(card.Pool) ?? FindCharacterByPool(card.VisualCardPool);
        OriginCharacterCache[key] = origin;
        return origin;
    }

    private static Player? ResolveOriginPlayer(CardModel card, ISkinSupportCharacter origin)
    {
        if (!card.IsMutable || card.Owner?.RunState is not RunState runState)
        {
            return null;
        }

        foreach (var player in runState.Players)
        {
            if (player.Character is ISkinSupportCharacter character && ReferenceEquals(character, origin))
            {
                return player;
            }
        }

        return null;
    }

    private static ISkinSupportCharacter? FindCharacterByPool(CardPoolModel pool)
    {
        foreach (var model in ModelDb.AllCharacters)
        {
            if (model is not ISkinSupportCharacter character)
            {
                continue;
            }

            if (model.CardPool.Id.Equals(pool.Id))
            {
                return character;
            }

            if (model is IExtraDeckSupportCharacter { ExtraCardPool: { } extraPool } &&
                extraPool.Id.Equals(pool.Id))
            {
                return character;
            }
        }

        return null;
    }
}