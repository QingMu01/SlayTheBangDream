using System.Collections.Generic;
using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Mechanics.ExtraDeck;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamInSpire.Scripts.Extensions;

/// <summary>
/// 额外卡组操作的稳定扩展入口。内容模组通过本类抽额外牌，不直接依赖 <c>ExtraPileCmd</c>。
/// </summary>
public static class ExtraDeckExtensions
{
    /// <summary>从额外抽牌堆抽牌。</summary>
    public static Task<IEnumerable<CardModel>> DrawExtraCards(this Player player,
        PlayerChoiceContext choiceContext, decimal count, bool fromHandDraw = false)
        => ExtraPileCmd.Draw(choiceContext, count, player, fromHandDraw);
}
