using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.FarmAnimals;
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

        /// <summary>The day the building and animal prices were last built for.</summary>
        private int DataDay = -1;


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
            ShopPricePatch.Book = this.Book;
            new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);

            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            helper.ConsoleCommands.Add("vm_prices", "Logs a day's prices compared to vanilla and writes a CSV.\n\nUsage: vm_prices [dayOffset]\nThe offset is in days from today (0 = today, 1 = tomorrow, -1 = yesterday).", this.OnPricesCommand);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Raised when an asset is being requested. Scales the cost of buildings and the price of farm animals for the current day.</summary>
        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!this.Config.Enabled)
                return;

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
                e.Edit(this.EditBuildings, AssetEditPriority.Late);
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/FarmAnimals"))
                e.Edit(this.EditFarmAnimals, AssetEditPriority.Late);
        }

        /// <summary>Scale the gold and material cost of every building.</summary>
        private void EditBuildings(IAssetData asset)
        {
            if (!this.Book.BuildingsActive)
                return;

            int day = this.Book.CurrentDay;
            foreach ((string id, BuildingData building) in asset.AsDictionary<string, BuildingData>().Data)
            {
                building.BuildCost = PriceBook.Scale(building.BuildCost, this.Book.GetFlatMultiplier("building", id, day));

                foreach (BuildingMaterial material in building.BuildMaterials ?? new List<BuildingMaterial>())
                    material.Amount = PriceBook.Scale(material.Amount, this.Book.GetFlatMultiplier("buildingMaterial", $"{id}|{material.ItemId}", day));
            }
        }

        /// <summary>Scale what farm animals cost to buy and fetch when sold.</summary>
        private void EditFarmAnimals(IAssetData asset)
        {
            if (!this.Book.AnimalsActive)
                return;

            int day = this.Book.CurrentDay;
            foreach ((string id, FarmAnimalData animal) in asset.AsDictionary<string, FarmAnimalData>().Data)
            {
                float multiplier = this.Book.GetFlatMultiplier("animal", id, day);
                animal.PurchasePrice = PriceBook.Scale(animal.PurchasePrice, multiplier);
                animal.SellPrice = PriceBook.Scale(animal.SellPrice, multiplier);
            }
        }

        /// <summary>Raised after the player loads a save.</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Book.Active = this.Config.Enabled;
            this.RefreshData();
        }

        /// <summary>Raised after the game returns to the title screen.</summary>
        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.Book.Active = false;
            this.DataDay = -1;
            this.InvalidatePriceData();
        }

        /// <summary>Raised after a new day starts.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!this.Book.Active)
                return;

            if (this.Book.CurrentDay != this.DataDay)
                this.RefreshData();

            if (this.Config.LogPricesAtSeasonStart && Game1.dayOfMonth == 1)
                this.Logger.Log(this.Book.CurrentDay);
        }

        /// <summary>Rebuild the game data that holds prices, so it reflects the current day's multipliers. Shop and sell prices are scaled when they're read, so only data assets need this.</summary>
        private void RefreshData()
        {
            this.DataDay = this.Book.CurrentDay;
            this.InvalidatePriceData();
        }

        /// <summary>Drop the cached data assets that hold prices.</summary>
        private void InvalidatePriceData()
        {
            this.Helper.GameContent.InvalidateCache("Data/Buildings");
            this.Helper.GameContent.InvalidateCache("Data/FarmAnimals");
        }

        /// <summary>Handle the <c>vm_prices</c> console command.</summary>
        private void OnPricesCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int offset = 0;
            if (args.Length > 0 && !int.TryParse(args[0], out offset))
            {
                this.Monitor.Log("The day offset must be a whole number, like 0, 3, or -1.", LogLevel.Warn);
                return;
            }

            int day = Game1.Date.TotalDays + offset;
            if (day < 0)
            {
                this.Monitor.Log("That's before the first day of the save.", LogLevel.Warn);
                return;
            }

            this.Logger.Log(day);
        }
    }
}
