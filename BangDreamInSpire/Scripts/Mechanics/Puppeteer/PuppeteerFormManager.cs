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

/// <summary>管理双生命池、形态切换、死亡拦截与冻结。RunData 跨战斗，激活位仅本场；
/// 未绑定玩家的注册单例必须在所有 hook 中早退。</summary>
[RegisterSingleton]
public class PuppeteerFormManager : HookedSingletonModel, IInCombatManager, IPuppeteerState
{
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

    /// <summary>当前形态，战斗外恒为 Host。</summary>
    public PuppeteerForm ActiveForm { get; private set; } = PuppeteerForm.Host;

    /// <inheritdoc />
    public bool IsPuppetForm => ActiveForm == PuppeteerForm.Puppet;

    /// <summary>本场战斗的激活分组，切换形态时清空。</summary>
    public PerformChord ActivatedGroups { get; private set; } = PerformChord.None;

    /// <summary>角色是否支持傀儡师机制。</summary>
    private IPuppeteerCharacter? PuppeteerCharacter => _player?.Character as IPuppeteerCharacter;


    /// <summary>战斗开始恢复本体并同步生命、冻结与激活状态。</summary>
    public void SubmitCombatState()
    {
        if (_player == null) return;
        var character = PuppeteerCharacter;
        if (character == null) return;

        var creature = _player.Creature;
        var data = InitializeRunData(creature, character);

        // 战斗中存档可能让 Creature 装着傀儡池，本体上限优先读 RunData。
        var savedPuppet = data.ActiveForm == PuppeteerForm.Puppet;
        // 旧存档缺 HostMaxHp 时回退当前上限，避免压成 1。
        var hostMax = savedPuppet && data.HostMaxHp > 0
            ? Math.Max(1, data.HostMaxHp)
            : Math.Max(1, creature.MaxHp);
        // 战斗开始本体至少 1 血。
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
            // 傀儡池只收敛越界；战斗中存档以 Creature 血量刷新。
            d.PuppetHp = Math.Clamp(savedPuppet ? creature.CurrentHp : d.PuppetHp, 1, d.PuppetMaxHp);
        });

        WriteCreatureHp(creature, hostMax, hostHp);

        var performManager = _player.AttachedData().PerformManager;
        performManager.SetPlaylistFrozen(false);
        performManager.SetActivatedGroups(PerformChord.None);

        // 清除残留的傀儡待机。
        ResetHostAnimationIfNeeded(character);

        var current = PuppeteerRunState.Get(_player);
        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) puppeteer combat start : " +
            $"host {hostHp}/{hostMax}, puppet {current.PuppetHp}/{current.PuppetMaxHp}.");
    }

    /// <summary>战斗结束幂等恢复本体。</summary>
    public void UnsubscribeCombatState()
    {
        RestoreHostForm("unsubscribe");
        ActivatedGroups = PerformChord.None;
    }


    /// <summary>仅拦截战斗内本体死亡并切换傀儡；傀儡死亡放行原版防死。
    /// 必须挂在运行态订阅组，先于 ShouldDie 遗物执行。</summary>
    public override bool ShouldDie(Creature creature)
    {
        if (_player == null) return true;
        if (PuppeteerCharacter == null) return true;
        // 战斗外致死不切换形态。
        if (!CombatManager.Instance.IsInProgress) return true;
        if (ActiveForm != PuppeteerForm.Host) return true;
        if (creature == null || creature != _player.Creature) return true;

        return false;
    }

    /// <summary>切换傀儡继续战斗；返回前必须恢复生命，避免原版递归重杀。</summary>
    public override Task AfterPreventingDeath(Creature creature)
    {
        if (_player == null) return Task.CompletedTask;
        var character = PuppeteerCharacter;
        if (character == null) return Task.CompletedTask;
        if (ActiveForm != PuppeteerForm.Host) return Task.CompletedTask;
        if (creature == null || creature != _player.Creature) return Task.CompletedTask;

        var data = PuppeteerRunState.Get(_player);
        var puppetMax = Math.Max(1, data.PuppetMaxHp > 0 ? data.PuppetMaxHp : character.PuppetMaxHp);
        // 空傀儡池至少恢复 1 血。
        var puppetHp = Math.Clamp(data.PuppetHp, 1, puppetMax);

        ActiveForm = PuppeteerForm.Puppet;
        ActivatedGroups = PerformChord.None;

        PuppeteerRunState.Modify(_player, d =>
        {
            // 离场前保存本体生命与上限，避免切换后丢失战斗内增益。
            d.HostMaxHp = Math.Max(d.HostMaxHp, creature.MaxHp);
            d.HostHp = Math.Max(0, creature.CurrentHp);
            d.ActiveForm = PuppeteerForm.Puppet;
            d.PuppetMaxHp = puppetMax;
            d.PuppetHp = puppetHp;
        });

        WriteCreatureHp(creature, puppetMax, puppetHp);

        // 傀儡形态冻结歌单，演奏仅激活分组。
        var performManager = _player.AttachedData().PerformManager;
        performManager.SetPlaylistFrozen(true);
        performManager.SetActivatedGroups(ActivatedGroups);

        // 优先播本体切出，由 NextState 接傀儡切入。
        PlayTrigger(character.HostSwitchOutTrigger is { Length: > 0 } hostSwitchOut
            ? hostSwitchOut
            : character.PuppetSwitchInTrigger);

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) host died, switch to puppet : {puppetHp}/{puppetMax}.");
        return Task.CompletedTask;
    }

    /// <summary>战斗胜利后恢复本体并解冻。</summary>
    public override Task AfterCombatVictory(CombatRoom room)
    {
        RestoreHostForm("victory");
        return Task.CompletedTask;
    }

    /// <summary>非胜利结局也恢复本体。</summary>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        RestoreHostForm("combat end");
        return Task.CompletedTask;
    }


    /// <summary>激活指定分组，空掩码使用默认组；四组齐备后复活。</summary>
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
            // 重复激活只刷新表现。
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

    /// <summary>按歌单容量恢复本体生命并解除冻结。</summary>
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
            // 离场前写回傀儡池。
            d.PuppetHp = Math.Clamp(creature.CurrentHp, 1, Math.Max(1, d.PuppetMaxHp));
        });

        WriteCreatureHp(creature, hostMax, reviveHp);

        performManager.SetPlaylistFrozen(false);
        performManager.SetActivatedGroups(PerformChord.None);

        PlayTrigger(character.PuppetSwitchOutTrigger);

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) all groups activated, host revived : " +
            $"{reviveHp}/{hostMax} (capacity {performManager.Capacity}).");
    }


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
        // 傀儡死亡是失败结算，不复活。
        if (creature.IsDead) return;

        var data = PuppeteerRunState.Get(_player);
        var wasPuppet = ActiveForm == PuppeteerForm.Puppet || data.ActiveForm == PuppeteerForm.Puppet;
        // 离场前写回当前形态生命池。
        var puppetHp = wasPuppet ? Math.Max(1, creature.CurrentHp) : data.PuppetHp;

        // Creature 只装当前形态，另一形态从 RunData 读取。
        var hostMax = wasPuppet
            ? (data.HostMaxHp > 0 ? data.HostMaxHp : Math.Max(1, creature.MaxHp))
            : Math.Max(1, creature.MaxHp);
        var hostHp = wasPuppet
            ? Math.Clamp(data.HostHp, 1, hostMax)
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
            // 多个结束钩子可能连续到达，仅首次需要切换。
            return;
        }

        WriteCreatureHp(creature, hostMax, hostHp);

        var performManager = _player.AttachedData().PerformManager;
        performManager.SetPlaylistFrozen(false);
        performManager.SetActivatedGroups(PerformChord.None);

        if (wasPuppet)
        {
            PlayTrigger(PuppeteerCharacter!.PuppetSwitchOutTrigger);
        }

        BangDreamInSpireCore.Logger.Info(
            $"Player {_player.Character} ({_player.NetId}) restore host form ({reason}) : {hostHp}/{hostMax}.");
    }

    /// <summary>先写上限再写当前生命，避免被旧上限截断。</summary>
    private static void WriteCreatureHp(Creature creature, int maxHp, int currentHp)
    {
        creature.SetMaxHpInternal(maxHp);
        creature.SetCurrentHpInternal(currentHp);
    }

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

    /// <summary>未指定分组时使用方案默认组。</summary>
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

        // 销毁队列中的节点不再触发动画。
        if (_player?.Creature.GetCreatureNode() is { } node && GodotObject.IsInstanceValid(node))
        {
            node.SetAnimationTrigger(trigger);
        }
    }

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
