using BangDreamLib.Scripts.Utils.Infos;

namespace BangDreamLib.Scripts.Interfaces;

public interface IVfxEffectHandler
{
    Task OnSpawn(VfxContext context) => Task.CompletedTask;
    Task OnBeforeHit(VfxContext context) => Task.CompletedTask;
    Task OnHit(VfxContext context) => Task.CompletedTask;
    Task OnAfterHit(VfxContext context) => Task.CompletedTask;
    Task OnFinish(VfxContext context) => Task.CompletedTask;
}