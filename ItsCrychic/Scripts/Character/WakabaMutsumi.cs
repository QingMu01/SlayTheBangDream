using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils.Enums;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Character.PotionPools;
using ItsCrychic.Scripts.Character.RelicPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Character;

public sealed class WakabaMutsumi()
    : BandMemberModel<MutsumiStandardCardPool, MutsumiRelicPool, MutsumiPotionPool>(
        CrychicMemberEnum.Mutsumi.GetMemberColor()), IExtraDeckSupportCharacter
{
    public override int StartingHp => 20;
    public override int StartingGold => 99;

    public override int GetDefaultCapacity => 1;

    public override CharacterGroup Group => CharacterGroup.Crychic;
    public override string MemberNameRoman => CrychicMemberEnum.Mutsumi.GetMemberNameRoman();
    public override string MemberClass => BangDreamClass.Guitar.GetBandClass();

    public override bool AllowSelect => false;

    public bool ShouldAlwaysShowExtraDeck => true;
    public bool ShouldAlwaysShowExtraPile => true;
    public CardPoolModel ExtraCardPool => ModelDb.CardPool<MutsumiMusicalCardPool>();

    public override List<string> CharacterSkinList =>
    [
        "res://ItsCrychic/skins/mutsumi/mutsumi_default.json",
        "res://ItsCrychic/skins/mutsumi/mutsumi_puppeteer.json"
    ];

    public override string CustomTrailPath =>
        "res://BangDreamLib/scenes/vfx/card_trail_sakiko.tscn";

    public override string SelectPoster =>
        "res://ItsCrychic/images/charui/img_mutsumi-wakaba_2.webp";

    public override string CustomIconPath =>
        "res://ItsCrychic/scenes/char_icon/sakiko_icon.tscn";

    public override string CustomIconTexturePath =>
        "res://ItsCrychic/images/charui/sakiko/character_icon_saki.png";

    public override string CustomIconOutlineTexturePath =>
        "res://ItsCrychic/images/charui/sakiko/character_icon_saki_outline.png";

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