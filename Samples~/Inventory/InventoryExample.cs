using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;
using StrixSDK.Runtime.Models;
using System;
using System.Diagnostics;

public class InventoryExample : MonoBehaviour
{
    public async void GetInventoryItems()
    {
        // Gets player's inventory from the server. Only async method
        List<InventoryItem> items = await Inventory.GetInventoryItems();

        foreach (var item in items)
        {
            Debug.Log($"Item: {item.EntityId}, Amount: {item.Amount}");
        }
    }

    public async void GetItemsAmount()
    {
        // Returned type is string and needs to be converted to a desired data type.
        // This is made for really big numbers that regular int32/64 cannot contain
        string stringifiedAmount = await Inventory.GetInventoryItemAmount("myItem");

        // Convert to int if needed
        if (int.TryParse(stringifiedAmount, out int amount))
        {
            Debug.Log($"Player has {amount} of myItem");
        }
    }

    public async void ItemsOperations()
    {
        // Bool indicates the ending of the operation on a server-side.
        // It is unlikely to get "false" unless there is a problem on Strix's side.
        bool success1 = await Inventory.AddInventoryItem("myItem", 5);
        bool success2 = await Inventory.RemoveInventoryItem("myItem", 5);

        if (success1)
        {
            Debug.Log("Successfully added 5 myItem to inventory");
        }

        if (success2)
        {
            Debug.Log("Successfully removed 5 myItem from inventory");
        }
    }

    public async void AdvancedInventoryOperations()
    {
        // Check if player has enough items before removing
        string currentAmountStr = await Inventory.GetInventoryItemAmount("coins");
        if (int.TryParse(currentAmountStr, out int currentAmount))
        {
            int costAmount = 100;
            if (currentAmount >= costAmount)
            {
                bool success = await Inventory.RemoveInventoryItem("coins", costAmount);
                if (success)
                {
                    Debug.Log($"Successfully spent {costAmount} coins");
                    // Give reward
                    await Inventory.AddInventoryItem("gems", 10);
                }
            }
            else
            {
                Debug.Log("Not enough coins!");
            }
        }
    }
}