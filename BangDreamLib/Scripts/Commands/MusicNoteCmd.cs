using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace BangDreamLib.Scripts.Commands;

public static class MusicNoteCmd
{
    public static Task DebugPlayVfx(Player source, MonsterModel? target, int shotCount = 1)
    {
        var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        var creatureSrc = NCombatRoom.Instance?.GetCreatureNode(source.Creature);
        var creatureTarget = NCombatRoom.Instance?.GetCreatureNode(target?.Creature);
        if (creatureSrc != null)
        {
            for (var i = 0; i < shotCount; i++)
            {
                var targetVec = creatureTarget?.VfxSpawnPosition ?? new Vector2(1000, creatureSrc.VfxSpawnPosition.Y);
                var node = MusicNoteVfx.Create(creatureSrc.VfxSpawnPosition, targetVec);
                if (vfxContainer != null)
                    vfxContainer.AddChildSafely(node);
                else
                    NRun.Instance?.GlobalUi.AddChildSafely(node);
            }
        }

        return Task.CompletedTask;
    }

    public static async Task FromCard(PlayerChoiceContext choiceContext, CardModel card, int baseCount,
        decimal baseDamage = 1m, Creature? target = null)
    {
        ArgumentNullException.ThrowIfNull(card.RunState);
        ArgumentNullException.ThrowIfNull(card.CombatState);
        ArgumentNullException.ThrowIfNull(card.Owner.Creature);

        var creatureSrc = NCombatRoom.Instance?.GetCreatureNode(card.Owner.Creature)!;
        var vfxPath = card.Owner.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVfx.MusicNote;
        var finalCount = BangDreamHook.ModifyMusicNoteShotCount(card.CombatState, card.Owner.Creature, baseCount, card);

        for (var i = 0; i < finalCount; i++)
        {
            var attackTarget =
                target ?? card.Owner.RunState.Rng.CombatTargets.NextItem(card.CombatState.HittableEnemies);
            var finalDamage = BangDreamHook.ModifyMusicNoteDamage(card.RunState, card.CombatState,
                attackTarget, card.Owner.Creature, 1m, card, ModifyDamageHookType.All, out _);
            var creatureTarget = attackTarget?.GetCreatureNode();
            if (attackTarget != null && creatureTarget != null)
            {
                Shoot(MusicNoteVfx.Create(creatureSrc.VfxSpawnPosition, creatureTarget.VfxSpawnPosition, vfxPath,
                    onReachedEndAsync: async _ =>
                    {
                        var damageTracker = card.Owner.AttachedData().MusicNoteDamageTracker;
                        var damageResults = await CreatureCmd.Damage(choiceContext, attackTarget,
                            new DamageVar(finalDamage, ValueProp.Unpowered | ValueProp.SkipHurtAnim), card);
                        foreach (var damageResult in damageResults)
                        {
                            damageTracker.AddMusicNoteDamage(card.CombatState.RoundNumber, damageResult);
                        }
                    }));
            }
            else
            {
                return;
            }

            await BangDreamHook.OnMusicNotePlayed(card.CombatState, card.Owner);
            await Task.Delay(Rng.Chaotic.NextInt(50, 100));
            if (i % 5 == 0)
            {
                await Task.Delay(100);
            }
        }
    }

    private static void Shoot(MusicNoteVfx node)
    {
        var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        if (vfxContainer != null)
            vfxContainer.AddChildSafely(node);
        else
            NRun.Instance?.GlobalUi.AddChildSafely(node);
    }
}