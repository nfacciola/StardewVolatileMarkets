using HarmonyLib;
using SObject = StardewValley.Object;

namespace VolatileMarkets.Patches
{
    /// <summary>Scales what the player is paid for an object. Patching at sale time (instead of editing <c>Data/Objects</c>) also covers crafted goods whose price was baked in when they were made.</summary>
    [HarmonyPatch(typeof(SObject), nameof(SObject.sellToStorePrice), new[] { typeof(long) })]
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
        ** Private methods
        *********/
        /// <summary>The method called after <see cref="SObject.sellToStorePrice"/>.</summary>
        private static void Postfix(SObject __instance, ref int __result)
        {
            if (Suspended || Book is not { Active: true })
                return;

            __result = Book.ApplySell(__instance, __result);
        }

        /// <summary>Get an object's sell price without any randomization.</summary>
        public static int GetVanillaPrice(SObject item)
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
    }
}
