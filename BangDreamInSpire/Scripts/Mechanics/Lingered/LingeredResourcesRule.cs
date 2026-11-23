using System.Runtime.CompilerServices;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Lingered;

[RegisterSingleton]
public sealed class LingeredResourcesRule() : HookedSingletonModel(HookType.Combat), ISecondaryResourceHookListener
{
    /// <summary>余音硬上限规则，转换与扣减共用同一上限。</summary>
    public const int HardMaxAmount = 7;

    private readonly ConditionalWeakTable<Player, SemaphoreSlim> _overflowLocks = new();
    private readonly AsyncLocal<IReadOnlySet<Player>?> _normalizingPlayers = new();

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card is not ISubsideCard subsideCard) return;

        var payment = cardPlay.SecondaryResources();
        if (payment.HasLines && payment.Shortfall(BangDreamConst.LingeredResource) == 0)
        {
            await subsideCard.OnSubside(context, cardPlay);
            await BangDreamHook.AfterCardSubside(context, cardPlay);
        }

        await Cmd.CustomScaledWait(0.1f, 0.2f);
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 未声明余音能力的角色不生成或归一化余音。
        if (cardPlay.Card.Owner.Character is not ILingeredResourceCharacter { AutoGenerateSubsideResource: true }) return;
        var canGenerateRes = true;
        if (cardPlay.Card is ISubsideCard subsideCard)
        {
            canGenerateRes = subsideCard.ShouldGenerateResources;
        }

        if (canGenerateRes && cardPlay.Resources.EnergySpent > 0)
        {
            await SecondaryResourceCmd.Gain(
                cardPlay.Card.Owner,
                BangDreamConst.LingeredResource,
                cardPlay.Resources.EnergySpent,
                cardPlay.Card
            );
        }
    }

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (!context.Definition.Id.Equals(BangDreamConst.LingeredResource)) return;
        if (_normalizingPlayers.Value?.Contains(context.Player) == true) return;

        var overflowLock = _overflowLocks.GetValue(context.Player, _ => new SemaphoreSlim(1, 1));
        await overflowLock.WaitAsync();
        var previousNormalizingPlayers = _normalizingPlayers.Value;
        var normalizingPlayers = previousNormalizingPlayers?.ToHashSet() ?? [];
        normalizingPlayers.Add(context.Player);
        _normalizingPlayers.Value = normalizingPlayers;
        try
        {
            while (SecondaryResourceCmd.Get(context.Player, BangDreamConst.LingeredResource) >= HardMaxAmount)
            {
                var topCardInPile = BangDreamConst.ExtraDraw.GetPile(context.Player).Cards.FirstOrDefault();
                if (topCardInPile != null)
                {
                    // 插曲附魔牌改为进入手牌。
                    await InsertSongRules.MoveIntoPerformArea(topCardInPile);
                    await Cmd.CustomScaledWait(0.05f, 0.1f);
                }

                await SecondaryResourceCmd.Lose(context.Player, BangDreamConst.LingeredResource, HardMaxAmount,
                    this);
            }
        }
        finally
        {
            try
            {
                var current = SecondaryResourceCmd.Get(context.Player, BangDreamConst.LingeredResource);
                var max = EffectiveMax(context.Player);
                var overflow = Math.Max(0, current - max);
                if (overflow > 0)
                {
                    await SecondaryResourceCmd.Lose(context.Player, BangDreamConst.LingeredResource, overflow, this);
                }
            }
            finally
            {
                _normalizingPlayers.Value = previousNormalizingPlayers;
                overflowLock.Release();
            }
        }
    }

    /// <summary>扣减上限取动态上限与 HardMaxAmount 的较小值，且不会扣成负数。</summary>
    private static int EffectiveMax(Player player)
    {
        var dynamicMax = SecondaryResourceCmd.GetMax(player, BangDreamConst.LingeredResource) ?? HardMaxAmount;
        return Math.Min(dynamicMax, HardMaxAmount);
    }

    public static bool IsSufficient(CardModel card)
    {
        return SecondaryResourcePaymentResolver.Plan(card).Lines
            .Where(line => line.ResourceId.Equals(BangDreamConst.LingeredResource))
            .All(line => line.IsAffordable);
    }
}