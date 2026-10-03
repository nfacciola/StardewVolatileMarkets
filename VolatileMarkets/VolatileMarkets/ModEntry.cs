using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Shops;
using VolatileMarkets.Patches;

namespace VolatileMarkets
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        /// <summary>The mod configuration.</summary>
        private ModConfig Config = null!;

        /// <summary>The source of all price multipliers.</summary>
        private PriceBook Book = null!;

        /// <summary>Logs price comparisons.</summary>
        private PriceLogger Logger = null!;

        /// <summary>The year the shop prices were last built for.</summary>
        private int ShopYear;


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.Book = new PriceBook(this.Config);
            this.Logger = new PriceLogger(this.Monitor, this.Book, Path.Combine(helper.DirectoryPath, "PriceLogs"));

            SellPricePatch.Book = this.Book;
            new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);

            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            helper.ConsoleCommands.Add("vm_prices", "Logs this year's prices compared to vanilla and writes a CSV.\n\nUsage: vm_prices [year]", this.OnPricesCommand);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Raised when an asset is being requested. Adds this year's multiplier to every shop item with a fixed ID.</summary>
        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!this.Config.Enabled || !e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
                return;

            e.Edit(asset =>
            {
                if (!this.Book.Active)
                    return;

                int year = this.Book.CurrentYear;
                foreach (ShopData shop in asset.AsDictionary<string, ShopData>().Data.Values)
                {
                    foreach (ShopItemData entry in shop.Items)
                    {
                        string? id = entry.ItemId;
                        if (string.IsNullOrWhiteSpace(id) || id.Contains(' ') || !ItemRegistry.IsQualifiedItemId(ItemRegistry.QualifyItemId(id)))
                            continue;

                        entry.PriceModifiers ??= new List<QuantityModifier>();
                        entry.PriceModifiers.Add(new QuantityModifier
                        {
                            Id = this.ModManifest.UniqueID,
                            Modification = QuantityModifier.ModificationType.Multiply,
                            Amount = this.Book.GetMultiplier(ItemRegistry.QualifyItemId(id), year)
                        });
                    }
                }
            });
        }

        /// <summary>Raised after the player loads a save.</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Book.Active = this.Config.Enabled;
            this.RefreshShops();
        }

        /// <summary>Raised after the game returns to the title screen.</summary>
        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.Book.Active = false;
            this.Helper.GameContent.InvalidateCache("Data/Shops");
        }

        /// <summary>Raised after a new day starts.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!this.Book.Active)
                return;

            if (Game1.year != this.ShopYear)
                this.RefreshShops();

            if (this.Config.LogPricesAtSeasonStart && Game1.dayOfMonth == 1)
                this.Logger.Log(Game1.year, Game1.currentSeason);
        }

        /// <summary>Rebuild the shop data so shop prices use the current year's multipliers.</summary>
        private void RefreshShops()
        {
            this.ShopYear = Game1.year;
            this.Helper.GameContent.InvalidateCache("Data/Shops");
        }

        /// <summary>Handle the <c>vm_prices</c> console command.</summary>
        private void OnPricesCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int year = Game1.year;
            if (args.Length > 0 && (!int.TryParse(args[0], out year) || year < 1))
            {
                this.Monitor.Log("The year must be a positive number.", LogLevel.Warn);
                return;
            }

            this.Logger.Log(year, year == Game1.year ? Game1.currentSeason : "preview");
        }
    }
}
