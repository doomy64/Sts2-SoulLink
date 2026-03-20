using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code;

[HarmonyPatch(typeof(EventSynchronizer))]
public class EventSoulLinkHandler
{
    private static bool InsideButtonPress = false;
    [HarmonyPatch("IsShared", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool SynchronizerIsSharedPatch(NEventRoom __instance, ref bool __result)
    {
        __result = true;
        return false;
    }
    
    [HarmonyPatch("BeginEvent")]
    [HarmonyPrefix]
    private static void BeginEventPatch(EventSynchronizer __instance, EventModel canonicalEvent, bool isPrefinished, Action<EventModel>? debugOnStart)
    {
        Traverse.Create(canonicalEvent).Field("<IsShared>k__BackingField").SetValue(true);
    }

    [HarmonyPatch(typeof(NEventRoom))]
    [HarmonyPatch("OptionButtonClicked")]
    [HarmonyPrefix]
    private static void OptionButtonClickedPrefix(NEventRoom __instance, EventOption option, int index)
    {
        InsideButtonPress = true;
    }
    
    [HarmonyPatch(typeof(NEventRoom))]
    [HarmonyPatch("OptionButtonClicked")]
    [HarmonyPostfix]
    private static void OptionButtonClickedPostfix(NEventRoom __instance, EventOption option, int index)
    {
        InsideButtonPress = false;
    }

    [HarmonyPatch(typeof(NEventLayout))]
    [HarmonyPatch("ClearOptions")]
    [HarmonyPrefix]
    private static bool ClearOptionsPrefix(NEventLayout __instance)
    {
        return !InsideButtonPress;
    }
    
}