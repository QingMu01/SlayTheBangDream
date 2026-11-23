using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Mutsumi.Attack;

/// <summary>
/// 脱手位移并向随机敌人异步发射音符；伤害不阻塞出牌，演出按生物串行。
/// </summary>
public class Waltz() : AbstractMutsumiCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(4),
        QuickVar.Repeat.Create(2)
    ];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var screen = ScreenSize();
        var rise = -screen.Y * 0.35f;
        var side = screen.X * 0.25f;
        var repeat = DynamicVars.Repeat.IntValue;
        var damage = DynamicVars.Damage.BaseValue;

        var steps = new List<VisualsAnimStep>
        {
            VisualsAnimExtensions.Move(new Vector2(0f, rise), 0.5f),
            VisualsAnimExtensions.Teleport(new Vector2(side, rise)),
            VisualsAnimExtensions.Move(new Vector2(side, 0f), 0.5f),
            VisualsAnimExtensions.Wait(0.5f)
        };

        for (var i = 0; i < repeat; i++)
        {
            // 对应 STS1 FlipProcess（rotateY 转一圈），不是左右翻面。
            steps.Add(VisualsAnimExtensions.Spin(1, 0.5f));
            steps.Add(VisualsAnimExtensions.Callback(async _ => await this.ShootMusicNotes(1, 0, damage)));
        }

        // 归位后强制重置位置与朝向，避免残留偏移。
        steps.Add(VisualsAnimExtensions.Move(Vector2.Zero, 0.5f));
        steps.Add(VisualsAnimExtensions.Reset());

        Owner.Creature.PlayVisualsAnim([.. steps]);

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }

    private static Vector2 ScreenSize()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            var size = tree.Root.GetVisibleRect().Size;
            if (size.X > 0f && size.Y > 0f)
                return size;
        }

        return new Vector2(1920f, 1080f);
    }
}
