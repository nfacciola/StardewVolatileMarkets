using System.Globalization;
using System.Text;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using VolatileMarkets.Patches;
using SObject = StardewValley.Object;

namespace VolatileMarkets
{
    /// <summary>Logs how a day's prices compare to vanilla, so a price change can be verified at a glance.</summary>
    internal sealed class PriceLogger
    {
        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly PriceBook Book;
        private readonly string OutputFolder;

        /// <summary>One row of the price comparison.</summary>
        private record Row(string Id, string Name, int VanillaPrice, int NewPrice, float Multiplier, float? PreviousDayMultiplier);


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="monitor">The SMAPI monitor.</param>
        /// <param name="book">The price book.</param>
        /// <param name="outputFolder">The folder to write CSV files to.</param>
        public PriceLogger(IMonitor monitor, PriceBook book, string outputFolder)
        {
            this.Monitor = monitor;
            this.Book = book;
            this.OutputFolder = outputFolder;
        }

        /// <summary>Log a summary to the console and write the full comparison to a CSV.</summary>
        /// <param name="day">The day to show prices for, as a <see cref="WorldDate.TotalDays"/> value.</param>
        public void Log(int day)
        {
            WorldDate date = WorldDate.ForDaysPlayed(day);
            string label = $"Year {date.Year}, {date.SeasonKey} {date.DayOfMonth}";

            int? previousOverride = this.Book.DayOverride;
            this.Book.DayOverride = day;
            try
            {
                List<Row> rows = this.BuildRows(day);
                string path = this.WriteCsv(rows, date);

                this.Monitor.Log($"Prices for {label} (vanilla -> now), whole market x{this.Book.GetMarketMultiplier(day):0.00}. Top 10 sell prices:", LogLevel.Info);
                foreach (Row row in rows.OrderByDescending(r => r.NewPrice).Take(10))
                {
                    string last = row.PreviousDayMultiplier.HasValue ? $", day before x{row.PreviousDayMultiplier.Value:0.00}" : "";
                    this.Monitor.Log($"  {row.Name}: {row.VanillaPrice}g -> {row.NewPrice}g (x{row.Multiplier:0.00}{last})", LogLevel.Info);
                }
                this.Monitor.Log($"Full comparison of {rows.Count} items written to {path}", LogLevel.Info);
            }
            finally
            {
                this.Book.DayOverride = previousOverride;
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Build a row for every sellable object, plus the wines, jellies, juices and pickles made from each fruit and vegetable.</summary>
        private List<Row> BuildRows(int day)
        {
            var rows = new List<Row>();
            ObjectDataDefinition objects = ItemRegistry.GetObjectTypeDefinition();

            foreach (string id in Game1.objectData.Keys)
            {
                SObject? item = this.TryCreate(() => ItemRegistry.Create<SObject>("(O)" + id));
                if (item is null)
                    continue;

                this.AddRow(rows, item, day);

                if (item.Category == SObject.FruitsCategory)
                {
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredWine(item)), day);
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredJelly(item)), day);
                }
                else if (item.Category == SObject.VegetableCategory)
                {
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredJuice(item)), day);
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredPickle(item)), day);
                }
            }

            return rows;
        }

        /// <summary>Add a row for an item if it can be sold.</summary>
        private void AddRow(List<Row> rows, SObject? item, int day)
        {
            if (item is null)
                return;

            int vanilla = SellPricePatch.GetVanillaPrice(item);
            if (vanilla <= 0)
                return;

            rows.Add(new Row(
                Id: item.QualifiedItemId,
                Name: item.DisplayName,
                VanillaPrice: vanilla,
                NewPrice: item.sellToStorePrice(),
                Multiplier: this.Book.GetItemMultiplier(item, day),
                PreviousDayMultiplier: day > 0 ? this.Book.GetItemMultiplier(item, day - 1) : null
            ));
        }

        /// <summary>Create an item, skipping any that fail to build.</summary>
        private T? TryCreate<T>(Func<T> create) where T : class
        {
            try
            {
                return create();
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Skipped an item while building the price log: {ex.Message}", LogLevel.Trace);
                return null;
            }
        }

        /// <summary>Write the rows to a CSV and return its path.</summary>
        private string WriteCsv(List<Row> rows, WorldDate date)
        {
            Directory.CreateDirectory(this.OutputFolder);
            string path = Path.Combine(this.OutputFolder, $"year{date.Year}_{date.SeasonKey}{date.DayOfMonth:00}.csv");

            var csv = new StringBuilder("ItemId,Name,VanillaPrice,NewPrice,Multiplier,PreviousDayMultiplier\n");
            foreach (Row row in rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                csv.Append(row.Id).Append(',')
                    .Append('"').Append(row.Name.Replace("\"", "\"\"")).Append("\",")
                    .Append(row.VanillaPrice).Append(',')
                    .Append(row.NewPrice).Append(',')
                    .Append(row.Multiplier.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.PreviousDayMultiplier?.ToString("0.000", CultureInfo.InvariantCulture) ?? "")
                    .Append('\n');
            }

            File.WriteAllText(path, csv.ToString());
            return path;
        }
    }
}
