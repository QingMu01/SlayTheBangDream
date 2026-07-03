using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace BangDreamLib.Scripts.Rewards;

public class MusicCardReward : ModCustomReward
{
    public override int RewardsSetIndex => 6;
    public override bool IsPopulated => _musicCards.Count > 0;
    public override RewardType ModRewardType => BangDreamConst.RewardMusic;
    protected override string RewardIconPath => "res://BangDreamLib/images/sceneui/music_reward.png";

    private NCardRewardSelectionScreen? _currentlyShownScreen;

    private readonly bool _cardsWereManuallySet;

    private readonly PlayerChoiceSynchronizer? _synchronizer;

    private readonly List<CardCreationResult> _musicCards = [];

    public IEnumerable<CardModel> Cards => _musicCards.Select(e => e.Card);

    private int OptionCount { get; }

    private CardCreationOptions Options { get; }


    public MusicCardReward(
        CardCreationOptions options,
        int cardCount,
        Player player,
        PlayerChoiceSynchronizer? synchronizer = null) : base(player)
    {
        OptionCount = cardCount;
        Options = options.WithFlags(CardCreationFlags.IsCardReward);
        _synchronizer = synchronizer ?? RunManager.Instance.PlayerChoiceSynchronizer;
        player.RelicObtained += OnRelicObtained;
    }

    public MusicCardReward(
        IEnumerable<CardModel> cardsToOffer,
        CardCreationSource source,
        Player player,
        PlayerChoiceSynchronizer? synchronizer = null) : base(player)
    {
        Options = new CardCreationOptions([], source,
            CardRarityOddsType.Uniform).WithFlags(
            CardCreationFlags.NoCardPoolModifications |
            CardCreationFlags.NoCardModelModifications |
            CardCreationFlags.IsCardReward
        );
        _cardsWereManuallySet = true;
        _musicCards = cardsToOffer.Select(c => new CardCreationResult(c)).ToList();
        OptionCount = _musicCards.Count;
        _synchronizer = synchronizer ?? RunManager.Instance.PlayerChoiceSynchronizer;
    }

    public override void Populate()
    {
        if (_cardsWereManuallySet)
        {
            if (!Hook.TryModifyCardRewardOptions(Player.RunState, Player, _musicCards, Options,
                    out var modifiers)) return;
            TaskHelper.RunSafely(Hook.AfterModifyingCardRewardOptions(Player.RunState, modifiers));
        }
        else
        {
            if (_musicCards.Count > 0)
                return;
            var forReward = CardFactory.CreateForReward(Player, OptionCount, Options);
            _musicCards.Clear();
            _musicCards.AddRange(forReward);
        }
    }

