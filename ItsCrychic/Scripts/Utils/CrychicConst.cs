using BangDreamLib.Scripts.Extensions;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Utils;

public static class CrychicConst
{
    public const string ModId = "ItsCrychic";

    public static readonly Func<CardModel, CardAssetProfile> DefaultCardAssetProfile = cardModel => new CardAssetProfile(
        PortraitPath: cardModel.GetType().Name.GetCardImg(ModId),
        BetaPortraitPath: cardModel.GetType().Name.GetCardBateImg(ModId)
    );
    
    public static readonly Func<RelicModel, RelicAssetProfile> DefaultRelicAssetProfile = cardModel => new RelicAssetProfile(
        IconPath: cardModel.GetType().Name.GetRelicImg(ModId),
        BigIconPath: cardModel.GetType().Name.GetBigRelicImg(ModId),
        IconOutlinePath: cardModel.GetType().Name.GetBigRelicImg(ModId)
    );
}