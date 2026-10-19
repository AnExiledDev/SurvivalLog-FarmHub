# Farm Hub

Every pot at home in one window: what grows in it, when it is ripe, when it rots, what it needs and what it gets planted with next. Plant, fertilize and tend everything with one press each, from planting presets with fallbacks, and keep your planter robots sowing.

Press `G`, or the plant button in the tool column on the left of the screen, to open it. The button shows how many pots have a chore waiting or could be planted now.

## Features

- **All pots at a glance**: every pot at home, grouped by floor and room, with its crop, growth and time left, time until a ripe crop rots, problems (pests, weeds, dry, light, cold), its fertilizer and the preset it follows. Filter by ripe, needs care or empty.
- **Plant, Fertilize and Tend all**: plants every empty pot from its preset, fertilizes every growing pot that has none, and has the chore each pot needs most done (harvest, clear a withered crop, uproot a poor one, pests, weeds, water). Each pot can also be handled on its own, and several checked pots together.
- **Planting presets with fallbacks**: for each pot size, an ordered list of seeds, so when one runs out the next is used. Each seed can keep a number back (keep at least 4 wheat seeds, say). Then an ordered list of fertilizers, the same way.
- **Fertilizer per crop**: a crop can have its own fertilizer order (watermelon always gets the best one). When its list runs out it either uses the preset's order or gets none, your choice per crop.
- **Skip crops a pot cannot grow**: a crop the pot's light or temperature cannot grow is passed over for the next seed.
- **Per pot, room or floor**: a preset can be used everywhere, on a floor, or in a room, and any pot can have its own. The most specific one wins. A pot's own preset is kept per save.
- **Auto replant**: a preset can plant its empty pots by itself, as if you pressed Plant.
- **Rotting soon**: a ripe crop that will rot within 12 hours (you pick 6 to 48) is flagged and sorts to the top.
- **Planter robots**: see each robot's three pot sizes and change the seed and fertilizer for each. Optionally, when a robot runs out of a seed, Farm Hub switches it to the next seed in its floor's preset so it keeps sowing.
- **Forecast**: what ripens and what rots in the next 48 hours, and the seeds and fertilizer you own.

Presets are kept in `BepInEx\config\farm-hub.json`, shared by every save. Farm Hub adds nothing to your save and does not edit game files. You can remove it at any time.

Farm Hub does what Greenline does. Use one or the other, not both.

## Requirements

- Survival Log 1.1.18902. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Optional: [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. If Greenline is installed, delete `Greenline.dll` from the `BepInEx\plugins` folder.
3. Optional, to change settings without a restart: install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from Nexus Mods, and extract it into the game folder (the folder with `SurvivalLog.exe`).
4. Download `FarmHub-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-FarmHub/releases/latest).
5. Extract the zip into the game folder. It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\FarmHub.dll`

## Uninstall

Delete `FarmHub.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.farmhub.cfg`, `BepInEx\config\farm-hub.json` and, if it exists, `BepInEx\farm-hub.log`. Robot sow settings changed from Farm Hub stay as they were set.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.farmhub.cfg`, created the first time the game starts with the mod. The rot warning and the robot switch can also be set in the window. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Farm Hub 0.1.0 started`, followed by `hotkeys are blocked while the panel is open`, `clicks go to the panel while it is open` and `listening for panel messages`. A line starting with `could not` means part of the mod does not work on this game version.

## Known issues

None known.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . When reporting a bug, attach `BepInEx\LogOutput.log` and, if you turned on `LogToFile`, `BepInEx\farm-hub.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx, the game's IL2CPP interop assemblies and Exiled's Toolkit, so a game install with BepInEx set up and started once is required. Those assemblies are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/FarmHub -c Release -p:GameDir="C:\path\to\Survival Log" -p:ToolkitDll="C:\path\to\ExiledToolkit.dll"
```

`GameDir` is the game folder and `ToolkitDll` is a built `ExiledToolkit.dll`. The defaults in the project file are local paths you will almost certainly need to override. The output DLL is at `src\FarmHub\bin\Release\FarmHub.dll`.

The unit tests cover the seed and fertilizer picks, presets, chores and window rules and need nothing from the game:

```
dotnet test tests/FarmHub.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
