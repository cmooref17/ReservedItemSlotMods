using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using System.IO;
using BepInEx;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor;
using System.Security.Cryptography;
using DunGen;
using Unity.Netcode;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Data;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    public static class TerminalPatcher
    {
        public static Terminal terminalInstance;
        public static bool initializedTerminalNodes = false;
        public static ReservedItemSlotData purchasingItemSlot;

        [HarmonyPatch(typeof(Terminal), "Awake")]
        [HarmonyPrefix]
        public static void InitializeTerminal(Terminal __instance)
        {
            terminalInstance = __instance;
            initializedTerminalNodes = false;
            EditExistingTerminalNodes();
        }


        [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
        [HarmonyPostfix]
        public static void OnBeginUsingTerminal(Terminal __instance)
        {
            if (!initializedTerminalNodes && ConfigSync.isSynced)
                EditExistingTerminalNodes();
        }


        public static void EditExistingTerminalNodes()
        {
            initializedTerminalNodes = true;

            if (ConfigSync.instance.disablePurchasingReservedSlots)
                return;
            foreach (TerminalNode node in terminalInstance.terminalNodes.specialNodes)
            {
                if (node.name == "Start" && !node.displayText.Contains("[ReservedItemSlots]"))
                {
                    string keyword = "Type \"Help\" for a list of commands.";
                    int insertIndex = node.displayText.IndexOf(keyword);
                    if (insertIndex != -1)
                    {
                        insertIndex += keyword.Length;
                        string addText = "\n\n[ReservedItemSlots]\nType \"reserved\" to purchase reserved item slots.";
                        node.displayText = node.displayText.Insert(insertIndex, addText);
                    }
                    else
                        Debug.LogError("Failed to add reserved item slots tip to terminal. Maybe an update broke it?");
                }

                else if (node.name == "HelpCommands" && !node.displayText.Contains(">RESERVED"))
                {
                    string keyword = "[numberOfItemsOnRoute]";
                    int insertIndex = node.displayText.IndexOf(keyword);
                    if (insertIndex != -1)
                    {
                        string addText = ">RESERVED\n" +
                            "Show purchasable reserved item slots.\n\n";
                        node.displayText = node.displayText.Insert(insertIndex, addText);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPrefix]
        public static void TextPostProcess(ref string modifiedDisplayText, TerminalNode node)
        {
            if (modifiedDisplayText.Length <= 0)
                return;

            if (modifiedDisplayText.Contains("[[[reservedItemSlotsSelectionList]]]") || (modifiedDisplayText.Contains("[[[") && modifiedDisplayText.Contains("]]]")))
            {
                int index0 = modifiedDisplayText.IndexOf("[[[");
                int index1 = modifiedDisplayText.IndexOf("]]]") + 3;
                string textToReplace = modifiedDisplayText.Substring(index0, index1 - index0);
                string replacementText = "";
                if (ConfigSync.instance.disablePurchasingReservedSlots)
                    replacementText += "Every reserved item slot is unlocked!\n\n";
                else
                {
                    replacementText += "Reserved Item Slots\n------------------------------\n\n";
                    replacementText += "To purchase a reserved item slot, type the following command.\n" +
                        "> RESERVED [item_slot]\n\n";

                    int longestNameSize = 0;
                    foreach (var reservedItemSlot in SyncManager.unlockableReservedItemSlotsDict.Values)
                        longestNameSize = Mathf.Max(longestNameSize, reservedItemSlot.slotName.Length);
                    foreach (var reservedItemSlot in SyncManager.unlockableReservedItemSlotsDict.Values)
                    {
                        string priceText = (SessionManager.unlockedReservedItemSlots.Contains(reservedItemSlot) || SessionManager.pendingUnlockedReservedItemSlots.Contains(reservedItemSlot)) ? "[Purchased]" : "$" + reservedItemSlot.purchasePrice;
                        replacementText += string.Format("* {0}{1}   //   {2}\n", reservedItemSlot.slotDisplayName, new string(' ', longestNameSize - reservedItemSlot.slotDisplayName.Length), priceText);
                    }
                }
                modifiedDisplayText = modifiedDisplayText.Replace(textToReplace, replacementText);
            }
        }




        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyPrefix]
        public static bool ParsePlayerSentence(ref TerminalNode __result, Terminal __instance)
        {
            if (__instance.screenText.text.Length <= 0)
                return true;

            string input = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded).ToLower();
            string[] args = input.Split(' ');
            ReservedItemSlotData reservedItemSlot = null;

            if (!ConfigSync.isSynced)
            {
                if (input.StartsWith("reserved"))
                {
                    __result = BuildTerminalNodeHostDoesNotHaveMod();
                    return false;
                }
                else
                    return true;
            }


            if (purchasingItemSlot != null)
            {
                if ("confirm".StartsWith(input))
                {
                    if (SessionManager.unlockedReservedItemSlots.Contains(purchasingItemSlot) || SessionManager.pendingUnlockedReservedItemSlots.Contains(purchasingItemSlot))
                    {
                        Debug.LogWarning("Attempted to confirm purchase on reserved item slot that was already unlocked. Item slot: " + purchasingItemSlot.slotDisplayName);
                        __result = BuildTerminalNodeAlreadyUnlocked(purchasingItemSlot);
                    }
                    else if (terminalInstance.groupCredits < purchasingItemSlot.purchasePrice)
                    {
                        Debug.LogWarning("Attempted to confirm purchase with insufficient credits. Current credits: " + terminalInstance.groupCredits + " Required credits: " + purchasingItemSlot.purchasePrice);
                        __result = BuildTerminalNodeInsufficientFunds(purchasingItemSlot);
                    }
                    else
                    {
                        SyncManager.SendUnlockItemSlotUpdateToServer(purchasingItemSlot.slotId);
                        terminalInstance.groupCredits -= purchasingItemSlot.purchasePrice;
                        terminalInstance.SyncGroupCreditsServerRpc(terminalInstance.groupCredits, terminalInstance.numberOfItemsInDropship);
                        Debug.Log("Purchasing reserved item slot: " + purchasingItemSlot.slotDisplayName + ". Price: " + purchasingItemSlot.purchasePrice);
                        __result = BuildTerminalNodeOnPurchased(purchasingItemSlot, terminalInstance.groupCredits);
                    }
                }
                else
                {
                    Plugin.Log("Canceling order.");
                    __result = BuildCustomTerminalNode("Canceled order.\n\n");
                }
                purchasingItemSlot = null;
                return false;
            }
            purchasingItemSlot = null;


            if (args.Length == 0 || args[0] != "reserved")
                return true;


            if (args.Length == 1)
            {
                __result = BuildTerminalNodeHome();
                return false;
            }

            string itemSlotName = input.Substring(9);
            reservedItemSlot = TryGetReservedItemSlot(itemSlotName);

            if (reservedItemSlot != null)
            {
                if (SessionManager.unlockedReservedItemSlots.Contains(reservedItemSlot) || SessionManager.pendingUnlockedReservedItemSlots.Contains(reservedItemSlot))
                {
                    Plugin.LogWarning("Attempted to start purchase on reserved item slot that was already unlocked. Item slot: " + reservedItemSlot.slotName);
                    __result = BuildTerminalNodeAlreadyUnlocked(reservedItemSlot);
                }
                else if (terminalInstance.groupCredits < reservedItemSlot.purchasePrice)
                {
                    Plugin.LogWarning("Attempted to start purchase with insufficient credits. Current credits: " + terminalInstance.groupCredits + ". Item slot price: " + reservedItemSlot.purchasePrice);
                    __result = BuildTerminalNodeInsufficientFunds(reservedItemSlot);
                }
                else
                {
                    Plugin.Log("Started purchasing reserved item slot: " + reservedItemSlot.slotName);
                    purchasingItemSlot = reservedItemSlot;
                    __result = BuildTerminalNodeConfirmDenyPurchase(reservedItemSlot);
                }
                return false;
            }
            else
            {
                Plugin.LogWarning("Attempted to start purchase on invalid reserved item slot. Item slot: " + itemSlotName);
                __result = BuildTerminalNodeInvalidReservedItemSlot(itemSlotName);
                return false;
            }
        }


        static TerminalNode BuildTerminalNodeHome()
        {
            TerminalNode homeTerminalNode = new TerminalNode
            {
                displayText = "[ReservedItemSlots]\n\n" +
                    "Store\n" +
                    "------------------------------\n" +
                    "[[[reservedItemSlotsSelectionList]]]\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            
            return homeTerminalNode;
        }


        static TerminalNode BuildTerminalNodeConfirmDenyPurchase(ReservedItemSlotData itemSlotData)
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You have requested to purchase a reserved item slot for $" + itemSlotData.purchasePrice + " credits.\n" +
                "> [" + itemSlotData.slotDisplayName + "]\n\n",
                isConfirmationNode = true,
                acceptAnything = false,
                clearPreviousText = true
            };

            terminalNode.displayText += "Credit balance: $" + terminalInstance.groupCredits + "\n";
            terminalNode.displayText += "\n";
            terminalNode.displayText += "Please CONFIRM or DENY.\n\n";

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeOnPurchased(ReservedItemSlotData itemSlotData, int newGroupCredits)
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You have successfully purchased a new reserved item slot!\n" +
                "> [" + itemSlotData.slotDisplayName + "]\n\n",
                buyUnlockable = true,
                clearPreviousText = true,
                acceptAnything = false,
                playSyncedClip = 0
            };

            terminalNode.displayText += "New credit balance: $" + newGroupCredits + "\n\n";

            if (SessionManager.preGame)
                terminalNode.displayText += "This slot will become available once the game has started.\n\n";

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeAlreadyUnlocked(ReservedItemSlotData itemSlot)
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You have already purchased this reserved item slot!\n" +
                "> [" + itemSlot.slotDisplayName+ "]\n\n",
                clearPreviousText = false,
                acceptAnything = false
            };

            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeInsufficientFunds(ReservedItemSlotData itemSlot)
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You could not afford this reserved item slot!\n" +
                "> [" + itemSlot.slotDisplayName + "]\n\n" +
                "Credit balance is $" + terminalInstance.groupCredits + "\n",
                clearPreviousText = true,
                acceptAnything = false
            };

            terminalNode.displayText += "Price of reserved item slot is $" + itemSlot.purchasePrice + "\n\n";
            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeInvalidReservedItemSlot(string reservedItemSlotName = "")
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "Reserved item slot does not exist.",
                clearPreviousText = false,
                acceptAnything = false
            };
            if (reservedItemSlotName != "")
                terminalNode.displayText += ("\n\"" + reservedItemSlotName + "\"");
            terminalNode.displayText += "\n";
            return terminalNode;
        }


        static TerminalNode BuildTerminalNodeHostDoesNotHaveMod(string itemSlotName = "")
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = "You cannot use the reserved item slot commands until you have synced with the host.\n\n" +
                    "You may also be seeing this because the host does not have this mod.\n\n",
                clearPreviousText = true,
                acceptAnything = false
            };
            if (itemSlotName != "")
                terminalNode.displayText += ("\n\"" + itemSlotName + "\"");
            terminalNode.displayText += "\n";
            return terminalNode;
        }


        static TerminalNode BuildCustomTerminalNode(string displayText, bool clearPreviousText = false, bool acceptAnything = false, bool isConfirmationNode = false)
        {
            TerminalNode terminalNode = new TerminalNode
            {
                displayText = displayText,
                clearPreviousText = clearPreviousText,
                acceptAnything = false,
                isConfirmationNode = isConfirmationNode
            };
            return terminalNode;
        }


        static ReservedItemSlotData TryGetReservedItemSlot(string itemSlotNameInput)
        {
            ReservedItemSlotData getItemSlot = null;
            foreach (var reservedItemSlot in SyncManager.unlockableReservedItemSlots)
            {
                string slotName = reservedItemSlot.slotDisplayName.ToLower();
                if (itemSlotNameInput == slotName || (itemSlotNameInput.Length >= 4 && slotName.StartsWith(itemSlotNameInput)))
                {
                    if ((getItemSlot == null || slotName.Length < getItemSlot.slotDisplayName.Length) && !"the company".StartsWith(itemSlotNameInput) && !"company".StartsWith(itemSlotNameInput))
                        getItemSlot = reservedItemSlot;
                }
            }
            return getItemSlot;
        }
    }
}