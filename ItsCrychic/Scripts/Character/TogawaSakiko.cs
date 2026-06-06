using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Character.PotionPools;
using ItsCrychic.Scripts.Character.RelicPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Character;

public sealed class TogawaSakiko() : BandMemberModel<SakikoStandardCardPool, SakikoRelicPool, SakikoPotionPool>(
    CrychicMemberEnum.Sakiko.GetMemberColor()), IExtraDeckSupportCharacter
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;

    public override int GetDefaultCapacity => 3;

    public override BangDreamBand Band => BangDreamBand.Crychic;
    public override string MemberNameRoman => CrychicMemberEnum.Sakiko.GetMemberNameRoman();
    public override string MemberClass => BangDreamClass.Keyboard.GetBandClass();

    public override string SelectPoster => "res://ItsCrychic/images/charui/img_sakiko-togawa_2.webp";

    public bool ShouldAlwaysShowExtraDeckAndPile => true;
    public CardPoolModel ExtraCardPool => ModelDb.CardPool<SakikoMusicalCardPool>();

    public override List<string> CharacterSkinList =>
    [
        "res://ItsCrychic/skins/sakiko/sakiko_default.json",
        "res://ItsCrychic/skins/sakiko/sakiko_melody.json"
    ];

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        var start = new AnimState("Start");
        var idle = new AnimState("Idle", true);
        var combat = new AnimState("Combat");
        var attack = new AnimState("Attack");
        var die = new AnimState("Die");

        start.NextState = idle;
        combat.NextState = idle;
        attack.NextState = idle;

        var animator = new CreatureAnimator(start, controller);
        animator.AddAnyState("Idle", idle);
        animator.AddAnyState("Dead", die);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("Cast", combat);
        return animator;
    }
}