using System.Reflection;
using HarmonyLib;
using StardewValley;
using SObject = StardewValley.Object;

namespace VolatileMarkets.Patches
{
    /// <summary>Scales what the player is paid for an item. Patching at sale time (instead of editing <c>Data/Objects</c>) also covers crafted goods whose price was baked in when they were made.</summary>
    /// <remarks>Both the base <see cref="Item"/> method (used by weapons, hats, rings, and so on) and the <see cref="SObject"/> override are patched. The override doesn't call the base method, so a price is never scaled twice.</remarks>
    [HarmonyPatch]
    internal static class SellPricePatch
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The price book to use.</summary>
        public static PriceBook? Book { get; set; }

        /// <summary>Whether the patch is bypassed, so callers can read the vanilla price.</summary>
        public static bool Suspended { get; set; }


        /*********
        ** Public methods
        *********/
        /// <summary>Get an item's sell price without any randomization.</summary>
        public static int GetVanillaPrice(Item item)
        {
            bool previous = Suspended;
            Suspended = true;
            try
            {
                return item.sellToStorePrice();
            }
            finally
            {
                Suspended = previous;
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get the methods to patch.</summary>
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] args = { typeof(long) };
            yield return AccessTools.DeclaredMethod(typeof(Item), nameof(Item.sellToStorePrice), args);
            yield return AccessTools.DeclaredMethod(typeof(SObject), nameof(SObject.sellToStorePrice), args);
        }

        /// <summary>The method called after <see cref="Item.sellToStorePrice"/>.</summary>
        private static void Postfix(Item __instance, ref int __result)
        {
            if (Suspended || Book is not { Active: true })
                return;

            __result = Book.ApplySell(__instance, __result);
        }
    }
}
