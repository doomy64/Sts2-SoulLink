using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code;

[HarmonyPatch(typeof(EventSynchronizer))]
public class EventSoulLinkHandler
{
    [HarmonyPatch("IsShared", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool SynchronizerIsSharedPatch(NEventRoom __instance, ref bool __result)
    {
        __result = true;
        return false;
    }
    
    [HarmonyPatch("BeginEvent")]
    [HarmonyPrefix]
    private static void ModelIsSharedPatch(EventSynchronizer __instance, EventModel canonicalEvent, bool isPrefinished, Action<EventModel>? debugOnStart)
    {
        Traverse.Create(canonicalEvent).Field("<IsShared>k__BackingField").SetValue(true);
    }
}