using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Extensions;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Saki;

[RegisterCard(typeof(SakikoStandardCardPool), Inherit = true)]
public abstract class AbstractSakikoCard(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : BandCardModel(baseCost, type, rarity, target, showInCardLibrary)
{
    protected override CardAssetProfile CardAssetProfile =>
        new(
            PortraitPath: GetType().Name.GetCardImg(CrychicConst.ModId),
            BetaPortraitPath: GetType().Name.GetCardBateImg(CrychicConst.ModId),
            FramePath: GetFreamPath(Type)
        );

    private static string GetFreamPath(CardType type)
    {
        return type switch
        {
            CardType.Attack => "res://ItsCrychic/images/charui/sakiko/sakiko_attack_fream.png",
            CardType.Power => "res://ItsCrychic/images/charui/sakiko/sakiko_power_fream.png",
            _ => "res://ItsCrychic/images/charui/sakiko/sakiko_skill_fream.png"
        };
    }
}