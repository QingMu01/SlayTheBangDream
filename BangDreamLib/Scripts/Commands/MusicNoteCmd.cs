using BangDreamLib.Scripts.Features.AsyncDamage;
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

    public static Task CustomShot(Creature dealer, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        Submit(dealer, baseCount, bounceCount, baseDamage, null, target);
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
        var effect = new MusicNoteAsyncDamageEffect(
            GetMusicNoteVfxPath(dealer.Player),
            baseDamage,
            capturedDamageAdditive);
        var request = new AsyncDamageBatchRequest
        {
            Dealer = dealer,
            Count = Math.Max(0, shot),
            Effect = effect,
            ChainCount = Math.Max(0, bounce),
            InitialVisualSource = visualDealer,
            FixedTarget = target,
            Source = source,
            TargetPolicy = AsyncDamageTargetPolicy.AvoidReservedLethal
        };
        TaskHelper.RunSafely(CombatAsyncDamageManager.Shared.SubmitAsync(request));
    }

    private static string GetMusicNoteVfxPath(Player player)
    {
        return BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVfx.MusicNote ??
               DefaultPath;
    }
}