using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Builder;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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

    public static async Task FromCard(PlayerChoiceContext choiceContext, CardModel card, int baseCount,
        decimal baseDamage = 1m, Creature? target = null)
    {
        ArgumentNullException.ThrowIfNull(card.RunState);
        ArgumentNullException.ThrowIfNull(card.CombatState);
        ArgumentNullException.ThrowIfNull(card.Owner.Creature);

        var vfxPath = GetMusicNoteVfxPath(card);

        var finalCount = BangDreamHook.ModifyMusicNoteShotCount(card.CombatState, card.Owner.Creature, baseCount, card);

        await new VfxBuilder<MusicNoteVfx>(vfxPath)
            .RepeatCount((int)finalCount)
            .SetOnSpawn((vfx, context) =>
            {
                var attackTarget = target ??
                                   card.Owner.RunState.Rng.CombatTargets.NextItem(card.CombatState.HittableEnemies);
                var creatureSrc = card.Owner.Creature.GetCreatureNode();
                var creatureTarget = attackTarget?.GetCreatureNode();

                if (creatureTarget != null && creatureSrc != null)
                {
                    context.Set("Target", attackTarget);
                    vfx.SetPath(creatureSrc.VfxSpawnPosition, creatureTarget.VfxSpawnPosition);
                }
                else
                {
                    vfx.QueueFree();
                    BangDreamLibCore.Logger.Warn(
                        $"Can't init MusicNoteVfx Move Path.(source: {creatureSrc}, target: {creatureTarget})");
                }

                return Task.CompletedTask;
            })
            .SetOnBeforeHit((_, context) =>
            {
                var attackTarget = context.Get<Creature>("Target");
                context.Set("Damage", BangDreamHook.ModifyMusicNoteDamage(card.CombatState,
                    attackTarget, card.Owner.Creature, baseDamage, card, ModifyDamageHookType.All));
                return Task.CompletedTask;
            })
            .SetOnHit(async (vfx, context) =>
            {
                var damage = context.Get<decimal>("Damage");
                var attackTarget = context.Get<Creature>("Target");
                if (attackTarget is { IsHittable: true })
                {
                    var results = await CreatureCmd.Damage(choiceContext, attackTarget,
                        new DamageVar(damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim), card);
                    context.Set("Results", results.ToList());
                }
                else
                {
                    vfx.QueueFree();
                }
            }).SetOnAfterHit((_, context) =>
            {
                var results = context.Get<List<DamageResult>>("Results");
                if (results != null)
                {
                    var damageTracker = card.Owner.AttachedData().MusicNoteDamageTracker;
                    foreach (var damageResult in results)
                    {
                        damageTracker.AddMusicNoteDamage(card.CombatState.RoundNumber, damageResult);
                    }
                }

                return Task.CompletedTask;
            })
            .SetOnFinish(async (_, _) =>
            {
                if (CombatManager.Instance.IsInProgress)
                {
                    var checkWinCondition = await CombatManager.Instance.CheckWinCondition();
                    if (checkWinCondition)
                    {
                        return;
                    }

                    if (card.CombatState != null)
                    {
                        await BangDreamHook.OnMusicNotePlayed(card.CombatState, card.Owner);
                    }
                }
            })
            .Emit(NoteSpawnInterval, NoteGroupSize, NoteGroupDelay);
    }

    private static string GetMusicNoteVfxPath(CardModel card)
    {
        return card.Owner.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVfx.MusicNote ?? DefaultPath;
    }
}