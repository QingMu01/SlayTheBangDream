using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.GameHook;

/// <summary>破绽与弱点击破广播。</summary>
public interface IWeaknessBreakHookListener
{
    /// <summary>实际获得破绽后广播。</summary>
    Task AfterWeaknessBreakDiscovered(PlayerChoiceContext? choiceContext, Creature target, int amount,
        AbstractModel? source)
    {
        return Task.CompletedTask;
    }

    /// <summary>进入弱点击破时广播。</summary>
    Task AfterWeakPointBroken(PlayerChoiceContext? choiceContext, Creature target, AbstractModel? source)
    {
        return Task.CompletedTask;
    }

    /// <summary>击破效果结算完毕后广播。</summary>
    Task AfterWeakPointBreakTriggered(PlayerChoiceContext? choiceContext, Creature target, CardPlay play)
    {
        return Task.CompletedTask;
    }
}
