using System.Globalization;
using System.Text;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using VolatileMarkets.Patches;
using SObject = StardewValley.Object;

namespace VolatileMarkets
{
    /// <summary>Logs how this year's prices compare to vanilla, so a price change can be verified at a glance.</summary>
    internal sealed class PriceLogger
    {
        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly PriceBook Book;
        private readonly string OutputFolder;

        /// <summary>One row of the price comparison.</summary>
        private record Row(string Id, string Name, int VanillaPrice, int NewPrice, float Multiplier, float? PreviousMultiplier);


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
        /// <param name="year">The year to show prices for.</param>
        /// <param name="label">A label for the log line and file name, like the season.</param>
        public void Log(int year, string label)
        {
            int? previousOverride = this.Book.YearOverride;
            this.Book.YearOverride = year;
            try
            {
                List<Row> rows = this.BuildRows(year);
                string path = this.WriteCsv(rows, year, label);

                this.Monitor.Log($"Prices for year {year}, {label} (vanilla -> now). Top 10 sell prices:", LogLevel.Info);
                foreach (Row row in rows.OrderByDescending(r => r.NewPrice).Take(10))
                {
                    string last = row.PreviousMultiplier.HasValue ? $", last year x{row.PreviousMultiplier.Value:0.00}" : "";
                    this.Monitor.Log($"  {row.Name}: {row.VanillaPrice}g -> {row.NewPrice}g (x{row.Multiplier:0.00}{last})", LogLevel.Info);
                }
                this.Monitor.Log($"Full comparison of {rows.Count} items written to {path}", LogLevel.Info);
            }
            finally
            {
                this.Book.YearOverride = previousOverride;
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Build a row for every sellable object, plus the wines, jellies, juices and pickles made from each fruit and vegetable.</summary>
        private List<Row> BuildRows(int year)
        {
            var rows = new List<Row>();
            ObjectDataDefinition objects = ItemRegistry.GetObjectTypeDefinition();

            foreach (string id in Game1.objectData.Keys)
            {
                SObject? item = this.TryCreate(() => ItemRegistry.Create<SObject>("(O)" + id));
                if (item is null)
                    continue;

                this.AddRow(rows, item, year);

                if (item.Category == SObject.FruitsCategory)
                {
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredWine(item)), year);
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredJelly(item)), year);
                }
                else if (item.Category == SObject.VegetableCategory)
                {
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredJuice(item)), year);
                    this.AddRow(rows, this.TryCreate(() => objects.CreateFlavoredPickle(item)), year);
                }
            }

            return rows;
        }

        /// <summary>Add a row for an item if it can be sold.</summary>
        private void AddRow(List<Row> rows, SObject? item, int year)
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
                Multiplier: this.Book.GetSellMultiplier(item, year),
                PreviousMultiplier: year > 1 ? this.Book.GetSellMultiplier(item, year - 1) : null
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
        private string WriteCsv(List<Row> rows, int year, string label)
        {
            Directory.CreateDirectory(this.OutputFolder);
            string path = Path.Combine(this.OutputFolder, $"year{year}_{label}.csv");

            var csv = new StringBuilder("ItemId,Name,VanillaPrice,NewPrice,Multiplier,PreviousYearMultiplier\n");
            foreach (Row row in rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                csv.Append(row.Id).Append(',')
                    .Append('"').Append(row.Name.Replace("\"", "\"\"")).Append("\",")
                    .Append(row.VanillaPrice).Append(',')
                    .Append(row.NewPrice).Append(',')
                    .Append(row.Multiplier.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.PreviousMultiplier?.ToString("0.000", CultureInfo.InvariantCulture) ?? "")
                    .Append('\n');
            }

            File.WriteAllText(path, csv.ToString());
            return path;
        }
    }
}
