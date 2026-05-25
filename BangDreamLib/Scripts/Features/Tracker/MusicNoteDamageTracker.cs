using BangDreamLib.Scripts.Attributes;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace BangDreamLib.Scripts.Features.Tracker;

[BangDreamIgnore]
public class MusicNoteDamageTracker : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public readonly Dictionary<int, List<DamageResult>> CombatHistory = new();

    public void AddMusicNoteDamage(int turn, DamageResult damageResult)
    {
        AssertMutable();
        if (CombatHistory.TryGetValue(turn, out var damageResults))
        {
            damageResults.Add(damageResult);
        }
        else
        {
            CombatHistory.Add(turn, [damageResult]);
        }
    }

    public List<DamageResult> GetAllDamageResults()
    {
        return CombatHistory.Values.SelectMany(result => result).ToList();
    }


    public List<DamageResult> GetTurnDamageResults(int turn)
    {
        return CombatHistory.TryGetValue(turn, out var damageResults)
            ? damageResults
            : [];
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        AssertMutable();
        CombatHistory.Clear();
        return base.AfterCombatEnd(room);
    }

    public override Task BeforeCombatStart()
    {
        AssertMutable();
        CombatHistory.Clear();
        return base.BeforeCombatStart();
    }
}