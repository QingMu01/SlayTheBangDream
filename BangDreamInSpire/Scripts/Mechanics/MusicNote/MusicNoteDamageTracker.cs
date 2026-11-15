using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace BangDreamInSpire.Scripts.Mechanics.MusicNote;

[RegisterSingleton]
public class MusicNoteDamageTracker : HookedSingletonModel, IMusicNoteTracker
{
    public MusicNoteDamageTracker() : base(HookType.Combat)
    {
    }


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

    public IReadOnlyList<DamageResult> GetAllDamageResults()
    {
        return CombatHistory.Values.SelectMany(result => result).ToList();
    }


    public IReadOnlyList<DamageResult> GetTurnDamageResults(int turn)
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