using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code.Cards;

[HarmonyPatch(typeof(NCardRewardSelectionScreen))]
public static class CardRewardHandler
{
    public static Color LockedColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
    public static List<Tuple<int, int>> RewardQueue = new List<Tuple<int, int>>();
    public static int ForcedChoice = -1;

    //Hovering and/or clicking on cards reorders their position in the scene
    //Cache the positions when the options are created to reliably reflect what slot was clicked
    public static List<NCardHolder> OrigOrder = new List<NCardHolder>();
    public static List<NRewardButton> CardRewards = new List<NRewardButton>();
    public static int CurrentPack = -1;
    private static List<Sprite2D> LoadedChains = new List<Sprite2D>();
    public static Texture2D ChainTexture;
    
    public static void Init()
    {
        ChainTexture = GD.Load<Texture2D>("res://images/SoulLink/Chains.png");
    }

    [HarmonyPatch("RefreshOptions")]
    [HarmonyPrefix]
    private static void RefreshOptionsPrefix(NCardRewardSelectionScreen __instance,
        IReadOnlyList<CardCreationResult> options, ref IReadOnlyList<CardRewardAlternative> extraOptions)
    {
        if (RewardQueue.Any(t => t.Item1 == CurrentPack && t.Item2 >= 0 && t.Item2 < options.Count))
        {
            List<CardRewardAlternative> keepOptions = new List<CardRewardAlternative>();
            keepOptions.AddRange(extraOptions.Where(e => e.AfterSelected == PostAlternateCardRewardAction.DoNothing));
            extraOptions = keepOptions;
        }
    }
    
    [HarmonyPatch("RefreshOptions")]
    [HarmonyPostfix]
    private static void RefreshOptionsPostfix(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> extraOptions)
    {
        Control? cardRow = __instance.GetNode<Control>("UI/CardRow");
        if (cardRow == null)
        {
            SoulLink.Logger.Error("Failed to find CardRow in NCardRewardSelectionScreen");
            return;
        }
        OrigOrder.Clear();
        OrigOrder = cardRow.GetChildren().OfType<NCardHolder>().ToList();

        int forceIndex = RewardQueue.FindIndex(t => t.Item1 == CurrentPack);
        if (forceIndex != -1)
        {
            ForcedChoice = RewardQueue[forceIndex].Item2;
            UpdateScreen(__instance);
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch("SelectCard")]
    private static void SelectCardPostfix(NCardRewardSelectionScreen __instance, NCardHolder cardHolder)
    {
        if (ForcedChoice != -1)
        {
            RewardQueue.RemoveAll(t => t.Item1 == CurrentPack);
            ForcedChoice = -1;
            CurrentPack = -1;
            Cleanup();
            return;
        }
        
        Player? me = SoulLinkHelpers.GetLocalPlayer();
        if (me == null){
            SoulLink.Logger.Error("Failed to find local player");
            return;
        }

        int index = OrigOrder.IndexOf(cardHolder);

        if (index == -1)
        {
            SoulLink.Logger.Error("Selected card holder does not exist in original cache");
            return;
        }
        
        SoulLink.Logger.Debug("Linking choice index: " + index);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkCardRewardAction(me, CurrentPack, index));
        CurrentPack = -1;
    }

    public static void UpdateScreen(NCardRewardSelectionScreen screen)
    {
        Cleanup();
        
        if (ForcedChoice == -1)
            return;
        
        for (int index = 0; index < OrigOrder.Count; index++)
        {
            if (index != ForcedChoice)
            {
                LockCard(OrigOrder[index]);
            }
        }

    }

    private static void LockCard(NCardHolder node)
    {
        node.SetBlockSignals(true);
        node.SetClickable(false);
        if (node.CardNode != null)
        {
            Sprite2D chain = new Sprite2D();
            chain.Texture = ChainTexture;
            chain.Scale = new Vector2(0.5f, 0.5f);
            node.CardNode.AddChild(chain);
            LoadedChains.Add(chain);
            
            node.CardNode.Modulate = LockedColor;
            node.CardNode.Scale *= 0.75f;
        }
        //TODO VFX
    }

    private static void UnlockCard(NCardHolder node)
    {
        if (node.IsBlockingSignals())
        {
            node.SetBlockSignals(false);
            node.SetClickable(true);
            if (node.CardNode != null)
            {
                node.CardNode.Modulate = Colors.White;
                node.CardNode.Scale /= 0.75f;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnAlternateRewardSelected")]
    private static void AlternateRewardPrefix(NCardRewardSelectionScreen __instance, ref PostAlternateCardRewardAction afterSelected)
    {
        if (afterSelected == PostAlternateCardRewardAction.DismissScreenAndKeepReward && ForcedChoice < -1)
            afterSelected = PostAlternateCardRewardAction.DismissScreenAndRemoveReward;
        
        if (afterSelected == PostAlternateCardRewardAction.DismissScreenAndRemoveReward)
        {
            if (ForcedChoice == -1)
            {
                Player? me = SoulLinkHelpers.GetLocalPlayer();
                if (me != null && ForcedChoice == -1)
                    RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkCardRewardAction(me, CurrentPack, -2));
            }
            else if (ForcedChoice < -1)
                RewardQueue.RemoveAll(t => t.Item1 == CurrentPack);
        }
        
        if (afterSelected != PostAlternateCardRewardAction.DoNothing)
        {
            ForcedChoice = -1;
            CurrentPack = -1;
        }
        
        Cleanup();
    }

    private static void Cleanup()
    {
        foreach (NCardHolder card in OrigOrder)
        {
            UnlockCard(card);
        }
        
        foreach (Sprite2D chain in LoadedChains)
        {
            chain.QueueFree();
        }
        LoadedChains.Clear();
        if (RewardQueue.Count <= 0)
            NMapScreen.Instance?.SetTravelEnabled(true);
    }

    [HarmonyPatch(typeof(NRewardsScreen))]
    [HarmonyPatch("TryEnableProceedButton")]
    [HarmonyPrefix]
    private static bool TryEnableProceedPrefix(NRewardsScreen __instance)
    {
        return RewardQueue.Count <= 0;
    }
    
    [HarmonyPatch(typeof(NRewardsScreen))]
    [HarmonyPatch("SetRewards")]
    [HarmonyPostfix]
    private static void SetRewardsPostfix(NRewardsScreen __instance, IEnumerable<Reward> rewards)
    {
        CardRewards.Clear();
        List<Control> rewardButtons = Traverse.Create(__instance).Field<List<Control>>("_rewardButtons").Value;
        CardRewards.AddRange(rewardButtons.Where(c => c is NRewardButton { Reward: CardReward}).Cast<NRewardButton>());
    }

    [HarmonyPatch(typeof(NRewardButton), "OnRelease")]
    [HarmonyPrefix]
    private static void EnterRewardPatch(NRewardButton __instance)
    {
        //Even if this isn't a card reward, we would want CurrentPack to be -1 anyway
        CurrentPack = CardRewards.IndexOf(__instance);
    }
    
    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("AfterRoomEntered")]
    [HarmonyPrefix]
    private static void AfterRoomEnteredHook(IRunState runState, AbstractRoom room)
    {
        ForcedChoice = -1;
        RewardQueue.Clear();
        CardRewards.Clear();
        Cleanup();
    }
}