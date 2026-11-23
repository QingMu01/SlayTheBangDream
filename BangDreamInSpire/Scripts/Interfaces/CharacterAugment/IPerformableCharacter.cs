namespace BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

public interface IPerformableCharacter : IExtraDeckSupportCharacter
{
    /// <summary>返回等价的不可变演奏方案；每场战斗的可变容量由 <c>PerformCapacityState</c> 单独承载。</summary>
    IPerformScheme CreatePerformScheme();
}
