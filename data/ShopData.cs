using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;

namespace FAUNA
{
    /// <summary>
    /// Defines a single familiar sold in a shop.
    /// </summary>
    public class FamiliarShopEntry
    {
        // Which familiar species is sold
        public string FamiliarId { get; set; } = "";

        // Purchase price
        public int Price { get; set; } = 5000;

        // Optional unlock condition
        public string RequiredMailFlag { get; set; } = "";

        // Optional seasonal restriction
        public string Season { get; set; } = "";

        // Hidden until unlocked?
        public bool HiddenUntilUnlocked { get; set; } = false;
        
        // GSQ condition that must pass for this entry to appear
        public string Condition { get; set; } = "";
    }

    /// <summary>
    /// Defines a familiar shop.
    /// </summary>
    public class FamiliarShopData
    {
        // Unique shop ID
        public string ShopId { get; set; } = "";

        // Display name shown in menu
        public string DisplayName { get; set; } = "Familiar Shop";

        // Optional NPC owner
        // Example: "Vael"
        public string ShopOwner { get; set; } = "";

        // Optional location
        // Example: "Custom_AetherShop"
        public string ShopLocation { get; set; } = "";

        // If true, framework fallback shop is used
        public bool UseDefaultAccess { get; set; } = true;

        // All familiars sold here
        public List<FamiliarShopEntry> Stock { get; set; } = new();
    }

    /// <summary>
    /// Central registry for all familiar shops.
    /// </summary>
    public static class FamiliarShopRegistry
    {
        private static readonly Dictionary<string, FamiliarShopData> Shops = new();

        /// <summary>
        /// Register a familiar shop.
        /// </summary>
        public static void RegisterShop(FamiliarShopData shop)
        {
            if (string.IsNullOrWhiteSpace(shop.ShopId))
                return;

            Shops[shop.ShopId] = shop;
        }

        /// <summary>
        /// Get a shop by ID.
        /// </summary>
        public static FamiliarShopData? GetShop(string shopId)
{
    Shops.TryGetValue(shopId, out FamiliarShopData? shop);
    return shop;
}

        /// <summary>
        /// Get all registered shops.
        /// </summary>
        public static List<FamiliarShopData> GetAllShops()
        {
            return Shops.Values.ToList();
        }

        /// <summary>
        /// Does a shop exist?
        /// </summary>
        public static bool HasShop(string shopId)
        {
            return Shops.ContainsKey(shopId);
        }
    }
}