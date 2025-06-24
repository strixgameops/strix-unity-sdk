using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;
using System.Diagnostics;

public class EntitiesExample : MonoBehaviour
{
    /// <summary>
    /// Example method to get imaginary shield's config parameter
    /// </summary>
    public void GetEntityConfigValues()
    {
        // Get config itself
        EntityConfig config = Entities.GetEntityConfig("shield", "shieldConfig");

        // Get its params
        var durability = Entities.GetConfigValue(config, "durability");
        var itemLevel = Entities.GetConfigValue(config, "itemLevel");

        // Get config value without auto-conversion (returns raw base64 for media files)
        var rawIcon = Entities.GetConfigValue(config, "icon", false);
    }

    /// <summary>
    /// Method to check if this entity is a currency (may be useful in some cases)
    /// </summary>
    public bool CheckIsCurrency()
    {
        Entity entity = Entities.GetEntityById("gems");
        return entity.IsCurrency;
    }

    /// <summary>
    /// Example of working with entity hierarchies
    /// </summary>
    public void EntityHierarchyExample()
    {
        // Get all entities
        Entity[] allEntities = Entities.GetAllEntities();

        // Get children of a category entity
        Entity[] children = Entities.GetEntityChildren("weaponsCategory");

        // Get parent of an entity
        Entity parent = Entities.GetEntityParent("sword");

        foreach (var child in children)
        {
            Debug.Log($"Child entity: {child.Id}");
        }
    }

    /// <summary>
    /// Example of working with entity configs
    /// </summary>
    public void EntityConfigsExample()
    {
        // Get all configs for an entity
        EntityConfig[] allConfigs = Entities.GetAllEntityConfigs("weapon");

        foreach (var config in allConfigs)
        {
            Debug.Log($"Config: {config.Id}");

            // Process config values
            var damage = Entities.GetConfigValue(config, "damage");
            var range = Entities.GetConfigValue(config, "range");

            Debug.Log($"Damage: {damage}, Range: {range}");
        }
    }

    /// <summary>
    /// Example of working with different config value types
    /// </summary>
    public void ConfigValueTypesExample()
    {
        EntityConfig config = Entities.GetEntityConfig("character", "characterStats");

        if (config != null)
        {
            // String value
            var name = Entities.GetConfigValue(config, "characterName");

            // Number value
            var health = Entities.GetConfigValue(config, "maxHealth");

            // Boolean value
            var isUnlocked = Entities.GetConfigValue(config, "isUnlocked");

            // Image value (auto-converted to Texture2D)
            var portrait = Entities.GetConfigValue(config, "portrait") as Texture2D;

            // Localized text
            var description = Entities.GetConfigValue(config, "description");

            Debug.Log($"Character: {name}, Health: {health}, Unlocked: {isUnlocked}");
        }
    }
}