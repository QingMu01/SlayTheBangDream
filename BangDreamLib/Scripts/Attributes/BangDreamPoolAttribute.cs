namespace BangDreamLib.Scripts.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class BangDreamPoolAttribute(Type pool) : Attribute
{
    public Type Pool { get; } = pool;
}