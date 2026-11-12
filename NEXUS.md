Name: Farm Hub
Summary: Every pot at home in one window, plus more in the game's plant list: empty pots, what each pot needs, and the next Tend Garden's chores. Tend Garden can replant, plant and fertilize from your presets. Presets set seed and fertilizer fallbacks per pot size, crop, room or floor. Planter robots with no seed set plant the preset's seeds for each pot size.

Description (BBCode):
[center][img]https://sldb.core-directive.com/media/save/1-save-safe.png[/img][/center]

Every pot at home in one window: what grows in it, when it is ripe, when it rots, what it needs and what it gets planted with next. Press the plant button in the tool column on the left of the screen to open it, or set a key for it (PanelKey in the config, unbound by default). The button shows how many pots have a chore waiting or could be planted now.

[size=5]Main features[/size]
[list]
[*][b]All pots at a glance[/b]: every pot at home, grouped by room with your floor marked, with its crop, time left, time until a ripe crop rots, problems and preset. Show only the pots that need you, the empty ones or the growing ones, or search by crop or room.[/*]
[*][b]What needs you[/b]: the harvests, care, bad spots, empty pots and fertilizer waiting, and what ripens or rots in the next 48 hours.[/*]
[*][b]Tend all[/b]: sends the game's Tend Garden (harvest, clear, weed, water), then plants every empty pot from its preset and fertilizes every growing pot that has none. Plant and Fertilize do just their part, and each pot can be handled on its own.[/*]
[*][b]More in the game's plant list[/b]: the plant list in the HUD shows each pot's room and what it needs, the empty pots with the seed they would get (click one to plant it), and a Needs you / All switch. The Tend Garden tip lists the chores the next Tend Garden will do and the stamina they take.[/*]
[*][b]Tend Garden from your presets[/b]: with the game's Replant box on, Tend Garden replants a harvested pot from its preset instead of opening the plant screen, then plants the empty pots and fertilizes the growing ones. Turn it off with the "from presets" box next to Replant.[/*]
[*][b]Planting presets with fallbacks[/b]: for each pot size, an ordered list of seeds, so when one runs out the next is used. Each seed can keep a number back. Fertilizers work the same way.[/*]
[*][b]Fertilizer per crop[/b]: a crop can have its own fertilizer order, and either fall back to the preset's order or get none when it runs out.[/*]
[*][b]Skip crops a pot cannot grow[/b]: a crop the pot's light or temperature cannot grow is skipped and the next seed is used.[/*]
[*][b]Per pot, room or floor[/b]: a preset can be used everywhere, on a floor, or in a room, and any pot can have one picked just for it.[/*]
[*][b]Auto replant[/b]: a preset can plant its empty pots by itself.[/*]
[*][b]Rotting soon[/b]: a ripe crop that will rot within 12 hours (you pick 6 to 48) is marked.[/*]
[*][b]Planter robots[/b]: change the seed and fertilizer for each pot size. A pot size with no seed set plants the seeds for that size from the preset for the robot's floor, and the Robots tab shows which ones. Turn on Keep robots stocked, and a seed you picked switches to the next one in that preset when it runs out.[/*]
[*][b]Stock[/b]: the seeds and fertilizer you own, and how many pots of each size they would fill.[/*]
[*][b]UI size[/b]: Exiled's Toolkit's UI size setting draws the window bigger or smaller, as far as it still fits on the screen. Farm Hub's UiScale setting can set a size for this mod alone.[/*]
[/list]
Presets are kept in [font=Courier New]BepInEx\config\farm-hub.json[/font], shared by every save. Farm Hub adds nothing to your save and does not edit game files. You can remove it at any time.

Farm Hub does what Greenline does. Use one or the other, not both.

Works in English and Chinese.

[size=5]Known issues[/size]
None known.

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.20719. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.19.0 or newer. Farm Hub does not load without it. It keeps Farm Hub's key quiet while you type, shows Farm Hub's messages as toasts while the window is closed, applies settings changed on the Mod Manager's Mods screen without a restart, and its UI size setting sizes the window.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/73]Mod Manager[/url]. With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]If Greenline is installed, delete [font=Courier New]Greenline.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\FarmHub.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]FarmHub.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. To also drop your presets, delete [font=Courier New]BepInEx\config\farm-hub.json[/font].

[size=5]Configuration[/size]
The window key is unbound by default, since the plant button opens the window. Set [font=Courier New]PanelKey[/font] to open it from the keyboard too. With [font=Courier New]PanelKeyShift[/font] on, it is pressed with Shift, so G gives Shift+G and G alone stays the game's Tend Garden. The key does nothing while you type in a text box. To change the window key, the rot warning or the robot switch, edit [font=Courier New]BepInEx\config\com.dev.survivallog.farmhub.cfg[/font] while the game is closed, or use the Mod Manager's Mods screen. The cfg file explains each setting in English and Chinese, and [url=https://github.com/AnExiledDev/SurvivalLog-FarmHub/blob/main/CONFIG.md]CONFIG.md[/url] lists them all. Presets are set in the Farm Hub window.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and, if you turned on [font=Courier New]LogToFile[/font], [font=Courier New]BepInEx\farm-hub.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord. You can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-FarmHub]github.com/AnExiledDev/SurvivalLog-FarmHub[/url]

[line]
[size=5]简体中文[/size]

在一个窗口里查看家中所有花盆：种着什么、何时成熟、何时腐烂、需要什么照料，以及下一次会种什么。点击屏幕左侧工具栏中的种植按钮即可打开，也可以为它设置按键（配置中的 PanelKey，默认不绑定）。按钮上的数字是有事要做或现在可以种植的花盆数量。

[size=5]主要功能[/size]
[list]
[*][b]花盆一览[/b]：家中所有花盆按房间分组，并标出你所在的楼层，显示作物、剩余时间、成熟作物距腐烂的时间、问题和所用预设。可只看需要你处理、空着或生长中的花盆，也可按作物或房间搜索。[/*]
[*][b]需要你处理[/b]：待收获、待照料、位置不佳、空着和待施肥的花盆，以及未来 48 小时内会成熟或腐烂的作物。[/*]
[*][b]全部照料[/b]：先触发游戏的「巡园」（收获、清理、除草、浇水），再按预设为所有空花盆种植，为所有未施肥的生长中花盆施肥。「种植」和「施肥」只做各自的部分，也可以单独处理一个花盆。[/*]
[*][b]游戏植物列表中的更多信息[/b]：HUD 中的植物列表会显示每个花盆所在的房间和需要什么，列出空花盆及将要种下的种子（点击即可种植），并可在「需要你处理」和「全部」之间切换。「巡园」的提示会列出下次巡园要做的事和所需体力。[/*]
[*][b]按预设巡园[/b]：勾选游戏的「补种」后，「巡园」收获后会按预设直接补种，而不是打开种植界面，然后为空花盆种植、为生长中的花盆施肥。可用「补种」旁的「按预设」关闭。[/*]
[*][b]带后备的种植预设[/b]：按花盆大小设置有序的种子清单，一种用完就换下一种。每种种子可以保留一定数量。肥料也是一样。[/*]
[*][b]按作物指定肥料[/b]：每种作物可以有自己的肥料顺序，用完后可选改用预设的肥料顺序或不施肥。[/*]
[*][b]跳过种不了的作物[/b]：花盆的光照或温度种不了的作物会被跳过，改用下一种种子。[/*]
[*][b]按花盆、房间或楼层[/b]：预设可以用于全部、某一楼层或某个房间，任何花盆也可以单独指定预设。[/*]
[*][b]自动补种[/b]：预设可以自动为空花盆种植。[/*]
[*][b]即将腐烂[/b]：12 小时内（可选 6 到 48）会腐烂的成熟作物会被标出。[/*]
[*][b]种植机器人[/b]：按花盆大小修改种子和肥料。没有设置种子的花盆大小，会种植机器人所在楼层预设中为这种大小列出的种子，「机器人」页会显示是哪些种子。打开「保持机器人补给」后，你选的种子用完时会换成该预设中的下一种。[/*]
[*][b]库存[/b]：你拥有的种子和肥料，以及它们够种多少个各尺寸的花盆。[/*]
[*][b]界面大小[/b]：可以用 Exiled's Toolkit 的界面大小设置放大或缩小窗口，最大放到窗口仍能完整显示在屏幕上为止。还可以用 Farm Hub 的 UiScale 设置单独调整本模组的大小。[/*]
[/list]
种植预设保存在 [font=Courier New]BepInEx\config\farm-hub.json[/font]，所有存档共用。Farm Hub 不会向存档添加任何内容，也不会修改游戏文件，可以随时移除。

Farm Hub 包含 Greenline 的功能，两者请只装一个。安装前如已装有 Greenline，请从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]Greenline.dll[/font]。

[size=5]已知问题[/size]
暂无。

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.20719。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.19.0 或更新版本。没有它本模组不会加载。打字时它让 Farm Hub 的按键不起作用。窗口关闭时，它把 Farm Hub 的消息显示为弹出通知。它让在 Mod Manager 的模组界面修改的设置无需重启即可生效，它的界面大小设置还能调整窗口的大小。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/73]Mod Manager[/url]。装了它，可以在游戏里它的「模组」界面修改每项设置，并看到每项的说明，不用打开配置文件。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\FarmHub.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]FarmHub.dll[/font]。如需同时删除种植预设，再删除 [font=Courier New]BepInEx\config\farm-hub.json[/font]。

[size=5]配置[/size]
窗口按键默认不绑定，因为种植按钮就能打开窗口。设置 [font=Courier New]PanelKey[/font] 后也可以用键盘打开。[font=Courier New]PanelKeyShift[/font] 打开时需同时按住 Shift，因此设为 G 即为 Shift+G，单按 G 仍是游戏的「巡园」。在输入框里打字时该按键不起作用。如需修改窗口按键、腐烂提醒或机器人自动换种，请在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.farmhub.cfg[/font]，或使用 Mod Manager 的「模组」界面。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-FarmHub/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font]，如果打开了 [font=Courier New]LogToFile[/font]，也附上 [font=Courier New]BepInEx\farm-hub.log[/font]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-FarmHub]github.com/AnExiledDev/SurvivalLog-FarmHub[/url]
