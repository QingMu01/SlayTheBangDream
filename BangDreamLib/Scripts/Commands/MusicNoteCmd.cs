using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Builder;
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

    public static async Task FromCard(PlayerChoiceContext choiceContext, CardModel card, int baseCount,
        decimal baseDamage = 1m, Creature? target = null)
    {
        ArgumentNullException.ThrowIfNull(card.RunState);
        ArgumentNullException.ThrowIfNull(card.CombatState);
        ArgumentNullException.ThrowIfNull(card.Owner.Creature);

        var vfxPath = GetMusicNoteVfxPath(card);

        var finalCount = BangDreamHook.ModifyMusicNoteShotCount(card.CombatState, card.Owner.Creature, baseCount, card);

        await new VfxBuilder<MusicNoteVfx>(vfxPath)
            .RepeatCount(finalCount)
            .SetOnSpawn(vfx =>
            {
                var attackTarget = target ??
                                   card.Owner.RunState.Rng.CombatTargets.NextItem(card.CombatState.HittableEnemies);
                var creatureSrc = card.Owner.Creature.GetCreatureNode();
                var creatureTarget = attackTarget?.GetCreatureNode();

                if (creatureTarget != null && creatureSrc != null)
                {
                    vfx.VfxContext.Set("Target", attackTarget);
                    vfx.SetPath(creatureSrc.VfxSpawnPosition, creatureTarget.VfxSpawnPosition);
                }
                else
                {
                    BangDreamLibCore.Logger.Warn(
                        $"Can't init MusicNoteVfx Move Path.(source: {creatureSrc}, target: {creatureTarget})");
                }

                return Task.CompletedTask;
            })
            .SetOnBeforeHit(vfx =>
            {
                var attackTarget = vfx.VfxContext.Get<Creature>("Target");
                vfx.VfxContext.Set("Damage", BangDreamHook.ModifyMusicNoteDamage(card.RunState, card.CombatState,
                    attackTarget, card.Owner.Creature, baseDamage, card, ModifyDamageHookType.All, out _));
                return Task.CompletedTask;
            })
            .SetOnHit(async vfx =>
            {
                var damage = vfx.VfxContext.Get<decimal>("Damage");
                var attackTarget = vfx.VfxContext.Get<Creature>("Target");
                if (attackTarget != null)
                {
                    var results = await CreatureCmd.Damage(choiceContext, attackTarget,
                        new DamageVar(damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim), card);
                    vfx.VfxContext.Set("Results", results.ToList());
                }
                else
                {
                    BangDreamLibCore.Logger.Warn($"Vfx {vfx} can't find attack target");
                }
            }).SetOnAfterHit(vfx =>
            {
                var results = vfx.VfxContext.Get<List<DamageResult>>("Results");
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
            .SetOnFinish(async _ =>
            {
                if (card.CombatState != null)
                {
                    await BangDreamHook.OnMusicNotePlayed(card.CombatState, card.Owner);
                }
            })
            .Emit(0.1f, 8, 0.25f);
    }

    private static string GetMusicNoteVfxPath(CardModel card)
    {
        return card.Owner.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVfx.MusicNote ?? DefaultPath;
    }
}