using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace SoulLink.Code.Shops;

public class NetSoulLinkPurchaseAction : INetAction
{
    public int buyerIndex;
    public MerchantHandler.ShopSlot slot;
    public int index;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(buyerIndex, 8);
        writer.WriteInt((int)slot, 8);
        writer.WriteInt(index, 8);
    }

    public void Deserialize(PacketReader reader)
    {
        buyerIndex = reader.ReadInt(8);
        slot = (MerchantHandler.ShopSlot)reader.ReadInt(8);
        index = reader.ReadInt(8);
    }

    public GameAction ToGameAction(Player player)
    {
        return new SoulLinkPurchaseAction(SoulLinkHelpers.GetAllPlayers()[buyerIndex], slot, index);
    }
}