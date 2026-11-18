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
    /// <summary>傀儡形态（墨缇丝）登场动作。两道 skin 共用同一骨架，动作同名。</summary>
    public const string DollSwitchInTrigger = "Doll_Switchin";

    /// <summary>切回本体形态的动作。</summary>
    public const string DollSwitchOutTrigger = "Doll_SwitchOut";

    /// <summary>本体（小睦）的切出动作。骨架里叫 <c>SwitchOut</c>，在切到傀儡前先播它。</summary>
    public const string HostSwitchOutAnim = "SwitchOut";

    /// <summary>傀儡形态待机动作。</summary>
    public const string DollIdleTrigger = "Doll_Idle";

    /// <summary>傀儡形态阵亡动作。</summary>
    public const string DollDieTrigger = "Doll_Die";

    public override int StartingHp => 20;
    public override int StartingGold => 99;

    /// <summary>墨缇丝（傀儡形态）的最大生命。</summary>
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
        // 骨架实际动作名（两套皮肤一致）：
        // Start / Idle / Attack / Skill_1 / Skill_2 / SwitchOut /
        // Doll_Default / Doll_Idle / Doll_Switchin / Doll_SwitchOut / Doll_Die。
        // 注意：骨架**没有**名为 "Combat" 的动作 —— 施法沿用 Skill_1（与祥子一致）。
        var start = new AnimState("Start");
        var idle = new AnimState("Idle", true);
        var combat = new AnimState("Skill_1");
        var attack = new AnimState("Attack");
        var die = new AnimState("Die");

        start.NextState = idle;
        combat.NextState = idle;
        attack.NextState = idle;

        // 傀儡形态（墨缇丝）：不换场景、不换皮肤，只切动作。
        // Switchin / SwitchOut 播完各回自己的循环待机（SwitchOut 回本体 Idle）。
        var dollIdle = new AnimState(DollIdleTrigger, true);
        var dollSwitchIn = new AnimState(DollSwitchInTrigger);
        var dollSwitchOut = new AnimState(DollSwitchOutTrigger);
        dollSwitchIn.NextState = dollIdle;
        // 回切链：小莫 switchout → 小睦 Start → 小睦 Idle（Start.NextState 已接 idle）。
        dollSwitchOut.NextState = start;

        // 本体切出 → 自动接傀儡切入 → 傀儡待机。切换时只需触发本体切出这一步，
        // 由 NextState 串起来，不需要游戏侧计时（避免把表现时长写进玩法逻辑）。
        var hostSwitchOut = new AnimState(HostSwitchOutAnim);
        hostSwitchOut.NextState = dollSwitchIn;

        var animator = new CreatureAnimator(start, controller);
        // "Idle" 必须按形态分流：不分流的话傀儡形态下收到通用 "Idle" 会播成小睦待机
        // （表现为「墨缇丝死亡/回合结束后自动切回小睦的动画」）。
        // 同时排除阵亡：死亡动作播完后不能再被拉回任何待机循环。
        animator.AddAnyState("Idle", idle, () => !creature.IsDead && !IsPuppetForm(creature));
        animator.AddAnyState("Idle", dollIdle, () => !creature.IsDead && IsPuppetForm(creature));
        // 傀儡形态阵亡用 Doll_Die；条件按「该 creature 对应玩家当前是否处于傀儡形态」判定，
        // 因此同角色先后登场的两名玩家也能各播各的。条件失败时落回通用死亡动作。
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

    /// <summary>该 creature 对应的玩家当前是否处于傀儡形态（用于选择死亡动作）。</summary>
    private static bool IsPuppetForm(Creature creature)
    {
        // 走 <c>IPuppeteerState</c> 契约：内容模组不直接依赖 PuppeteerFormManager（见耦合边界 H1）。
        return creature.Player is { } player &&
               player.AttachedData().Puppeteer.IsPuppetForm;
    }
}