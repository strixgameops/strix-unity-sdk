using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;

public class EntitiesExample : MonoBehaviour
{
    /// <summary>
    /// Example method to get imaginary shield's config parameter
    /// </summary>
    public void GetEntityConfigValues()
    {
        // Get config itself
        EntityConfig config = Entities.GetEntityConfig("shield", "shieldConfig");

        // Get it's params
        var durability = Entities.GetConfigValue(config, "durability");
        var itemLevel = Entities.GetConfigValue(config, "itemLevel");
    }

    /// <summary>
    /// Method to check if this entity is a currency (may be useful in some cases)
    /// </summary>
    public bool CheckIsCurrency()
    {
        Entity entity = Entities.GetEntityById("gems");
        return entity.IsCurrency;
    }
}