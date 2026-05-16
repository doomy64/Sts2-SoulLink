using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace SoulLink.Code.Shops;

[HarmonyPatch]
public static class MerchantHandler
{
    public enum ShopSlot
    {
        Card,
        Relic,
        Potion,
        Removal,
        MAX
    }

    public static List<List<List<int>>> ShopPrices = new();
    //Different than each players' current gold, as this will account for items they are currently forced to buy
    public static List<int> EffectiveGold = new();
    public static List<Tuple<ShopSlot, int>> ForcedBuys = new ();
    public static Color FadedColor = new Color(0.7f, 0.3f, 0.3f, 0.7f);
    public static Color NormalColor = Colors.White;
    
    public static int GetPrice(int player, ShopSlot slot, int index)
    {
        
        if (player >= ShopPrices.Count)
            return -1;

        List<List<int>> slots = ShopPrices[player];
        if ((int)slot >= slots.Count)
            return -1;
        
        List<int> prices = slots[(int)slot];
        if (index >= prices.Count)
            return -1;

        return prices[index];
    }

    public static bool CanAfford(int player, ShopSlot slot, int index)
    {
        if (player >= EffectiveGold.Count)
            return false;
        
        return EffectiveGold[player] >= GetPrice(player, slot, index);
    }

    public static bool CanAfford(ShopSlot slot, int index)
    {
        for (int player = 0; player < SoulLinkHelpers.GetAllPlayers().Count; player++)
        {
            if (!CanAfford(player, slot, index))
                return false;
        }

        return true;
    }

    public static void Purchase(ShopSlot slot, int index)
    {
        if (!CanAfford(slot, index))
            return;
        
        for (int player = 0; player < SoulLinkHelpers.GetAllPlayers().Count; player++)
        {
            int price = GetPrice(player, slot, index);
            if (price >= 0)
                EffectiveGold[player] -= price;
            
            ShopPrices[player][(int)slot][index] = -1;
        }
    }

