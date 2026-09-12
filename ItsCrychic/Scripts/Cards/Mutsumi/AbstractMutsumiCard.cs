using BangDreamLib.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Mutsumi;

[RegisterCard(typeof(MutsumiMusicalCardPool), Inherit = true)]
public abstract class AbstractMutsumiCard(
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
            CardType.Attack => "res://ItsCrychic/images/charui/mutsumi/mutsumi_attack_fream.png",
            CardType.Power => "res://ItsCrychic/images/charui/mutsumi/mutsumi_power_fream.png",
            _ => "res://ItsCrychic/images/charui/mutsumi/mutsumi_skill_fream.png"
        };
    }
}