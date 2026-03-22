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
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code.Cards;

[HarmonyPatch(typeof(NCardRewardSelectionScreen))]
public class CardRewardHandler
{
    public static Color LockedColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
    public static Array<int> RewardQueue = new Array<int>();
    public static int ForcedChoice = -1;

    public static Texture2D ChainTexture;
    //Hovering and/or clicking on cards reorders their position in the scene
    //Cache the positions when the options are created to reliably reflect what slot was clicked
    public static List<NCardHolder> origOrder = new List<NCardHolder>();
    private static List<Sprite2D> LoadedChains = new List<Sprite2D>();
    
    public static void Init()
    {
        //ChainTexture = ImageTexture.CreateFromImage(Image.LoadFromFile("res://images/SoulLink/Chains.png"));
        ChainTexture = GD.Load<Texture2D>("res://images/SoulLink/Chains.png");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch("RefreshOptions")]
    private static void RefreshOptionsPostfix(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> extraOptions)
    {
        Control? cardRow = __instance.GetNode<Control>("UI/CardRow");
        if (cardRow == null)
        {
            SoulLink.Logger.Error("Failed to find CardRow in NCardRewardSelectionScreen");
            return;
        }
        origOrder.Clear();
        origOrder = cardRow.GetChildren().OfType<NCardHolder>().ToList();
        
        if (RewardQueue.Count > 0 && ForcedChoice == -1)
        {
            ForcedChoice = RewardQueue[0];
            RewardQueue.RemoveAt(0);
        }

        if (ForcedChoice != -1)
        {
            UpdateScreen(__instance);
        }

    }
    
    [HarmonyPrefix]
    [HarmonyPatch("SelectCard")]
    private static void SelectCardPostfix(NCardRewardSelectionScreen __instance, NCardHolder cardHolder)
    {
        if (ForcedChoice != -1)
        {
            ForcedChoice = -1;
            Cleanup();
            return;
        }
        
        Player? me = SoulLinkHelpers.GetLocalPlayer();
        if (me == null){
            SoulLink.Logger.Error("Failed to find local player");
            return;
        }

        int index = origOrder.IndexOf(cardHolder);

        if (index == -1)
        {
            SoulLink.Logger.Error("Selected card holder does not exist in original cache");
            return;
        }
        
        SoulLink.Logger.Debug("Linking choice index: " + index);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkCardRewardAction(me, index));
        
    }

    public static void UpdateScreen(NCardRewardSelectionScreen screen)
    {
        Cleanup();
        
        if (ForcedChoice == -1)
        {
            return;
        }
        
        bool allCardsLocked = true;
        for (int index = 0; index < origOrder.Count; index++)
        {
            if (index != ForcedChoice)
            {
                LockCard(origOrder[index]);
            }
            else
            {
                allCardsLocked = false;
            }
        }

        if (allCardsLocked) return;
        
        Array<Node> rewardAlternatives = screen.FindChildren("RewardAlternatives", "Control");
        foreach (Node button in rewardAlternatives)
        {
            LockButton(button as Control);
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

    private static void LockButton(Control? node)
    {
        if (node == null)
        {
            return;
        }
        node.QueueFree();
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnAlternateRewardSelected")]
    private static void AlternateRewardPrefix(NCardRewardSelectionScreen __instance, PostAlternateCardRewardAction afterSelected)
    {
        TaskCompletionSource<Tuple<IEnumerable<NCardHolder>, bool>> completionSource = Traverse.Create(__instance)
            .Field("_completionSource").GetValue<TaskCompletionSource<Tuple<IEnumerable<NCardHolder>, bool>>>();

        if (completionSource != null && afterSelected != PostAlternateCardRewardAction.DoNothing)
        {
            completionSource.SetResult(new Tuple<IEnumerable<NCardHolder>, bool>(System.Array.Empty<NCardHolder>(), true));
            Player? me = SoulLinkHelpers.GetLocalPlayer();
            if (me != null && ForcedChoice == -1)
            {
                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkCardRewardAction(me, -2)); 
            }
        }

        if (afterSelected != PostAlternateCardRewardAction.DoNothing)
        {
            ForcedChoice = -1;
        }
    }

    private static void Cleanup()
    {
        foreach (NCardHolder card in origOrder)
        {
            UnlockCard(card);
        }
        
        foreach (Sprite2D chain in LoadedChains)
        {
            chain.QueueFree();
        }
        LoadedChains.Clear();
    }

    [HarmonyPatch(typeof(NRewardsScreen))]
    [HarmonyPatch("TryEnableProceedButton")]
    [HarmonyPrefix]
    private static bool TryEnableProceedPrefix(NRewardsScreen __instance)
    {
        List<Control> rewardButtons = Traverse.Create(__instance).Field<List<Control>>("_rewardButtons").Value;
        return !rewardButtons.Any((c) => c is NRewardButton { Reward: CardReward });
    }
    
    [HarmonyPatch(typeof(NRewardsScreen))]
    [HarmonyPatch("SetRewards")]
    [HarmonyPostfix]
    private static void SetRewardsPostfix(NRewardsScreen __instance, IEnumerable<Reward> rewards)
    {
        if (rewards.Any((c) => c is CardReward))
        {
            Traverse.Create(__instance).Field<NProceedButton>("_proceedButton").Value.Disable();
        }
    }
    
    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("AfterRoomEntered")]
    [HarmonyPrefix]
    private static void AfterRoomEnteredHook(IRunState runState, AbstractRoom room)
    {
        ForcedChoice = -1;
        RewardQueue.Clear();
        Cleanup();
    }
}