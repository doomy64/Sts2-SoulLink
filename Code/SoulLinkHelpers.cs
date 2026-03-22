using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code;

public static class SoulLinkHelpers
{
    public static Player? GetLocalPlayer()
    {
        RunState state = Traverse.Create(RunManager.Instance).Property<RunState>("State").Value;
        Player? me = LocalContext.GetMe(state);
        return me;
    }

    public static List<Player> GetAllPlayers()
    {
        RunState state = Traverse.Create(RunManager.Instance).Property<RunState>("State").Value;
        return state.Players.ToList();
    }
    
    public static int GetPlayerIndex(Player? player)
    {
        return player?.RunState == null ? -1 : player.RunState.Players.ToList().IndexOf(player);
    }
}