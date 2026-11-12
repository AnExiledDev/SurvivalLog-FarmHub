# Farm Hub

Every pot at home in one window: what grows in it, when it is ripe, when it rots, what it needs and what it gets planted with next. Plant, fertilize and tend every pot with one press each. Planting presets pick the seeds and fertilizer, with backups for when one runs out, and your planter robots can plant from them too.

Press the plant button in the tool column on the left of the screen to open it, or set a key for it (`PanelKey`, unbound by default). The button shows how many pots have a chore waiting or could be planted now.

## Features

- **All pots at a glance**: every pot at home, grouped by room with your floor marked, with its crop, growth and time left, time until a ripe crop rots, problems (pests, weeds, dry, light, cold) and the preset it follows. Show only the pots that need you, the empty ones or the growing ones, or search by crop or room.
- **What needs you**: with no pot picked, the right side counts the harvests, care, bad spots, empty pots and fertilizer waiting, and lists what ripens or rots in the next 48 hours.
- **Tend all, Plant and Fertilize**: Tend all sends the game's Tend Garden (harvest, clear, weed, water), then plants every empty pot from its preset and fertilizes every growing pot that has none. Plant and Fertilize do just their part. Each pot can also be handled on its own.
- **More in the game's plant list**: the plant list in the HUD shows each pot's room and what it needs, the empty pots with the seed they would get (click one to plant it), and a Needs you / All switch. The Tend Garden tip lists the chores the next Tend Garden will do and the stamina they take.
- **Tend Garden from your presets**: with the game's Replant box on, Tend Garden replants a harvested pot from its preset instead of opening the plant screen, then plants the empty pots and fertilizes the growing ones. Turn it off with the "from presets" box next to Replant.
- **Planting presets with backups**: for each pot size, an ordered list of seeds, so when one runs out the next is used. Each seed can keep some back (keep at least 4 wheat seeds, say). Fertilizers get an ordered list too, and work the same way.
- **Fertilizer per crop**: a crop can have its own fertilizer order (watermelon always gets the best one). When its list runs out it either uses the preset's order or gets none, your choice per crop.
- **Skip crops a pot cannot grow**: a crop the pot's light or temperature cannot grow is skipped and the next seed is used.
- **Per pot, room or floor**: a preset can be used everywhere, on a floor, or in a room, and any pot can have its own. A pot uses its own preset first, then its room's, then its floor's, then the one used everywhere. A pot's own preset is kept per save.
- **Auto replant**: a preset can plant its empty pots by itself, as if you pressed Plant.
- **Rotting soon**: a ripe crop that is close to rotting is marked and moves to the top. You set how close in the Settings tab, 12 hours by default.
- **Planter robots**: see each robot's three pot sizes and change the seed and fertilizer for each. When a pot size has no seed set, the robot plants the seeds listed for that size in its floor's preset, and the Robots tab shows which ones. With Keep robots stocked on, a seed you picked switches to the next one in that preset when it runs out.
- **Stock**: the Planting presets tab shows the seeds and fertilizer you own and how many pots of each size they would fill.
- **UI size**: Exiled's Toolkit's UI size setting (`UiScale`) draws the window bigger or smaller, as far as the window still fits on the screen. The HUD button and the additions to the game's plant list keep the game's size. Farm Hub's `UiScale` under Interface sets a size for this mod alone. At 0, the default, it follows the Toolkit's.

Presets are kept in `BepInEx\config\farm-hub.json`, shared by every save. Farm Hub adds nothing to your save and does not edit game files. You can remove it at any time.

Farm Hub does what Greenline does, so install one or the other.

## Requirements

- Survival Log 1.1.20719. Built for this version. A game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.19.0 or newer. Farm Hub does not load without it. It stops Farm Hub's key from firing while you type, shows Farm Hub's messages as toasts while the window is closed, applies settings changed on the Mod Manager's Mods screen without a restart, lays the HUD's tool buttons out four high, in columns, when there are more than four, and its UI size setting sizes the window.
- Optional: [Mod Manager](https://www.nexusmods.com/survivallog/mods/73). With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. If Greenline is installed, delete `Greenline.dll` from the `BepInEx\plugins` folder.
3. Install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
4. Download `FarmHub-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-FarmHub/releases/latest).
5. Extract the zip into the game folder. It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\FarmHub.dll`

## Uninstall

Delete `FarmHub.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.farmhub.cfg`, `BepInEx\config\farm-hub.json` and, if it exists, `BepInEx\farm-hub.log`. Seeds Farm Hub set on your robots stay set.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.farmhub.cfg`, created the first time the game starts with the mod. The window key (`PanelKey`) is unbound by default, since the plant button opens the window. Set one to open it from the keyboard too: with `PanelKeyShift` on, the key is pressed with Shift, so `G` gives Shift+G and G alone stays the game's Tend Garden. The key does nothing while you type in a text box. The rot warning and the robot switch can also be set in the window, and `TendFromPresets` with the "from presets" box in the HUD's plant list. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Farm Hub 0.5.8 started`, followed by `the panel key waits while you type in the game's text boxes`, `Shift with the panel key opens the window without also sending Tend Garden`, `Farm Hub sees when Tend Garden replants`, `Tend Garden replants from the presets` and `Tend Garden also plants and fertilizes from the presets`. A line starting with `could not hook` means part of the mod does not work on this game version.

## Known issues

None known.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . When reporting a bug, attach `BepInEx\LogOutput.log` and, if you turned on `LogToFile`, `BepInEx\farm-hub.log`.

To reach me directly, message `exiled.dev` on Discord. You can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

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
