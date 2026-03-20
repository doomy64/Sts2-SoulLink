using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code;

public class SoulLinkCardRewardAction(Player player, int index) : GameAction
{
    public override ulong OwnerId => player.NetId;

    public override GameActionType ActionType => GameActionType.NonCombat;

    protected override Task ExecuteAction()
    {
        if (SoulLinkHelpers.GetLocalPlayer() == player)
        {
            return Task.CompletedTask;
        }
        
        SoulLink.Logger.Info("Next card selection choice: " + index);
        
        IScreenContext? currentScreen = NOverlayStack.Instance?.Peek();
        if (currentScreen is NCardRewardSelectionScreen screen && NCardRewardSelectionScreenPatch.ForcedChoice == -1)
        {
            NCardRewardSelectionScreenPatch.ForcedChoice = index;
            NCardRewardSelectionScreenPatch.UpdateScreen(screen);
        }
        else
        {
            NCardRewardSelectionScreenPatch.RewardQueue.Add(index);
        }
        
        return Task.CompletedTask;
    }

    public override INetAction ToNetAction()
    {
        return new NetSoulLinkCardRewardAction()
        {
            giverIndex = SoulLinkHelpers.GetPlayerIndex(player),
            cardIndex = index
        };
    }
    
}