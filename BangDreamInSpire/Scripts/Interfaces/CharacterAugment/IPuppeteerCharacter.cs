namespace BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

/// <summary>
/// 傀儡师角色：拥有两组独立生命池（本体 / 傀儡）与两种登场形态，
/// 傀儡形态下歌单冻结、演奏改为激活分组。
/// <para>
/// 库侧代码不出现任何具体角色名与动作名，傀儡上限与形态动作由角色实现提供；
/// 形态状态与生命池由 <c>PuppeteerRunData</c> 跨战斗保存。
/// </para>
/// </summary>
public interface IPuppeteerCharacter : IPerformableCharacter
{
    /// <summary>傀儡形态的最大生命。</summary>
    int PuppetMaxHp { get; }

    /// <summary>切到傀儡形态时播放的动作触发名（例如 <c>Doll_Switchin</c>）。</summary>
    string PuppetSwitchInTrigger { get; }

    /// <summary>切回本体形态时播放的动作触发名（例如 <c>Doll_SwitchOut</c>）。</summary>
    string PuppetSwitchOutTrigger { get; }

    /// <summary>傀儡形态的待机动作触发名（例如 <c>Doll_Idle</c>）。</summary>
    string PuppetIdleTrigger { get; }

    /// <summary>傀儡形态阵亡时要播的死亡动作（例如 <c>Doll_Die</c>）；返回空串表示沿用通用死亡动作。</summary>
    string PuppetDieTrigger { get; }
}
