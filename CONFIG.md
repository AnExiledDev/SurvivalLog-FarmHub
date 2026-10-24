# Farm Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.farmhub.cfg`, created the first time the game starts with the mod. Edit it while the game is closed, or change the settings in game on the Mod Manager's Mods screen. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start.

Planting presets and each pot's own preset are not in this file. They are set in the Farm Hub window and kept in `BepInEx\config\farm-hub.json`, shared by every save. Delete that file to start over with one empty preset.

## General

| Setting | Default | What it does |
| --- | --- | --- |
| `PanelKey` | `G` | Opens and closes the Farm Hub window, pressed with Shift while `PanelKeyShift` is `true`. `None` turns the key off; the plant button in the tool column on the left of the screen still opens it. |
| `PanelKeyShift` | `true` | When `true`, the window opens with Shift and `PanelKey` (Shift+G), and G alone stays the game's Tend Garden. When `false`, `PanelKey` alone opens the window; with G, that also runs Tend Garden. |
| `TendFromPresets` | `true` | When `true`, the game's Tend Garden (G, or the plant button in the HUD's plant list) replants each harvested pot from its planting preset instead of opening the plant screen, then plants the empty pots and fertilizes the growing ones from their presets. Replanting needs the game's Replant box on. A pot whose preset has no seed left opens the plant screen as usual. Also the "from presets" box next to Replant in the HUD's plant list. |
| `RotWarnHours` | `12` | A ripe crop that will rot within this many hours (1 to 96) is flagged as rotting soon. Also in the window's Settings tab, as 6, 12, 24 or 48. |

## Robots

| Setting | Default | What it does |
| --- | --- | --- |
| `RobotKeepStocked` | `false` | When a planter robot runs out of the seed for a pot size, switch that size to the next seed in the planting preset for the robot's floor, so it keeps sowing. This changes the robot's own sow settings. Also in the window's Robots tab. |

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `LogToFile` | `false` | Also writes what Farm Hub does to `BepInEx\farm-hub.log`, which is kept between game starts. Turn it on when reporting a problem. |
