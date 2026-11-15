using System.Threading.Tasks;
using BangDreamInSpire.Scripts.Mechanics.MusicNote;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Extensions;

/// <summary>
/// 音符发射的稳定扩展入口。内容模组通过本类调用机制，不直接依赖 <c>MusicNoteCmd</c>。
/// </summary>
public static class MusicNoteExtensions
{
    /// <summary>从卡牌发射音符。</summary>
    public static Task ShootMusicNotes(this CardModel source, int baseCount,
        int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
        => MusicNoteCmd.FromCard(source, baseCount, bounceCount, baseDamage, target, shouldImmediate);

    /// <summary>从能力（Power）发射音符。</summary>
    public static Task ShootMusicNotes(this PowerModel source, int baseCount,
        int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
        => MusicNoteCmd.FromPower(source, baseCount, bounceCount, baseDamage, target, shouldImmediate);

    /// <summary>从生物直接发射音符（无卡牌/能力来源）。</summary>
    public static Task ShootMusicNotes(this Creature dealer, int baseCount,
        int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null, bool shouldImmediate = false)
        => MusicNoteCmd.CustomShot(dealer, baseCount, bounceCount, baseDamage, target, shouldImmediate);
}
