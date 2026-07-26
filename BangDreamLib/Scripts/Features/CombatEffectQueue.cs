namespace BangDreamLib.Scripts.Features;

public sealed class CombatEffectQueue
{
    private readonly Lock _lock = new();
    
    private Task _tail = Task.CompletedTask;

    public static CombatEffectQueue Shared { get; } = new();

    public Task Enqueue(Func<Task> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        lock (_lock)
        {
            _tail = ExecuteAfterAsync(_tail, effect);
            return _tail;
        }
    }

    private static async Task ExecuteAfterAsync(Task previous, Func<Task> effect)
    {
        try
        {
            await previous;
            await effect();
        }
        catch (Exception e)
        {
            BangDreamLibCore.Logger.Error($"Combat effect queue error: {e}");
        }
    }
}
