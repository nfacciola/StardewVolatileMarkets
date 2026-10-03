namespace VolatileMarkets
{
    /// <summary>The mod configuration, loaded from <c>config.json</c>.</summary>
    internal sealed class ModConfig
    {
        /// <summary>Whether prices are randomized at all.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>The lowest yearly price multiplier an item can roll.</summary>
        public float MinMultiplier { get; set; } = 0.5f;

        /// <summary>The highest yearly price multiplier an item can roll.</summary>
        public float MaxMultiplier { get; set; } = 1.5f;

        /// <summary>How far an artisan good's own roll can move it away from its ingredient's multiplier (0.15 = ±15%).</summary>
        public float ArtisanSpread { get; set; } = 0.15f;

        /// <summary>Whether to log a price comparison (and write a CSV) on the first day of each season.</summary>
        public bool LogPricesAtSeasonStart { get; set; } = true;
    }
}
