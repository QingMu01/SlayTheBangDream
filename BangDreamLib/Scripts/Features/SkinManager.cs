using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Saved;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib;
using STS2RitsuLib.Data;

namespace BangDreamLib.Scripts.Features;

public class SkinManager
{
    private static readonly Dictionary<Type, List<SkinInfo>> SkinsMap = new();

    private readonly ModDataStore? _modDataStore;
    private readonly SavedSkin _localSkinConfig;

    public SkinInfo? CurrentSkin { get; private set; }

    public SkinManager(Player player)
    {
        _modDataStore = RitsuLibFramework.GetDataStore(BangDreamConst.ModId);
        _localSkinConfig = _modDataStore.Get<SavedSkin>(BangDreamConst.SaveKeySkin);

        foreach (var skinsMapKey in SkinsMap.Keys.Where(skinsMapKey =>
                     !_localSkinConfig.CurrentIndexMap.TryGetValue(skinsMapKey, out _)))
        {
            _localSkinConfig.CurrentIndexMap[skinsMapKey] = 0;
        }

        if (player.Character is ISkinSupportCharacter)
        {
            var type = player.Character.GetType();
            if (SkinsMap.TryGetValue(type, out var skinList) && skinList.Count > 0)
            {
                var index = Math.Clamp(GetCurrentSkinIndex(type), 0, skinList.Count - 1);
                CurrentSkin = skinList[index];
            }
            else
            {
                BangDreamLibCore.Logger.Warn($"No skins registered for {type.Name}");
                CurrentSkin = null;
            }
        }
        else
        {
            BangDreamLibCore.Logger.Info($"Player.Character({player.Character} is unsupported skin.)");
        }
    }

    public SkinManager(Player player, SavedSkin savedSkin) : this(player)
    {
        _modDataStore = null;
        _localSkinConfig = savedSkin;

        if (player.Character is ISkinSupportCharacter)
        {
            var type = player.Character.GetType();
            CurrentSkin = SkinsMap[type][GetCurrentSkinIndex(type)];
        }
    }

    public void SetCurrentSkin(Type type, int index = 0)
    {
        if (_modDataStore == null)
        {
            return;
        }

        if (index < 0)
        {
            index = 0;
        }

        if (SkinsMap.TryGetValue(type, out var value))
        {
            if (index >= value.Count)
            {
                index = value.Count - 1;
            }

            _modDataStore.Modify<SavedSkin>(BangDreamConst.SaveKeySkin,
                savedSkin => { savedSkin.CurrentIndexMap[type] = index; });
            if (_modDataStore.Get<SavedSkin>(BangDreamConst.SaveKeySkin).CurrentIndexMap[type] == index)
            {
                CurrentSkin = value[index];
                _localSkinConfig.CurrentIndexMap[type] = index;
            }
        }
    }

    private int GetCurrentSkinIndex(Type type)
    {
        return _localSkinConfig.CurrentIndexMap.GetValueOrDefault(type, 0);
    }

    public static void RegisterCharacterSkin(Type type, SkinTemplate skinTemplates)
    {
        if (SkinsMap.TryGetValue(type, out var skinInfos))
        {
            if (skinInfos.Any(skinInfo => skinInfo.SkinTemplate.Equals(skinTemplates)))
            {
                return;
            }

            skinInfos.Add(new SkinInfo(skinTemplates));
        }
        else
        {
            SkinsMap[type] = [new SkinInfo(skinTemplates)];
        }
    }

    public static (int, List<SkinInfo>) GetCharacterSkins(Type type)
    {
        if (SkinsMap.TryGetValue(type, out var value))
        {
            var savedSkin = RitsuLibFramework.GetDataStore(BangDreamConst.ModId).Get<SavedSkin>(BangDreamConst.SaveKeySkin);
            return savedSkin.CurrentIndexMap.TryGetValue(type, out var index) ? (index, value) : (0, value);
        }

        return (-1, []);
    }
}