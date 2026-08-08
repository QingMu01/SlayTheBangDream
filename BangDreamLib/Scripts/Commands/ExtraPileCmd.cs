using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace BangDreamLib.Scripts.Commands;

public static class ExtraPileCmd
{
    public static async Task<IEnumerable<CardModel>> FromExtraDeckForRemoval(Player player,
        CardSelectorPrefs prefs)
    {
        var cards = BangDreamConst.ExtraDeck.GetPile(player).Cards
            .Where(card => card.IsRemovable)
            .ToList();
        if (cards.Count == 0)
        {
            return [];
        }

        if (!prefs.RequireManualConfirmation && cards.Count <= prefs.MinSelect)
        {
            return cards;
        }

        if (CardSelectCmd.Selector != null)
        {
            return await CardSelectCmd.Selector.GetSelectedCards(cards, prefs.MinSelect, prefs.MaxSelect);
        }

        var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player);
        IEnumerable<CardModel> selected;
        if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay)
        {
            if (CardSelectCmd.LocalSelector != null)
            {
                selected = await CardSelectCmd.LocalSelector.GetSelectedCards(cards, prefs.MinSelect, prefs.MaxSelect);
            }
            else
            {
                var screen = NDeckCardSelectScreen.Create(cards, prefs);
                NOverlayStack.Instance!.Push(screen);
                selected = await screen.CardsSelected();
                RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                    player, choiceId,
                    PlayerChoiceResult.FromIndexes(selected.Select(card => cards.IndexOf(card)).ToList()));
            }
        }
        else
        {
            selected = (await RunManager.Instance.PlayerChoiceSynchronizer.WaitForRemoteChoice(player, choiceId))
                .AsIndexes()
                .Where(index => index >= 0 && index < cards.Count)
                .Select(index => cards[index])
                .ToList();
        }

        return selected;
    }

    public static async Task RemoveFromExtraDeck(CardModel card)
    {
        if (card.Pile?.Type != BangDreamConst.ExtraDeck)
        {
            throw new InvalidOperationException("You cannot remove a card that is not in the extra deck.");
        }

        await Hook.BeforeCardRemoved(card.Owner.RunState, card);
        card.RemoveFromState();
    }

    public static async Task<IEnumerable<CardModel>> FromExtraDeckForUpgrade(Player player, CardSelectorPrefs prefs)
    {
        var upgradableCards = BangDreamConst.ExtraDeck.GetPile(player).Cards
            .Where(card => card.IsUpgradable)
            .ToList();
        if (upgradableCards.Count == 0)
            return [];

        IEnumerable<CardModel> selectedCards;
        if (upgradableCards.Count <= prefs.MinSelect && !prefs.RequireManualConfirmation)
        {
            selectedCards = upgradableCards;
        }
        else if (CardSelectCmd.Selector != null)
        {
            selectedCards = await CardSelectCmd.Selector.GetSelectedCards(
                upgradableCards, prefs.MinSelect, prefs.MaxSelect);
        }
        else
        {
            var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(player);
            if (ShouldSelectLocalCard(player))
            {
                if (CardSelectCmd.LocalSelector != null)
                {
                    selectedCards = await CardSelectCmd.LocalSelector.GetSelectedCards(
                        upgradableCards, prefs.MinSelect, prefs.MaxSelect);
                }
                else
                {
                    var localSelection = (await NDeckUpgradeSelectScreen
                        .ShowScreen(upgradableCards, prefs, player.RunState)
                        .CardsSelected()).ToList();
                    selectedCards = localSelection;
                    RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                        player,
                        choiceId,
                        PlayerChoiceResult.FromIndexes(localSelection
                            .Select(card => upgradableCards.IndexOf(card))
                            .ToList()));
                }
            }
            else
            {
                selectedCards = (await RunManager.Instance.PlayerChoiceSynchronizer
                        .WaitForRemoteChoice(player, choiceId))
                    .AsIndexes()
                    .Select(index => upgradableCards[index])
                    .ToList();
            }
        }

        var result = selectedCards.ToList();
        LogChoice(player, result);
        return result;
    }

    private static bool ShouldSelectLocalCard(Player player)
    {
        return LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay;
    }

    private static void LogChoice(Player player, IEnumerable<CardModel> cards)
    {
        var cardIds = string.Join(",", cards.Select(card => card.Id.Entry));
        BangDreamLibCore.Logger.Info($"Player {player.NetId} chose extra deck cards [{cardIds}]");
    }

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
        var drawPile = BangDreamConst.ExtraDraw.GetPile(player);
        var drawsRequested = count > 0M ? (int)Math.Ceiling(count) : 0;
        if (drawsRequested == 0)
            return result;
        var num = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
        if (num == 0)
        {
            CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(player);
            return result;
        }

        for (var i = 0; i < drawsRequested; ++i)
        {
            if (num <= 0)
                break;
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(player))
                break;

            var card = drawPile.Cards.ToList().FirstOrDefault();
            if (card == null || hand.Cards.Count >= CardPile.MaxCardsInHand)
                break;

            result.Add(card);
            await CardPileCmd.Add(card, hand);
            CombatManager.Instance.History.CardDrawn(combatState, card, fromHandDraw);
            await Hook.AfterCardDrawn(combatState, choiceContext, card, fromHandDraw);
            card.InvokeDrawn();
            NDebugAudioManager.Instance?.Play("card_deal.mp3", 0.25f, PitchVariance.Small);
            num = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
        }

        return result;
    }

    private static bool CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot(Player player)
    {
        if (BangDreamConst.ExtraDraw.GetPile(player).Cards.Count == 0)
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
