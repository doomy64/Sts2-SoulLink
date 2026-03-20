using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace SoulLink.Code;

public class NetSoulLinkCardRewardAction : INetAction, IPacketSerializable
{
    public int giverIndex;
    public int cardIndex;
    
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(giverIndex, 8);
        writer.WriteInt(cardIndex, 8);
    }

    public void Deserialize(PacketReader reader)
    {
        this.giverIndex = reader.ReadInt(8);
        this.cardIndex = reader.ReadInt(8);
    }

    public GameAction ToGameAction(Player player)
    {
        return new SoulLinkCardRewardAction(player.RunState.Players[giverIndex], cardIndex);
    }
}