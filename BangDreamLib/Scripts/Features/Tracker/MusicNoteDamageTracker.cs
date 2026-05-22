using BangDreamLib.Scripts.Attributes;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace BangDreamLib.Scripts.Features.Tracker;

[BangDreamIgnore]
public class MusicNoteDamageTracker : SingletonModel
{
    public override bool ShouldReceiveCombatHooks => true;

    private readonly Dictionary<int, List<DamageResult>> _combatHistory = new();

    public void AddMusicNoteDamage(int turn, DamageResult damageResult)
    {
        AssertMutable();
        if (_combatHistory.TryGetValue(turn, out var damageCounter))
        {
            damageCounter.Add(damageResult);
        }
        else
        {
            _combatHistory.Add(turn, [damageResult]);
        }
    }

    public List<DamageResult> GetAllDamageResults()
    {
        return _combatHistory.Values.SelectMany(result => result).ToList();
    }


    public List<DamageResult> GetTurnDamageResults(int turn)
    {
        return _combatHistory.TryGetValue(turn, out var damageCounter)
            ? damageCounter
            : [];
    }

    public decimal GetTurnDamagedTotal(int turn)
    {
        return _combatHistory.TryGetValue(turn, out var damageCounter)
            ? damageCounter.Sum(musicNoteDamagedInfo => musicNoteDamagedInfo.TotalDamage)
            : 0m;
    }

    public int HistoryLength => _combatHistory.Count;

    public override Task AfterCombatEnd(CombatRoom room)
    {
        AssertMutable();
        _combatHistory.Clear();
        return base.AfterCombatEnd(room);
    }

    public override Task BeforeCombatStart()
    {
        AssertMutable();
        _combatHistory.Clear();
        return base.BeforeCombatStart();
    }
}