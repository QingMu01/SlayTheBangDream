using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Players;

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
    /// 解析某个玩家当前应使用的皮肤（<see cref="SkinInfo" />）。
    /// <para>
    /// 优先取玩家当前选中的皮肤；若尚未选过皮肤（<c>SkinPath</c> 为空，例如从未打开过皮肤选择器），
    /// 回落到该角色注册的第一个皮肤。
    /// </para>
    /// <para>
    /// **这是「按玩家」解析皮肤的唯一入口**。模组的皮肤是 per-player 的（<c>PlayerRunSavedData</c> 按
    /// <c>NetId</c> 索引），而 RitsuLib 的资产覆盖（<c>IModCharacterAssetOverrides.AssetProfile</c> /
    /// <c>CustomVisualsPath</c> / <c>WorldProceduralVisuals</c>）是 **per-CharacterModel** 的静态属性，
    /// 无法表达「同一角色、不同玩家、不同皮肤」。因此**不要**用那些接口来承载皮肤，
    /// 否则多人下同角色的两个玩家会拿到同一份视觉。
    /// </para>
    /// </summary>
    public static SkinInfo? ResolvePlayerSkin(Player player)
    {
        if (player.Character is not ISkinSupportCharacter skinSupportCharacter)
        {
            return null;
        }

        // 玩家数据可能尚未注册（例如极早期的创建流程），失败时退回占位视觉而不是让调用方崩溃。
        try
        {
            var selected = BangDreamConst.PlayerSkin.Get(player).GetSkin();
            if (selected != null)
            {
                return selected;
            }

            foreach (var skinPath in skinSupportCharacter.CharacterSkinList)
            {
                var fallback = GetSkinInfo(skinPath);
                if (fallback == null)
                {
                    continue;
                }

                BangDreamInSpireCore.Logger.Warn(
                    $"Player {player.NetId} 尚未选择皮肤，回落到首个皮肤：{skinPath}");
                return fallback;
            }

            BangDreamInSpireCore.Logger.Error(
                $"{player.Character} 没有任何可用皮肤（CharacterSkinList 为空或全部未注册）。");
            return null;
        }
        catch (Exception ex)
        {
            BangDreamInSpireCore.Logger.Warn($"解析玩家皮肤失败，使用占位视觉：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 解析某个玩家当前应使用的角色视觉场景（<see cref="SkinTemplate.MultiplayerVisual" /> 的
    /// <c>VisualScene</c>），供所有「按玩家取角色视觉」的入口共用。
    /// <para>
    /// 这一步回落是必要的：模组角色的 <c>AssetProfile</c> 为空，RitsuLib 的
    /// <c>CharacterAssetProfiles.Resolve</c> 会用 <c>PlaceholderCharacterId</c>（默认
    /// <c>ironclad</c>）补全路径，因此 <c>CharacterModel.CreateVisuals()</c> 在拿不到皮肤时会
    /// 返回**铁甲**的 creature visuals，而不是报错。
    /// </para>
    /// </summary>
    public static string? ResolvePlayerVisualScene(Player player)
    {
        return ResolvePlayerSkin(player)?.SkinTemplate.MultiplayerVisual.VisualScene;
    }
}
