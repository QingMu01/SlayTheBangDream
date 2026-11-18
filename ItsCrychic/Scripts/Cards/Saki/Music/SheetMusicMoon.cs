using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
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
        await PowerCmd.Apply<SheetMusicMoonPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        _grantedAmount += amount;
    }

    public async Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this) return;

        await ConvertGrantedToPermanentAsync(choiceContext);
    }

    /// <summary>
    /// 回合结束前主动转化。本体 <see cref="TemporaryDexterityPower" /> 在自己的
    /// <c>AfterSideTurnEnd</c> 里移除并扣回敏捷，而本 hook（<c>BeforeSideTurnEnd</c>）先于它执行，
    /// 因此在这里先把本次授予的临时敏捷转成永久，保证文案承诺的「离开歌单时保留」不会因为
    /// 回合结束先移除临时 Power 而静默失效。
    /// </summary>
    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 与临时 Power 的移除条件一致：只在本方回合结束（自己参与该回合）时转化。
        if (!participants.Contains(Owner.Creature)) return;

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
        // 先清零再 await：避免转化途中被重入（例如同一时点又触发一次离场）导致重复结算。
        _grantedAmount = 0;

        var temporaryPower = Owner.Creature.GetPower<SheetMusicMoonPower>();
        if (temporaryPower == null)
        {
            // 临时 Power 已被提前移除：此时再给永久敏捷等于凭空加属性，只能告警说明承诺未兑现。
            ItsCrychic.Logger.Warn(
                $"{nameof(SheetMusicMoon)}: temporary dexterity power is gone, {amount} temporary dexterity cannot be converted.");
            return;
        }

        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        await PowerCmd.ModifyAmount(choiceContext, temporaryPower, -amount,
            Owner.Creature, this);
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