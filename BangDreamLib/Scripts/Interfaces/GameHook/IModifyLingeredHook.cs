namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface IModifyLingeredHook
{
    decimal ModifyLingeredEnergyAdd(decimal amount)
    {
        return amount;
    }

    decimal ModifyLingeredEnergyReduce(decimal amount)
    {
        return amount;
    }
}