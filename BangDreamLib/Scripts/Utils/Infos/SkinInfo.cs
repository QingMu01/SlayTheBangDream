using System.Reflection;
using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Utils.Infos;

public class SkinInfo
{
    private static readonly Dictionary<Type, Func<string, AbstractModel?>> ModelFactories = new()
    {
        [typeof(CardModel)] = BangDreamModelHelper.GetCardModelByEntry,
        [typeof(RelicModel)] = BangDreamModelHelper.GetRelicModelByEntry,
        [typeof(PotionModel)] = BangDreamModelHelper.GetPotionModelByEntry
    };

    public SkinTemplate SkinTemplate { get; }

    public SkinInfo(SkinTemplate skinTemplate)
    {
        var multiplayerVisual = new MultiplayerVisual(
            VisualScene: skinTemplate.MultiplayerVisual.VisualScene?.EmptyStringFilter(),
            RestSiteScene: skinTemplate.MultiplayerVisual.RestSiteScene?.EmptyStringFilter(),
            MerchantScene: skinTemplate.MultiplayerVisual.MerchantScene?.EmptyStringFilter(),
            ArmPointingTexture: skinTemplate.MultiplayerVisual.ArmPointingTexture?.EmptyStringFilter(),
            ArmRockTexture: skinTemplate.MultiplayerVisual.ArmRockTexture?.EmptyStringFilter(),
            ArmPaperTexture: skinTemplate.MultiplayerVisual.ArmPaperTexture?.EmptyStringFilter(),
            ArmScissorsTexture: skinTemplate.MultiplayerVisual.ArmScissorsTexture?.EmptyStringFilter(),
            RestSiteAnimName: skinTemplate.MultiplayerVisual.RestSiteAnimName?.EmptyStringFilter(),
            MerchantAnimName: skinTemplate.MultiplayerVisual.MerchantAnimName?.EmptyStringFilter()
        );

        var localVisual = new LocalVisual(
            EnergyCounterScene: skinTemplate.LocalVisual.EnergyCounterScene?.EmptyStringFilter(),
            IconScene: skinTemplate.LocalVisual.IconScene?.EmptyStringFilter(),
            IconTexture: skinTemplate.LocalVisual.IconTexture?.EmptyStringFilter(),
            IconTextureOutline: skinTemplate.LocalVisual.IconTextureOutline?.EmptyStringFilter(),
            MapMarker: skinTemplate.LocalVisual.MapMarker?.EmptyStringFilter()
        );

        var multiplayerVfx = new MultiplayerVfx(
            MusicNote: skinTemplate.MultiplayerVfx.MusicNote?.EmptyStringFilter()
        );

        var localVfx = new LocalVfx(
            CardTrail: skinTemplate.LocalVfx.CardTrail?.EmptyStringFilter()
        );

        SkinTemplate =
            new SkinTemplate(multiplayerVisual, localVisual, multiplayerVfx, localVfx, skinTemplate.Starting);
    }

    public IEnumerable<string> GetAllVisualResourcePaths()
    {
        foreach (var s in GetNonEmptyStrings(SkinTemplate.MultiplayerVisual)) yield return s;
        foreach (var s in GetNonEmptyStrings(SkinTemplate.MultiplayerVfx)) yield return s;
        foreach (var s in GetNonEmptyStrings(SkinTemplate.LocalVisual)) yield return s;
        foreach (var s in GetNonEmptyStrings(SkinTemplate.LocalVfx)) yield return s;
    }

    public IEnumerable<CardModel> GetStartingDeck()
    {
        return SkinTemplate.Starting.StartingDeck == null
            ? []
            : IdsToModels<CardModel>(SkinTemplate.Starting.StartingDeck);
    }

    public IEnumerable<RelicModel> GetStartingRelics()
    {
        return SkinTemplate.Starting.StartingRelics == null
            ? []
            : IdsToModels<RelicModel>(SkinTemplate.Starting.StartingRelics);
    }

    public IEnumerable<CardModel> GetStartingExtraDeck()
    {
        return SkinTemplate.Starting.StartingExtraDeck == null
            ? []
            : IdsToModels<CardModel>(SkinTemplate.Starting.StartingExtraDeck);
    }

    public IEnumerable<PotionModel> GetStartingPotions()
    {
        return SkinTemplate.Starting.StartingPotions == null
            ? []
            : IdsToModels<PotionModel>(SkinTemplate.Starting.StartingPotions);
    }

    private static IEnumerable<string> GetNonEmptyStrings<T>(T obj)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Where(p => !p.Name.EndsWith("AnimName"));
        foreach (var prop in props)
        {
            var value = (string?)prop.GetValue(obj);
            if (!string.IsNullOrEmpty(value))
                yield return value;
        }
    }

    private static IEnumerable<T> IdsToModels<T>(IEnumerable<string> strings) where T : AbstractModel
    {
        if (!ModelFactories.TryGetValue(typeof(T), out var factory))
            yield break;
        var skinIds = strings.ToList();
        string? lastSearch = null;
        T? lastFind = null;
        foreach (var id in skinIds)
        {
            if (id == lastSearch && lastFind != null)
            {
                yield return lastFind;
            }
            else
            {
                lastSearch = id;
                lastFind = factory(id) as T;
                if (lastFind != null)
                    yield return lastFind;
                else
                    BangDreamLibCore.Logger.Error($"Not Find Model :ModelId.Entry={lastSearch}");
            }
        }
    }
}

// Json模板
public record SkinTemplate(
    MultiplayerVisual MultiplayerVisual,
    LocalVisual LocalVisual,
    MultiplayerVfx MultiplayerVfx,
    LocalVfx LocalVfx,
    StartingInfo Starting
);

// 可配置项 多人模式下所有人可见且可替换的视觉资源
public record MultiplayerVisual(
    string? VisualScene = null,
    string? RestSiteScene = null,
    string? MerchantScene = null,
    string? ArmPointingTexture = null,
    string? ArmRockTexture = null,
    string? ArmPaperTexture = null,
    string? ArmScissorsTexture = null,
    string? RestSiteAnimName = null,
    string? MerchantAnimName = null
);

// 可配置项 多人模式下所有人可见且可替换的特效资源
public record MultiplayerVfx(
    string? MusicNote = null
);

// 可配置项 单人模式下可替换的视觉资源
public record LocalVisual(
    string? EnergyCounterScene = null,
    string? IconScene = null,
    string? IconTexture = null,
    string? IconTextureOutline = null,
    string? MapMarker = null);

// 可配置项 单人模式下可替换的特效资源
public record LocalVfx(
    string? CardTrail = null
);

// 可配置项 初始卡组、遗物、药水、额外牌组
public record StartingInfo(
    IEnumerable<string>? StartingDeck = null,
    IEnumerable<string>? StartingRelics = null,
    IEnumerable<string>? StartingPotions = null,
    IEnumerable<string>? StartingExtraDeck = null
);