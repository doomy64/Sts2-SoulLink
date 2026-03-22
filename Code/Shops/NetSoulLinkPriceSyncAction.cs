using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace SoulLink.Code.Shops;

public class NetSoulLinkPriceSyncAction : INetAction
{
    public int playerIndex;
    public List<int> cardPrices = new List<int>();
    public List<int> relicPrices = new List<int>();
    public List<int> potionPrices = new List<int>();
    public int removalPrice;

    private const int PriceSeperator = 32767;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(playerIndex, 8);
        foreach(int i in cardPrices)
            writer.WriteInt(i, 16);
        
        writer.WriteInt(PriceSeperator, 16);
        foreach(int i in relicPrices)
            writer.WriteInt(i, 16);
        
        writer.WriteInt(PriceSeperator, 16);
        foreach(int i in potionPrices)
            writer.WriteInt(i, 16);
        
        writer.WriteInt(PriceSeperator, 16);
        writer.WriteInt(removalPrice, 16);
    }

    public void Deserialize(PacketReader reader)
    {
        playerIndex = reader.ReadInt(8);
        
        cardPrices = new List<int>();
        int read = reader.ReadInt(16);
        while (read != PriceSeperator)
        {
            cardPrices.Add(read);
            read = reader.ReadInt(16);
        }

        relicPrices = new List<int>();
        read = reader.ReadInt(16);
        while (read != PriceSeperator)
        {
            relicPrices.Add(read);
            read = reader.ReadInt(16);
        }
        
        potionPrices = new List<int>();
        read = reader.ReadInt(16);
        while (read != PriceSeperator)
        {
            potionPrices.Add(read);
            read = reader.ReadInt(16);
        }
        
        removalPrice = reader.ReadInt(16);
    }

    public GameAction ToGameAction(Player player)
    {
        return new SoulLinkPriceSyncAction(player.RunState.Players[playerIndex], cardPrices, relicPrices, potionPrices, removalPrice);
    }
}