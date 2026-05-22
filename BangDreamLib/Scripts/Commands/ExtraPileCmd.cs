using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Commands;

public static class ExtraPileCmd
{
    public static async Task<IEnumerable<CardModel>> Draw(PlayerChoiceContext choiceContext,
        decimal count,
        Player player,
        bool fromHandDraw = false)
    {
        if (CombatManager.Instance.IsOverOrEnding || player.Creature.CombatState == null)
            return [];
        if (!Hook.ShouldDraw(player.Creature.CombatState, player, fromHandDraw, out var modifier))
        {
            if (modifier != null)
            {
                await Hook.AfterPreventingDraw(player.Creature.CombatState, modifier);
            }

            return [];
        }

        var combatState = player.Creature.CombatState;
        var result = new List<CardModel>();
        var hand = PileType.Hand.GetPile(player);
        var drawPile = BangDreamConst.PileExtraDraw.GetPile(player);
        var drawsRequested = count > 0M ? (int)Math.Ceiling(count) : 0;
        if (drawsRequested == 0)
            return result;
        var num = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
        if (num == 0)
        {
            CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(player);
            return result;
        }

        for (var i = 0;
             i < drawsRequested && num > 0 && CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(player);
             ++i)
        {
            await CardPileCmd.ShuffleIfNecessary(choiceContext, player);
            if (CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(player))
            {
                var card = drawPile.Cards.ToList().FirstOrDefault();
                if (card != null && hand.Cards.Count < CardPile.MaxCardsInHand)
                {
                    result.Add(card);
                    await CardPileCmd.Add(card, hand);
                    CombatManager.Instance.History.CardDrawn(combatState, card, fromHandDraw);
                    await Hook.AfterCardDrawn(combatState, choiceContext, card, fromHandDraw);
                    card.InvokeDrawn();
                    NDebugAudioManager.Instance?.Play("card_deal.mp3", 0.25f, PitchVariance.Small);
                    num = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
                }
                else
                    break;
            }
            else
                break;
        }

        return result;
    }

    private static bool CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(Player player)
    {
        if (BangDreamConst.PileExtraDraw.GetPile(player).Cards.Count == 0)
        {
            ThinkCmd.Play(new LocString("combat_messages", "NO_DRAW"), player.Creature, 2.0);
            return false;
        }

        if (PileType.Hand.GetPile(player).Cards.Count < CardPile.MaxCardsInHand)
            return true;
        ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), player.Creature, 2.0);
        return false;
    }
}