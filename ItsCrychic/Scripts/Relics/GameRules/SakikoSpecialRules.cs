using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Mechanics.ExtraDeck;
using BangDreamLib.Scripts.Relics;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Character;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Relics.GameRules;

[RegisterRelic(typeof(DeprecatedRelicPool))]
public class SakikoSpecialRules : HiddenRelic, IPerformTriggerListener, ISecondaryResourceHookListener
{
    private const int ChanceStep = 15;
    [SavedProperty] public int MusicRewardChance { get; set; } = 20;

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player == Owner && rewards.Count > 0 && player.Character is IPerformableCharacter extraDeck && room != null)
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

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player.Character is TogawaSakiko)
        {
            options.Add(new TechnicalPracticeOption(player));
            return true;
        }

        return false;
    }

    public Task OnCardTriggeredPerform(PlayerChoiceContext? choiceContext, CardModel cardModel)
    {
        if (cardModel.Owner != Owner) return Task.CompletedTask;

        return Owner.AttachedData().PerformManager.TryInstant(cardModel, choiceContext);
    }

    /// <summary>
    /// 余音数量即目标槽位，据此演奏对应槽位；数量超出当前容量时不演奏。
    /// 本遗物同时会被睦的皮肤授予（睦的起始配置暂沿用祥子的），故按角色过滤：余音为祥子专属机制。
    /// </summary>
    public Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Player != Owner ||
            Owner.Character is not TogawaSakiko ||
            !context.Definition.Id.Equals(BangDreamConst.LingeredResource)
           ) return Task.CompletedTask;

        var manager = Owner.AttachedData().PerformManager;
        if (context.NewAmount > 0 && context.NewAmount <= manager.Capacity)
        {
            manager.RequestSlotPerform(context.NewAmount, context.Reason == SecondaryResourceChangeReason.Spend);
        }

        return Task.CompletedTask;
    }
}