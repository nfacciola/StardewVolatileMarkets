using System.Text;
using StardewValley;
using SObject = StardewValley.Object;

namespace VolatileMarkets
{
    /// <summary>The single source of truth for price multipliers. Every multiplier is a pure function of the save, the year, and the item, so nothing needs to be persisted.</summary>
    internal sealed class PriceBook
    {
        /*********
        ** Fields
        *********/
        /// <summary>The mod configuration.</summary>
        private readonly ModConfig Config;


        /*********
        ** Accessors
        *********/
        /// <summary>Whether a save is loaded and multipliers should be applied.</summary>
        public bool Active { get; set; }

        /// <summary>A year to use instead of the current game year, for previewing other years.</summary>
        public int? YearOverride { get; set; }

        /// <summary>The year multipliers are currently computed for.</summary>
        public int CurrentYear => this.YearOverride ?? Game1.year;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="config">The mod configuration.</param>
        public PriceBook(ModConfig config)
        {
            this.Config = config;
        }

        /// <summary>Get the yearly multiplier for a plain item.</summary>
        /// <param name="qualifiedItemId">The qualified item ID.</param>
        /// <param name="year">The year to roll for.</param>
        public float GetMultiplier(string qualifiedItemId, int year)
        {
            return this.Roll($"item|{qualifiedItemId}", year, this.Config.MinMultiplier, this.Config.MaxMultiplier);
        }

        /// <summary>Get the multiplier for an item as it is sold. Artisan goods inherit their ingredient's multiplier and add their own smaller spread.</summary>
        /// <param name="item">The item being sold.</param>
        /// <param name="year">The year to roll for.</param>
        public float GetSellMultiplier(SObject item, int year)
        {
            string? ingredientId = item.preservedParentSheetIndex.Value;
            if (string.IsNullOrEmpty(ingredientId))
                return this.GetMultiplier(item.QualifiedItemId, year);

            string qualifiedIngredientId = ItemRegistry.QualifyItemId(ingredientId) ?? ingredientId;
            float spread = this.Config.ArtisanSpread;
            float own = this.Roll($"artisan|{item.QualifiedItemId}|{qualifiedIngredientId}", year, 1 - spread, 1 + spread);

            float combined = this.GetMultiplier(qualifiedIngredientId, year) * own;
            return Math.Clamp(combined, this.Config.MinMultiplier, this.Config.MaxMultiplier + 0.25f);
        }

        /// <summary>Scale a vanilla sell price for the current year.</summary>
        /// <param name="item">The item being sold.</param>
        /// <param name="vanillaPrice">The vanilla price.</param>
        public int ApplySell(SObject item, int vanillaPrice)
        {
            if (vanillaPrice <= 0)
                return vanillaPrice;

            float multiplier = this.GetSellMultiplier(item, this.CurrentYear);
            return Math.Max(1, (int)Math.Round(vanillaPrice * multiplier));
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Roll a value between two bounds. The average of two uniform rolls is used so results cluster around the middle and the economy stays roughly neutral.</summary>
        private float Roll(string key, int year, float min, float max)
        {
            Random random = new(StableSeed($"{Game1.uniqueIDForThisGame}|{year}|{key}"));
            double t = (random.NextDouble() + random.NextDouble()) / 2;
            return (float)(min + t * (max - min));
        }

        /// <summary>Get a seed that is stable across processes (unlike <see cref="string.GetHashCode()"/>).</summary>
        private static int StableSeed(string text)
        {
            ulong hash = 14695981039346656037UL; // FNV-1a
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return (int)(hash ^ (hash >> 32));
        }
    }
}
