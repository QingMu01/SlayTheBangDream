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
    /// 解析某个玩家当前应使用的角色视觉场景（<see cref="SkinTemplate.MultiplayerVisual" /> 的
    /// <c>VisualScene</c>），供所有「按玩家取角色视觉」的入口共用。
    /// <para>
    /// 优先取玩家当前选中的皮肤；若尚未选过皮肤（<c>SkinPath</c> 为空，例如从未打开过皮肤选择器），
    /// 回落到该角色注册的第一个皮肤。
    /// </para>
    /// <para>
    /// 这一步回落是必要的：模组角色的 <c>AssetProfile</c> 为空，RitsuLib 的
    /// <c>CharacterAssetProfiles.Resolve</c> 会用 <c>PlaceholderCharacterId</c>（默认
    /// <c>ironclad</c>）补全路径，因此 <c>CharacterModel.CreateVisuals()</c> 在拿不到皮肤时会
    /// 返回**铁甲**的 creature visuals，而不是报错。
    /// </para>
    /// </summary>
    public static string? ResolvePlayerVisualScene(Player player)
    {
        if (player.Character is not ISkinSupportCharacter skinSupportCharacter)
        {
            return null;
        }

        // 玩家数据可能尚未注册（例如极早期的创建流程），失败时退回占位视觉而不是让调用方崩溃。
        try
        {
            var selected = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVisual
                .VisualScene;
            if (!string.IsNullOrEmpty(selected))
            {
                return selected;
            }

            foreach (var skinPath in skinSupportCharacter.CharacterSkinList)
            {
                var fallback = GetSkinInfo(skinPath)?.SkinTemplate.MultiplayerVisual.VisualScene;
                if (string.IsNullOrEmpty(fallback))
                {
                    continue;
                }

                BangDreamInSpireCore.Logger.Warn(
                    $"Player {player.NetId} 尚未选择皮肤，角色视觉回落到首个皮肤：{skinPath}");
                return fallback;
            }

            BangDreamInSpireCore.Logger.Error(
                $"{player.Character} 没有任何可用皮肤（CharacterSkinList 为空或全部未注册）。");
            return null;
        }
        catch (Exception ex)
        {
            BangDreamInSpireCore.Logger.Warn($"解析玩家皮肤视觉失败，使用占位视觉：{ex.Message}");
            return null;
        }
    }
}
