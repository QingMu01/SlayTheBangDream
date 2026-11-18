using Godot;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Puppeteer;

/// <summary>
/// 傀儡师形态管理器：每玩家一份，负责两套生命池的切换、死亡拦截、歌单冻结与分组激活。
/// <para>
/// 状态分成两层：<b>跨战斗</b>部分放在 <see cref="PuppeteerRunData" />（每玩家运行数据）；
/// <b>本场战斗</b>部分（分组激活位）只存在这个模型实例上，战斗结束即清空。
/// </para>
/// <para>
/// 本体 0.113 起 SingletonModel 是 RunModel：注册的单例本身也会以"局内模型单例"身份进入
/// <c>IterateHookListeners</c>，那一份没有绑定玩家。所有钩子实现都先判 <see cref="_player" />
/// 为空直接放行，真正的每玩家结算由 <see cref="PuppeteerMechanic.GetCombatHookModels" /> 注册的
/// 这份克隆负责（与 <c>PerformManager</c> 同一套约定）。
/// </para>
/// </summary>
[RegisterSingleton]
public class PuppeteerFormManager : HookedSingletonModel, IInCombatManager, IPuppeteerState
{
    /// <summary>本体形态的待机动作触发名。原版 <c>CreatureAnimator</c> 的通用 Idle 触发器。</summary>
    private const string HostIdleTrigger = "Idle";

    public PuppeteerFormManager() : base(HookType.Combat)
    {
    }

    private Player? _player;

    public override bool ShouldReceiveCombatHooks => true;

    public Player Player
    {
        get => _player ?? throw new InvalidOperationException("Owner is not Initialized.");
        set
        {
            AssertMutable();
            BangDreamTools.Init(ref _player, value, nameof(Player));
        }
    }

    /// <summary>当前登场形态。战斗外恒为 <see cref="PuppeteerForm.Host" />。</summary>
    public PuppeteerForm ActiveForm { get; private set; } = PuppeteerForm.Host;

    /// <inheritdoc />
    public bool IsPuppetForm => ActiveForm == PuppeteerForm.Puppet;

    /// <summary>本场战斗已激活的分组（位掩码）。只在傀儡形态下累积，切换形态即清空。</summary>
    public PerformChord ActivatedGroups { get; private set; } = PerformChord.None;

    /// <summary>当前角色是否实现傀儡师能力；未实现时本模型保持空转（机制对全角色实例化）。</summary>
    private IPuppeteerCharacter? PuppeteerCharacter => _player?.Character as IPuppeteerCharacter;

