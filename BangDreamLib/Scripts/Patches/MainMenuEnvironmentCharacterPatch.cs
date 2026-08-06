using BangDreamLib.Scripts.Nodes.MainMenu;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

internal sealed class MainMenuEnvironmentCharacterPatch : IPatchMethod
{
    public static string PatchId => "insert_main_menu_environment_character";

    private const string CharacterNodeName = "BangDreamEnvironmentCharacter";
    private const string BlurBackBufferCopyName = "BangDreamMainMenuBlurBackBufferCopy";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMainMenu), nameof(NMainMenu._Ready))];
    }

    public static void Postfix(NMainMenu __instance, NMainMenuBg ____bg)
    {
        try
        {
            EnsureBlurBackBufferCopy(__instance);

            if (____bg.GetNodeOrNull(CharacterNodeName) != null)
                return;

            var character = PreloadKey.MainMenuEnvironmentCharacter.GetScene()
                .Instantiate<MainMenuEnvironmentCharacter>();
            character.Name = CharacterNodeName;
            ____bg.AddChildSafely(character);
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn($"Failed to insert main menu environment character: {ex}");
        }
    }

    private static void EnsureBlurBackBufferCopy(NMainMenu mainMenu)
    {
        if (mainMenu.GetNodeOrNull(BlurBackBufferCopyName) != null)
            return;

        var blurBackstop = mainMenu.GetNodeOrNull<Control>("BlurBackstop");
        if (blurBackstop == null)
            return;

        var copy = new BackBufferCopy
        {
            Name = BlurBackBufferCopyName,
            CopyMode = BackBufferCopy.CopyModeEnum.Viewport
        };
        mainMenu.AddChildSafely(copy);
        mainMenu.MoveChild(copy, blurBackstop.GetIndex());
    }
}