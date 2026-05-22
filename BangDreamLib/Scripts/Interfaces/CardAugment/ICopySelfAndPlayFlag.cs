namespace BangDreamLib.Scripts.Interfaces.CardAugment;

public interface ICopySelfAndPlayFlag
{
    bool ShouldCopySelfAndPlay => false;

    bool ShouldCopySelfAndPlayOnce { get; set; }
}