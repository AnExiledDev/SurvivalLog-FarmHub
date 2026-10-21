Name: Farm Hub
Summary: Every pot at home in one window. Plant, fertilize and tend everything with one press, from planting presets with seed and fertilizer fallbacks, and keep planter robots sowing. 在一个窗口里查看家中所有花盆，一键按带后备种子和肥料的种植方案完成种植、施肥和照料，并让种植机器人持续播种。

Description (BBCode):
Every pot at home in one window: what grows in it, when it is ripe, when it rots, what it needs and what it gets planted with next. Press [font=Courier New]G[/font], or the plant button in the tool column on the left of the screen, to open it. The button shows how many pots have a chore waiting or could be planted now.

[size=5]Main features[/size]
[list]
[*][b]All pots at a glance[/b]: every pot at home, grouped by room, crop or state, with its crop, time left, time until a ripe crop rots, problems, fertilizer and preset. Filter to the ripe, rotting, dry or empty pots, or one crop or room, and do a pot's chore from its card.[/*]
[*][b]Plant, Fertilize and Tend all[/b]: plants every empty pot from its preset, fertilizes every growing pot that has none, and has the chore each pot needs most done. Each pot can also be handled on its own, and several checked pots together.[/*]
[*][b]Planting presets with fallbacks[/b]: for each pot size, an ordered list of seeds, so when one runs out the next is used. Each seed can keep a number back. Fertilizers work the same way.[/*]
[*][b]Fertilizer per crop[/b]: a crop can have its own fertilizer order, and either fall back to the preset's order or get none when it runs out.[/*]
[*][b]Skip crops a pot cannot grow[/b]: a crop the pot's light or temperature cannot grow is passed over for the next seed.[/*]
[*][b]Per pot, room or floor[/b]: a preset can be used everywhere, on a floor, or in a room, and any pot can have its own.[/*]
[*][b]Auto replant[/b]: a preset can plant its empty pots by itself.[/*]
[*][b]Rotting soon[/b]: a ripe crop that will rot within 12 hours (you pick 6 to 48) is flagged and sorts to the top.[/*]
[*][b]Planter robots[/b]: change the seed and fertilizer for each pot size, and optionally switch a robot to the next seed in its floor's preset when it runs out.[/*]
[*][b]Forecast[/b]: what ripens and what rots in the next 48 hours, and the seeds and fertilizer you own.[/*]
[/list]
Presets are kept in [font=Courier New]BepInEx\config\farm-hub.json[/font], shared by every save. Farm Hub adds nothing to your save and does not edit game files. You can remove it at any time.

Farm Hub does what Greenline does. Use one or the other, not both.

Works in English and Chinese.

[size=5]Known issues[/size]
None known.

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.19131. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.[/*]
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
To change the window key, the rot warning or the robot switch, edit [font=Courier New]BepInEx\config\com.dev.survivallog.farmhub.cfg[/font] while the game is closed, or use the Mod Manager's Mods screen (see [url=https://github.com/AnExiledDev/SurvivalLog-FarmHub/blob/main/CONFIG.md]CONFIG.md[/url] for every setting; the cfg file explains each setting in English and Chinese). Presets are set in the Farm Hub window.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and, if you turned on [font=Courier New]LogToFile[/font], [font=Courier New]BepInEx\farm-hub.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-FarmHub]github.com/AnExiledDev/SurvivalLog-FarmHub[/url]

[line]
[size=5]简体中文[/size]

在一个窗口里查看家中所有花盆：种着什么、何时成熟、何时腐烂、需要什么照料，以及下一次会种什么。按 [font=Courier New]G[/font]，或点击屏幕左侧工具栏中的种植按钮即可打开。按钮上的数字是有事要做或现在可以种植的花盆数量。

[size=5]主要功能[/size]
[list]
[*][b]花盆一览[/b]：家中所有花盆可按房间、作物或状态分组，显示作物、剩余时间、成熟作物距腐烂的时间、问题、肥料和所用方案。可只看成熟、快腐烂、缺水或空着的花盆，或某种作物、某个房间，并可直接在卡片上处理该花盆的活。[/*]
[*][b]全部种植、全部施肥、全部照料[/b]：按方案为所有空花盆种植，为所有未施肥的生长中花盆施肥，并为每个花盆完成最需要的照料。也可以单独处理一个花盆，或同时处理勾选的多个花盆。[/*]
[*][b]带后备的种植方案[/b]：按花盆大小设置有序的种子清单，一种用完就换下一种。每种种子可以保留一定数量。肥料也是一样。[/*]
[*][b]按作物指定肥料[/b]：每种作物可以有自己的肥料顺序，用完后可选改用方案的肥料顺序或不施肥。[/*]
[*][b]跳过种不了的作物[/b]：花盆的光照或温度种不了的作物会被跳过，改用下一种种子。[/*]
[*][b]按花盆、房间或楼层[/b]：方案可以用于全部、某一楼层或某个房间，任何花盆也可以单独指定方案。[/*]
[*][b]自动补种[/b]：方案可以自动为空花盆种植。[/*]
[*][b]即将腐烂[/b]：12 小时内（可选 6 到 48）会腐烂的成熟作物会被标出并排在最前。[/*]
[*][b]种植机器人[/b]：按花盆大小修改种子和肥料；还可以在机器人种子用完时，自动换成所在楼层方案中的下一种种子。[/*]
[*][b]预测[/b]：未来 48 小时内会成熟和会腐烂的作物，以及你拥有的种子和肥料。[/*]
[/list]
种植方案保存在 [font=Courier New]BepInEx\config\farm-hub.json[/font]，所有存档共用。Farm Hub 不会向存档添加任何内容，也不会修改游戏文件，可以随时移除。

Farm Hub 包含 Greenline 的功能，两者请只装一个。安装前如已装有 Greenline，请从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]Greenline.dll[/font]。

[size=5]已知问题[/size]
暂无。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.19131。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 或更新版本。安装后，在 Mod Manager 的模组界面修改的设置无需重启游戏即可生效；未安装时，在下次启动游戏时生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\FarmHub.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]FarmHub.dll[/font]。如需同时删除种植方案，再删除 [font=Courier New]BepInEx\config\farm-hub.json[/font]。

[size=5]配置[/size]
如需修改窗口按键、腐烂提醒或机器人自动换种，请在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.farmhub.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-FarmHub/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。修改会在下次启动游戏时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\farm-hub.log[/font]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-FarmHub]github.com/AnExiledDev/SurvivalLog-FarmHub[/url]
