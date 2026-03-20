using System.Runtime.InteropServices.JavaScript;
using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code;

[HarmonyPatch(typeof(NCardRewardSelectionScreen))]
public class NCardRewardSelectionScreenPatch
{
    public static Color LockedColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
    public static Array<int> RewardQueue = new Array<int>();
    public static int ForcedChoice = -1;

    public static Texture2D ChainTexture;
    //Hovering and/or clicking on cards reorders their position in the scene
    //Cache the positions when the options are created to reliably reflect what slot was clicked
    public static List<NCardHolder> origOrder = new List<NCardHolder>();

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
        
        SoulLink.Logger.Info("Linking choice index: " + index);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkCardRewardAction(me, index));
        
    }

    public static void UpdateScreen(NCardRewardSelectionScreen screen)
    {
        foreach (NCardHolder card in origOrder)
        {
            UnlockCard(card);
        }
        
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
            chain.Name = "ChainTexture";
            chain.Scale = new Vector2(0.5f, 0.5f);
            node.CardNode.AddChild(chain);
            
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
                
                Node? chainTexture = node.CardNode.FindChild("ChainTexture");
                if (chainTexture != null)
                {
                    chainTexture.QueueFree();
                }
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

        if (completionSource != null && afterSelected != PostAlternateCardRewardAction.DoNothing && ForcedChoice == -1)
        {
            completionSource.SetResult(new Tuple<IEnumerable<NCardHolder>, bool>(System.Array.Empty<NCardHolder>(), true));
            Player? me = SoulLinkHelpers.GetLocalPlayer();
            if (me != null)
            {
                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkCardRewardAction(me, -2)); 
            }
        }

        if (afterSelected != PostAlternateCardRewardAction.DoNothing)
        {
            ForcedChoice = -1;
        }
        
        Control? cardRow = __instance.GetNode<Control>("UI/CardRow");
        if (cardRow == null)
        {
            SoulLink.Logger.Error("Failed to find CardRow in NCardRewardSelectionScreen");
            return;
        }

        foreach (NCardHolder card in cardRow.GetChildren().OfType<NCardHolder>())
        {
            UnlockCard(card);
        }
    }
    
    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch("AfterRoomEntered")]
    [HarmonyPrefix]
    private static void AfterRoomEnteredHook(IRunState runState, AbstractRoom room)
    {
        ForcedChoice = -1;
        RewardQueue.Clear();
    }
}