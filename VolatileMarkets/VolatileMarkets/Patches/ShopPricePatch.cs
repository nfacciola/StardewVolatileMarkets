using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;

namespace VolatileMarkets.Patches
{
    /// <summary>Scales the gold price of everything a shop sells once its menu is built. Patching the menu (instead of editing <c>Data/Shops</c>) covers items with no fixed ID, tool upgrades, and the traveling cart, and means prices can change every day without reloading any game data.</summary>
    [HarmonyPatch]
    internal static class ShopPricePatch
    {
        /*********
        ** Fields
        *********/
        /// <summary>The price each stock entry had before it was scaled, so scaling the same entry twice can't compound.</summary>
        private static readonly ConditionalWeakTable<ItemStockInformation, StrongBox<int>> BasePrices = new();


        /*********
        ** Accessors
        *********/
        /// <summary>The price book to use.</summary>
        public static PriceBook? Book { get; set; }


        /*********
        ** Private methods
        *********/
        /// <summary>Get the methods to patch: every <see cref="ShopMenu"/> constructor.</summary>
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredConstructors(typeof(ShopMenu), searchForStatic: false);
        }

        /// <summary>The method called after a <see cref="ShopMenu"/> is constructed.</summary>
        private static void Postfix(ShopMenu __instance)
        {
            // currency 0 is gold; star tokens, Qi coins, and Qi gems are priced separately
            if (Book is not { ShopsActive: true } || __instance.currency != 0)
                return;

            foreach ((ISalable item, ItemStockInformation stock) in __instance.itemPriceAndStock)
            {
                // sold items bought back keep the price the player was paid
                if (__instance.buyBackItems.Contains(item))
                    continue;

                if (!BasePrices.TryGetValue(stock, out StrongBox<int>? basePrice))
                {
                    basePrice = new StrongBox<int>(stock.Price);
                    BasePrices.Add(stock, basePrice);
                }

                if (basePrice.Value > 0)
                    stock.Price = Book.ApplyShop(item, basePrice.Value);
            }
        }
    }
}
