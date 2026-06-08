using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Builder;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

    public static async Task FromCard(CardModel card, int baseCount,
        decimal baseDamage = 1m, Creature? target = null, Action<MusicNoteFlyingVfx>? beforeInstance = null)
    {
        ArgumentNullException.ThrowIfNull(card.RunState);
        ArgumentNullException.ThrowIfNull(card.CombatState);
        ArgumentNullException.ThrowIfNull(card.Owner.Creature);

        var vfxPath = GetMusicNoteVfxPath(card);

        var finalCount = BangDreamHook.ModifyMusicNoteShotCount(card.CombatState, card.Owner.Creature, baseCount, card);

        var noteVfxes = new VfxCreator<MusicNoteFlyingVfx>(vfxPath).CreateBatch((int)finalCount);

        var vfxHandlerPairs = noteVfxes.Select(vfx =>
        {
            var handler = new MusicNoteEffectHandler(card, baseDamage, target);
            return (vfx, handler);
        }).ToList();

        await SubmitNote(vfxHandlerPairs, NoteSpawnInterval, NoteGroupSize, NoteGroupDelay, beforeInstance);
    }

    private static async Task SubmitNote(IEnumerable<(MusicNoteFlyingVfx vfx, MusicNoteEffectHandler handler)> pairs,
        float eachDelay = 0.0f, int groupCount = 0, float groupDelay = 0.0f,
        Action<MusicNoteFlyingVfx>? beforeInstance = null)
    {
        if (groupCount < 0)
        {
            groupCount = 0;
        }

        var currentIndex = 0;
        foreach (var (vfx, handler) in pairs)
        {
            beforeInstance?.Invoke(vfx);

            BangDreamVfxManager.Instance?.SubmitVfx(vfx, handler);

            if (groupCount > 0)
            {
                var isNewGroup = currentIndex > 0 && currentIndex % groupCount == 0;
                await Cmd.Wait(isNewGroup ? groupDelay : eachDelay);
            }
            else
            {
                await Cmd.Wait(eachDelay);
            }

            currentIndex++;
        }
    }

    private static string GetMusicNoteVfxPath(CardModel card)
    {
        return card.Owner.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVfx.MusicNote ?? DefaultPath;
    }
}

internal class MusicNoteEffectHandler(
    CardModel card,
    decimal baseDamage,
    Creature? target)
    : IVfxEffectHandler
{
    public Task OnSpawn(VfxContext context)
    {
        ArgumentNullException.ThrowIfNull(card.RunState);
        ArgumentNullException.ThrowIfNull(card.CombatState);
        ArgumentNullException.ThrowIfNull(card.Owner.Creature);

        var attackTarget = target ?? card.Owner.RunState.Rng.CombatTargets.NextItem(card.CombatState.HittableEnemies);

        var creatureSrc = card.Owner.Creature.GetCreatureNode();
        var creatureTarget = attackTarget?.GetCreatureNode();

        if (creatureTarget != null && creatureSrc != null)
        {
            context.Set("Target", attackTarget);
            ((MusicNoteFlyingVfx)context.VfxNode!).SetPath(creatureSrc.VfxSpawnPosition, creatureTarget.VfxSpawnPosition);
        }
        else
        {
            context.VfxNode?.QueueFreeSafely();
            BangDreamLibCore.Logger.Warn(
                $"Can't init MusicNoteVfx Move Path.(source: {creatureSrc}, target: {creatureTarget})");
        }

        return Task.CompletedTask;
    }

    public async Task OnHit(VfxContext context)
    {
        var attackTarget = context.Get<Creature>("Target");

        if (attackTarget is { IsHittable: true } && card.CombatState != null)
        {
            var damage = BangDreamHook.ModifyMusicNoteDamage(
                card.CombatState,
                attackTarget,
                card.Owner.Creature,
                baseDamage,
                card,
                ModifyDamageHookType.All
            );

            var results = await CreatureCmd.Damage(
                new BlockingPlayerChoiceContext(),
                attackTarget,
                new DamageVar(damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim),
                card
            );

            var damageTracker = card.Owner.AttachedData().MusicNoteDamageTracker;
            foreach (var damageResult in results)
            {
                damageTracker.AddMusicNoteDamage(card.CombatState.RoundNumber, damageResult);
            }
        }
        else
        {
            context.VfxNode?.QueueFreeSafely();
        }
    }

    public async Task OnFinish(VfxContext context)
    {
        if (card.CombatState != null)
        {
            await BangDreamHook.OnMusicNotePlayed(card.CombatState, card.Owner);
        }

        var index = context.Get<int?>("index");
        var total = context.Get<int?>("total");
        if (index.HasValue && total.HasValue && index.Value == total.Value - 1)
        {
            if (context.VfxNode != null) context.VfxNode.UpdateCombatTracker = true;
        }

        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }
}