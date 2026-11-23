using BangDreamInSpire.Scripts.Character;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using ItsCrychic.Scripts.Character.PerformSchemes;
using ItsCrychic.Scripts.Character.CardPools;
using ItsCrychic.Scripts.Character.PotionPools;
using ItsCrychic.Scripts.Character.RelicPools;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Character;

public sealed class WakabaMutsumi()
    : BandMemberModel<MutsumiStandardCardPool, MutsumiRelicPool, MutsumiPotionPool>(
        CrychicMemberEnum.Mutsumi.GetMemberColor()), IPuppeteerCharacter
{
    public const string DollSwitchInTrigger = "Doll_Switchin";

    public const string DollSwitchOutTrigger = "Doll_SwitchOut";

    public const string HostSwitchOutAnim = "SwitchOut";

    public const string DollIdleTrigger = "Doll_Idle";

    public const string DollDieTrigger = "Doll_Die";

    public override int StartingHp => 20;
    public override int StartingGold => 99;

    public int PuppetMaxHp => 50;

    public string PuppetSwitchInTrigger => DollSwitchInTrigger;
    public string HostSwitchOutTrigger => HostSwitchOutAnim;
    public string PuppetSwitchOutTrigger => DollSwitchOutTrigger;
    public string PuppetIdleTrigger => DollIdleTrigger;
    public string PuppetDieTrigger => DollDieTrigger;

    public override CharacterGroup Group => CharacterGroup.Crychic;
    public override string MemberNameRoman => CrychicMemberEnum.Mutsumi.GetMemberNameRoman();
    public override string MemberClass => BangDreamClass.Guitar.GetBandClass();

    public IPerformScheme CreatePerformScheme() => new MutsumiPerformScheme();

    public bool ShouldAlwaysShowExtraDeck => true;
    public bool ShouldAlwaysShowExtraPile => true;
    public CardPoolModel ExtraCardPool => ModelDb.CardPool<MutsumiMusicalCardPool>();

    public override List<string> CharacterSkinList =>
    [
        "res://ItsCrychic/skins/mutsumi/mutsumi_default.json",
        "res://ItsCrychic/skins/mutsumi/mutsumi_puppeteer.json"
    ];

    public override string SelectPoster =>
        "res://ItsCrychic/images/charui/img_mutsumi-wakaba_2.webp";

    public override string SelectLogo =>
        "res://ItsCrychic/images/charui/mutsumi/mutsumi_logo.png";

    public override string CustomTrailPath =>
        "res://BangDreamInSpire/scenes/vfx/card_trail.tscn";

    public override string CustomIconPath =>
        "res://ItsCrychic/scenes/char_icon/mutsumi_icon.tscn";

    public override string CustomIconTexturePath =>
        "res://ItsCrychic/images/charui/mutsumi/character_icon_mutsumi.png";

    public override string CustomIconOutlineTexturePath =>
        "res://ItsCrychic/images/charui/mutsumi/character_icon_mutsumi_outline.png";

    public override CreatureAnimator GenerateAnimator(MegaSprite controller, Creature creature)
    {
        // 骨架没有 Combat 动作，施法使用 Skill_1；两套皮肤动作同名。
        var start = new AnimState("Start");
        var idle = new AnimState("Idle", true);
        var combat = new AnimState("Skill_1");
        var attack = new AnimState("Attack");
        var die = new AnimState("Die");

        start.NextState = idle;
        combat.NextState = idle;
        attack.NextState = idle;

        // 傀儡切换只换动作；Switchin / SwitchOut 由 NextState 接回各自待机。
        var dollIdle = new AnimState(DollIdleTrigger, true);
        var dollSwitchIn = new AnimState(DollSwitchInTrigger);
        var dollSwitchOut = new AnimState(DollSwitchOutTrigger);
        dollSwitchIn.NextState = dollIdle;
        dollSwitchOut.NextState = start;

        // 本体切出后由 NextState 自动接傀儡切入与待机，无需玩法侧计时。
        var hostSwitchOut = new AnimState(HostSwitchOutAnim);
        hostSwitchOut.NextState = dollSwitchIn;

        var animator = new CreatureAnimator(start, controller);
        // Idle 按形态分流，阵亡后不得切回待机。
        animator.AddAnyState("Idle", idle, () => !creature.IsDead && !IsPuppetForm(creature));
        animator.AddAnyState("Idle", dollIdle, () => !creature.IsDead && IsPuppetForm(creature));
        // 按对应玩家的傀儡状态选择 Doll_Die，支持同角色多玩家。
        var dollDie = new AnimState(DollDieTrigger);
        animator.AddAnyState("Dead", dollDie, () => IsPuppetForm(creature));
        animator.AddAnyState("Dead", die);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("Cast", combat);
        animator.AddAnyState(HostSwitchOutAnim, hostSwitchOut);
        animator.AddAnyState(DollSwitchInTrigger, dollSwitchIn);
        animator.AddAnyState(DollSwitchOutTrigger, dollSwitchOut);
        animator.AddAnyState(DollIdleTrigger, dollIdle);
        return animator;
    }

    private static bool IsPuppetForm(Creature creature)
    {
        // 通过 IPuppeteerState 隔离内容模组与核心实现。
        return creature.Player is { } player &&
               player.AttachedData().Puppeteer.IsPuppetForm;
    }
}