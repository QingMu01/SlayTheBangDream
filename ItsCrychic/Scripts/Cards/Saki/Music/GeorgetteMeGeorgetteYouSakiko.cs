using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class GeorgetteMeGeorgetteYouSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget),
    IPerformHookListener
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.AnySlot;

    private bool _isRepeatingPerformance;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(3)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, null)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (perform.Card == this || perform.Card.Owner != Owner || Pile?.Type != BangDreamConst.PerformPile ||
            perform.Card is GeorgetteMeGeorgetteYouSakiko || _isRepeatingPerformance) return;

        _isRepeatingPerformance = true;
        try
        {
            await Owner.AttachedData().PerformManager.PerformCard(this, true);
        }
        finally
        {
            _isRepeatingPerformance = false;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}