# Changelog

## 0.3.0

- New window layout, in the style of the other hubs. The pot list is grouped by room, with your floor marked and each room's preset named in its header. Show only the pots that need you, the empty ones or the growing ones, or search by crop, room or pot.
- With no pot picked, the right side lists what needs you (harvest, care, bad spots, empty pots, fertilizer) and what ripens or rots in the next 48 hours. Pick a pot to see its timing, size, bonus, power and robot, change its preset, and do its next chore.
- Tend all now sends the game's own Tend Garden (harvest, clear, weed, water), then plants and fertilizes from your presets.
- The window now opens with Shift+G, so G stays the game's Tend Garden. Set the new PanelKeyShift setting to false to open it with G alone, as before.
- The planting preset editor and the seed and fertilizer stock sit side by side on the Planting presets tab. The separate forecast tab is gone; its 48 hour list is on the Pots tab.
- Checking several pots at once, and grouping or sorting the list another way, were removed.
- Game version 1.1.19611.

## 0.2.0

- The window is much faster: a refresh that only moves the clocks updates the times in place instead of rebuilding the window, picking or checking a pot no longer redraws the list, and words are sent to the window once.
- Pots: filter by state, crop or room from the side list, group by room, crop or state, sort by urgency, ripeness or name, and do a pot's next chore straight from its card.
- With no pot picked, the right side lists every pot waiting on you, soonest first. A picked pot shows its timing, the seeds its next planting will try in order, and its preset.
- Planting presets: seeds are set per pot size in three columns, with the next seed marked, an add-seed picker per size, and a panel that says what the preset will do with the seeds you own.
- Robots are shown side by side. The forecast shows the next 48 hours on a timeline.
- Cleaner layout: rooms, grouping and sorting sit above the crop list, a card leaves out the preset its room header already names, long seed and fertilizer names no longer clip, and forecast entries no longer overlap.

- After a game update, a hook that can no longer be placed is logged as an error, so the Mod Manager can mark the mod as maybe needing an update.

## 0.1.1

- With Exiled's Toolkit 0.6.0 or later, the HUD button takes its place in the Toolkit's tidy layout of the tool buttons.

## 0.1.0

First release. A window for every pot at home: crop, timing, rot warning and problems, Plant, Fertilize and Tend all, planting presets with seed and fertilizer fallbacks and fertilizer per crop, auto replant, planter robot sow settings, and a 48 hour forecast.
