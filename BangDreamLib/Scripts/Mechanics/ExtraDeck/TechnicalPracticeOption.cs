using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamLib.Scripts.Mechanics.ExtraDeck;

public class TechnicalPracticeOption(Player owner) : ModRestSiteOptionTemplate(owner)
{
    private IEnumerable<CardModel> _selection = [];

    public override string OptionId => "BANG_DREAM_LIB_TECHNICAL_PRACTICE";

    public override string CustomIconPath => "res://BangDreamLib/images/sceneui/technical_practice.png";

    public override LocString Description => new(
        "rest_site_ui",
        $"OPTION_{OptionId}.{(IsEnabled ? "description" : "descriptionDisabled")}"
    );

    public override bool IsEnabled => BangDreamConst.ExtraDeck.GetPile(Owner).Cards.Any(card => card.IsUpgradable);

    public override async Task<bool> OnSelect()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        _selection = await ExtraPileCmd.FromExtraDeckForUpgrade(Owner, prefs);
        var selectedCards = _selection.ToList();
        if (selectedCards.Count == 0)
            return false;

        foreach (var card in selectedCards)
        {
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        }

        await Hook.AfterRestSiteSmith(Owner.RunState, Owner);
        return true;
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardSmithVfx.Create(_selection.ToArray()));
        await Cmd.CustomScaledWait(1f, 2f, cancellationToken: ct);
    }

    public override Task DoRemotePostSelectVfx()
    {
        var parent = NRestSiteRoom.Instance?.Characters.First(character => character.Player == Owner);
        var smithVfx = NCardSmithVfx.Create();
        if (smithVfx == null)
            return Task.CompletedTask;

        parent?.AddChildSafely(smithVfx);
        smithVfx.Position = Vector2.Zero;
        return Task.CompletedTask;
    }
}