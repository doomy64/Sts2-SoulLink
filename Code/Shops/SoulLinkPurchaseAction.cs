using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace SoulLink.Code.Shops;

public class SoulLinkPurchaseAction(Player buyer, MerchantHandler.ShopSlot slot, int index) : GameAction
{
    public override ulong OwnerId => buyer.NetId;
    public override GameActionType ActionType => GameActionType.NonCombat;
    
    protected override Task ExecuteAction()
    {
        MerchantHandler.Purchase(slot, index);
        if (SoulLinkHelpers.GetLocalPlayer() != buyer)
            MerchantHandler.ForcedBuys.Add(new Tuple<MerchantHandler.ShopSlot, int>(slot, index));

        MerchantHandler.Update();
        
        return Task.CompletedTask;
    }

    public override INetAction ToNetAction()
    {
        return new NetSoulLinkPurchaseAction()
        {
            buyerIndex = SoulLinkHelpers.GetPlayerIndex(buyer),
            slot = slot,
            index = index
        };
    }
}