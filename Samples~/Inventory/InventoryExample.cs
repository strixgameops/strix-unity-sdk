using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;
using System;

public class InventoryExample : MonoBehaviour
{
    public async void GetInventoryItems()
    {
        // Gets player's inventory from the server. Only async method
        List<InventoryItem> items = await Inventory.GetInventoryItems();
    }

    public async void GetItemsAmount()
    {
        // Returned type is string and needs to be converted to a desired data type.
        // This is made for really big numbers that regular int32/64 cannot contain
        string stringifiedAmount = await Inventory.GetInventoryItemAmount("myItem");
    }

    public async void ItemsOperations()
    {
        // Bool indicates the ending of the operation on a server-side.
        // It is unlikely to get "false" unless there is a problem on Strix's side.
        bool success1 = await Inventory.AddInventoryItem("myItem", 5);
        bool success2 = await Inventory.RemoveInventoryItem("myItem", 5);
    }
}