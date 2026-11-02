using BangDreamInSpire.Scripts.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class LingeringSound() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    // 这张牌打出后激活；回合结束时记录「本回合最后一张攻击牌」（不含自身），下回合开始自动打出。
    private bool _armed;
    private CardModel? _lastAttackCard;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(8)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(play.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        _armed = true;
        _lastAttackCard = null;
    }

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!_armed || side != CombatSide.Player || !participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        // 卡牌在被打出前无法记录历史，故在「本回合」窗口末尾扫描本回合已完成的出牌，
        // 取最后一张攻击牌（排除自身，避免下回合无限自循环）。
        var combatState = CombatState;
        if (combatState != null)
        {
            _lastAttackCard = CombatManager.Instance.History.CardPlaysFinished
                .Where(entry => entry.HappenedThisTurn(combatState)
                                && entry.CardPlay.Player == Owner
                                && entry.CardPlay.Card != this
                                && entry.CardPlay.Card.Type == CardType.Attack)
                .Select(entry => entry.CardPlay.Card)
                .LastOrDefault();
        }

        if (_lastAttackCard == null)
        {
            _armed = false;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner)
        {
            return;
        }

        // 回合开始时先取走记录并复位，避免跨回合残留。
        var shouldPlay = _armed;
        var card = _lastAttackCard;
        _armed = false;
        _lastAttackCard = null;

        // 该牌若已不在任何战斗牌堆中则 no-op。
        if (!shouldPlay || card == null || card.Owner != Owner || card.Pile == null)
        {
            return;
        }

        // 从任意位置（手牌 / 抽牌堆 / 弃牌堆 / 消耗堆等）打出。
        await CardCmd.AutoPlay(choiceContext, card, null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
