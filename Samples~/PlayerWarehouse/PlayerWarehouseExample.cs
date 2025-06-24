using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;
using System;

public class PlayerWarehouseExample : MonoBehaviour
{
    public async void SimpleGetElementValue()
    {
        // Returns current value that is stored on this client
        object value = PlayerWarehouse.GetPlayerElementValue("lose_streak");
        if (value != null)
        {
            value = Convert.ToInt32(value);
        }

        // Returns current actual value from the server (slower and must be awaited)
        object valueAsync = await PlayerWarehouse.GetPlayerElementValueAsync("lose_streak");
        if (valueAsync != null)
        {
            valueAsync = Convert.ToInt32(valueAsync);
        }
    }

    public void SimpleSetElementValue()
    {
        object newValue = PlayerWarehouse.SetPlayerElementValue("lose_streak", 3);
    }

    public void SimpleNumericElementValue()
    {
        // AddPlayerElementValue/SubtractPlayerElementValue can conceptually only be used for numeric elements, so though it receives "object" type,
        // it can virtually be either float or integer.
        object newValue1 = PlayerWarehouse.AddPlayerElementValue("lose_streak", 1);
        object newValue2 = PlayerWarehouse.SubtractPlayerElementValue("lose_streak", 1);
    }
}