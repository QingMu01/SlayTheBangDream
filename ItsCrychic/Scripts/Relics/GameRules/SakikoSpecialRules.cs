using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Relics;
using BangDreamLib.Scripts.Rewards;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ItsCrychic.Scripts.Relics.GameRules;

public class SakikoSpecialRules : HiddenRelic, ILingeredChangedHook
{
    private const int ChanceStep = 15;
    [SavedProperty] public int MusicRewardChance { get; set; } = 20;

    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (card is not ISubsideCardFlag or ISubsideCardFlag { ShouldIncreaseLingeredEnergy: true })
            await LingeredCmd.AddLeByCard(card, amount);
    }

    public async Task<bool> OnLingeredEnergyFilled(Player player, int amount)
    {
        if (player == Owner && amount >= 7)
        {
            var filledCount = amount / LingeredEnergyCounter.MaxLingeredEnergy;
            for (var i = 0; i < filledCount; i++)
            {
                var extraDraw = BangDreamConst.PileExtraDraw.GetPile(player);
                var firstOrDefault = extraDraw.Cards.ToList().FirstOrDefault();
                if (firstOrDefault != null)
                {
                    var result = await CardPileCmd.Add(firstOrDefault, BangDreamConst.PilePerformance.GetPileType());
                    if (result.success)
                    {
                        await LingeredCmd.ReduceLeByRelic(this, LingeredEnergyCounter.MaxLingeredEnergy);
                        ItsCrychic.Logger.Debug(
                            $"Player {player} ({player.Character}) got filled lingered energy and moved a card.");
                    }
                }

                await Cmd.CustomScaledWait(0.1f, 0.2f);
            }
        }

        return player.AttachedData().LingeredEnergy.Counter < 7;
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player == Owner && player.Character is IExtraDeckSupportCharacter extraDeck && room != null)
        {
            MusicCardReward? cardReward = null;
            switch (room.RoomType)
            {
                case RoomType.Boss:
                    cardReward = new MusicCardReward(
                        BangDreamModelHelper.CardCreationOptionsForRoom(extraDeck.ExtraCardPool, RoomType.Boss),
                        2, player);
                    ItsCrychic.Logger.Info($"Player {player} ({player.Character}) got a [boss] music card reward.");
                    break;
                case RoomType.Elite:
                    cardReward = new MusicCardReward(
                        BangDreamModelHelper.CardCreationOptionsForRoom(extraDeck.ExtraCardPool, RoomType.Elite),
                        2, player);
                    ItsCrychic.Logger.Info($"Player {player} ({player.Character}) got an [elite] music card reward.");
                    break;
                case RoomType.Monster:
                {
                    var roll = player.RunState.Rng.CombatCardGeneration.NextInt(0, 99);
                    if (roll <= MusicRewardChance)
                    {
                        cardReward = new MusicCardReward(
                            BangDreamModelHelper.CardCreationOptionsForRoom(extraDeck.ExtraCardPool, RoomType.Monster),
                            2, player);
                        MusicRewardChance = 0;
                        ItsCrychic.Logger.Info(
                            $"Player {player} ({player.Character}) got a [normal] music card reward.");
                    }
                    else
                    {
                        MusicRewardChance += ChanceStep;
                    }

                    break;
                }
                case RoomType.Unassigned:
                case RoomType.Treasure:
                case RoomType.Shop:
                case RoomType.Event:
                case RoomType.RestSite:
                case RoomType.Map:
                default:
                    break;
            }

            if (cardReward != null)
            {
                rewards.Add(cardReward);
                return true;
            }
        }

        return false;
    }
}