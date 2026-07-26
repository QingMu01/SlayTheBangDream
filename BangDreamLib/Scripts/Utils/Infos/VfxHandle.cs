using BangDreamLib.Scripts.Enums;

namespace BangDreamLib.Scripts.Utils.Infos;

public sealed class VfxHandle(VfxContext context)
{
    private readonly TaskCompletionSource<VfxResult> _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<VfxResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public VfxContext Context { get; } = context;
    public Task<VfxResult> Arrived => _arrived.Task;
    public Task<VfxResult> Finished => _finished.Task;

    internal void CompleteArrival(VfxResult result)
    {
        _arrived.TrySetResult(result);
    }

    internal void Complete(VfxResult result)
    {
        _arrived.TrySetResult(result);
        _finished.TrySetResult(result);
    }
}
