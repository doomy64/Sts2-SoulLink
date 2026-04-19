using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace SoulLink.Code.Cards;

public class SoulLinkCardRewardAction(Player player, int packIndex, int cardIndex) : GameAction
{
    public override ulong OwnerId => player.NetId;

    public override GameActionType ActionType => GameActionType.NonCombat;

    protected override Task ExecuteAction()
    {
        if (CardRewardHandler.RewardQueue.Any(t => t.Item1 == packIndex))
            return Task.CompletedTask;
        
        SoulLink.Logger.Debug($"Received card selection choice for Pack: {packIndex} Card: {cardIndex} ");
        
        IScreenContext? currentScreen = NOverlayStack.Instance?.Peek();
        if (currentScreen is NCardRewardSelectionScreen screen && CardRewardHandler.CurrentPack == packIndex)
        {
            CardRewardHandler.ForcedChoice = cardIndex;
            CardRewardHandler.UpdateScreen(screen);
        }
        if (packIndex >= CardRewardHandler.CardRewards.Count || packIndex == -1) 
            return Task.CompletedTask;
        
        if (CardRewardHandler.CardRewards[packIndex].IsValid())
            CardRewardHandler.CardRewards[packIndex].Modulate = Colors.IndianRed;
            
        CardRewardHandler.RewardQueue.Add(new Tuple<int, int>(packIndex, cardIndex));
        SoulLinkHelpers.PreventTravel();

        return Task.CompletedTask;
    }

    public override INetAction ToNetAction()
    {
        return new NetSoulLinkCardRewardAction()
        {
            giverIndex = SoulLinkHelpers.GetPlayerIndex(player),
            packIndex = packIndex,
            cardIndex = cardIndex
        };
    }
    
}