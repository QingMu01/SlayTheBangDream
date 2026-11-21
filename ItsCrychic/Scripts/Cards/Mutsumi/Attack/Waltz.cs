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
/// 华尔兹：一段脱手的角色位移演出，位移过程中反复向敌人发射音符。
/// <para>
/// 演出通过 <see cref="VisualsAnimExtensions" /> 脱手并按生物串行，
/// 伤害通过 <see cref="MusicNoteExtensions" /> 异步结算，不阻塞出牌。
/// </para>
/// <para>
/// 与 STS1 原版《华尔兹》对全体敌人直接结算的 Cleave 不同，本牌向随机目标齐射音符。
/// 演出编排（上移 → 传送到敌方一侧上方 → 下移 → 每次命中前自转一圈 → 归位）
/// 与 `FlipProcess`（rotateY 360°）保持一致。
/// </para>
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
            // STS1 原版每次命中前是 FlipProcess（rotateY 转一圈），不是左右翻面。
            steps.Add(VisualsAnimExtensions.Spin(1, 0.5f));
            steps.Add(VisualsAnimExtensions.Callback(async _ => await this.ShootMusicNotes(1, 0, damage)));
        }

        // 先缓动归位，再强制对齐到基准位置与基准朝向（避免残留偏移）。
        steps.Add(VisualsAnimExtensions.Move(Vector2.Zero, 0.5f));
        steps.Add(VisualsAnimExtensions.Reset());

        // 脱手：演出在动作队列之外推进，出牌动作立即结束，玩家可以继续出牌。
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
