using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BangDreamLib.Scripts.Console;

public class VfxConsoleCmd : AbstractConsoleCmd
{
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (int.TryParse(args[0], out var index))
        {
            if (index < 0 || index >= BangDreamPreloadManager.VfxAssets.Count)
                return new CmdResult(false, "Invalid index!");
            return new CmdResult(PlayVfx(index), true, "Vfx instantiated!");
        }

        return new CmdResult(false, "index must be int value!");
    }

    public override string CmdName => "vfxtest";
    public override string Args => "preload manager vfx VfxAssets index";
    public override string Description => "dev test vfx in screen center";
    public override bool IsNetworked => false;

    private static async Task PlayVfx(int index)
    {
        var vfxPath = BangDreamPreloadManager.VfxAssets.ToList()[index];
        var instantiate = PreloadManager.Cache.GetScene(vfxPath).Instantiate<Node2D>();
        instantiate.GlobalPosition = NCombatRoom.Instance?.CombatVfxContainer.GetViewportRect().GetCenter() ??
                                     new Vector2(960f, 540f);
        await Task.Delay(500);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(instantiate);
    }
}