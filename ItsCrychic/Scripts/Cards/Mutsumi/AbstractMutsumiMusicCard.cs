using BangDreamInSpire.Scripts.Cards;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Mutsumi;

// 预留占位：睦目前没有音乐卡，本基类与 MutsumiMusicalCardPool 暂无具体使用方。
// 若确定不再需要，按 docs/cleanup-authoring.md 走 obsolete-file-cleanup 流程签字后删除，勿直接移除。
[RegisterCard(typeof(MutsumiMusicalCardPool), Inherit = true)]
public abstract class AbstractMutsumiMusicCard(int baseCost, CardRarity rarity, TargetType target)
    : MusicCardModel(baseCost, rarity, target)
{
    protected AbstractMutsumiMusicCard(CardRarity rarity, TargetType target) : this(0, rarity, target)
    {
    }
}