    protected override async Task<bool> OnSelect()
    {
        BangDreamLibCore.Logger.Info($"Player {Player.NetId} selected music card reward");
        var rewardComplete = false;
        var endSelection = false;
        var chosenCardIds = new List<CardModel>();
        var cardRewardOption = new List<CardRewardAlternative>
        {
            new("Skip", PostAlternateCardRewardAction.EndSelectionAndDoNotCompleteReward)
        };

        if (LocalContext.IsMe(Player))
        {
            _currentlyShownScreen = NCardRewardSelectionScreen.ShowScreen(_musicCards, cardRewardOption);
        }

        if (_synchronizer == null)
        {
            throw new InvalidOperationException("PlayerChoiceSynchronizer unset during test!");
        }

        while (!endSelection)
        {
            var choiceId = _synchronizer.ReserveChoiceId(Player);
            int? num;
            CardModel? obtainedCard;
            if (LocalContext.IsMe(Player))
            {
                if (_currentlyShownScreen != null)
                {
                    num = await _currentlyShownScreen.OptionSelected();
                }
                else
                {
                    var selection = CardSelectCmd.Selector?.GetSelectedCardReward(_musicCards, cardRewardOption);
                    if (!selection.HasValue)
                    {
                        throw new InvalidOperationException("Card selector unset during test!");
                    }

                    if (selection.Value.alternative != null)
                    {
                        num = _musicCards.Count + cardRewardOption.FirstIndex(r => r == selection.Value.alternative);
                    }
                    else
                    {
                        obtainedCard = selection.Value.card;
                        num = obtainedCard != null
                            ? new int?(_musicCards.FirstIndex(c => c.Card == selection.Value.card))
                            : null;
                    }
                }

                var result = PlayerChoiceResult.FromIndex(num);
                _synchronizer.SyncLocalChoice(Player, choiceId, result);
            }
            else
            {
                num = (await _synchronizer.WaitForRemoteChoice(Player, choiceId)).AsIndexOrNull();
            }

            NCardHolder? cardHolder;
            CardRewardAlternative? cardRewardAlternative;
            if (num.HasValue)
            {
                if (num < _musicCards.Count)
                {
                    obtainedCard = _musicCards[num.Value].Card;
                    rewardComplete = true;
                    endSelection = true;
                    cardHolder = _currentlyShownScreen?.GetCardHolder(obtainedCard);
                    cardRewardAlternative = null;
                }
                else
                {
                    if (!(num < _musicCards.Count + cardRewardOption.Count))
                    {
                        BangDreamLibCore.Logger.Error(
                            $"Received bad player choice index {num} for a card reward with {_musicCards.Count} cards and {cardRewardOption.Count} alternatives!");
                        continue;
                    }

                    cardRewardAlternative = cardRewardOption[num.Value - _musicCards.Count];
                    rewardComplete = cardRewardAlternative.AfterSelected ==
                                     PostAlternateCardRewardAction.EndSelectionAndCompleteReward;
                    var afterSelected = cardRewardAlternative.AfterSelected;
                    var flag = (uint)(afterSelected - 1) <= 1u;
                    endSelection = flag;
                    cardHolder = null;
                    obtainedCard = null;
                }
            }
            else
            {
                rewardComplete = false;
                endSelection = true;
                cardHolder = null;
                obtainedCard = null;
                cardRewardAlternative = null;
            }

            if (!(obtainedCard != null || cardRewardAlternative != null || rewardComplete))
            {
                continue;
            }

            if (obtainedCard != null)
            {
                var cardPileAddResult = await CardPileCmd.Add(obtainedCard, BangDreamConst.ExtraDeck);
                if (cardPileAddResult.success)
                {
                    obtainedCard = cardPileAddResult.cardAdded;
                    chosenCardIds.Add(obtainedCard);
                    _musicCards.RemoveAll(c => c.Card == obtainedCard);
                    var cardNode = cardHolder?.CardNode;
                    if (cardNode != null)
                    {
                        NRun.Instance?.GlobalUi.ReparentCard(cardNode);
                        cardHolder?.QueueFreeSafely();
                        NRun.Instance?.GlobalUi.TopBar.TrailContainer.AddChildSafely(NCardFlyVfx.Create(cardNode,
                            BangDreamConst.ExtraDeck, isAddingToPile: true, obtainedCard.Owner.Character.TrailPath));
                    }

                    BangDreamLibCore.Logger.Info($"Player {Player.NetId} obtained {obtainedCard.Id} from card reward");
                }
            }
            else if (cardRewardAlternative != null)
            {
                await cardRewardAlternative.OnSelect();
            }
        }

        Player.RelicObtained -= OnRelicObtained;
        foreach (var item in chosenCardIds)
        {
            Player.RunState.CurrentMapPointHistoryEntry?.GetEntry(Player.NetId).CardChoices
                .Add(new CardChoiceHistoryEntry(item, wasPicked: true));
        }

        if (rewardComplete)
        {
            foreach (var card in _musicCards)
            {
                Player.RunState.CurrentMapPointHistoryEntry?.GetEntry(Player.NetId).CardChoices
                    .Add(new CardChoiceHistoryEntry(card.Card, wasPicked: false));
            }
        }

        if (_currentlyShownScreen != null)
        {
            NOverlayStack.Instance?.Remove(_currentlyShownScreen);
            _currentlyShownScreen = null;
        }

        return rewardComplete;
    }

    public override void MarkContentAsSeen()
    {
    }

    public override void OnSkipped()
    {
        foreach (var card in _musicCards)
        {
            Player.RunState.CurrentMapPointHistoryEntry?.GetEntry(Player.NetId).CardChoices
                .Add(new CardChoiceHistoryEntry(card.Card, wasPicked: false));
        }

        Player.RelicObtained -= OnRelicObtained;
    }


    public override SerializableReward ToSerializable()
    {
        if (Options.CardPools.Count <= 0)
            throw new InvalidOperationException(
                "Tried to serialize a MusicCardReward without any card pools! This is not currently supported.");
        if (Options.CardPoolFilter != null)
            throw new InvalidOperationException(
                "Tried to serialize a MusicCardReward with a card pool filter! This is not currently supported.");

        var cardCreationFlags = Options.Flags & ~CardCreationFlags.IsCardReward;
        if (cardCreationFlags != ~CardCreationFlags.NoModifications)
            throw new InvalidOperationException(
                $"Tried to serialize a MusicCardReward with card creation flags! This is not currently supported. Flags: {cardCreationFlags}");

        return new SerializableReward
        {
            RewardType = RewardType,
            Source = Options.Source,
            RarityOdds = Options.RarityOdds,
            CardPoolIds = Options.CardPools.Select(p => p.Id).ToList(),
            OptionCount = OptionCount
        };
    }

    private void OnRelicObtained(RelicModel relic)
    {
        if (_musicCards == null)
            throw new InvalidOperationException("cards must be set first before you can update them");
        if (relic.TryModifyCardRewardOptions(Player, _musicCards, Options))
            TaskHelper.RunSafely(relic.AfterModifyingRewards());
        if (!relic.TryModifyCardRewardOptionsLate(Player, _musicCards, Options))
            return;
        TaskHelper.RunSafely(relic.AfterModifyingRewards());
    }
}