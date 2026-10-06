using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Extensions;
using Godot;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Saki;

[RegisterCard(typeof(SakikoMusicalCardPool), Inherit = true)]
public abstract class AbstractSakikoMusicCard(int baseCost, CardRarity rarity, TargetType target)
    : MusicCardModel(baseCost, rarity, target)
{
    private const string SakikoSuffix = "Sakiko";
    private const string BetaPortraitSuffix = "_Bate";

    protected AbstractSakikoMusicCard(CardRarity rarity, TargetType target) : this(0, rarity, target)
    {
    }

    public override CardAssetProfile AssetProfile => base.AssetProfile with
    {
        PortraitPath = FindSharedPortrait(string.Empty) ?? GetType().GetCardImg(),
        BetaPortraitPath = FindSharedPortrait(BetaPortraitSuffix) ?? GetType().GetCardBateImg()
    };

    private string? FindSharedPortrait(string nameSuffix)
    {
        var type = GetType();
        if (!type.Name.EndsWith(SakikoSuffix, StringComparison.Ordinal)) return null;

        var modFolder = type.Namespace?.Split(".")[0];
        if (string.IsNullOrEmpty(modFolder)) return null;

        var sharedName = type.Name[..^SakikoSuffix.Length] + nameSuffix;
        var path = $"res://{modFolder}/images/card_portraits/{sharedName}.png";
        return ResourceLoader.Exists(path) ? path : null;
    }
}
