using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
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
    public override CardAssetProfile AssetProfile => base.AssetProfile with { FramePath = GetFreamPath(Type) };

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