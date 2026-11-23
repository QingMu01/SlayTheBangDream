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

    /// <summary>按玩家解析当前皮肤，未选择时使用首个注册皮肤。皮肤按 NetId 区分，
    /// 不能用 CharacterModel 静态资产接口替代。</summary>
    public static SkinInfo? ResolvePlayerSkin(Player player)
    {
        if (player.Character is not ISkinSupportCharacter skinSupportCharacter)
        {
            return null;
        }

        var selected = TryGetSelectedSkin(player);
        if (selected != null)
        {
            return selected;
        }

        // 菜单阶段也可读取角色注册皮肤。
        foreach (var skinPath in skinSupportCharacter.CharacterSkinList)
        {
            var fallback = GetSkinInfo(skinPath);
            if (fallback == null)
            {
                continue;
            }

            // 回落日志按玩家去重。
            WarnOnce(player.NetId, "fallback",
                $"Player {player.NetId} 尚未选择皮肤，回落到首个皮肤：{skinPath}");
            return fallback;
        }

        WarnOnce(player.NetId, "none",
            $"{player.Character} 没有任何可用皮肤（CharacterSkinList 为空或全部未注册）。");
        return null;
    }

    private static readonly HashSet<(ulong NetId, string Reason)> WarnedOnce = [];
    private static readonly object WarnedOnceLock = new();

    private static void WarnOnce(ulong netId, string reason, string message)
    {
        lock (WarnedOnceLock)
        {
            if (!WarnedOnce.Add((netId, reason)))
            {
                return;
            }
        }

        BangDreamInSpireCore.Logger.Warn(message);
    }

    /// <summary>读取已选皮肤，数据未注册或失败时返回 null。</summary>
    private static SkinInfo? TryGetSelectedSkin(Player player)
    {
        try
        {
            return BangDreamConst.PlayerSkinOrNull?.Get(player).GetSkin();
        }
        catch (Exception ex)
        {
            BangDreamInSpireCore.Logger.Warn($"读取玩家皮肤失败，按未选择处理：{ex.Message}");
            return null;
        }
    }

    /// <summary>解析玩家视觉场景；无皮肤时显式回退，避免 RitsuLib 使用 ironclad 占位。</summary>
    public static string? ResolvePlayerVisualScene(Player player)
    {
        return ResolvePlayerSkin(player)?.SkinTemplate.MultiplayerVisual.VisualScene;
    }
}
