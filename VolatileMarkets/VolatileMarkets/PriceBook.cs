using System.Text;
using StardewValley;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace VolatileMarkets
{
    /// <summary>The single source of truth for price multipliers. Every multiplier is a pure function of the save, the day, and the item, so nothing needs to be persisted.</summary>
    /// <remarks>
    /// A multiplier is built from three layers that are rolled fresh each day and multiplied together:
    /// <list type="number">
    /// <item><description>a <b>market</b> roll shared by everything,</description></item>
    /// <item><description>a <b>category</b> roll shared by everything in the same category (so crops can boom while fish crash), and</description></item>
    /// <item><description>an <b>item</b> roll unique to the item.</description></item>
    /// </list>
    /// Each layer is uniform in log space, so a ×2 swing is exactly as likely as a ÷2 swing.
    /// </remarks>
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

        /// <summary>A day (as a <see cref="WorldDate.TotalDays"/> value) to use instead of the current game day, for previewing other days.</summary>
        public int? DayOverride { get; set; }

        /// <summary>The day multipliers are currently computed for.</summary>
        public int CurrentDay => this.DayOverride ?? Game1.Date.TotalDays;

        /// <summary>Whether shop prices should currently be randomized.</summary>
        public bool ShopsActive => this.Active && this.Config.RandomizeShops;

        /// <summary>Whether building costs should currently be randomized.</summary>
        public bool BuildingsActive => this.Active && this.Config.RandomizeBuildings;

        /// <summary>Whether farm animal prices should currently be randomized.</summary>
        public bool AnimalsActive => this.Active && this.Config.RandomizeAnimals;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="config">The mod configuration.</param>
        public PriceBook(ModConfig config)
        {
            this.Config = config;
        }

        /// <summary>Get the multiplier that applies to the whole market on a day, for display.</summary>
        /// <param name="day">The day to roll for.</param>
        public float GetMarketMultiplier(int day)
        {
            return MathF.Exp(this.Layer(day, "market", this.Config.MarketSpread));
        }

        /// <summary>Get the multiplier for a plain item.</summary>
        /// <param name="itemKey">A key identifying the item.</param>
        /// <param name="category">The item's category, which groups items that rise and fall together.</param>
        /// <param name="day">The day to roll for.</param>
        public float GetMultiplier(string itemKey, int category, int day)
        {
            return this.Combine(day, $"item|{itemKey}", $"category|{category}");
        }

        /// <summary>Get the multiplier for something that isn't an item, like a building or farm animal.</summary>
        /// <param name="kind">What kind of thing it is. Things of the same kind rise and fall together.</param>
        /// <param name="id">The thing's ID.</param>
        /// <param name="day">The day to roll for.</param>
        public float GetFlatMultiplier(string kind, string id, int day)
        {
            return this.Combine(day, $"{kind}|{id}", $"kind|{kind}");
        }

        /// <summary>Get the multiplier for an item as it is bought or sold. Artisan goods inherit their ingredient's multiplier and add their own smaller spread.</summary>
        /// <param name="item">The item.</param>
        /// <param name="day">The day to roll for.</param>
        public float GetItemMultiplier(Item item, int day)
        {
            string? ingredientId = (item as SObject)?.preservedParentSheetIndex.Value;
            if (string.IsNullOrEmpty(ingredientId))
                return this.GetMultiplier(GetItemKey(item), item.Category, day);

            string qualifiedIngredientId = ItemRegistry.QualifyItemId(ingredientId) ?? ingredientId;
            int ingredientCategory = ItemRegistry.GetData(qualifiedIngredientId)?.Category ?? 0;
            float own = MathF.Exp(this.Layer(day, $"artisan|{item.QualifiedItemId}|{qualifiedIngredientId}", this.Config.ArtisanSpread));

            return this.Clamp(this.GetMultiplier(qualifiedIngredientId, ingredientCategory, day) * own);
        }

        /// <summary>Scale a vanilla sell price for the current day.</summary>
        /// <param name="item">The item being sold.</param>
        /// <param name="vanillaPrice">The vanilla price.</param>
        public int ApplySell(Item item, int vanillaPrice)
        {
            return Scale(vanillaPrice, this.GetItemMultiplier(item, this.CurrentDay));
        }

        /// <summary>Scale a vanilla shop price for the current day.</summary>
        /// <param name="item">The item for sale.</param>
        /// <param name="vanillaPrice">The vanilla price.</param>
        public int ApplyShop(ISalable item, int vanillaPrice)
        {
            return item is Item real
                ? Scale(vanillaPrice, this.GetItemMultiplier(real, this.CurrentDay))
                : vanillaPrice;
        }

        /// <summary>Scale a vanilla amount by a multiplier, never dropping a positive amount below 1.</summary>
        /// <param name="vanilla">The vanilla amount.</param>
        /// <param name="multiplier">The multiplier.</param>
        public static int Scale(int vanilla, float multiplier)
        {
            if (vanilla <= 0)
                return vanilla;
            return Math.Max(1, (int)Math.Round(vanilla * multiplier));
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get a key identifying an item. Tools are keyed by upgrade level too, since each upgrade is a separate purchase.</summary>
        private static string GetItemKey(Item item)
        {
            return item is Tool tool
                ? $"{item.QualifiedItemId}|{tool.UpgradeLevel}"
                : item.QualifiedItemId;
        }

        /// <summary>Roll the three layers for a day and combine them.</summary>
        private float Combine(int day, string itemKey, string categoryKey)
        {
            double log =
                this.Layer(day, "market", this.Config.MarketSpread)
                + this.Layer(day, categoryKey, this.Config.CategorySpread)
                + this.Layer(day, itemKey, this.Config.ItemSpread);
            return this.Clamp((float)(Math.Exp(log) / this.MeanBias));
        }

        /// <summary>The average of the unadjusted multiplier. A layer that's symmetric in log space has a mean above 1 (<c>sinh(a) / a</c> for a layer spanning ±<c>a</c>), so dividing by this keeps the economy roughly neutral instead of drifting richer than vanilla.</summary>
        private double MeanBias => new[] { this.Config.MarketSpread, this.Config.CategorySpread, this.Config.ItemSpread }
            .Where(spread => spread > 0)
            .Select(spread => Math.Log(1 + spread))
            .Aggregate(1d, (bias, a) => bias * Math.Sinh(a) / a);

        /// <summary>Keep a multiplier within the configured bounds.</summary>
        private float Clamp(float multiplier)
        {
            return Math.Clamp(multiplier, this.Config.MinMultiplier, this.Config.MaxMultiplier);
        }

        /// <summary>Roll one layer, returned as a natural log. It's uniform between <c>-ln(1 + spread)</c> and <c>+ln(1 + spread)</c>, so the multiplier ranges evenly (in ratio terms) from <c>1 / (1 + spread)</c> to <c>1 + spread</c>.</summary>
        private float Layer(int day, string key, float spread)
        {
            if (spread <= 0)
                return 0;

            Random random = new(StableSeed($"{Game1.uniqueIDForThisGame}|{day}|{key}"));
            double t = random.NextDouble() * 2 - 1;
            return (float)(t * Math.Log(1 + spread));
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
