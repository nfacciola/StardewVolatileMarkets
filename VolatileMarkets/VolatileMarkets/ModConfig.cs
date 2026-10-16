namespace VolatileMarkets
{
    /// <summary>The mod configuration, loaded from <c>config.json</c>.</summary>
    internal sealed class ModConfig
    {
        /// <summary>Whether prices are randomized at all.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>The lowest daily price multiplier anything can end up with.</summary>
        public float MinMultiplier { get; set; } = 0.2f;

        /// <summary>The highest daily price multiplier anything can end up with.</summary>
        public float MaxMultiplier { get; set; } = 4f;

        /// <summary>How far the whole-market roll can move every price on a day (0.25 = between ÷1.25 and ×1.25).</summary>
        public float MarketSpread { get; set; } = 0.25f;

        /// <summary>How far a category's roll (crops, minerals, fish, and so on) can move its prices on a day (0.5 = between ÷1.5 and ×1.5).</summary>
        public float CategorySpread { get; set; } = 0.5f;

        /// <summary>How far an individual item's own roll can move its price on a day (1 = between ÷2 and ×2).</summary>
        public float ItemSpread { get; set; } = 1f;

        /// <summary>How far an artisan good's own roll can move it away from its ingredient's multiplier (0.25 = between ÷1.25 and ×1.25).</summary>
        public float ArtisanSpread { get; set; } = 0.25f;

        /// <summary>Whether shop prices are randomized, including tool upgrades and the traveling cart.</summary>
        public bool RandomizeShops { get; set; } = true;

        /// <summary>Whether the gold and material cost of Robin's buildings is randomized.</summary>
        public bool RandomizeBuildings { get; set; } = true;

        /// <summary>Whether the price of buying and selling farm animals is randomized.</summary>
        public bool RandomizeAnimals { get; set; } = true;

        /// <summary>Whether to log a price comparison (and write a CSV) on the first day of each season.</summary>
        public bool LogPricesAtSeasonStart { get; set; } = true;
    }
}
