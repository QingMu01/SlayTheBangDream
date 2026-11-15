using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BangDreamInSpire.Scripts.Interfaces.Mechanics;

/// <summary>
/// 音符伤害统计的只读契约。内容模组通过它读取本场战斗的音符伤害记录。
/// </summary>
public interface IMusicNoteTracker
{
    /// <summary>本场战斗的全部音符伤害记录。</summary>
    IReadOnlyList<DamageResult> GetAllDamageResults();

    /// <summary>指定回合的音符伤害记录。</summary>
    IReadOnlyList<DamageResult> GetTurnDamageResults(int turn);
}
