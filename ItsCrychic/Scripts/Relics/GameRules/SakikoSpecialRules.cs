using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Relics;
using BangDreamLib.Scripts.Rewards;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Relics.GameRules;

[RegisterRelic(typeof(DeprecatedRelicPool))]
public class SakikoSpecialRules : HiddenRelic
{
    private const int ChanceStep = 15;
    [SavedProperty] public int MusicRewardChance { get; set; } = 20;

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player == Owner && rewards.Count > 0 && player.Character is IExtraDeckSupportCharacter extraDeck &&
            room != null)
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