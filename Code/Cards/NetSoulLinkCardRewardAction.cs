using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace SoulLink.Code.Cards;

public class NetSoulLinkCardRewardAction : INetAction, IPacketSerializable
{
    public int giverIndex;
    public int packIndex;
    public int cardIndex;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(giverIndex, 8);
        writer.WriteInt(packIndex, 8);
        writer.WriteInt(cardIndex);
    }

    public void Deserialize(PacketReader reader)
    {
        this.giverIndex = reader.ReadInt(8);
        this.packIndex = reader.ReadInt(8);
        this.cardIndex = reader.ReadInt();
    }

    public GameAction ToGameAction(Player player)
    {
        return new SoulLinkCardRewardAction(player.RunState.Players[giverIndex], packIndex, cardIndex);
    }
}