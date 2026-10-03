# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

VolatileMarkets is a Stardew Valley mod (SMAPI, C#/.NET 6) whose stated goal is to randomize market prices and sell values (see `manifest.json`). The code is currently the stock SMAPI starter template: `ModEntry` only subscribes to `ButtonPressed` and logs presses. The price-randomization logic has not been written yet.

## Layout

The repo root holds only `.gitignore`; everything lives under `VolatileMarkets/`:

- `VolatileMarkets.slnx`: solution file (new XML `.slnx` format, needs a recent Visual Studio or .NET SDK).
- `VolatileMarkets/VolatileMarkets.csproj`: the single mod project.
- `VolatileMarkets/ModEntry.cs`: mod entry point (`ModEntry : Mod`, `Entry(IModHelper)` registers event handlers).
- `VolatileMarkets/manifest.json`: SMAPI manifest (`UniqueID` `NicholasFacciola.VolatileMarkets`, `EntryDll` `VolatileMarkets.dll`, `MinimumApiVersion` 4.0.0).

## Build

Run from `VolatileMarkets/`:

```
dotnet build VolatileMarkets.slnx
```

The build uses `Pathoschild.Stardew.ModBuildConfig`. It locates the Stardew Valley install and references the game and SMAPI assemblies automatically. It also copies the compiled mod into the game's `Mods` folder and writes a release zip to `bin/<Configuration>/net6.0/VolatileMarkets <version>.zip`. The build therefore needs a local Stardew Valley and SMAPI install. It fails if the game path can't be detected.

There is no test project and no linter configured. To try changes, build, then launch the game through SMAPI.

## Conventions

- `Nullable` and `ImplicitUsings` are enabled.
- `ModEntry.cs` follows the SMAPI template style: `/*** Public methods ***/` and `/*** Private methods ***/` banner comments, `this.`-qualified member access, and XML doc comments on handlers. New code should match.
- Guard gameplay handlers with `Context.IsWorldReady`, since events fire before a save is loaded.
- Keep the `manifest.json` `Version` in sync with release zips. The zip name is derived from it.
