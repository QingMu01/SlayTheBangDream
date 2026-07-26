using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Commands;

public static class MusicNoteCmd
{
    private const string DefaultPath = "res://ItsCrychic/scenes/vfx/flying_music_note_default.tscn";

    public static Task FromCard(CardModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        Submit(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target, source);
        return Task.CompletedTask;
    }

    public static Task FromPower(PowerModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        Submit(source.Owner, baseCount, bounceCount, baseDamage, null, target, source);
        return Task.CompletedTask;
    }

    public static Task FromRelic(RelicModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        Submit(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target, source);
        return Task.CompletedTask;
    }

    internal static void Submit(Creature dealer, int count, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? visualDealer = null, Creature? target = null, AbstractModel? source = null)
    {
        ArgumentNullException.ThrowIfNull(dealer);
        ArgumentNullException.ThrowIfNull(dealer.CombatState);
        ArgumentNullException.ThrowIfNull(dealer.Player?.RunState);

        var shot = (int)BangDreamHook.ModifyMusicNoteShotCount(dealer.CombatState, dealer, count, source);
        var bounce = (int)BangDreamHook.ModifyMusicNoteBounceCount(dealer.CombatState, dealer, bounceCount, source);
        var capturedDamageAdditive = shot > 0
            ? BangDreamHook.CaptureMusicNoteDamageAdditive(dealer.CombatState, dealer, source)
            : 0m;
        var request = new MusicNoteVolleyRequest(
            dealer,
            Math.Max(0, shot),
            Math.Max(0, bounce),
            baseDamage,
            capturedDamageAdditive,
            GetMusicNoteVfxPath(dealer.Player),
            visualDealer,
            target,
            source);
        TaskHelper.RunSafely(new MusicNoteVolleyRunner(request).RunAsync());
    }

    private static string GetMusicNoteVfxPath(Player player)
    {
        return BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVfx.MusicNote ??
               DefaultPath;
    }
}
