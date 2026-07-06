using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Builder;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Commands;

public static class MusicNoteCmd
{
    private const string DefaultPath = "res://ItsCrychic/scenes/vfx/flying_music_note_default.tscn";

    private const float NoteSpawnInterval = 0.1f;
    private const int NoteGroupSize = 8;
    private const float NoteGroupDelay = 0.25f;

    public static async Task FromCard(CardModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        await ShotInternal(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target);
    }

    public static async Task FromPower(PowerModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        await ShotInternal(source.Owner, baseCount, bounceCount, baseDamage, null, target);
    }

    public static async Task FromRelic(RelicModel source, int baseCount, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? target = null)
    {
        await ShotInternal(source.Owner.Creature, baseCount, bounceCount, baseDamage, null, target);
    }

    internal static async Task ShotInternal(Creature dealer, int count, int bounceCount = 0, decimal baseDamage = 1m,
        Creature? visualDealer = null, Creature? target = null)
    {
        await SubmitNote(
            GenerateNoteInternal(dealer, count, bounceCount, baseDamage, visualDealer, target),
            NoteSpawnInterval, NoteGroupSize, NoteGroupDelay
        );
    }

    private static IEnumerable<(MusicNoteFlyingVfx, MusicNoteEffectHandler)> GenerateNoteInternal(
        Creature dealer, int count, int bounceCount, decimal baseDamage, Creature? target, Creature? visualDealer,
        AbstractModel? source = null)
    {
        ArgumentNullException.ThrowIfNull(dealer);
        ArgumentNullException.ThrowIfNull(dealer.CombatState);
        ArgumentNullException.ThrowIfNull(dealer.Player?.RunState);

        var vfxPath = GetMusicNoteVfxPath(dealer.Player);

        var shot = (int)BangDreamHook.ModifyMusicNoteShotCount(dealer.CombatState, dealer, count, source);
        var bounce = (int)BangDreamHook.ModifyMusicNoteBounceCount(dealer.CombatState, dealer, bounceCount, source);

        var batch = new VfxCreator<MusicNoteFlyingVfx>(vfxPath).CreateBatch(shot);

        foreach (var vfx in batch)
            yield return (vfx, new MusicNoteEffectHandler(dealer, bounce, baseDamage, visualDealer, target, source));
    }

    private static async Task SubmitNote(IEnumerable<(MusicNoteFlyingVfx, MusicNoteEffectHandler)> pairs,
        float eachDelay = 0.0f, int groupCount = 0, float groupDelay = 0.0f)
    {
        if (groupCount < 0)
        {
            groupCount = 0;
        }

        var currentIndex = 0;
        foreach (var (vfx, handler) in pairs)
        {
            BangDreamVfxManager.Instance?.SubmitVfx(vfx, handler);

            if (groupCount > 0)
            {
                var isNewGroup = currentIndex > 0 && currentIndex % groupCount == 0;
                if (isNewGroup && (handler.VisualDealer == null || handler.VisualDealer == handler.Dealer))
                {
                    await CreatureCmd.TriggerAnim(handler.Dealer, "Cast", 0);
                    await Cmd.Wait(groupDelay);
                }
                else
                {
                    await Cmd.Wait(eachDelay);
                }
            }
            else
            {
                await Cmd.Wait(eachDelay);
            }

            currentIndex++;
        }
    }

    private static string GetMusicNoteVfxPath(Player player)
    {
        return BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVfx.MusicNote ??
               DefaultPath;
    }
}

internal record MusicNoteEffectHandler(
    Creature Dealer,
    int BounceCount,
    decimal BaseDamage,
    Creature? VisualDealer,
    Creature? Target,
    AbstractModel? Source = null
) : IVfxEffectHandler
{
    public async Task OnSpawn(VfxContext context)
    {
        ArgumentNullException.ThrowIfNull(Dealer);
        ArgumentNullException.ThrowIfNull(Dealer.Player?.RunState);
        ArgumentNullException.ThrowIfNull(Dealer.CombatState);

        context.Set("IsPrototype", VisualDealer == null);

        var attackTarget =
            Target ?? Dealer.Player.RunState.Rng.CombatTargets.NextItem(Dealer.CombatState.HittableEnemies);

        var creatureSrc = VisualDealer?.GetCreatureNode() ?? Dealer.GetCreatureNode();
        var creatureTarget = attackTarget?.GetCreatureNode();

        if (creatureTarget != null && creatureSrc != null)
        {
            context.Set("Target", attackTarget);
            ((MusicNoteFlyingVfx)context.VfxNode!).SetPath(creatureSrc.VfxSpawnPosition,
                creatureTarget.VfxSpawnPosition);
            await BangDreamHook.OnMusicNoteSpawn(Dealer.CombatState, context, Dealer.Player);
        }
        else
        {
            context.VfxNode?.QueueFreeSafely();
            BangDreamLibCore.Logger.Warn(
                $"Can't init MusicNoteVfx Move Path.(source: {creatureSrc}, target: {creatureTarget})");
        }
    }

    public async Task OnHit(VfxContext context)
    {
        var attackTarget = context.Get<Creature>("Target");

        if (attackTarget is { IsHittable: true } && Dealer.CombatState != null)
        {
            var damage = BangDreamHook.ModifyMusicNoteDamage(
                Dealer.CombatState,
                attackTarget,
                Dealer,
                BaseDamage,
                Source,
                ModifyDamageHookType.All
            );

            var results = await CreatureCmd.Damage(
                new BlockingPlayerChoiceContext(),
                attackTarget,
                new DamageVar(damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim),
                Dealer
            );

            var damageTracker = Dealer.Player?.AttachedData().MusicNoteDamageTracker;
            if (damageTracker != null)
            {
                foreach (var damageResult in results)
                {
                    damageTracker.AddMusicNoteDamage(Dealer.CombatState.RoundNumber, damageResult);
                }
            }
        }
        else
        {
            context.VfxNode?.QueueFreeSafely();
        }
    }

    public async Task OnFinish(VfxContext context)
    {
        var index = context.Get<int?>("index");
        var total = context.Get<int?>("total");
        if (index.HasValue && total.HasValue && index.Value == total.Value - 1)
        {
            if (context.VfxNode != null) context.VfxNode.UpdateCombatTracker = true;
        }

        if (CombatManager.Instance.IsInProgress)
        {
            if (!await CombatManager.Instance.CheckWinCondition() && BounceCount > 0)
            {
                var oldTarget = context.Get<Creature>("Target");
                var hittableEnemies = Dealer.CombatState?.HittableEnemies.Where(item => item != oldTarget);
                if (hittableEnemies != null)
                {
                    var newTarget = Dealer.Player?.RunState.Rng.CombatTargets.NextItem(hittableEnemies);
                    if (newTarget != null)
                    {
                        var bounceCount = BounceCount - 1;
                        await MusicNoteCmd.ShotInternal(Dealer, 1, bounceCount, BaseDamage, oldTarget, newTarget);
                    }
                }
            }
        }
    }
}