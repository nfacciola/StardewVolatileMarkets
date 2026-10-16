# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

VolatileMarkets is a Stardew Valley mod (SMAPI, C#/.NET 6) that re-rolls prices every in-game day: sell prices, shop prices (including tool upgrades and the traveling cart), building costs, and farm animal prices.

## Layout

The repo root holds `.gitignore` and this file. The solution is one level down, and the project is one level below that:

- `VolatileMarkets/VolatileMarkets.slnx`: solution file (new XML `.slnx` format; .NET SDK 8 can't read it, so build the `.csproj` directly).
- `VolatileMarkets/VolatileMarkets/`: the single mod project.
  - `ModEntry.cs`: entry point. Registers events, applies the Harmony patches, edits `Data/Buildings` and `Data/FarmAnimals`, and re-invalidates those two assets each day. Also defines the `vm_prices` console command.
  - `PriceBook.cs`: the single source of truth for multipliers. Every multiplier is a pure function of save ID, day, and item, so nothing is persisted.
  - `ModConfig.cs`: `config.json` options (spreads, clamps, per-feature toggles).
  - `PriceLogger.cs`: writes a vanilla-vs-now comparison to the console and a CSV in `PriceLogs/`.
  - `Patches/SellPricePatch.cs`: Harmony postfix on `Item.sellToStorePrice` and `Object.sellToStorePrice`.
  - `Patches/ShopPricePatch.cs`: Harmony postfix on every `ShopMenu` constructor, scaling gold prices in `itemPriceAndStock`.
  - `manifest.json`: SMAPI manifest (`UniqueID` `NicholasFacciola.VolatileMarkets`, `EntryDll` `VolatileMarkets.dll`, `MinimumApiVersion` 4.0.0, GitHub update key).

## How prices work

A multiplier is the product of three layers rolled fresh each day: a market layer shared by everything, a category layer (crops, fish, and so on), and an item layer. Each layer is uniform in log space, so a x2 swing is as likely as a /2 swing. The product is divided by `MeanBias` (so the average stays at 1) and clamped to `MinMultiplier`..`MaxMultiplier`.

- Artisan goods inherit their ingredient's multiplier and add their own `ArtisanSpread`.
- An item's buy and sell prices share one roll per day.
- Shop and sell prices are scaled when read (via the Harmony patches), so they need no cache invalidation. Buildings and animals live in data assets, so `ModEntry` invalidates them each new day.
- `ShopPricePatch` remembers each stock entry's original price so scaling the same entry twice can't compound. It skips non-gold shops and buyback items.
- Not randomized: house upgrade, renovation, and boat repair costs (hardcoded in the game).

## Build

Run from `VolatileMarkets/VolatileMarkets/`:

```
dotnet build VolatileMarkets.csproj
```

The build uses `Pathoschild.Stardew.ModBuildConfig`. It locates the Stardew Valley install and references the game and SMAPI assemblies automatically. It also copies the compiled mod into the game's `Mods` folder and writes a release zip to `bin/<Configuration>/net6.0/VolatileMarkets <version>.zip`. The build therefore needs a local Stardew Valley and SMAPI install. It fails if the game path can't be detected.

There is no test project and no linter configured. To try changes, build, then launch the game through SMAPI. `vm_prices [dayOffset]` in the SMAPI console prints and exports a day's prices.

## Release

1. Bump `Version` in `manifest.json` (the zip name is derived from it).
2. Build with `dotnet build VolatileMarkets.csproj -c Release`.
3. Commit, push, then tag `v<version>` and create a GitHub release with `gh release create`, attaching the zip. Past assets are named `VolatileMarkets.<version>.zip` (dots, no space), so rename the built zip to match.

## Conventions

- `Nullable` and `ImplicitUsings` are enabled.
- Code follows the SMAPI template style: `/*** Public methods ***/` and `/*** Private methods ***/` banner comments, `this.`-qualified member access, and XML doc comments. New code should match.
- Guard gameplay handlers with `Context.IsWorldReady`, since events fire before a save is loaded.
