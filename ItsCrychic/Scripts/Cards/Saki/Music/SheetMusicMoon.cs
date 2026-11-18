using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Power.Temporary;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SheetMusicMoon() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Basic;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    private decimal _grantedAmount;

    public override bool IsInstant => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        var amount = QuickVar.Buff.GetVar(this).BaseValue;
        var power = await PowerCmd.Apply<SheetMusicMoonPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        // 双向校验：能力到期/被移除时，它需要反过来确认这张牌是否还在歌单里。
        power?.LinkCard(this);
        _grantedAmount += amount;
    }

    public async Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this) return;

        await ConvertGrantedToPermanentAsync(choiceContext);
    }

    /// <summary>
    /// 把本次授予的临时敏捷转成永久敏捷，并等量削减临时 Power（与离场逻辑同一口径）。
    /// 幂等：转化后清零 <see cref="_grantedAmount" />，之后的离场与再次触发都不会重复结算。
    /// </summary>
    private async Task ConvertGrantedToPermanentAsync(PlayerChoiceContext choiceContext)
    {
        if (_grantedAmount <= 0) return;

        var amount = _grantedAmount;
        // 先清零再 await：避免转化途中被重入（例如同一时点又触发一次离场或能力到期）导致重复结算。
        _grantedAmount = 0;

        var temporaryPower = Owner.Creature.GetPower<SheetMusicMoonPower>();
        if (temporaryPower == null)
        {
            // 能力已不在：双向校验由能力的到期/移除回调完成——牌已离场时它已转永久，牌仍在歌单时已失效。
            return;
        }

        // 卡牌离场且能力仍在：撤掉能力（等量削减到 0）并把本次授予转成永久。
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        await PowerCmd.ModifyAmount(choiceContext, temporaryPower, -amount,
            Owner.Creature, this);
    }

    /// <summary>
    /// 双向校验的另一半：能力在回合结束（或其他原因）即将被移除时，由能力回调。
    /// 牌已不在歌单 → 按文案承诺把本次授予转成永久；牌仍在歌单 → 本回合内有效，到期失效。
    /// </summary>
    internal async Task OnTemporaryPowerExpiringAsync(PlayerChoiceContext choiceContext)
    {
        var amount = _grantedAmount;
        _grantedAmount = 0;
        if (amount <= 0) return;

        // 战斗已结束/正在结束时不再补属性（此时 Creature 也会被回收）。
        if (!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress) return;

        if (IsInPerformArea) return;

        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
    }

    /// <summary>这张牌当前是否仍在歌单（能力到期时据此决定是否保留）。</summary>
    private bool IsInPerformArea
    {
        get
        {
            var performPile = BangDreamConst.PerformPile.GetPile(Owner);
            return performPile != null && ReferenceEquals(Pile, performPile);
        }
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
        {
            _grantedAmount = 0;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(1);
    }
}