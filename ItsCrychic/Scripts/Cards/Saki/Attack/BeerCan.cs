using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class BeerCan() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    IMusicNotePlayedHook
{
    private const int CustomCost = 2;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.RandomEnemy;

    private int _musicNoteCounter;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordMusicNote.GetModKeywordCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new DamageVar(20m, ValueProp.Move),
        new IntVar("Count", 4)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingRandomOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }

    public async Task OnMusicNotePlayed(PlayerChoiceContext choiceContext)
    {
        if (Pile?.Type == BangDreamConst.PileExtraDraw.GetModCardPileType())
        {
            _musicNoteCounter++;

            if (_musicNoteCounter >= DynamicVars["Count"].IntValue)
            {
                _musicNoteCounter = 0;
                await CardCmd.AutoPlay(choiceContext, this, null);
            }
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _musicNoteCounter = 0;
        return base.AfterCombatEnd(room);
    }
}