using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace SoulLink.Code.Shops;

public class SoulLinkPriceSyncAction(
    Player player,
    List<int> cardPrices,
    List<int> relicPrices,
    List<int> potionPrices,
    int removalPrice)
    : GameAction
{
    public override ulong OwnerId => player.NetId;
    public override GameActionType ActionType => GameActionType.NonCombat;


    protected override Task ExecuteAction()
    {
        int playerIndex = SoulLinkHelpers.GetPlayerIndex(player);
        MerchantHandler.ShopPrices[playerIndex].Clear();
        MerchantHandler.ShopPrices[playerIndex].Add(cardPrices);
        MerchantHandler.ShopPrices[playerIndex].Add(relicPrices);
        MerchantHandler.ShopPrices[playerIndex].Add(potionPrices);
        List<int> removal = new List<int>();
        removal.Add(removalPrice);
        MerchantHandler.ShopPrices[playerIndex].Add(removal);
        MerchantHandler.Update();
        return Task.CompletedTask;
    }

    public override INetAction ToNetAction()
    {
        return new NetSoulLinkPriceSyncAction()
        {
            playerIndex = SoulLinkHelpers.GetPlayerIndex(player),
            cardPrices = cardPrices,
            relicPrices = relicPrices,
            potionPrices = potionPrices,
            removalPrice = removalPrice,
        };
    }
    
}