    [HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom.AfterRoomIsLoaded))]
    [HarmonyPostfix]
    public static void EnterMerchantRoomPatch(NMerchantRoom __instance)
    {
        Player? me = SoulLinkHelpers.GetLocalPlayer();
        if (me == null)
        {
            SoulLink.Logger.Error("Failed to find local player");
            return;
        }
        
        List<Player> players = SoulLinkHelpers.GetAllPlayers();
        EffectiveGold.Clear();
        for (int player = 0; player < players.Count; player++)
        {
            EffectiveGold.Add(players[player].Gold);
            List<List<int>> prices = new List<List<int>>();
            for (int slot = 0; slot < (int)ShopSlot.MAX; slot++)
                prices.Add(new List<int>());

            ShopPrices.Add(prices);
        }
        
        MerchantInventory? inventory = __instance.Inventory.Inventory;
        if (inventory == null)
            return;
        
        List<int> currentCards = new List<int>();
        inventory.CardEntries.Do(c =>
        {
            currentCards.Add(c.Cost);
            c.PurchaseCompleted += OnPurchase;
        });

        List<int> currentRelics = new List<int>();
        inventory.RelicEntries.Do(r =>
        {
            currentRelics.Add(r.Cost);
            r.PurchaseCompleted += OnPurchase;
        });
        
        List<int> currentPotions = new List<int>();
        inventory.PotionEntries.Do(p =>
        {
            currentPotions.Add(p.Cost);
            p.PurchaseCompleted += OnPurchase;
        });

        int removal = -1;
        if (inventory.CardRemovalEntry != null)
        {
            removal = inventory.CardRemovalEntry.Cost;
            inventory.CardRemovalEntry.PurchaseCompleted += OnPurchase;
        }

        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
            new SoulLinkPriceSyncAction(me, currentCards, currentRelics, currentPotions, removal));
    }

    private static Tuple<ShopSlot, int> GetEntryIndex(MerchantEntry entry)
    {
        Tuple<ShopSlot, int> nullResult = new Tuple<ShopSlot, int>(ShopSlot.MAX, -1);
        NMerchantRoom? room = NMerchantRoom.Instance;
        if (room == null)
        {
            SoulLink.Logger.Error("Attempted to purchased a item outside of a merchant");
            return nullResult;
        }

        MerchantInventory? inventory = room.Inventory.Inventory;
        if (inventory == null)
        {
            SoulLink.Logger.Error("Attempted to purchased an item in non-existent inventory");
            return nullResult;
        }

        ShopSlot slot = ShopSlot.MAX;
        int index = -1;
        
        switch (entry)
        {
            case MerchantCardEntry cardEntry:
                slot = ShopSlot.Card;
                index = inventory.CardEntries.ToList().IndexOf(cardEntry);
                break;
            case MerchantRelicEntry relicEntry:
                slot = ShopSlot.Relic;
                index = inventory.RelicEntries.ToList().IndexOf(relicEntry);
                break;
            case MerchantPotionEntry potionEntry:
                slot = ShopSlot.Potion;
                index = inventory.PotionEntries.ToList().IndexOf(potionEntry);
                break;
            case MerchantCardRemovalEntry removalEntry:
                slot = ShopSlot.Removal;
                index = 0;
                break;
        }
        if (index == -1)
        {
            SoulLink.Logger.Error("Purchased item that doesn't exist in inventory");
            return nullResult;
        }

        return new Tuple<ShopSlot, int>(slot, index);
    }

    private static void OnPurchase(PurchaseStatus status, MerchantEntry entry)
    {
        if (status != PurchaseStatus.Success)
            return;
        
        Tuple<ShopSlot, int> entryIndex = GetEntryIndex(entry);

        if (entryIndex.Item1 == ShopSlot.MAX || entryIndex.Item2 == -1)
            return;

        ShopSlot slot = entryIndex.Item1;
        int index = entryIndex.Item2;

        Player? me = SoulLinkHelpers.GetLocalPlayer();
        if (me == null)
        {
            SoulLink.Logger.Error("Failed to find local player in OnSuccessfulPurchase");
            return;
        }

        int forceIndex = ForcedBuys.FindIndex(t => t.Item1 == slot && t.Item2 == index);
        if (forceIndex != -1)
        {
            ForcedBuys.RemoveAt(forceIndex);
            Update();
        }
    }

    [HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
    [HarmonyPrefix]
    public static bool TryPurchasePatch(MerchantEntry __instance, Task<bool> __result, MerchantInventory? inventory,
        bool ignoreCost)
    {
        Tuple<ShopSlot, int> entryIndex = GetEntryIndex(__instance);
        int forceIndex = ForcedBuys.FindIndex(t => t.Item1 == entryIndex.Item1 && t.Item2 == entryIndex.Item2);
        if (forceIndex != -1)
            return true;

        if (__instance.IsStocked && __instance.EnoughGold && !ignoreCost)
        {
            
            Player? me = SoulLinkHelpers.GetLocalPlayer();
            if (me == null)
            {
                SoulLink.Logger.Error("Failed to find local player in OnSuccessfulPurchase");
                return true;
            }
            
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SoulLinkPurchaseAction(me, entryIndex.Item1, entryIndex.Item2));
            __result = new Task<bool>(() => false);
            return false;
        }

        return true;
    }
    
    [HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.EnoughGold), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool EnoughGoldPatch(MerchantEntry __instance, ref bool __result)
    {
        NMerchantRoom? room = NMerchantRoom.Instance;
        if (room == null)
            return true;

        MerchantInventory? inventory = room.Inventory.Inventory;
        if (inventory == null)
            return true;
        
        int index = -1;
        ShopSlot slot = ShopSlot.MAX;
        switch (__instance)
        {
            case MerchantCardEntry entry:
                index = inventory.CardEntries.ToList().IndexOf(entry);
                slot = ShopSlot.Card;
                break;
            case MerchantRelicEntry entry:
                index = inventory.RelicEntries.ToList().IndexOf(entry);
                slot = ShopSlot.Relic;
                break;
            case MerchantPotionEntry entry:
                index = inventory.PotionEntries.ToList().IndexOf(entry);
                slot = ShopSlot.Potion;
                break;
            case MerchantCardRemovalEntry entry:
                index = 0;
                slot = ShopSlot.Removal;
                break;
            default:
                SoulLink.Logger.Error($"Entry of unknown type {nameof(__instance)}");
                break;
        }

        if (index < 0)
            return true;

        if (ForcedBuys.Count > 0)
        {
            __result = ForcedBuys.Any(t => t.Item1 == slot && t.Item2 == index);
            return false;
        }
        
        bool result = CanAfford(slot, index);
        if (!result)
            __result = false;
        
        return result;
    }

    public static void Update()
    {
        Player? me = SoulLinkHelpers.GetLocalPlayer();
        if (me == null)
        {
            SoulLink.Logger.Error("Failed to find local player in MerchantHandler.Update");
            return;
        }
        
        Traverse.Create(me).Field<Action>("GoldChanged").Value.Invoke();
        
        NMerchantRoom? room = NMerchantRoom.Instance;
        if (room == null)
            return;
        
        NMerchantInventory inventory = room.Inventory;
        MerchantInventory? innerInventory = inventory.Inventory;
        if (innerInventory == null)
            return;
        
        List<NMerchantSlot> slots = inventory.FindChildren("*").Where(n => n is NMerchantSlot).Cast<NMerchantSlot>().ToList();
        
        if (ForcedBuys.Count == 0)
        {
            slots.Do(slot => slot.Modulate = NormalColor);
            NMapScreen.Instance?.SetTravelEnabled(true);
            return;
        }
        
        SoulLinkHelpers.PreventTravel();
        
        foreach (NMerchantSlot slot in slots)
        {
            ShopSlot itemSlot = slot switch
            {
                NMerchantCard => ShopSlot.Card,
                NMerchantRelic => ShopSlot.Relic,
                NMerchantPotion => ShopSlot.Potion,
                NMerchantCardRemoval => ShopSlot.Removal,
                _ => ShopSlot.MAX
            };

            int index = itemSlot switch
            {
                ShopSlot.Card => innerInventory.CardEntries.ToList().IndexOf((MerchantCardEntry)slot.Entry),
                ShopSlot.Relic => innerInventory.RelicEntries.ToList().IndexOf((MerchantRelicEntry)slot.Entry),
                ShopSlot.Potion => innerInventory.PotionEntries.ToList().IndexOf((MerchantPotionEntry)slot.Entry),
                ShopSlot.Removal => 0,
                _ => -1
            };

            slot.Modulate = ForcedBuys.All(t => t.Item1 != itemSlot || t.Item2 != index) ? FadedColor : NormalColor;

        }
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetTravelEnabled))]
    [HarmonyPrefix]
    private static void TravelEnabledPatch(NMapScreen __instance, ref bool enabled)
    {
        if (NMerchantRoom.Instance != null && ForcedBuys.Count > 0)
            enabled = false;
        
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddRelicInternal))]
    [HarmonyPostfix]
    private static void AddRelicPatch(Player __instance, RelicModel relic, int index, bool silent)
    {
        if (!(relic is TheCourier) && !(relic is MembershipCard))
            return;

        NMerchantRoom? room = NMerchantRoom.Instance;
        if (room == null)
            return;
        
        MerchantInventory? inventory = room.Inventory.Inventory;
        if (inventory == null)
            return;
        
        List<int> currentCards = new List<int>();
        inventory.CardEntries.Do(c =>
        {
            currentCards.Add(c.IsStocked ? c.Cost : -1);
        });

        List<int> currentRelics = new List<int>();
        inventory.RelicEntries.Do(r =>
        {
            currentRelics.Add(r.IsStocked ? r.Cost : -1);
        });
        
        List<int> currentPotions = new List<int>();
        inventory.PotionEntries.Do(p =>
        {
            currentPotions.Add(p.IsStocked ? p.Cost : -1);
        });

        int removal = -1;
        if (inventory.CardRemovalEntry != null && inventory.CardRemovalEntry.IsStocked)
        {
            removal = inventory.CardRemovalEntry.Cost;
        }

        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
            new SoulLinkPriceSyncAction(__instance, currentCards, currentRelics, currentPotions, removal));

    }
}