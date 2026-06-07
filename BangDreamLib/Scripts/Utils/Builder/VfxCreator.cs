using BangDreamLib.Scripts.Nodes.VFX;
using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace BangDreamLib.Scripts.Utils.Builder;

public class VfxCreator<T>(string scene)
    where T : NBangDreamVfx
{
    private PackedScene Scene { get; } = PreloadManager.Cache.GetScene(scene);

    public T Create(Action<T>? configure = null)
    {
        var vfx = Scene.Instantiate<T>();
        configure?.Invoke(vfx);
        return vfx;
    }

    public IEnumerable<T> CreateBatch(int count, Action<T, int>? configure = null)
    {
        for (var i = 0; i < count; i++)
        {
            var vfx = Scene.Instantiate<T>();
            vfx.Context.Set("total", count);
            vfx.Context.Set("index", i);
            configure?.Invoke(vfx, i);
            yield return vfx;
        }
    }
}