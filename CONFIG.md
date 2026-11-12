# Farm Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.farmhub.cfg`, created the first time the game starts with the mod. Edit it while the game is closed, or change the settings in game on the Mod Manager's Mods screen. Delete the file to get the defaults back.

Planting presets, and the preset picked for each pot, are not in this file. They are set in the Farm Hub window and kept in `BepInEx\config\farm-hub.json`, shared by every save. Delete that file to start over with one empty preset.

## General

| Setting | Default | What it does |
| --- | --- | --- |
| `PanelKey` | `None` | Opens and closes the Farm Hub window, pressed with Shift while `PanelKeyShift` is `true`. Unbound by default: the plant button in the tool column on the left of the screen opens the window. Set a key here to open it from the keyboard too. The key does nothing while you type in a text box. |
| `PanelKeyShift` | `true` | When `true`, the window opens with Shift and `PanelKey`, so `PanelKey` can be `G` (Shift+G) while G alone stays the game's Tend Garden. When `false`, `PanelKey` alone opens the window. If that key is G, it also runs Tend Garden. |
| `TendFromPresets` | `true` | When `true`, the game's Tend Garden (G, or the plant button in the HUD's plant list) replants each harvested pot from its planting preset instead of opening the plant screen, then plants the empty pots and fertilizes the growing ones from their presets. Replanting needs the game's Replant box on. A pot whose preset has no seed left opens the plant screen as usual. The "from presets" box next to Replant in the HUD's plant list changes this too. |
| `RotWarnHours` | `12` | A ripe crop that will rot within this many hours (1 to 96) is marked as rotting soon. You can also pick 6, 12, 24 or 48 in the window's Settings tab. |

## Robots

| Setting | Default | What it does |
| --- | --- | --- |
| `RobotKeepStocked` | `false` | When `true` and a seed you picked for a planter robot runs out, Farm Hub switches that pot size to the next seed in the planting preset for the robot's floor, so it keeps sowing. When a pot size has no seed set, the robot always plants the seeds listed for that size in the preset, whatever this is set to. Either way, Farm Hub changes the robot's sow settings in the game. You can also turn it on or off in the window's Robots tab. |

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Farm Hub's windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). |

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogToFile` | `false` | Also writes what Farm Hub does to `BepInEx\farm-hub.log`, which is kept between game starts. Turn it on when reporting a problem. |
