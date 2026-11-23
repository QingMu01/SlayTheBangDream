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
        power?.LinkCard(this);
        _grantedAmount += amount;
    }

    public async Task OnCardLeavePerformArea(PlayerChoiceContext choiceContext, CardModel cardModel)
    {
        if (cardModel != this) return;

        await ConvertGrantedToPermanentAsync(choiceContext);
    }

    /// <summary>将登记的临时敏捷转为永久并扣减临时能力；清零登记以防重复结算。</summary>
    private async Task ConvertGrantedToPermanentAsync(PlayerChoiceContext choiceContext)
    {
        if (_grantedAmount <= 0) return;

        var amount = _grantedAmount;
        _grantedAmount = 0;

        var temporaryPower = Owner.Creature.GetPower<SheetMusicMoonPower>();
        if (temporaryPower == null)
        {
            // 能力移除回调已处理转化。
            return;
        }

        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        await PowerCmd.ModifyAmount(choiceContext, temporaryPower, -amount,
            Owner.Creature, this);
    }

    /// <summary>能力移除时，若牌已离开歌单且战斗仍在进行，将登记量转为永久。</summary>
    internal async Task OnTemporaryPowerExpiringAsync(PlayerChoiceContext choiceContext)
    {
        var amount = _grantedAmount;
        _grantedAmount = 0;
        if (amount <= 0) return;

        if (!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress) return;

        if (IsInPerformArea) return;

        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
    }

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