namespace BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

/// <summary>傀儡师角色契约，提供生命上限与形态动作；状态由 PuppeteerRunData 保存。</summary>
public interface IPuppeteerCharacter : IPerformableCharacter
{
    /// <summary>傀儡最大生命。</summary>
    int PuppetMaxHp { get; }

    /// <summary>傀儡切入动作。</summary>
    string PuppetSwitchInTrigger { get; }

    /// <summary>傀儡切出动作。</summary>
    string PuppetSwitchOutTrigger { get; }

    /// <summary>本体切出动作；空串时直接播傀儡切入。</summary>
    string HostSwitchOutTrigger => "";

    /// <summary>傀儡待机动作。</summary>
    string PuppetIdleTrigger { get; }

    /// <summary>傀儡死亡动作，空串使用通用动作。</summary>
    string PuppetDieTrigger { get; }
}
