using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
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

    public static void PreventTravel()
    {
        Player? me = GetLocalPlayer();
        if (me == null)
            return;

        NMapScreen? map = NMapScreen.Instance;
        if (map == null)
            return;
        
        map.SetTravelEnabled(false);

        MapVote? vote = RunManager.Instance.MapSelectionSynchronizer.GetVote(me);
        ref MapVote? local = ref vote;
        MapCoord? oldCoord = local.HasValue ? new MapCoord?(local.GetValueOrDefault().coord) : null;
        Traverse.Create(map).Method("OnPlayerVoteChangedInternal", [typeof(Player), typeof(MapCoord?), typeof(MapCoord?)])
            .GetValue(me, oldCoord, null);
        
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new VoteForMapCoordAction(
            me, new RunLocation(me.RunState.CurrentMapCoord, me.RunState.CurrentActIndex), null));
    }
}