    // ── 战斗生命周期 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 战斗开始：归位本体形态、按运行数据把本体生命写进 Creature、解冻歌单、清空分组激活位。
    /// 由 <see cref="PuppeteerMechanic.SubmitCombatState" /> 在演奏机制之后调用。
    /// </summary>
    public void SubmitCombatState()
    {
        if (_player == null) return;
        var character = PuppeteerCharacter;
        if (character == null) return;

        var creature = _player.Creature;
        var data = InitializeRunData(creature, character);

        // 战斗外 Creature 始终是本体配置，所以上限取本体池与当前上限的较大值（休息处回血 / 遗物加血都会反映进来）；
        // 若运行数据记录着"上一场停在傀儡形态"（战斗中存档的残留），Creature 上的数值是傀儡池，必须以运行数据为准。
        var savedPuppet = data.ActiveForm == PuppeteerForm.Puppet;
        // 旧存档可能只有 ActiveForm 而没有 HostMaxHp（字段缺失 → 0）：此时不能把本体上限压成 1，
        // 回落到当前 Creature 上限（与 InitializeRunData 首次播种的口径一致）。
        var hostMax = savedPuppet && data.HostMaxHp > 0
            ? Math.Max(1, data.HostMaxHp)
            : Math.Max(1, creature.MaxHp);
        // 本体已死（0 血）时强制复活到 1 点：战斗开始必须有小睦登场。
        var hostHp = Math.Clamp(savedPuppet ? data.HostHp : creature.CurrentHp, 1, hostMax);

        ActiveForm = PuppeteerForm.Host;
        ActivatedGroups = PerformChord.None;

        PuppeteerRunState.Modify(_player, d =>
        {
            d.Initialized = true;
            d.ActiveForm = PuppeteerForm.Host;
            d.HostMaxHp = hostMax;
            d.HostHp = hostHp;
            d.PuppetMaxHp = Math.Max(1, d.PuppetMaxHp > 0 ? d.PuppetMaxHp : character.PuppetMaxHp);
            // 傀儡池跨战斗保存：只做越界收敛，不回满。
            // 若存档停在傀儡形态（战斗中存档），Creature 上的血量就是傀儡当前血量，用它刷新。
            d.PuppetHp = Math.Clamp(savedPuppet ? creature.CurrentHp : d.PuppetHp, 1, d.PuppetMaxHp);
        });

        WriteCreatureHp(creature, hostMax, hostHp);

        var performManager = _player.AttachedData().PerformManager;
        performManager.SetPlaylistFrozen(false);
        performManager.SetActivatedGroups(PerformChord.None);

        // 上一场战斗如果停在傀儡动作上，这里强制回到本体待机。
        ResetHostAnimationIfNeeded(character);

        var current = PuppeteerRunState.Get(_player);
        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) puppeteer combat start : " +
            $"host {hostHp}/{hostMax}, puppet {current.PuppetHp}/{current.PuppetMaxHp}.");
    }

    /// <summary>
    /// 战斗结束清理：兜底归位本体形态。
    /// <c>AfterCombatVictory</c> / <c>AfterCombatEnd</c> / 本清理三者的到达顺序不保证，因此实现必须幂等。
    /// </summary>
    public void UnsubscribeCombatState()
    {
        RestoreHostForm("unsubscribe");
        ActivatedGroups = PerformChord.None;
    }

    // ── 死亡拦截与形态切换 ─────────────────────────────────────────────────────

    /// <summary>
    /// 死亡拦截：只有「战斗内 + 本体形态 + 目标是本玩家角色」才拦下。
    /// 傀儡形态正常阵亡（此时才真正失败结算）；未绑定玩家的克隆、战斗外的致死一律放行。
    /// <para>
    /// **设计语义（不要改成 <c>ShouldDieLate</c>）**：本体形态（睦）不触发原版防死 ——
    /// 她受伤致死的唯一回应是切成傀儡；傀儡形态（墨缇丝）阵亡是真正的失败，
    /// 此时本方法返回 <c>true</c>，原版防死（精灵药水走 <c>ShouldDie</c>、蜥蜴尾巴走 <c>ShouldDieLate</c>）
    /// 照常生效并正常消耗。
    /// </para>
    /// <para>
    /// **顺序前提**：原版 <c>Hook.ShouldDie</c> 先遍历全部 <c>ShouldDie</c>（首个 <c>false</c> 胜出），
    /// 全部通过后才遍历 <c>ShouldDieLate</c>。因此本模型必须通过
    /// <see cref="PuppeteerMechanic.GetRunStateHookModels" /> 挂在**运行态订阅组**（排在遗物 / 药水之前）；
    /// 若挂到 <c>GetCombatHookModels</c>（战斗组最末、排在遗物之后），
    /// 「精灵药水」这类 <c>ShouldDie</c> 型防死就会抢先消费，本体形态不再切傀儡。
    /// </para>
    /// </summary>
    public override bool ShouldDie(Creature creature)
    {
        if (_player == null) return true;
        if (PuppeteerCharacter == null) return true;
        // 战斗外始终本体形态：事件 / 诅咒等战斗外致死不走形态切换。
        if (!CombatManager.Instance.IsInProgress) return true;
        if (ActiveForm != PuppeteerForm.Host) return true;
        if (creature == null || creature != _player.Creature) return true;

        return false;
    }

    /// <summary>
    /// 本体阵亡后切换傀儡形态继续战斗。
    /// <para>
    /// 原版在 <c>AfterPreventingDeath</c> 之后会检查 <c>creature.IsDead</c> 并递归重杀（10 次后抛异常），
    /// 所以这里必须先把血量抬到 &gt; 0 再返回。
    /// </para>
    /// </summary>
    public override Task AfterPreventingDeath(Creature creature)
    {
        if (_player == null) return Task.CompletedTask;
        var character = PuppeteerCharacter;
        if (character == null) return Task.CompletedTask;
        if (ActiveForm != PuppeteerForm.Host) return Task.CompletedTask;
        if (creature == null || creature != _player.Creature) return Task.CompletedTask;

        var data = PuppeteerRunState.Get(_player);
        var puppetMax = Math.Max(1, data.PuppetMaxHp > 0 ? data.PuppetMaxHp : character.PuppetMaxHp);
        // 傀儡池已空时抬到 1：不抬会陷入"切完立刻又死"的递归重杀。
        var puppetHp = Math.Clamp(data.PuppetHp, 1, puppetMax);

        ActiveForm = PuppeteerForm.Puppet;
        ActivatedGroups = PerformChord.None;

        PuppeteerRunState.Modify(_player, d =>
        {
            // 本体在场期间吃到的「最大生命 +N」必须在此刻留档：一旦切到傀儡形态，
            // Creature 上装的是傀儡池，本体上限就只剩运行数据可查。
            // 漏写会让 RestoreHostForm / ReviveHost 取到战斗开始时的旧快照，
            // 把 +MaxHp 永久回退（跨战斗丢失）。
            d.HostMaxHp = Math.Max(d.HostMaxHp, creature.MaxHp);
            d.HostHp = Math.Max(0, creature.CurrentHp); // 本体阵亡，写回 0
            d.ActiveForm = PuppeteerForm.Puppet;
            d.PuppetMaxHp = puppetMax;
            d.PuppetHp = puppetHp;
        });

        WriteCreatureHp(creature, puppetMax, puppetHp);

        // 傀儡形态歌单冻结：不能进曲目、不能移除、演奏改为激活分组。
        var performManager = _player.AttachedData().PerformManager;
        performManager.SetPlaylistFrozen(true);
        performManager.SetActivatedGroups(ActivatedGroups);

        // 有本体切出动作时先播它，animator 会自动接傀儡切入；没有就直接播傀儡切入。
        PlayTrigger(character.HostSwitchOutTrigger is { Length: > 0 } hostSwitchOut
            ? hostSwitchOut
            : character.PuppetSwitchInTrigger);

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) host died, switch to puppet : {puppetHp}/{puppetMax}.");
        return Task.CompletedTask;
    }

    /// <summary>战斗胜利：切回本体形态并解冻歌单。</summary>
    public override Task AfterCombatVictory(CombatRoom room)
    {
        RestoreHostForm("victory");
        return Task.CompletedTask;
    }

    /// <summary>战斗结束兜底（结局分支为"非胜利"时也要保证战斗外处于本体形态）。</summary>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        RestoreHostForm("combat end");
        return Task.CompletedTask;
    }

    // ── 分组激活与复活 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 傀儡形态下的「演奏」：不发动效果，改为点亮分组（幂等）。
    /// <paramref name="groups" /> 为 <see cref="PerformChord.None" /> 时按演奏方案的默认分组兜底。
    /// 四组全部激活后本体复活。
    /// </summary>
    public void ActivateGroups(PerformChord groups)
    {
        if (_player == null) return;
        if (PuppeteerCharacter == null) return;
        if (ActiveForm != PuppeteerForm.Puppet) return;

        var schemeMask = ResolveSchemeGroups();
        if (schemeMask == PerformChord.None) return;

        var mask = NormalizeActivationMask(groups, schemeMask);
        if (mask == PerformChord.None) return;

        var updated = ActivatedGroups | mask;
        if (updated == ActivatedGroups)
        {
            // 幂等：重复"演奏"同一分组不产生额外变化，只刷新一次表现。
            SyncPerformAreaActivation();
            return;
        }

        ActivatedGroups = updated;
        SyncPerformAreaActivation();

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) puppet activate groups : {ActivatedGroups}.");

        if ((updated & schemeMask) == schemeMask)
        {
            ReviveHost();
        }
    }

    /// <summary>
    /// 复活本体：生命 = 歌单容量 × 2（默认四组各 1 槽 → 8），上限本体最大生命；
    /// 切回本体、解冻歌单、清空激活位，并播放切出动作。
    /// </summary>
    private void ReviveHost()
    {
        if (_player == null) return;
        var character = PuppeteerCharacter;
        if (character == null) return;

        var creature = _player.Creature;
        var data = PuppeteerRunState.Get(_player);
        var hostMax = Math.Max(1, data.HostMaxHp > 0 ? data.HostMaxHp : creature.MaxHp);
        var performManager = _player.AttachedData().PerformManager;
        var reviveHp = Math.Clamp(performManager.Capacity * 2, 1, hostMax);

        ActiveForm = PuppeteerForm.Host;
        ActivatedGroups = PerformChord.None;

        PuppeteerRunState.Modify(_player, d =>
        {
            d.ActiveForm = PuppeteerForm.Host;
            d.HostMaxHp = hostMax;
            d.HostHp = reviveHp;
            // 离场前写回傀儡池当前血量（复活时 Creature 上装的就是傀儡血量）。
            d.PuppetHp = Math.Clamp(creature.CurrentHp, 1, Math.Max(1, d.PuppetMaxHp));
        });

        WriteCreatureHp(creature, hostMax, reviveHp);

        performManager.SetPlaylistFrozen(false);
        performManager.SetActivatedGroups(PerformChord.None);

        // 切出动作非循环，NextState 自动回到本体 Idle。
        PlayTrigger(character.PuppetSwitchOutTrigger);

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) all groups activated, host revived : " +
            $"{reviveHp}/{hostMax} (capacity {performManager.Capacity}).");
    }

    // ── 内部工具 ───────────────────────────────────────────────────────────────

    /// <summary>首次进入战斗时用角色 / 生物初始值填充运行数据。</summary>
    private PuppeteerRunData InitializeRunData(Creature creature, IPuppeteerCharacter character)
    {
        var player = Player;
        var data = PuppeteerRunState.Get(player);
        if (data.Initialized) return data;

        PuppeteerRunState.Modify(player, d =>
        {
            d.Initialized = true;
            d.HostMaxHp = Math.Max(1, creature.MaxHp);
            d.HostHp = Math.Clamp(creature.CurrentHp, 1, d.HostMaxHp);
            d.PuppetMaxHp = Math.Max(1, character.PuppetMaxHp);
            d.PuppetHp = d.PuppetMaxHp;
            d.ActiveForm = PuppeteerForm.Host;
        });

        return PuppeteerRunState.Get(player);
    }

    /// <summary>战斗结束后把形态归位本体。幂等，且不在玩家真的阵亡（傀儡死亡 / 败北）时复活。</summary>
    private void RestoreHostForm(string reason)
    {
        if (_player == null) return;
        if (PuppeteerCharacter == null) return;

        var creature = _player.Creature;
        if (creature == null) return;
        // 傀儡形态真的阵亡：那是失败结算，不能复活。
        if (creature.IsDead) return;

        var data = PuppeteerRunState.Get(_player);
        var wasPuppet = ActiveForm == PuppeteerForm.Puppet || data.ActiveForm == PuppeteerForm.Puppet;
        // 傀儡形态离场前先把它的当前血量写回池子：傀儡池也要跨战斗保存。
        var puppetHp = wasPuppet ? Math.Max(1, creature.CurrentHp) : data.PuppetHp;

        // 本体在场上时以 Creature 为准（战斗中受到的伤害、遗物抬的上限都在上面）；
        // 傀儡在场上时 Creature 装的是傀儡池，本体数值只能取运行数据。
        var hostMax = wasPuppet
            ? (data.HostMaxHp > 0 ? data.HostMaxHp : Math.Max(1, creature.MaxHp))
            : Math.Max(1, creature.MaxHp);
        var hostHp = wasPuppet
            ? Math.Clamp(data.HostHp, 1, hostMax) // 本体已死 → 强制复活 1 点
            : Math.Clamp(creature.CurrentHp, 1, hostMax);

        var creatureMatches = creature.MaxHp == hostMax && creature.CurrentHp == hostHp;

        ActiveForm = PuppeteerForm.Host;
        ActivatedGroups = PerformChord.None;

        PuppeteerRunState.Modify(_player, d =>
        {
            d.ActiveForm = PuppeteerForm.Host;
            d.HostMaxHp = hostMax;
            d.HostHp = hostHp;
            d.PuppetHp = Math.Clamp(puppetHp, 1, Math.Max(1, d.PuppetMaxHp));
        });

        if (!wasPuppet && creatureMatches)
        {
            // 幂等：AfterCombatVictory / AfterCombatEnd / 战斗结束清理可能依次到达；
            // 本体一直在场上时只有第一次需要真正做切换。
            return;
        }

        WriteCreatureHp(creature, hostMax, hostHp);

        var performManager = _player.AttachedData().PerformManager;
        performManager.SetPlaylistFrozen(false);
        performManager.SetActivatedGroups(PerformChord.None);

        if (wasPuppet)
        {
            // 切回本体：切出动作非循环，NextState 自动回到本体 Idle。
            PlayTrigger(PuppeteerCharacter!.PuppetSwitchOutTrigger);
        }

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) restore host form ({reason}) : {hostHp}/{hostMax}.");
    }

    /// <summary>
    /// 写回 Creature 的双值。必须先写上限：<c>SetMaxHpInternal</c> 会把当前值压进新上限，
    /// 反过来写会被旧的傀儡上限截断。
    /// </summary>
    private static void WriteCreatureHp(Creature creature, int maxHp, int currentHp)
    {
        creature.SetMaxHpInternal(maxHp);
        creature.SetCurrentHpInternal(currentHp);
    }

    /// <summary>演奏方案覆盖的全部分组（掩码）。</summary>
    private PerformChord ResolveSchemeGroups()
    {
        if (_player == null) return PerformChord.None;

        var mask = PerformChord.None;
        foreach (var descriptor in _player.AttachedData().PerformManager.Scheme.Groups)
        {
            mask |= descriptor.Chord;
        }

        return mask;
    }

    /// <summary>把一次"演奏"请求归一化成要激活的分组：未指定时取方案默认分组。</summary>
    private PerformChord NormalizeActivationMask(PerformChord groups, PerformChord schemeMask)
    {
        var mask = groups & schemeMask;
        if (mask != PerformChord.None) return mask;

        if (_player == null) return schemeMask;
        var defaultGroup = _player.AttachedData().PerformManager.Scheme.DefaultGroup & schemeMask;
        return defaultGroup != PerformChord.None ? defaultGroup : schemeMask;
    }

    private void SyncPerformAreaActivation()
    {
        if (_player == null) return;
        _player.AttachedData().PerformManager.SetActivatedGroups(ActivatedGroups);
    }

    private void PlayTrigger(string? trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return;

        // 战斗结束清理期节点可能已入销毁队列：先判活再触碰 Spine。
        if (_player?.Creature.GetCreatureNode() is { } node && GodotObject.IsInstanceValid(node))
        {
            node.SetAnimationTrigger(trigger);
        }
    }

    /// <summary>若还停在傀儡动作上，强制回到本体待机（战斗开始 / 形态归位时使用）。</summary>
    private void ResetHostAnimationIfNeeded(IPuppeteerCharacter character)
    {
        var node = _player?.Creature.GetCreatureNode();
        if (node == null || !GodotObject.IsInstanceValid(node)) return;

        var current = node.SpineAnimation.GetCurrentAnimationName();
        if (current == null) return;
        if (!string.Equals(current, character.PuppetIdleTrigger, StringComparison.Ordinal) &&
            !string.Equals(current, character.PuppetSwitchInTrigger, StringComparison.Ordinal))
        {
            return;
        }

        node.SetAnimationTrigger(HostIdleTrigger);
    }
}
