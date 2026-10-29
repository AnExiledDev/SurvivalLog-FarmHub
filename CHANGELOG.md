# Changelog

## 0.5.1

- With the window closed, Farm Hub's messages (such as auto replant results) now show as Exiled's Toolkit toasts. Those toasts also go to the Notifications history.
- Exiled's Toolkit 0.11.0 or newer is now required.
- Made for Survival Log 1.1.19611.

## 0.5.0

- New: `UiScale` under Interface sets the size of Farm Hub's own windows on their own, from 75% to 200%, whatever Exiled's Toolkit's UI size is. At 0, the default, they follow the Toolkit's. Needs Exiled's Toolkit 0.11.0 or newer.
- Made for Survival Log 1.1.19611.

## 0.4.4

- The Farm Hub window and its pop-up messages can be drawn bigger or smaller with the UI size setting of Exiled's Toolkit 0.10.0 or newer. A change shows at once, and the window only grows as far as it still fits on the screen.
- The HUD button and the additions to the game's plant list keep the game's size, like the game's own buttons and text around them.
- Made for Survival Log 1.1.19611.

## 0.4.3

- Less stutter: Farm Hub now checks your pots a few at a time over several frames instead of all in one frame.
- Less stutter with the game's plant list open: Farm Hub now redraws only the pots that changed, and does nothing while your farm stays the same.
- The Farm Hub window redraws only the pots that changed when it refreshes, so it stays smooth with many pots.
- The first refresh after loading a save is quicker.
- Made for Survival Log 1.1.19611.

## 0.4.2

- Fixed: the search boxes and text boxes in Farm Hub now accept Chinese and other typing that needs an input method.
- Made for Survival Log 1.1.19611.

## 0.4.1

- Less stutter: the farm read skips furniture that is not a pot sooner, rechecks one empty pot's light and warmth at a time, and the auto replant waits a frame instead of running with the farm read.

- The game's plant list in the HUD shows more: each pot's room and what it needs (rots in how long, what it gets replanted with, or the chore or fertilizer it waits for), the empty pots with the seed they would get (click one to plant it), a Needs you / All switch that folds away most of the growing pots, and a link to the Farm Hub window.
- The game's Tend Garden tip lists the chores the next Tend Garden will do and the stamina they take, and warns when your stamina is short or a preset has no seed left.
- Tend Garden can work from your presets: with the game's Replant box on, a harvested pot is replanted from its preset instead of opening the plant screen, and after each Tend Garden the empty pots are planted and the growing ones fertilized from their presets. Turn it on or off with the new "from presets" box next to Replant, or the new TendFromPresets setting (on by default). A pot whose preset has no seed left opens the plant screen as before.
- The HUD button's count and the window refresh every 2 seconds while the window is closed, so the plant list stays current.

## 0.3.0

- New window layout, in the style of the other hubs. The pot list is grouped by room, with your floor marked and each room's preset named in its header. Show only the pots that need you, the empty ones or the growing ones, or search by crop, room or pot.
- With no pot picked, the right side lists what needs you (harvest, care, bad spots, empty pots, fertilizer) and what ripens or rots in the next 48 hours. Pick a pot to see its timing, size, bonus, power and robot, change its preset, and do its next chore.
- Tend all now sends the game's own Tend Garden (harvest, clear, weed, water), then plants and fertilizes from your presets.
- The window now opens with Shift+G, so G stays the game's Tend Garden. Set the new PanelKeyShift setting to false to open it with G alone, as before.
- The planting preset editor and the seed and fertilizer stock sit side by side on the Planting presets tab. The separate forecast tab is gone; its 48 hour list is on the Pots tab.
- Checking several pots at once, and grouping or sorting the list another way, were removed.
- Updated for Survival Log 1.1.19611.

## 0.2.0

- The window is much faster: a refresh that only moves the clocks updates the times in place instead of rebuilding the window, picking or checking a pot no longer redraws the list, and words are sent to the window once.
- Pots: filter by state, crop or room from the side list, group by room, crop or state, sort by urgency, ripeness or name, and do a pot's next chore straight from its card.
- With no pot picked, the right side lists every pot waiting on you, soonest first. A picked pot shows its timing, the seeds its next planting will try in order, and its preset.
- Planting presets: seeds are set per pot size in three columns, with the next seed marked, an add-seed picker per size, and a panel that says what the preset will do with the seeds you own.
- Robots are shown side by side. The forecast shows the next 48 hours on a timeline.
- Cleaner layout: rooms, grouping and sorting sit above the crop list, a card leaves out the preset its room header already names, long seed and fertilizer names no longer clip, and forecast entries no longer overlap.

- After a game update, Mod Manager can tell you if this mod may need an update.

## 0.1.1

- With Exiled's Toolkit 0.6.0 or later, the HUD button lines up neatly with other mods' buttons.

## 0.1.0

- First release.
- A window lists every pot at home with its crop, timing, rot warning and problems.
- Plant, Fertilize and Tend all work on every pot at once.
- Planting presets give each pot a seed and fertilizer order to fall back on, with fertilizer set per crop.
- Harvested pots can be replanted on their own.
- The planter robot's sowing can be set from the window.
- A forecast shows what ripens or rots in the next 48 hours.
