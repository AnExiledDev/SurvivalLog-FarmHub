// Farm Hub's web half. Runs in Root.html, the document every game page iframe sits in. Draws the Farm Hub window over
// all of them, styled like the game's panels, and puts a plant button with a count badge in the HUD's tool column
// (bottom left, above the phone and bag buttons) through Exiled's Toolkit's page API. Talks to the plugin with
// "slmod|farmhub|<verb>" messages through the same API and is driven by window.__slfarmhub.
(function () {
  if (window.__slfarmhub) return;

  var PREFIX = 'slmod|farmhub|';
  var HUD_CLASS = 'fh-hud-btn';
  // Farm Hub's place in the HUD's tool column when Exiled's Toolkit lays it out in columns of four.
  var DOCK_ORDER = '30';
  var HUD_PAGE = /CoreUI1\.html/;
  var BUSY_TIMEOUT_MS = 5000;
  var MAX_ADD = 30;
  var MAX_KEEP = 99;
  var MAX_COMING = 8;
  var MAX_NAMES = 4;
  var FORECAST_HOURS = 48;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A sprout: a stem with two leaves over a pot.
  var SPROUT_PATH = 'M11 21v-7.2C8.4 13.6 6 11.4 6 8c2.9 0 5 1.6 5 4.4V12h2v.4C13 9.6 15.1 8 18 8c0 3.4-2.4 5.6-5 5.8V21h-2zM6 18h12l-1 4H7l-1-4z';
  // The game's Tend Garden key, which Farm Hub leaves alone.
  var GAME_TEND_KEY = 'G';
  var TABS = ['pots', 'presets', 'robots', 'settings'];
  var TAB_WORD = { pots: 'tabPots', presets: 'tabPresets', robots: 'tabRobots', settings: 'tabSettings' };
  var STATE_WORD = { empty: 'stEmpty', growing: 'stGrowing', ripe: 'stRipe', withered: 'stWithered', poor: 'stPoor' };
  var PROBLEM_WORD = { pests: 'pPests', weeds: 'pWeeds', dry: 'pDry', dark: 'pDark', bright: 'pBright', cold: 'pCold', feed: 'pFeed' };
  var CHORE_WORD = { harvest: 'tHarvest', clear: 'tClear', uproot: 'tUproot', pests: 'tPests', weeds: 'tWeeds', water: 'tWater' };
  var FROM_WORD = { pot: 'fromPot', room: 'fromRoom', floor: 'fromFloor', all: 'fromAll' };
  var SIZE_WORD = { 1: 'szSmall', 2: 'szMedium', 4: 'szLarge' };
  var SIZE_SHORT = { 1: 'szS', 2: 'szM', 4: 'szL' };
  var SHOWS = [['needs', 'fNeeds'], ['empty', 'fEmpty'], ['growing', 'fGrowing'], ['all', 'fAll']];
  var ROT_WARNS = [6, 12, 24, 48];

  var LOCAL = {
    tab: pickTab, pick: pickPot, pickat: pickAt, back: unpick,
    show: function (v) { state.show = v; render(); },
    editpreset: function (id) { state.tab = 'presets'; pickPreset(id); },
    preset: pickPreset, newpreset: newPreset,
    seedup: function (a) { moveSeed(a, -1); }, seeddown: function (a) { moveSeed(a, 1); }, seeddel: removeSeed,
    keepless: function (a) { changeKeep(a, -1); }, keepmore: function (a) { changeKeep(a, 1); },
    addopen: openSeedPicker, seedadd: addSeed,
    fertup: function (i) { moveIn(state.draft.ferts, +i, -1); }, fertdown: function (i) { moveIn(state.draft.ferts, +i, 1); },
    fertdel: function (i) { editDraft(function (d) { d.ferts.splice(+i, 1); }); }, fertadd: addFert,
    cropopen: function () { state.isCropPicking = !state.isCropPicking; render(); },
    cropadd: addCrop, cropdel: removeCrop, cropfert: toggleCropFert, cropout: setCropOut,
    skip: function () { editDraft(function (d) { d.skipUnsuitable = !d.skipUnsuitable; }); },
    fertilizeon: function () { editDraft(function (d) { d.fertilize = !d.fertilize; }); },
    autoreplant: function () { editDraft(function (d) { d.autoReplant = !d.autoReplant; }); },
    use: toggleUse, tieredit: editTier, tierseed: function (v) { pickTierPart('seed', +v); }, tierfert: function (v) { pickTierPart('fert', +v); },
    tierdone: function () { state.tierEdit = null; render(); }
  };

  var CONFIRMED = { deletepreset: true };

  var ENGLISH = {
    tabPots: 'Pots',
    tabPresets: 'Planting presets',
    tabRobots: 'Robots',
    tabSettings: 'Settings',
    sumPots: '{0} pots',
    cEmpty: '{0} empty',
    plantAll: 'Plant empty ({0})',
    fertAll: 'Fertilize ({0})',
    tendAll: 'Tend all ({0})',
    tHarvest: 'Harvest',
    tClear: 'Clear withered crop',
    tUproot: 'Uproot poor crop',
    tPests: 'Remove Pests',
    tWeeds: 'Remove Weeds',
    tWater: 'Water',
    fAll: 'All',
    fEmpty: 'Empty',
    noMatch: 'No pot matches these filters.',
    floor: 'Floor {0}',
    here: 'your floor',
    otherRoom: 'Elsewhere',
    nPots: '{0} pots',
    onePot: '1 pot',
    stEmpty: 'Empty',
    stGrowing: 'Growing',
    stRipe: 'Ripe',
    stWithered: 'Withered',
    stPoor: 'Poor',
    pPests: 'Pests',
    pWeeds: 'Weeds',
    pDry: 'Dry',
    pDark: 'Too dark',
    pBright: 'Too bright',
    pCold: 'Too cold',
    pFeed: 'Needs fertilizer today',
    toRipe: '{0} to ripe',
    beforeRot: '{0} before it rots',
    rotSoon: 'Rotting soon',
    noRot: 'Does not rot',
    hours: '{0} h',
    noFert: 'No fertilizer',
    robot: 'Robot',
    queued: 'Walk queued',
    unpowered: 'No power',
    nextShort: 'Next: {0} x{1}',
    nextNone: 'No seed left for its preset',
    noPreset: 'No preset',
    plant: 'Plant',
    fertilize: 'Fertilize',
    fromPot: 'Picked for this pot',
    fromRoom: 'From its room',
    fromFloor: 'From its floor',
    fromAll: 'From the preset used everywhere',
    useDefault: 'Room or floor preset',
    empty: 'No pots placed at home yet.',
    needsYou: 'Needs you',
    allFine: 'Nothing needs you right now.',
    size: 'Size',
    light: 'Light +{0}',
    heat: 'Heat +{0}',
    power: 'Needs power',
    faster: 'Grows {0}% faster',
    bonus: 'Bonus',
    none: 'None',
    timing: 'Timing',
    state: 'State',
    ripeIn: 'Ripe in',
    rotIn: 'Rots in',
    grown: 'Grown',
    fertWord: 'Fertilizer',
    fertNext: 'Next fertilizer',
    nextPlanting: 'Next planting',
    nextBadge: 'next',
    nSeeds: '{0} seeds',
    keepN: 'keep {0}',
    plantNote: 'Plants the first seed you have more of than its keep count.',
    noSaveYet: 'Save the game once to pick presets per pot.',
    confirm: 'Sure?',
    newPreset: 'New preset',
    name: 'Name',
    save: 'Save',
    delete: 'Delete',
    unsaved: 'Unsaved changes',
    presetsHead: 'Presets',
    usedNowhere: 'Not used yet',
    seedsBySize: 'Seeds by pot size',
    seedNote: 'The first seed you have more of than its keep count is planted.',
    szSmall: 'Small pots',
    szMedium: 'Medium pots',
    szLarge: 'Large pots',
    tierCap: '{0}-slot pots',
    keep: 'keep',
    tooBig: 'Too big for this pot',
    addSeed: '+ Add seed',
    search: 'Search',
    more: '{0} more. Search to narrow the list.',
    nothingToAdd: 'Nothing to add. Search for a seed you do not have yet.',
    ferts: 'Fertilizer order',
    fertNote: 'A crop without its own order below gets the first of these you have.',
    noFerts: 'None: crops are planted without fertilizer.',
    addFert: 'Add:',
    owned: '{0} owned',
    perCrop: 'Fertilizer per crop',
    perCropNote: 'A crop listed here uses its own order first.',
    colCrop: 'Crop',
    colOrder: 'Fertilizer, in order',
    colOut: 'When none are left',
    colTime: 'Grow time',
    woPreset: 'Preset order',
    woNone: 'None',
    never: 'Never fertilize',
    addCrop: '+ Add crop',
    skipUnsuitable: 'Skip crops the pot cannot grow',
    skipNote: 'Seeds whose crop needs more light or heat than the pot gives are skipped.',
    fertilizeOn: 'Fertilize when planting',
    fertilizeNote: 'Uses the fertilizer order above.',
    autoReplant: 'Replant empty pots by themselves',
    autoNote: 'Plants an empty pot from this preset on its own, as if you pressed Plant.',
    usedFor: 'Used for',
    useAll: 'Everywhere',
    usedNote: 'A pot uses the preset picked for it first, then its room\'s, then its floor\'s, then the one used everywhere.',
    willDo: 'What it will do',
    plantingsLeft: '{0} plantings left',
    ok: 'OK',
    low: 'Low',
    out: 'Out',
    planNote: 'Plantings left counts only the seeds above the keep counts. OK means you have enough to plant every pot twice.',
    nextBySize: 'Next crop for each size',
    nothingLeft: 'No seed left',
    seedOut: '{0}: not enough seed, skipped.',
    noPotsUse: 'No pot uses this preset yet.',
    keepStocked: 'Keep robots stocked',
    keepNote: 'When a seed you picked for a robot runs out, switch it to the next seed in the preset for the robot\'s floor.',
    noRobots: 'No planter robots at home.',
    battery: 'Battery {0}%',
    working: 'Working',
    robotOff: 'Switched off',
    colSize: 'Size',
    colSeed: 'Seed now',
    noSeed: 'No seed',
    sizeSeeds: 'Seeds by pot size',
    sizeSeedsNote: 'The robot plants the seeds listed for this pot size in the preset for its floor, in that order, with the preset\'s fertilizer.',
    followSeeds: 'Uses the seeds for this pot size from preset {0}: {1}',
    followNone: 'Preset {0} has no seeds for this pot size, so the robot plants nothing here. Add some on the Planting presets tab.',
    followOut: 'Out of the seeds for this pot size in preset {0}: {1}',
    pickedNote: 'You picked this seed, so the preset\'s seeds are not used here. To use them again, press Change and pick Seeds by pot size.',
    change: 'Change',
    cancel: 'Cancel',
    seed: 'Seed',
    stockHead: 'Seeds and fertilizer',
    stockNote: 'How many pots of each size you can fill with what you have, not counting what you keep.',
    colItem: 'Item',
    colHave: 'Have',
    comingUp: 'Next 48 hours',
    nothingSoon: 'Nothing ripens or rots in the next 48 hours.',
    rotWarn: 'Rotting soon means within',
    rotWarnNote: 'Pots this close to rotting are marked red and counted on the plant button.',
    hotkey: 'Hotkey',
    keyOpen: '{0} opens and closes this window.',
    keyNone: 'No key is set. Use the plant button on the left of the screen.',
    presetFile: 'Presets are kept in BepInEx/config/farm-hub.json, shared by every save.',
    escClose: 'Esc to close',
    fNeeds: 'Needs you',
    fGrowing: 'Growing',
    cNeed: '{0} need you',
    keyTend: 'Tend Garden (game)',
    keyThis: 'this window',
    nJobs: '{0} jobs',
    todoCare: 'Clear, weed or water',
    todoSpot: 'Bad spot',
    todoPlant: 'Plant empty pots',
    inHours: 'in {0}',
    readyPill: 'Ripens',
    rotsPill: 'Rots',
    tendNote: 'Harvests, clears, weeds and waters like the game\'s Tend Garden, then plants and fertilizes from your presets.',
    backNeeds: 'Needs you',
    thisPot: 'This pot',
    afterHarvest: 'After harvest',
    szS: 'S',
    szM: 'M',
    szL: 'L',
    keysHead: 'Keys',
    warnHead: 'Warnings',
    otherHead: 'Other',
    gTwice: 'G opens this window and also runs the game\'s Tend Garden. Set PanelKeyShift to true to open it with Shift+G instead.',
    tip: 'Your pots: ripe crops, problems and planting',
    fromPresets: 'from presets',
    hNextTend: 'Next Tend:',
    hChores: '{0} chores',
    hStamina: '{0} of {1} stamina',
    hThen: 'then plant {0}, fertilize {1}',
    hShow: 'Show',
    hNeeds: 'Needs you ({0})',
    hAll: 'All ({0})',
    hMore: '+ {0} more growing',
    hEmptyHead: 'Empty pots: plant from your presets',
    hGrowingHead: 'Growing: next ready',
    hEmptyPot: 'Empty pot',
    hPlantSeed: 'plant {0}',
    hNoSeed: 'no seed',
    hSeeds: '{0} seeds',
    hRotsIn: 'rots in {0}',
    hThenCrop: 'then',
    hClean: 'Clean up',
    hNeedsFert: 'needs fertilizer',
    hThenHead: 'Then, from your presets',
    hReplant: 'Replant',
    hPlantEmpty: 'Plant empty pots',
    hShort: 'Not enough stamina for all of it. The rest waits.',
    hNoSeedFor: 'No seed left for {0}',
    hNoSeedMore: 'No seed left for {0} and {1} more',
    hIdle: 'Nothing to tend right now.'
  };

  var state = {
    data: { ready: false, note: '' }, isOpen: false, isLive: false, key: '', badge: 0, tab: 'pots', sel: '', show: 'all', potSearch: '',
    presetId: '', draft: null, draftOf: '', pendingNewFrom: null, addCap: 0, addSearch: '', isCropPicking: false, cropSearch: '',
    tierEdit: null, armed: '', isTyping: false, isStale: false
  };
  var cat = { kinds: {}, crops: {}, seeds: {}, ferts: {}, seedList: [], fertList: [], cropList: [], caps: [] };
  var ui = null;
  var shownShape = '';
  var isBusy = false;
  var busyTimer = 0;
  var hasReportedHudPlaced = false;
  // Bumped when the catalog or the words change, so nodes drawn from the old ones are drawn again.
  var catSeq = 0;
  var wordsSeq = 0;
  var seenWords = null;
  // The window's pot rows by pot id, reused while the pot looks the same: { key, node, live }.
  var rowCache = {};

  function say(key) {
    var words = window.__slfarmhubWords || ENGLISH;
    var text = words[key] || ENGLISH[key] || key;

    for (var i = 1; i < arguments.length; i++) text = text.split('{' + (i - 1) + '}').join(String(arguments[i]));

    return text;
  }

  // The plugin swaps in a new words object when the game's language changes; this notices the swap.
  function wordsKey() {
    var words = window.__slfarmhubWords || ENGLISH;

    if (words !== seenWords) {
      seenWords = words;
      wordsSeq++;
    }

    return wordsSeq;
  }

  // A phrase with its number picked out, as in "5 ripe": the number is bold and coloured, the rest is plain.
  function sayCount(key, count, colorClass) {
    var parts = say(key).split('{0}');
    var node = el('span');

    put(node, text(parts[0]), el('b', colorClass, String(count)), text(parts.slice(1).join('{0}')));

    return node;
  }

  // Through the Toolkit's bridge. Nothing goes before its page API is there; the page says it is ready once it is.
  function send(message) {
    if (etk) etk.send(PREFIX, message);
  }

  // One press at a time: a double click would otherwise ask twice. Every press is answered by a status line, which
  // frees the buttons; the timer frees them anyway if that answer never comes.
  function sendAction(message) {
    if (isBusy) return;

    isBusy = true;
    clearTimeout(busyTimer);
    busyTimer = setTimeout(clearBusy, BUSY_TIMEOUT_MS);
    send(message);
  }

  function clearBusy() {
    isBusy = false;
    clearTimeout(busyTimer);
  }

  function el(tag, className, content) {
    var node = document.createElement(tag);

    if (className) node.className = className;
    if (content != null) node.textContent = content;

    return node;
  }

  function text(content) {
    return content ? document.createTextNode(content) : null;
  }

  function put(parent) {
    for (var i = 1; i < arguments.length; i++) {
      if (arguments[i]) parent.appendChild(arguments[i]);
    }

    return parent;
  }

  function setClass(node, name, isOn) {
    var names = String(node.className || '').split(' ').filter(function (n) { return n !== '' && n !== name; });

    if (isOn) names.push(name);

    node.className = names.join(' ');
  }

  function act(node, action, args) {
    node.setAttribute('data-act', action);

    if (args != null) node.setAttribute('data-args', String(args));

    return node;
  }

  function button(content, action, args, isEnabled, extraClass) {
    return plainButton('fh-btn' + (extraClass ? ' ' + extraClass : ''), content, action, args, isEnabled);
  }

  // A button without the panel button look: the small arrows, crosses and steppers.
  function plainButton(className, content, action, args, isEnabled) {
    var node = act(el('button', className, content), action, args);

    if (isEnabled === false) node.disabled = true;

    return node;
  }

  function chip(content, action, args, isOn, count, iconItem) {
    var node = act(el('button', 'fh-chip' + (isOn ? ' fh-on' : '') + (iconItem ? ' fh-ic' : '')), action, args);

    put(node, iconItem ? icon(iconItem, 'fh-i18') : null, content ? el('span', '', content) : null, count != null ? el('span', 'fh-n', String(count)) : null);

    return node;
  }

  // A row of choices where one is on, like the game's tab strips; a choice may carry a count.
  function segment(choices, current, action) {
    var seg = el('div', 'fh-seg');

    choices.forEach(function (c) {
      seg.appendChild(put(act(el('button', 'fh-sg' + (c[0] === current ? ' fh-on' : ''), c[1]), action, c[0]), c[2] != null ? el('span', 'fh-n', String(c[2])) : null));
    });

    return seg;
  }

  function switchButton(isOn, action, args) {
    return act(el('button', 'fh-tog' + (isOn ? ' fh-on' : '')), action, args);
  }

  function badge(content, colorClass) {
    return el('span', 'fh-pill' + (colorClass ? ' ' + colorClass : ''), content);
  }

  function section(title, right) {
    var head = el('div', 'fh-sect', title);

    if (right) head.appendChild(right);

    return head;
  }

  function setRow(label, note, control) {
    return put(el('div', 'fh-setrow'), put(el('div', 'fh-lbl'), el('b', '', label), note ? el('small', '', note) : null), control);
  }

  var CSS = [
    '#fh-root{--ac:#FFC83A;--tx:#f0e8d8;--t2:#cbbfa8;--t3:#a89a82;--t4:#7a6e5a;--ln:rgba(220,188,120,.20);--ls:rgba(220,188,120,.11);--cf:rgba(70,58,44,.55);--cd:rgba(84,70,52,.45);--gr:#8cc56a;--rd:#e57373;--or:#ffa726;--glow:0 0 0 1px rgba(255,200,58,.55),0 0 14px rgba(255,200,58,.22);position:fixed;inset:0;z-index:2147483000;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.45);font-family:"Noto Sans SC","Microsoft YaHei","PingFang SC",sans-serif;color:var(--tx);user-select:none;font-size:14px}',
    '#fh-root *{box-sizing:border-box}',
    '#fh-root button{font:inherit}',
    '.fh-win{position:relative;width:min(1580px,96vw);height:min(920px,92vh);display:flex;flex-direction:column;padding:22px 26px 14px;border:1px solid rgba(220,188,120,.28);border-radius:4px;background:radial-gradient(ellipse at 15% 0%,rgba(150,100,40,.30),transparent 55%),linear-gradient(180deg,#3a3129 0%,#2b241d 55%,#241e18 100%);box-shadow:0 18px 60px rgba(0,0,0,.7),inset 0 1px 0 rgba(255,220,150,.08)}',
    '.fh-win::before{content:"";position:absolute;left:24px;right:24px;top:0;height:2px;background:linear-gradient(90deg,transparent,var(--ac),transparent);opacity:.75}',
    '.fh-whd{display:flex;align-items:center;gap:14px;padding-bottom:14px;border-bottom:1px solid var(--ln)}',
    '.fh-logo{width:46px;height:46px;flex:none;border-radius:4px;display:flex;align-items:center;justify-content:center;background:linear-gradient(180deg,#ffd25a,#e8a91c);color:#2a1f10;box-shadow:0 2px 8px rgba(0,0,0,.4)}',
    '.fh-logo svg{width:28px;height:28px;fill:currentColor}',
    '.fh-ttl{font-size:26px;letter-spacing:6px;text-transform:uppercase;color:#fff;font-weight:700;line-height:1.1;text-shadow:0 2px 6px rgba(0,0,0,.5)}',
    '.fh-ttl small{display:block;font-size:14px;letter-spacing:0;text-transform:none;color:var(--t3);font-weight:400;margin-top:4px;text-shadow:none}.fh-ttl small b{color:var(--ac)}',
    '.fh-tabs{margin-left:auto;display:flex;gap:6px}',
    '.fh-tab{position:relative;padding:8px 20px;border:1px solid var(--ln);border-radius:3px;background:rgba(0,0,0,.18);color:var(--t2);font-size:15px;cursor:pointer;white-space:nowrap}',
    '.fh-tab:hover{color:#fff;border-color:rgba(255,200,58,.45)}.fh-tab.fh-on{color:var(--ac);border-color:var(--ac);background:rgba(255,200,58,.13);box-shadow:var(--glow);font-weight:700}',
    '.fh-tab .fh-n{position:absolute;top:-9px;right:-9px;min-width:20px;height:20px;padding:0 6px;border-radius:10px;background:#d84a3a;color:#fff;font-size:12px;font-weight:700;line-height:20px;text-align:center}',
    '.fh-x{width:36px;height:36px;flex:none;padding:0;border:1px solid var(--ln);border-radius:3px;background:rgba(0,0,0,.18);color:var(--t3);font-size:20px;line-height:32px;cursor:pointer}.fh-x:hover{color:#fff;border-color:var(--ac)}',
    '.fh-wbody{flex:1;min-height:0;display:flex;gap:18px;padding-top:14px}',
    '.fh-main{flex:1;min-width:0;min-height:0;display:flex;flex-direction:column;gap:10px}',
    '.fh-side{width:430px;flex:none;min-height:0;display:flex;flex-direction:column;gap:10px;padding:14px 16px;border:1px solid var(--ls);border-radius:3px;background:rgba(0,0,0,.18)}',
    '.fh-side>.fh-scroll{flex:1}',
    '.fh-c330{width:330px;flex:none;min-height:0;display:flex;flex-direction:column;gap:10px}.fh-c330>.fh-scroll{flex:1}',
    '.fh-scroll{min-height:0;overflow-y:auto;padding-right:6px}',
    '.fh-scroll::-webkit-scrollbar{width:6px}.fh-scroll::-webkit-scrollbar-thumb{background:rgba(255,200,58,.35);border-radius:3px}',
    '.fh-ed{flex:1;min-width:0}',
    '.fh-wfoot{display:flex;align-items:center;gap:16px;margin-top:12px;padding-top:10px;border-top:1px solid var(--ln);color:var(--t3);font-size:14px}',
    '.fh-status{flex:1;min-width:0;color:var(--t2);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-kbd{display:inline-block;font-family:Consolas,monospace;font-size:13px;color:var(--tx);border:1px solid var(--ln);border-bottom-width:2px;border-radius:3px;padding:0 6px;margin-right:6px;background:rgba(0,0,0,.25)}',
    '.fh-sect{display:flex;align-items:center;gap:10px;font-size:15px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:var(--ac);margin:6px 0 8px}',
    '.fh-sect::before{content:"";width:4px;height:16px;background:var(--ac);flex:none;border-radius:1px}',
    '.fh-sect::after{content:"";flex:1;height:1px;background:linear-gradient(90deg,var(--ln),transparent);order:1}',
    '.fh-sect .fh-r{order:2;letter-spacing:0;text-transform:none;font-weight:400;font-size:13px;color:var(--t3)}',
    '.fh-btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;padding:8px 14px;border:1px solid var(--ln);border-radius:3px;background:linear-gradient(180deg,rgba(92,78,60,.9),rgba(66,55,42,.92));color:var(--tx);font-size:14px;cursor:pointer;white-space:nowrap}',
    '.fh-btn:hover:not(:disabled){border-color:var(--ac);color:#fff}.fh-btn:disabled{opacity:.4;cursor:default}',
    '.fh-btn.fh-sm{padding:4px 10px;font-size:13px}',
    '.fh-btn.fh-q{background:rgba(255,200,58,.08);border-color:rgba(255,200,58,.6);color:var(--ac)}',
    '.fh-btn.fh-gold{background:linear-gradient(180deg,#ffd25a,#e8a91c);border-color:#ffd25a;color:#2a1f10;font-weight:700}.fh-btn.fh-gold:hover:not(:disabled){color:#000;box-shadow:var(--glow)}',
    '.fh-btn.fh-big{width:100%;padding:13px;font-size:18px}',
    '.fh-btn.fh-ghost{background:none;border-color:transparent;color:var(--t3);padding-left:0}.fh-btn.fh-ghost:hover:not(:disabled){color:var(--ac);border-color:transparent}',
    '.fh-btn.fh-danger:hover:not(:disabled),.fh-btn.fh-armed{border-color:var(--rd);color:var(--rd)}',
    '.fh-seg{display:inline-flex;border:1px solid var(--ln);border-radius:3px;overflow:hidden;flex:none}',
    '.fh-sg{padding:6px 14px;border:none;border-right:1px solid var(--ls);background:rgba(0,0,0,.18);color:var(--t2);font-size:14px;cursor:pointer;white-space:nowrap}.fh-sg:last-child{border-right:none}.fh-sg:hover{color:#fff}',
    '.fh-sg.fh-on{background:var(--ac);color:#2a1f10;font-weight:700}.fh-sg .fh-n{margin-left:6px;opacity:.7}',
    '.fh-chips{display:flex;gap:6px;flex-wrap:wrap;margin-bottom:6px}',
    '.fh-chip{display:inline-flex;align-items:center;gap:6px;padding:4px 10px;border-radius:3px;background:rgba(0,0,0,.2);border:1px solid var(--ln);color:var(--t2);font-size:13px;cursor:pointer;white-space:nowrap}',
    '.fh-chip.fh-ic{padding-left:4px}.fh-chip:hover:not(:disabled){border-color:rgba(255,200,58,.6);color:#fff}.fh-chip:disabled{opacity:.4;cursor:default}',
    '.fh-chip.fh-on{border-color:var(--ac);color:var(--ac);background:rgba(255,200,58,.12)}.fh-chip .fh-n{color:var(--ac);font-weight:600}',
    '.fh-chip.fh-add{border-style:dashed;color:var(--t3);background:none}',
    '.fh-inp{font:inherit;font-size:14px;color:#fff;background:rgba(0,0,0,.35);border:1px solid var(--ln);border-radius:3px;padding:6px 10px;user-select:text}',
    '.fh-inp:focus{outline:none;border-color:var(--ac)}.fh-inp.fh-small{font-size:13px;padding:4px 8px;width:100%}.fh-inp.fh-name{font-size:17px;font-weight:700;width:320px}',
    '.fh-tog{width:40px;height:22px;padding:0;border-radius:11px;background:rgba(0,0,0,.35);border:1px solid var(--ln);position:relative;flex:none;cursor:pointer}',
    '.fh-tog::after{content:"";position:absolute;top:3px;left:3px;width:14px;height:14px;border-radius:50%;background:var(--t4);transition:left .15s}',
    '.fh-tog.fh-on{background:rgba(255,200,58,.3);border-color:var(--ac)}.fh-tog.fh-on::after{left:21px;background:var(--ac)}',
    '.fh-stepper{display:inline-flex;align-items:center;border:1px solid var(--ln);border-radius:3px;overflow:hidden;font-size:13px;flex:none}',
    '.fh-stp{padding:1px 8px;color:var(--t3);background:rgba(0,0,0,.2);border:none;cursor:pointer}.fh-stp:hover:not(:disabled){color:var(--ac)}.fh-stp:disabled{opacity:.3;cursor:default}',
    '.fh-stepper b{padding:1px 6px;color:var(--ac);font-weight:700;min-width:28px;text-align:center}',
    '.fh-ib{width:20px;height:20px;padding:0;border-radius:3px;border:1px solid var(--ls);background:rgba(0,0,0,.2);color:var(--t3);font-size:11px;line-height:18px;text-align:center;cursor:pointer}',
    '.fh-ib:hover:not(:disabled){border-color:var(--ac);color:var(--ac)}.fh-ib:disabled{opacity:.2;cursor:default}.fh-ib.fh-del:hover{border-color:var(--rd);color:var(--rd)}',
    '.fh-pill{display:inline-block;flex:none;padding:2px 9px;border-radius:10px;font-size:12px;white-space:nowrap;background:rgba(255,255,255,.08);color:var(--t2);font-weight:400}',
    '.fh-p-ready{background:var(--ac);color:#2a1f10;font-weight:700}.fh-p-urgent{background:rgba(229,115,115,.22);color:var(--rd)}.fh-p-warn{background:rgba(255,167,38,.18);color:var(--or)}',
    '.fh-p-good{background:rgba(140,197,106,.18);color:var(--gr)}.fh-p-dim{background:rgba(255,255,255,.05);color:var(--t4)}',
    '.fh-bar{display:inline-block;width:150px;height:6px;border-radius:3px;background:rgba(0,0,0,.4);overflow:hidden;flex:none}.fh-bar i{display:block;height:100%;background:var(--gr)}',
    '.fh-bar.fh-gold i{background:var(--ac)}.fh-bar.fh-red i{background:var(--rd)}.fh-bar.fh-wide{width:100%;height:8px}',
    '.fh-tool{display:flex;align-items:center;gap:10px}.fh-tool .fh-inp{margin-left:auto;width:280px}',
    '.fh-grp{margin-bottom:14px}',
    '.fh-grp-h{display:flex;align-items:center;gap:10px;margin:0 2px 6px;font-size:13px;color:var(--t3)}.fh-grp-h b{font-size:14px;letter-spacing:2px;text-transform:uppercase;color:#fff}.fh-grp-h .fh-r{margin-left:auto}',
    '.fh-grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:6px}',
    '.fh-row{position:relative;display:flex;align-items:center;gap:12px;padding:8px 12px;border-radius:3px;background:var(--cf);border:1px solid transparent;cursor:pointer;min-height:62px}',
    '.fh-row:hover{background:rgba(92,76,56,.7)}.fh-row.fh-sel-on{border-color:var(--ac);box-shadow:var(--glow)}',
    '.fh-row.fh-e-gold::before,.fh-row.fh-e-red::before,.fh-row.fh-e-or::before{content:"";position:absolute;left:0;top:6px;bottom:6px;width:3px;border-radius:2px;background:var(--ac)}',
    '.fh-row.fh-e-red::before{background:var(--rd)}.fh-row.fh-e-or::before{background:var(--or)}',
    '.fh-mid{flex:1;min-width:0;display:flex;flex-direction:column;gap:4px}',
    '.fh-nm{font-size:15px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.fh-nm small{font-size:12px;color:var(--t3);margin-left:8px}',
    '.fh-row.fh-blank .fh-nm{font-style:italic;color:var(--t2)}',
    '.fh-meta{display:flex;align-items:center;gap:8px;font-size:13px;color:var(--t2);white-space:nowrap;overflow:hidden}',
    '.fh-end{display:flex;flex-direction:column;align-items:flex-end;gap:5px;flex:none}',
    '.fh-wh{font-size:13px;color:var(--t3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-todo{display:flex;align-items:center;gap:12px;padding:9px 12px;border-radius:3px;background:var(--cf);margin-bottom:6px;cursor:pointer}.fh-todo:hover{background:rgba(92,76,56,.7)}',
    '.fh-todo .fh-t{flex:1;min-width:0;font-size:15px;color:#fff}.fh-todo .fh-t small{display:block;font-size:12px;color:var(--t3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-cnt{font-size:20px;font-weight:700;color:var(--ac);min-width:32px;text-align:right}.fh-cnt.fh-red{color:var(--rd)}.fh-cnt.fh-dim{color:var(--t4)}',
    '.fh-fc{display:grid;grid-template-columns:64px minmax(0,1fr) auto;gap:10px;align-items:center;padding:6px 8px;border-bottom:1px solid var(--ls);font-size:13px;cursor:pointer}.fh-fc:hover{background:rgba(255,255,255,.03)}',
    '.fh-tm{font-family:Consolas,monospace;color:var(--t2)}.fh-fc .fh-wh{font-size:12px}',
    '.fh-cta{margin-top:auto;display:flex;flex-direction:column;gap:8px;padding-top:10px;border-top:1px solid var(--ls)}.fh-cap{font-size:13px;color:var(--t3);text-align:center}',
    '.fh-subacts{display:flex;gap:8px}.fh-subacts .fh-btn{flex:1}',
    '.fh-det-hd{display:flex;align-items:center;gap:14px;margin:4px 0 12px}.fh-det-hd .fh-nm{font-size:22px;font-weight:700}',
    '.fh-tl{margin-bottom:12px}.fh-tl-lab{display:flex;justify-content:space-between;font-size:12px;color:var(--t3);margin-top:4px}',
    '.fh-kv{display:grid;grid-template-columns:130px minmax(0,1fr);gap:6px 12px;font-size:14px;margin-bottom:12px}.fh-kv .fh-k{color:var(--t3)}.fh-kv b{font-weight:600;display:flex;gap:6px;align-items:center;flex-wrap:wrap}',
    '.fh-nrow{display:flex;align-items:center;gap:10px;font-size:13px;padding:5px 8px;border-radius:3px}',
    '.fh-nrow.fh-next{background:rgba(140,197,106,.13)}.fh-nrow.fh-out{opacity:.45}.fh-ordn{width:16px;text-align:center;color:var(--t4);font-size:12px}',
    '.fh-pcard{padding:10px 12px;border-radius:3px;background:var(--cf);border:1px solid transparent;margin-bottom:6px;cursor:pointer}.fh-pcard:hover{background:rgba(92,76,56,.7)}',
    '.fh-pcard.fh-on{border-color:var(--ac);box-shadow:var(--glow)}.fh-pcard .fh-nm{font-size:16px;font-weight:700}',
    '.fh-ics{display:flex;gap:3px;margin-top:6px}',
    '.fh-hint{font-size:12px;color:var(--t3);line-height:1.45;padding:2px}',
    '.fh-edhd{display:flex;align-items:center;gap:10px;margin-bottom:10px}',
    '.fh-cols3{display:grid;gap:10px;margin-bottom:10px}',
    '.fh-colh{display:flex;font-size:13px;font-weight:700;color:var(--t2);letter-spacing:1px;text-transform:uppercase;margin-bottom:6px}',
    '.fh-fb{display:flex;flex-direction:column;gap:4px;min-width:0;padding:8px;border-radius:3px;background:rgba(0,0,0,.18);border:1px solid var(--ls)}',
    '.fh-it{display:grid;grid-template-columns:18px 24px minmax(0,1fr) auto;gap:6px;align-items:center;padding:5px 7px;border-radius:3px;background:var(--cf);border-left:2px solid transparent}',
    '.fh-it.fh-short{border-left-color:var(--rd)}.fh-it.fh-go{border-left-color:var(--gr)}.fh-it.fh-out{opacity:.55}',
    '.fh-o{width:18px;height:18px;border-radius:50%;display:flex;align-items:center;justify-content:center;background:rgba(255,200,58,.18);color:var(--ac);font-weight:700;font-size:11px}.fh-go .fh-o{background:rgba(140,197,106,.3);color:var(--gr)}',
    '.fh-sinfo{min-width:0}.fh-sinfo b{display:block;font-size:13px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-sinfo small{display:block;font-size:11px;color:var(--t3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-sctl{display:flex;flex-direction:column;align-items:flex-end;gap:4px}.fh-moves{display:flex;gap:3px}',
    '.fh-picker{display:flex;flex-direction:column;gap:3px;padding:6px;border-radius:3px;background:rgba(0,0,0,.35);border:1px solid rgba(255,200,58,.4)}',
    '.fh-plist{display:flex;flex-direction:column;gap:2px}',
    '.fh-prow{display:flex;align-items:center;gap:8px;font-size:12px;padding:4px;border-radius:3px;cursor:pointer}.fh-prow:hover{background:rgba(255,200,58,.12)}',
    '.fh-chain{display:flex;flex-wrap:wrap;align-items:center;gap:8px;margin-bottom:8px}',
    '.fh-link{display:inline-flex;align-items:center;gap:6px;padding:4px 6px 4px 4px;border-radius:3px;background:var(--cf);border:1px solid var(--ls);font-size:13px}',
    '.fh-link.fh-out .fh-lname{text-decoration:line-through;color:var(--t4)}.fh-arrow{color:var(--t4)}',
    '.fh-ctable{display:flex;flex-direction:column;gap:4px;margin-bottom:10px}',
    '.fh-crow{display:grid;grid-template-columns:150px minmax(0,1fr) 150px 100px 22px;gap:10px;align-items:center;padding:7px 10px;border-radius:3px;background:var(--cf);font-size:13px}',
    '.fh-crow.fh-chead{background:none;padding:2px 10px;color:var(--t4);font-size:11px;letter-spacing:1px;text-transform:uppercase}',
    '.fh-crow .fh-chips{margin:0}.fh-cname{display:flex;align-items:center;gap:8px;min-width:0;white-space:nowrap;overflow:hidden}',
    '.fh-setrow{display:flex;align-items:center;gap:14px;padding:10px 14px;border-radius:3px;background:var(--cf);margin-bottom:6px}',
    '.fh-lbl{flex:1;min-width:0}.fh-lbl b{display:block;font-size:14px;font-weight:600;color:#fff}.fh-lbl small{display:block;font-size:12px;color:var(--t3);margin-top:2px}',
    '.fh-warnbox{padding:10px 14px;border-radius:3px;border:1px solid rgba(255,167,38,.55);background:rgba(255,167,38,.10);color:#ffcc80;font-size:14px;margin-bottom:8px;line-height:1.5}',
    '.fh-prow2{display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:3px;background:var(--cf);font-size:13px;margin-bottom:4px}',
    '.fh-trow{display:grid;gap:6px;align-items:center;padding:5px 4px;border-bottom:1px solid var(--ls);font-size:13px}',
    '.fh-trow.fh-thead{color:var(--t4);font-size:12px;white-space:nowrap}.fh-trow .fh-tc{text-align:right;font-variant-numeric:tabular-nums}',
    '.fh-rgrid{display:grid;grid-template-columns:repeat(auto-fill,minmax(600px,1fr));gap:12px;align-items:start}',
    '.fh-rcard{padding:12px 14px;border-radius:3px;background:var(--cd);border:1px solid var(--ls)}',
    '.fh-rcard .fh-hd{display:flex;align-items:center;gap:12px;margin-bottom:10px}.fh-rcard .fh-hd .fh-nm{font-size:18px;font-weight:700}',
    '.fh-tier{display:grid;grid-template-columns:110px minmax(0,1.2fr) minmax(0,1fr) auto auto;gap:10px;align-items:center;padding:7px 10px;border-radius:3px;background:rgba(0,0,0,.18);margin-bottom:4px;font-size:13px}',
    '.fh-tier.fh-thead{background:none;color:var(--t4);font-size:12px;padding:2px 10px}',
    '.fh-tcell{display:flex;align-items:center;gap:8px;min-width:0;white-space:nowrap;overflow:hidden}',
    '.fh-tedit{padding:10px 12px;margin-bottom:6px;border-radius:3px;border:1px solid rgba(255,200,58,.5);background:rgba(255,200,58,.05);display:flex;flex-direction:column;gap:6px}',
    '.fh-tedit-fert{display:flex;flex-direction:column;gap:6px}',
    '.fh-tnote{padding:0 10px 6px;margin-top:-2px;color:var(--t3);font-size:12px}',
    '.fh-row2{display:flex;align-items:center;gap:8px;flex-wrap:wrap}',
    '.fh-icon{flex:none;display:flex;align-items:center;justify-content:center;border-radius:3px;background:rgba(0,0,0,.25);overflow:hidden;color:var(--t2);font-weight:600}',
    '.fh-icon img{width:100%;height:100%;object-fit:contain}',
    '.fh-i56{width:64px;height:64px;font-size:22px}.fh-i40{width:42px;height:42px;font-size:15px}.fh-i30{width:30px;height:30px;font-size:12px}.fh-i22{width:22px;height:22px;font-size:11px}.fh-i18{width:18px;height:18px;font-size:10px}',
    '.fh-none{padding:24px;color:var(--t3);font-size:14px;text-align:center}',
    '.fh-note{font-size:12px;color:var(--t3);padding:4px 2px 8px}',
    '.fh-banner{padding:8px 12px;margin-top:6px;border-radius:3px;border:1px solid rgba(255,167,38,.55);background:rgba(255,167,38,.10);color:#ffcc80;font-size:13px}',
    '.fh-lockmsg{padding:12px 16px;border-radius:3px;border:1px solid rgba(255,200,58,.6);background:rgba(255,200,58,.10);color:var(--ac);font-size:15px}',
    '.fh-grow{flex:1;min-width:0}.fh-dim{color:var(--t3);font-size:12px}.fh-red{color:var(--rd)}.fh-green{color:var(--gr)}.fh-or{color:var(--or)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = el('div');
    var win = el('div', 'fh-win');
    var head = el('div', 'fh-whd');
    var title = el('div', 'fh-ttl', 'Farm Hub');
    var sum = el('small');
    var tabs = el('div', 'fh-tabs');
    var body = el('div', 'fh-wbody');
    var foot = el('div', 'fh-wfoot');
    var status = el('div', 'fh-status');
    var keys = el('div');
    var esc = el('div');

    root.id = 'fh-root';
    title.appendChild(sum);
    put(head, put(el('div', 'fh-logo'), sprout(document)), title, tabs, plainButton('fh-x', '\u00d7', 'close', null, true));
    put(foot, status, keys, esc);
    put(win, head, body, foot);
    root.appendChild(win);
    document.body.appendChild(root);

    // Drawn at the player's UI size (Exiled's Toolkit). The full-screen mask always fills the screen, so the window on
    // it is what must fit.
    uiSize(root, { fit: win });

    root.addEventListener('click', onClick);
    root.addEventListener('input', onInput);
    root.addEventListener('focusin', onFocus);
    root.addEventListener('focusout', onBlur);
    root.addEventListener('keydown', onKey);
    // Clicks inside the window must not reach the game pages underneath; the mask itself closes nothing on purpose.
    root.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    // A broken icon path swaps to the first-letter placeholder; error events do not bubble, so capture them.
    root.addEventListener('error', function (e) {
      var img = e.target;

      if (img && img.tagName === 'IMG' && img.parentNode) img.parentNode.textContent = img.getAttribute('data-letter') || '?';
    }, true);

    ui = { root: root, sum: sum, tabs: tabs, body: body, status: status, keys: keys, esc: esc, live: [], rows: null, side: null };
  }

  // Hands a panel to Exiled's Toolkit to be drawn at the player's UI size. Before the Toolkit's script has loaded it
  // waits in a list the Toolkit reads; without the Toolkit nothing happens.
  function uiSize(node, options) {
    options = Object.assign({ mod: 'FarmHub' }, options);
    var toolkit = window.ExiledUi;

    if (toolkit) {
      toolkit.size(node, options);
      return;
    }

    var waiting = window.__exiledUiWait = window.__exiledUiWait || [];
    waiting.push([node, options]);
    if (waiting.length > 32) waiting.shift();
  }

  // One copy, which outlives a lost window root. A window drawn before the Toolkit's page API is there gets it when
  // the API arrives.
  function addStyle() {
    if (etk) etk.addStyle(document, 'fh-style', CSS);
  }

  function onClick(e) {
    var target = e.target.closest('[data-act]');

    if (!target) return;

    e.stopPropagation();

    if (target.disabled) return;

    var action = target.getAttribute('data-act');
    var args = target.getAttribute('data-args');
    var armKey = action + '|' + (args || '');

    if (CONFIRMED[action] && state.armed !== armKey) {
      state.armed = armKey;
      render();

      return;
    }

    state.armed = '';

    if (LOCAL[action]) {
      LOCAL[action](args);

      return;
    }

    // Opening or shutting the window gets no status line back, so it must not hold the buttons busy.
    if (action === 'close' || action === 'toggle') {
      send(action);

      return;
    }

    if (action === 'savepreset') {
      savePreset();

      return;
    }

    if (action === 'settier') {
      setTier();

      return;
    }

    sendAction(args != null ? action + '|' + args : action);
  }

  // Text boxes: typing pauses the game's keys, and the window is not redrawn under the cursor.
  function onFocus(e) {
    if (!isTextBox(e.target)) return;

    state.isTyping = true;
    send('focus|1');
  }

  function onBlur(e) {
    if (!isTextBox(e.target)) return;

    state.isTyping = false;
    send('focus|0');

    if (state.isStale) render();
  }

  function onKey(e) {
    if (e.key === 'Escape' && isTextBox(e.target) && e.target.blur) e.target.blur();
  }

  function onInput(e) {
    var field = e.target.getAttribute && e.target.getAttribute('data-field');

    if (field === 'name' && state.draft) {
      state.draft.name = e.target.value;
      markDirty();
    } else if (field === 'potsearch') {
      state.potSearch = e.target.value;

      if (ui.potList) fillPotList();
    } else if (field === 'addsearch') {
      state.addSearch = e.target.value;
      fillSeedAdd();
    } else if (field === 'cropsearch') {
      state.cropSearch = e.target.value;
      fillCropAdd();
    }
  }

  function isTextBox(node) {
    return !!node && node.tagName === 'INPUT';
  }

  // ---------- data ----------

  function catalog(data) {
    cat = { kinds: {}, crops: {}, seeds: {}, ferts: {}, seedList: [], fertList: [], cropList: [], caps: [] };

    ((data && data.kinds) || []).forEach(function (k) {
      cat.kinds[k.id] = k;

      if (cat.caps.indexOf(k.cap) < 0) cat.caps.push(k.cap);
    });
    ((data && data.crops) || []).forEach(function (c) { cat.crops[c.id] = c; cat.cropList.push(c); });
    ((data && data.seeds) || []).forEach(function (s) { cat.seeds[s.id] = s; cat.seedList.push(s); });
    ((data && data.ferts) || []).forEach(function (f) { cat.ferts[f.id] = f; cat.fertList.push(f); });
    cat.caps.sort(function (a, b) { return a - b; });
    catSeq++;

    if (state.isOpen) render();
  }

  function kindOf(id) { return cat.kinds[id] || { id: id, name: '#' + id, cap: 1, light: 0, heat: 0, power: false, faster: 0 }; }

  function cropOf(id) { return cat.crops[id] || { id: id, name: '#' + id, size: 1, grow: 0, harvest: 0, feed: false }; }

  function seedOf(id) { return cat.seeds[id] || { id: id, name: '#' + id, plant: 0 }; }

  function fertOf(id) { return cat.ferts[id] || { id: id, name: '#' + id, speed: 0 }; }

  function cropOfSeed(id) { return cropOf(seedOf(id).plant); }

  function presets() { return state.data.presets || []; }

  function presetById(id) { return find(presets(), function (p) { return p.id === id; }); }

  function owned(itemId) { return (state.data.owned || {})[itemId] || 0; }

  function pots() { return state.data.pots || []; }

  function potById(id) { return find(pots(), function (p) { return p.id === id; }); }

  function find(list, test) {
    for (var i = 0; i < list.length; i++) {
      if (test(list[i])) return list[i];
    }

    return null;
  }

  function byName(a, b) { return a.name < b.name ? -1 : a.name > b.name ? 1 : 0; }

  // Hours as the window shows them: one decimal under ten hours, whole hours above.
  function hours(h) {
    return say('hours', h < 10 ? Math.round(h * 10) / 10 : Math.round(h));
  }

  function sizeName(cap) {
    return SIZE_WORD[cap] ? say(SIZE_WORD[cap]) : say('tierCap', cap);
  }

  function perPot(seedId, cap) {
    var size = cropOfSeed(seedId).size || 1;

    return size > cap ? 0 : Math.floor(cap / size);
  }

  // How many pots of a size a seed step can still fill: the seeds above its keep count, a pot's worth at a time.
  function plantings(step, cap) {
    var each = perPot(step.item, cap);

    return each ? Math.floor(Math.max(0, owned(step.item) - step.keep) / each) : 0;
  }

  function potName(pot) {
    return pot.plant ? cropOf(pot.plant).name : kindOf(pot.kind).name;
  }

  function hasProblem(pot, names) {
    return (pot.problems || []).some(function (p) { return names.indexOf(p) >= 0; });
  }

  // Anything the player must walk over for besides a harvest: a dead crop, pests, weeds, water, a bad spot.
  function needsCare(pot) {
    return (!!pot.chore && pot.chore !== 'harvest') || (pot.problems || []).length > 0 || pot.state === 'withered' || pot.state === 'poor';
  }

  function needsYou(pot) {
    return !!pot.chore || !!pot.rotSoon || needsCare(pot);
  }

  // Lower is sooner: rotting crops, dead crops, ripe ones by how long they keep, other chores, then growing ones by
  // how long they have left.
  function urgency(pot) {
    if (pot.rotSoon) return [0, pot.rotIn || 0];
    if (pot.state === 'withered' || pot.state === 'poor') return [1, 0];
    if (pot.state === 'ripe') return [2, pot.rotIn == null ? 1e6 : pot.rotIn];
    if (needsCare(pot)) return [3, 0];
    if (pot.state === 'growing') return [4, pot.ripeIn == null ? 1e6 : pot.ripeIn];

    return [5, 0];
  }

  function compareKeys(a, b) {
    return a[0] - b[0] || a[1] - b[1];
  }

  // ---------- frame ----------

  // The window as data without its clocks: when only the clocks moved, the times are written in place instead of
  // rebuilding every card, which is what kept the window smooth while the game runs.
  function shapeOf(data) {
    return JSON.stringify(data, function (key, value) { return key === 'ripeIn' || key === 'rotIn' || key === 'progress' ? undefined : value; });
  }

  function render() {
    if (!ui || !state.isOpen) return;

    // Typing into a box must not lose the cursor to a refresh; the refresh shows when the box loses focus.
    if (state.isTyping) {
      state.isStale = true;

      return;
    }

    state.isStale = false;

    var scrolls = keepScrolls();

    ui.live = [];
    ui.rows = null;
    ui.side = null;
    renderHead();
    renderTabs();
    ui.body.textContent = '';

    if (state.tab === 'settings') renderSettings();
    else if (!state.data.ready) ui.body.appendChild(lockMessage(state.data.note));
    else if (state.tab === 'presets') renderPresets();
    else if (state.tab === 'robots') renderRobots();
    else renderPots();

    restoreScrolls(scrolls);
    renderKeys();
  }

  // A node that shows a pot's clock: written now, and again whenever the clocks move.
  function live(node, potId, update) {
    ui.live.push({ node: node, id: potId, update: update });
    update(node, potById(potId));

    return node;
  }

  function updateLive() {
    var byId = {};

    pots().forEach(function (p) { byId[p.id] = p; });
    ui.live = ui.live.filter(function (entry) { return ui.root.contains(entry.node); });
    ui.live.forEach(function (entry) {
      if (byId[entry.id]) entry.update(entry.node, byId[entry.id]);
    });
  }

  function keepScrolls() {
    var kept = {};

    ui.body.querySelectorAll('[data-scroll]').forEach(function (node) { kept[node.getAttribute('data-scroll')] = node.scrollTop; });

    return kept;
  }

  function restoreScrolls(kept) {
    ui.body.querySelectorAll('[data-scroll]').forEach(function (node) {
      var top = kept[node.getAttribute('data-scroll')];

      if (top) node.scrollTop = top;
    });
  }

  function scroller(key, className) {
    var node = el('div', 'fh-scroll' + (className ? ' ' + className : ''));
    node.setAttribute('data-scroll', key);

    return node;
  }

  function lockMessage(content) {
    var box = el('div', 'fh-col');
    box.style.flex = '1';
    box.appendChild(el('div', 'fh-lockmsg', content || ''));

    return box;
  }

  function renderHead() {
    var c = state.data.counts;

    ui.sum.textContent = '';

    if (!state.data.ready || !c) return;

    var need = pots().filter(needsYou).length;

    ui.sum.appendChild(text(say('sumPots', c.total)));

    if (need) put(ui.sum, text(' \u00b7 '), sayCount('cNeed', need, ''));
    if (c.empty) put(ui.sum, text(' \u00b7 ' + say('cEmpty', c.empty)));
  }

  function renderTabs() {
    ui.tabs.textContent = '';

    TABS.forEach(function (tab) {
      var node = act(el('button', 'fh-tab' + (state.tab === tab ? ' fh-on' : ''), say(TAB_WORD[tab])), 'tab', tab);

      if (tab === 'pots' && state.badge > 0) node.appendChild(el('span', 'fh-n', String(state.badge)));

      ui.tabs.appendChild(node);
    });
  }

  // The footer names the game's Tend Garden key too, unless Farm Hub's own key is that same key.
  function renderKeys() {
    ui.keys.textContent = '';

    if (state.key !== GAME_TEND_KEY) put(ui.keys, el('span', 'fh-kbd', GAME_TEND_KEY), text(say('keyTend')), state.key ? text(' \u00b7 ') : null);
    if (state.key) put(ui.keys, el('span', 'fh-kbd', state.key), text(say('keyThis')));

    ui.esc.textContent = say('escClose');
  }

  function pickTab(tab) {
    if (TABS.indexOf(tab) < 0) return;

    state.tab = tab;
    render();
  }

  // ---------- Pots tab ----------

  function renderPots() {
    var main = el('div', 'fh-main');
    var search = el('input', 'fh-inp');

    search.setAttribute('data-field', 'potsearch');
    search.setAttribute('placeholder', say('search'));
    search.value = state.potSearch;
    ui.potList = scroller('pots', 'fh-grow');
    ui.side = el('div', 'fh-side');
    put(main, put(el('div', 'fh-tool'), segment(SHOWS.map(function (s) {
      return [s[0], say(s[1]), pots().filter(function (p) { return shows(p, s[0]); }).length];
    }), state.show, 'show'), search), ui.potList);
    fillPotList();
    fillSide();
    put(ui.body, main, ui.side);
  }

  function shows(pot, show) {
    switch (show) {
      case 'needs':
        return needsYou(pot);
      case 'empty':
        return pot.state === 'empty';
      case 'growing':
        return pot.state === 'growing';
      default:
        return true;
    }
  }

  function roomKey(pot) {
    return pot.floor + '|' + pot.area;
  }

  function matchesSearch(pot) {
    var query = state.potSearch.trim().toLowerCase();

    if (!query) return true;

    return [potName(pot), kindOf(pot.kind).name, pot.room || ''].some(function (words) { return String(words).toLowerCase().indexOf(query) >= 0; });
  }

  // Rooms keep the order the plugin sends (floor, then room), so they read top floor to bottom as the house does;
  // inside a room the pots that need the player soonest come first.
  function fillPotList() {
    var well = ui.potList;
    var shown = pots().filter(function (p) { return shows(p, state.show) && matchesSearch(p); });
    var groups = [];

    well.textContent = '';
    ui.rows = {};

    if (!pots().length) {
      well.appendChild(el('div', 'fh-none', say('empty')));

      return;
    }

    if (!shown.length) {
      well.appendChild(el('div', 'fh-none', say('noMatch')));

      return;
    }

    shown.forEach(function (pot) {
      var key = roomKey(pot);
      var group = find(groups, function (g) { return g.key === key; });

      if (!group) groups.push(group = { key: key, first: pot, pots: [] });

      group.pots.push(pot);
    });

    groups.forEach(function (group) {
      var grid = el('div', 'fh-grid2');

      sorted(group.pots).forEach(function (pot) { grid.appendChild(potRow(pot)); });
      well.appendChild(put(el('div', 'fh-grp'), groupHead(group), grid));
    });

    // Rows kept for pots that are gone, as after loading another save, are let go.
    var listed = {};

    pots().forEach(function (p) { listed[p.id] = true; });
    Object.keys(rowCache).forEach(function (id) {
      if (!listed[id]) delete rowCache[id];
    });
  }

  function groupHead(group) {
    var pot = group.first;
    var preset = presetById(pot.preset);
    var where = say('floor', pot.floor) + (pot.floor === state.data.floor ? ' (' + say('here') + ')' : '') + ' \u00b7 ' + (group.pots.length === 1 ? say('onePot') : say('nPots', group.pots.length));
    var presetChip = preset ? chip(preset.name, 'editpreset', preset.id, false) : null;

    if (presetChip) setClass(presetChip, 'fh-r', true);

    return put(el('div', 'fh-grp-h'), el('b', '', pot.room || say('otherRoom')), el('span', '', where), presetChip);
  }

  function sorted(list) {
    var keyed = list.map(function (pot, i) { return { pot: pot, i: i }; });

    keyed.sort(function (a, b) { return compareKeys(urgency(a.pot), urgency(b.pot)) || a.i - b.i; });

    return keyed.map(function (k) { return k.pot; });
  }

  function edgeClass(pot) {
    if (pot.rotSoon || pot.state === 'withered' || pot.state === 'poor') return ' fh-e-red';
    if ((pot.chore && pot.chore !== 'harvest') || (pot.problems || []).length) return ' fh-e-or';
    if (pot.state === 'ripe') return ' fh-e-gold';

    return '';
  }

  // A pot whose data, catalog and words are as when its row was drawn keeps that row: only its clocks are written and
  // its picked mark set. A redraw of the list then builds only the rows of pots that changed.
  function potRow(pot) {
    var key = shapeOf(pot) + '|' + catSeq + '|' + wordsKey();
    var kept = rowCache[pot.id];

    if (kept && kept.key === key) {
      kept.live.forEach(function (entry) {
        ui.live.push(entry);
        entry.update(entry.node, pot);
      });

      if (kept.node.classList.contains('fh-sel-on') !== (pot.id === state.sel)) setClass(kept.node, 'fh-sel-on', pot.id === state.sel);

      ui.rows[pot.id] = kept.node;

      return kept.node;
    }

    var liveFrom = ui.live.length;
    var kind = kindOf(pot.kind);
    var crop = pot.plant ? cropOf(pot.plant) : null;
    var row = act(el('div', 'fh-row' + edgeClass(pot) + (crop ? '' : ' fh-blank') + (pot.id === state.sel ? ' fh-sel-on' : '')), 'pick', pot.id);
    var name = put(el('div', 'fh-nm'), text(crop ? crop.name : say('stEmpty')), el('small', '', kind.name));
    var end = el('div', 'fh-end');

    end.appendChild(pot.state === 'empty' && pot.canPlant ? button(say('plant'), 'plant', pot.id, !pot.pending, 'fh-sm fh-q') : statePill(pot));
    put(row, icon(crop || kind, 'fh-i40'), put(el('div', 'fh-mid'), name, metaLine(pot)), end);
    ui.rows[pot.id] = row;
    rowCache[pot.id] = { key: key, node: row, live: ui.live.slice(liveFrom) };

    return row;
  }

  function metaLine(pot) {
    var line = el('div', 'fh-meta');

    if (pot.state === 'growing') put(line, progressBar(pot), live(el('span'), pot.id, growingText));
    else if (pot.state === 'ripe') line.appendChild(live(el('span'), pot.id, ripeText));
    else if (pot.state === 'empty') line.appendChild(nextPlantLine(pot));

    if ((pot.problems || []).length) line.appendChild(el('span', 'fh-or', problemText(pot)));
    if (pot.robot) line.appendChild(badge(say('robot'), 'fh-p-dim'));
    if (pot.pending) line.appendChild(badge(say('queued'), 'fh-p-good'));
    if (!pot.powered) line.appendChild(badge(say('unpowered'), 'fh-p-urgent'));

    return line;
  }

  function statePill(pot) {
    if (pot.rotSoon) return badge(say('rotSoon'), 'fh-p-urgent');
    if (pot.state === 'withered' || pot.state === 'poor') return badge(say(STATE_WORD[pot.state]), 'fh-p-urgent');
    if (pot.chore && pot.chore !== 'harvest') return badge(say(CHORE_WORD[pot.chore]), 'fh-p-warn');
    if (pot.state === 'ripe') return badge(say('stRipe'), 'fh-p-ready');
    if (pot.state === 'growing') return badge(say('stGrowing'), 'fh-p-good');

    return badge(say(STATE_WORD[pot.state] || pot.state), 'fh-p-dim');
  }

  function progressBar(pot, isWide) {
    var track = el('span', 'fh-bar' + (isWide ? ' fh-wide' : ''));
    var fill = el('i');

    track.appendChild(fill);

    return live(track, pot.id, function (node, p) { fill.style.width = Math.max(0, Math.min(100, p.progress | 0)) + '%'; });
  }

  function growingText(node, pot) {
    node.textContent = pot.ripeIn != null ? say('toRipe', hours(pot.ripeIn)) : pot.progress + '%';
  }

  function ripeText(node, pot) {
    node.textContent = pot.rotIn != null ? say('beforeRot', hours(pot.rotIn)) : say('noRot');
    node.className = pot.rotSoon ? 'fh-red' : 'fh-green';
  }

  function nextPlantLine(pot) {
    if (!pot.canPlant) return el('span', 'fh-dim', '');
    if (!pot.next) return el('span', 'fh-red', say('nextNone'));

    return el('span', 'fh-green', say('nextShort', seedOf(pot.next.seed).name, pot.next.count));
  }

  function problemText(pot) {
    return (pot.problems || []).map(function (p) { return say(PROBLEM_WORD[p] || p); }).join(', ');
  }

  function roomTitle(pot) {
    return (pot.room || say('otherRoom')) + ' \u00b7 ' + say('floor', pot.floor);
  }

  // The right side: a picked pot's detail, else what needs the player.
  function fillSide() {
    var pot = potById(state.sel);

    ui.side.textContent = '';

    if (pot) potDetail(ui.side, pot);
    else needsPanel(ui.side);
  }

  // Up to a few distinct names, then how many more.
  function names(list) {
    var seen = [];

    list.forEach(function (n) {
      if (n && seen.indexOf(n) < 0) seen.push(n);
    });

    return seen.slice(0, MAX_NAMES).join(', ') + (seen.length > MAX_NAMES ? ' +' + (seen.length - MAX_NAMES) : '');
  }

  function todo(count, title, sub, show, countClass) {
    return put(act(el('div', 'fh-todo'), 'show', show), put(el('div', 'fh-t'), text(title), sub ? el('small', '', sub) : null), el('span', 'fh-cnt' + (countClass ? ' ' + countClass : ''), String(count)));
  }

  function needsPanel(side) {
    var c = state.data.counts || {};
    var harvest = pots().filter(function (p) { return p.chore === 'harvest'; });
    var care = pots().filter(function (p) { return p.chore && p.chore !== 'harvest'; });
    var spot = pots().filter(function (p) { return !p.chore && (p.problems || []).length; });
    var jobs = harvest.length + care.length + spot.length;
    var scroll = scroller('side', 'fh-grow');
    var rotting = harvest.some(function (p) { return p.rotSoon; });

    scroll.appendChild(section(say('needsYou'), el('span', 'fh-r', say('nJobs', jobs))));

    if (harvest.length) scroll.appendChild(todo(harvest.length, say('tHarvest'), names(harvest.map(potName)), 'needs', rotting ? 'fh-red' : ''));
    if (care.length) scroll.appendChild(todo(care.length, say('todoCare'), names(care.map(function (p) { return say(CHORE_WORD[p.chore]); })), 'needs'));
    if (spot.length) scroll.appendChild(todo(spot.length, say('todoSpot'), names(spot.map(problemText)), 'needs'));
    if (c.plant) scroll.appendChild(todo(c.plant, say('todoPlant'), '', 'empty'));
    if (c.fert) scroll.appendChild(todo(c.fert, say('fertilize'), '', 'growing', 'fh-dim'));
    if (!jobs && !c.plant && !c.fert) scroll.appendChild(el('div', 'fh-none', say('allFine')));

    scroll.appendChild(section(say('comingUp')));
    comingRows(scroll);
    put(side, scroll, put(el('div', 'fh-cta'), button(say('tendAll', jobs + (c.plant || 0) + (c.fert || 0)), 'tendall', null, true, 'fh-gold fh-big'),
      el('div', 'fh-cap', say('tendNote')),
      put(el('div', 'fh-subacts'), button(say('plantAll', c.plant || 0), 'plantall', null, true), button(say('fertAll', c.fert || 0), 'fertall', null, true))));
  }

  // What ripens or rots in the next two days, soonest first; a row opens its pot.
  function comingRows(well) {
    var events = [];

    pots().forEach(function (pot) {
      if (pot.state === 'growing' && pot.ripeIn != null && pot.ripeIn <= FORECAST_HOURS) events.push({ pot: pot, at: pot.ripeIn, isRot: false });
      if (pot.state === 'ripe' && pot.rotIn != null && pot.rotIn <= FORECAST_HOURS) events.push({ pot: pot, at: pot.rotIn, isRot: true });
    });
    events.sort(function (a, b) { return a.at - b.at; });

    if (!events.length) well.appendChild(el('div', 'fh-none', say('nothingSoon')));

    events.slice(0, MAX_COMING).forEach(function (e) {
      var time = live(el('span', 'fh-tm'), e.pot.id, function (n, p) {
        var h = e.isRot ? p.rotIn : p.ripeIn;

        n.textContent = h != null ? say('inHours', hours(h)) : '-';
      });

      well.appendChild(put(act(el('div', 'fh-fc'), 'pickat', e.pot.id), time,
        put(el('div', 'fh-cut'), text(potName(e.pot) + ' '), el('span', 'fh-wh', roomTitle(e.pot))),
        e.isRot ? badge(say('rotsPill'), 'fh-p-urgent') : badge(say('readyPill'), 'fh-p-good')));
    });
  }

  function potDetail(side, pot) {
    var kind = kindOf(pot.kind);
    var crop = pot.plant ? cropOf(pot.plant) : null;
    var scroll = scroller('detail', 'fh-grow');
    var subs = el('div', 'fh-subacts');

    put(scroll, button('\u2039 ' + say('backNeeds'), 'back', null, true, 'fh-ghost fh-sm'),
      put(el('div', 'fh-det-hd'), icon(crop || kind, 'fh-i56'),
        put(el('div', 'fh-mid'), el('div', 'fh-nm', crop ? crop.name : say('stEmpty')), el('div', 'fh-wh', kind.name + ' \u00b7 ' + roomTitle(pot))), statePill(pot)));

    if (pot.state === 'growing') {
      scroll.appendChild(put(el('div', 'fh-tl'), progressBar(pot, true), put(el('div', 'fh-tl-lab'),
        live(el('span'), pot.id, function (n, p) { n.textContent = say('grown') + ' ' + p.progress + '%'; }), live(el('span'), pot.id, growingText))));
    }

    put(scroll, section(say('timing')), timingGrid(pot, kind), section(say('thisPot')), presetPick(pot), section(say('nextPlanting')), nextPlantingList(pot, kind));

    if (pot.canPlant) subs.appendChild(button(say('plant'), 'plant', pot.id, !pot.pending));
    if ((pot.nextFert || 0) > 0) subs.appendChild(button(say('fertilize'), 'fert', pot.id, !pot.pending));

    put(side, scroll, put(el('div', 'fh-cta'), pot.chore ? button(say(CHORE_WORD[pot.chore]), 'tend', pot.id, !pot.pending, 'fh-gold fh-big') : null,
      subs.firstChild ? subs : null));
  }

  function kv(grid, key, value) {
    put(grid, el('span', 'fh-k', key), typeof value === 'string' ? el('b', '', value) : value);
  }

  function timingGrid(pot, kind) {
    var grid = el('div', 'fh-kv');
    var stateLine = put(el('b'), text(say(STATE_WORD[pot.state] || pot.state)));
    var bonus = [];

    if (pot.rotSoon) stateLine.appendChild(badge(say('rotSoon'), 'fh-p-urgent'));

    (pot.problems || []).forEach(function (p) { stateLine.appendChild(badge(say(PROBLEM_WORD[p] || p), 'fh-p-warn')); });
    kv(grid, say('state'), stateLine);

    if (pot.state === 'growing') kv(grid, say('ripeIn'), live(el('b'), pot.id, function (n, p) { n.textContent = p.ripeIn != null ? hours(p.ripeIn) : '-'; }));
    if (pot.state === 'ripe') kv(grid, say('rotIn'), live(el('b'), pot.id, function (n, p) { n.textContent = p.rotIn != null ? hours(p.rotIn) : say('noRot'); n.className = p.rotSoon ? 'fh-red' : ''; }));
    if (pot.state !== 'empty') kv(grid, say('fertWord'), pot.fert ? fertOf(pot.fert).name : say('noFert'));
    if ((pot.nextFert || 0) > 0) kv(grid, say('fertNext'), fertOf(pot.nextFert).name);
    if (pot.pending) kv(grid, '', say('queued'));

    if (kind.light) bonus.push(say('light', kind.light));
    if (kind.heat) bonus.push(say('heat', kind.heat));
    if (kind.faster) bonus.push(say('faster', Math.round(kind.faster * 100)));

    kv(grid, say('size'), sizeName(kind.cap));
    kv(grid, say('bonus'), bonus.length ? bonus.join(', ') : say('none'));

    if (kind.power) kv(grid, say('power'), pot.powered ? say('ok') : say('unpowered'));
    if (pot.robot) kv(grid, say('robot'), say('ok'));

    return grid;
  }

  // The pot's preset order for its size, with what the player owns and the seed the next planting will use.
  function nextPlantingList(pot, kind) {
    var preset = presetById(pot.preset);
    var steps = preset ? (preset.seeds || {})[kind.cap] || [] : [];
    var box = el('div');
    var nextSeed = pot.next ? pot.next.seed : (find(steps, function (s) { return plantings(s, kind.cap) > 0; }) || {}).item;

    if (!steps.length) {
      box.appendChild(el('div', 'fh-dim', preset ? say('nextNone') : say('noPreset')));

      return box;
    }

    steps.forEach(function (step, i) {
      var seed = seedOf(step.item);
      var isNext = step.item === nextSeed;
      var isOut = !plantings(step, kind.cap);
      var fits = perPot(step.item, kind.cap) > 0;

      box.appendChild(put(el('div', 'fh-nrow' + (isNext ? ' fh-next' : isOut ? ' fh-out' : '')), el('span', 'fh-ordn', String(i + 1)), icon(seed, 'fh-i22'),
        put(el('span', 'fh-grow'), text(cropOfSeed(step.item).name + ' '), step.keep ? el('span', 'fh-dim', say('keepN', step.keep)) : null),
        isNext ? badge(say('nextBadge'), 'fh-p-good') : null, el('span', isOut ? 'fh-red' : 'fh-dim', fits ? say('nSeeds', owned(step.item)) : say('tooBig'))));
    });
    box.appendChild(el('div', 'fh-note', say('plantNote')));

    return box;
  }

  // The pot's preset, where it comes from, and a pick of its own.
  function presetPick(pot) {
    var preset = presetById(pot.preset);
    var source = put(el('div', 'fh-row2'), el('b', '', preset ? preset.name : say('none')), preset ? el('span', 'fh-dim', say(FROM_WORD[pot.from] || 'fromAll')) : null);
    var pick = el('div', 'fh-chips');
    var isOwn = pot.from === 'pot';

    pick.appendChild(chip(say('useDefault'), 'assign', '|' + pot.id, !isOwn));
    presets().forEach(function (p) { pick.appendChild(chip(p.name, 'assign', p.id + '|' + pot.id, isOwn && p.id === pot.preset)); });

    if (!state.data.saved) pick.querySelectorAll('[data-act]').forEach(function (node) { node.disabled = true; });

    return put(el('div'), source, pick, state.data.saved ? null : el('div', 'fh-note', say('noSaveYet')));
  }

  // Picking only touches the rows involved and the side beside them; the list is not rebuilt.
  function pickPot(id) {
    state.sel = state.sel === id ? '' : id;
    showPicked();
  }

  function pickAt(id) {
    state.sel = id;
    showPicked();
  }

  function unpick() {
    state.sel = '';
    showPicked();
  }

  function showPicked() {
    if (state.tab !== 'pots' || !ui.rows || !ui.side) {
      state.tab = 'pots';
      render();

      return;
    }

    Object.keys(ui.rows).forEach(function (key) { setClass(ui.rows[key], 'fh-sel-on', key === state.sel); });
    fillSide();
  }

  // ---------- Planting presets tab ----------

  function renderPresets() {
    syncDraft();

    var left = el('div', 'fh-c330');
    var list = scroller('plist');
    var editor = scroller('editor', 'fh-ed');
    var right = el('div', 'fh-c330');
    var plan = scroller('plan');

    list.appendChild(section(say('presetsHead')));
    presets().forEach(function (p) { list.appendChild(presetCard(p)); });
    put(list, button('+ ' + say('newPreset'), 'newpreset', null, true, 'fh-q'), el('div', 'fh-hint', say('usedNote')));

    if (state.draft) {
      fillEditor(editor);
      fillPlan(plan);
    }

    fillStock(plan);
    left.appendChild(list);
    right.appendChild(plan);
    put(ui.body, left, editor, right);
  }

  function presetCard(p) {
    var icons = el('div', 'fh-ics');
    var seen = [];

    Object.keys(p.seeds || {}).forEach(function (cap) {
      p.seeds[cap].forEach(function (s) {
        var crop = cropOfSeed(s.item);

        if (seen.length < 6 && seen.indexOf(crop.id) < 0) {
          seen.push(crop.id);
          icons.appendChild(icon(crop, 'fh-i22'));
        }
      });
    });

    return put(act(el('div', 'fh-pcard' + (p.id === state.presetId ? ' fh-on' : '')), 'preset', p.id),
      el('div', 'fh-nm', p.name), el('div', 'fh-wh', (p.uses || []).length ? p.uses.map(useName).join(', ') : say('usedNowhere')), icons);
  }

  function useName(use) {
    if (use === 'all') return say('useAll');
    if (use.charAt(0) === 'f') return say('floor', use.substring(1));

    var room = find(state.data.rooms || [], function (r) { return 'a' + r.area === use; });

    return room ? room.name : use;
  }

  // The editor works on a copy, so a refresh from the game never throws away what the player is typing.
  function syncDraft() {
    if (state.pendingNewFrom) {
      var fresh = presets().filter(function (p) { return state.pendingNewFrom.indexOf(p.id) < 0; })[0];

      if (fresh) {
        state.pendingNewFrom = null;
        state.presetId = fresh.id;
        state.draft = null;
      }
    }

    if (state.draft && state.draftOf === state.presetId && (state.presetId === '' || presetById(state.presetId))) return;

    var source = presetById(state.presetId) || (state.presetId === '' && state.draft ? null : presets()[0]);

    if (!source) return;

    state.presetId = source.id;
    state.draft = copyPreset(source);
    state.draftOf = source.id;
  }

  function copyPreset(p) {
    var seeds = {};

    Object.keys(p.seeds || {}).forEach(function (cap) {
      seeds[cap] = p.seeds[cap].map(function (s) { return { item: s.item, keep: s.keep }; });
    });

    return {
      id: p.id, name: p.name, seeds: seeds, ferts: (p.ferts || []).slice(),
      perCrop: (p.perCrop || []).map(function (c) { return { plant: c.plant, order: c.order.slice(), whenOut: c.whenOut }; }),
      skipUnsuitable: p.skipUnsuitable, fertilize: p.fertilize, autoReplant: p.autoReplant, uses: (p.uses || []).slice()
    };
  }

  // Empty seed lists are not kept, so a pot size opened and left empty does not count as a change.
  function comparable(d) {
    var copy = JSON.parse(JSON.stringify(d));

    Object.keys(copy.seeds).forEach(function (cap) {
      if (!copy.seeds[cap].length) delete copy.seeds[cap];
    });
    copy.uses.sort();

    return JSON.stringify(copy);
  }

  function isDirty() {
    var saved = presetById(state.draftOf);

    return !saved || comparable(copyPreset(saved)) !== comparable(state.draft);
  }

  function fillEditor(editor) {
    var d = state.draft;
    var name = el('input', 'fh-inp fh-name');
    var isArmed = state.armed === 'deletepreset|' + d.id;

    name.setAttribute('data-field', 'name');
    name.setAttribute('placeholder', say('name'));
    name.value = d.name;

    put(editor,
      put(el('div', 'fh-edhd'), name, button(say('save'), 'savepreset', null, true, 'fh-gold'),
        d.id ? button(isArmed ? say('confirm') : say('delete'), 'deletepreset', d.id, true, 'fh-danger' + (isArmed ? ' fh-armed' : '')) : null,
        el('span', 'fh-grow'), el('span', 'fh-pill fh-p-warn fh-dirty', say('unsaved'))));
    markDirty(editor);
    put(editor, section(say('usedFor')), useChips(d));
    seedSection(editor);
    editor.appendChild(setRow(say('skipUnsuitable'), say('skipNote'), switchButton(d.skipUnsuitable, 'skip')));
    fertSection(editor);
    cropSection(editor);
    put(editor, setRow(say('fertilizeOn'), say('fertilizeNote'), switchButton(d.fertilize, 'fertilizeon')),
      section(say('afterHarvest')), setRow(say('autoReplant'), say('autoNote'), switchButton(d.autoReplant, 'autoreplant')));
  }

  // One column per pot size, each with its own seed order and its own add button.
  function seedSection(editor) {
    var cols = el('div', 'fh-cols3');

    cols.style.gridTemplateColumns = 'repeat(' + Math.max(1, cat.caps.length) + ',minmax(0,1fr))';
    cat.caps.forEach(function (cap) { cols.appendChild(capColumn(cap)); });
    put(editor, section(say('seedsBySize'), el('span', 'fh-r', say('seedNote'))), cols);
    fillSeedAdd();
  }

  function stepsFor(cap) {
    var d = state.draft;

    if (!d.seeds[cap]) d.seeds[cap] = [];

    return d.seeds[cap];
  }

  function capColumn(cap) {
    var steps = stepsFor(cap);
    var nextAt = firstPlantable(steps, cap);
    var col = put(el('div', 'fh-fb'), put(el('div', 'fh-colh'), el('span', 'fh-grow', sizeName(cap)), el('span', 'fh-dim', say('keep'))));

    steps.forEach(function (step, i) { col.appendChild(seedStep(step, i, cap, steps.length, i === nextAt)); });

    var isAdding = state.addCap === cap;

    col.appendChild(act(el('button', 'fh-chip fh-add' + (isAdding ? ' fh-on' : ''), say('addSeed')), 'addopen', cap));

    if (isAdding) col.appendChild(picker('addsearch', state.addSearch, 'addlist'));

    return col;
  }

  function firstPlantable(steps, cap) {
    for (var i = 0; i < steps.length; i++) {
      if (plantings(steps[i], cap) > 0) return i;
    }

    return -1;
  }

  function seedStep(step, i, cap, count, isNext) {
    var seed = seedOf(step.item);
    var crop = cropOfSeed(step.item);
    var each = perPot(step.item, cap);
    var have = owned(step.item);
    var row = el('div', 'fh-it' + (!each ? ' fh-out' : !plantings(step, cap) ? ' fh-short' : isNext ? ' fh-go' : ''));
    var detail = each ? say('nSeeds', have) + ' \u00b7 ' + hours(crop.grow) : say('tooBig');
    var args = cap + '|' + i;
    var info = put(el('div', 'fh-sinfo'), put(el('b'), el('span', 'fh-cut', crop.name)),
      el('small', !each || have - step.keep < each ? 'fh-red' : isNext ? 'fh-green' : '', detail));
    var stepper = put(el('div', 'fh-stepper'), plainButton('fh-stp', '-', 'keepless', args, step.keep > 0), el('b', '', String(step.keep)),
      plainButton('fh-stp', '+', 'keepmore', args, step.keep < MAX_KEEP));
    var moves = put(el('div', 'fh-moves'), plainButton('fh-ib', '\u2191', 'seedup', args, i > 0), plainButton('fh-ib', '\u2193', 'seeddown', args, i < count - 1),
      plainButton('fh-ib fh-del', '\u00d7', 'seeddel', args, true));

    put(row, el('span', 'fh-o', String(i + 1)), icon(seed, 'fh-i30'), info, put(el('div', 'fh-sctl'), moves, stepper));

    return row;
  }

  function picker(field, value, listId) {
    var box = el('div', 'fh-picker');
    var search = el('input', 'fh-inp fh-small');
    var list = el('div', 'fh-plist');

    list.style.gap = '2px';
    search.setAttribute('data-field', field);
    search.setAttribute('placeholder', say('search'));
    search.value = value;
    put(box, search, list);
    ui[listId] = list;

    return box;
  }

  // Every seed that fits the pot size; with no search, only the ones the player owns.
  function fillSeedAdd() {
    var list = ui.addlist;
    var cap = state.addCap;

    if (!list || !state.draft || !cap || !list.parentNode) return;

    var used = stepsFor(cap).map(function (s) { return s.item; });
    var query = state.addSearch.trim().toLowerCase();
    var found = cat.seedList.filter(function (s) {
      return used.indexOf(s.id) < 0 && perPot(s.id, cap) > 0 && (query ? s.name.toLowerCase().indexOf(query) >= 0 || cropOfSeed(s.id).name.toLowerCase().indexOf(query) >= 0 : owned(s.id) > 0);
    }).sort(function (a, b) { return owned(b.id) - owned(a.id) || byName(a, b); });

    list.textContent = '';
    found.slice(0, MAX_ADD).forEach(function (s) {
      list.appendChild(put(act(el('div', 'fh-prow'), 'seedadd', cap + '|' + s.id), icon(s, 'fh-i22'), el('span', 'fh-grow', cropOfSeed(s.id).name),
        el('span', 'fh-dim', say('nSeeds', owned(s.id)))));
    });

    if (!found.length) list.appendChild(el('div', 'fh-dim', say('nothingToAdd')));
    if (found.length > MAX_ADD) list.appendChild(el('div', 'fh-dim', say('more', found.length - MAX_ADD)));
  }

  function fertSection(editor) {
    var d = state.draft;
    var chain = el('div', 'fh-chain');
    var adds = el('div', 'fh-chain');

    d.ferts.forEach(function (id, i) {
      var fert = fertOf(id);
      var link = put(el('span', 'fh-link' + (owned(id) ? '' : ' fh-out')), icon(fert, 'fh-i22'), el('span', 'fh-lname', fert.name), el('span', 'fh-dim', say('owned', owned(id))),
        plainButton('fh-ib', '\u2190', 'fertup', i, i > 0), plainButton('fh-ib', '\u2192', 'fertdown', i, i < d.ferts.length - 1), plainButton('fh-ib fh-del', '\u00d7', 'fertdel', i, true));

      if (i) chain.appendChild(el('span', 'fh-arrow', '\u2192'));

      chain.appendChild(link);
    });

    if (!d.ferts.length) chain.appendChild(el('span', 'fh-dim', say('noFerts')));

    adds.appendChild(el('span', 'fh-dim', say('addFert')));
    cat.fertList.filter(function (f) { return d.ferts.indexOf(f.id) < 0; }).forEach(function (f) {
      adds.appendChild(chip(f.name, 'fertadd', f.id, false, null, f));
    });

    put(editor, section(say('ferts'), el('span', 'fh-r', say('fertNote'))), chain, adds.children.length > 1 ? adds : null);
  }

  // A crop with its own fertilizer order: a toggle per fertilizer (numbered in order), what to do when none are left,
  // and the grow time with no fertilizer against the first one picked.
  function cropSection(editor) {
    var d = state.draft;
    var table = el('div', 'fh-ctable');

    if (d.perCrop.length) {
      table.appendChild(put(el('div', 'fh-crow fh-chead'), el('span', '', say('colCrop')), el('span', '', say('colOrder')), el('span', '', say('colOut')),
        el('span', '', say('colTime')), el('span')));
    }

    d.perCrop.forEach(function (entry) { table.appendChild(cropRow(entry)); });
    table.appendChild(act(el('button', 'fh-chip fh-add' + (state.isCropPicking ? ' fh-on' : ''), say('addCrop')), 'cropopen'));

    if (state.isCropPicking) table.appendChild(picker('cropsearch', state.cropSearch, 'croplist'));

    put(editor, section(say('perCrop'), el('span', 'fh-r', say('perCropNote'))), table);
    fillCropAdd();
  }

  function cropRow(entry) {
    var crop = cropOf(entry.plant);
    var order = el('div', 'fh-chips');
    var first = entry.order.length ? fertOf(entry.order[0]) : null;
    var time = put(el('span'), text(hours(crop.grow)), first ? text(' \u2192 ') : null, first ? el('b', 'fh-green', hours(growHours(crop, first.speed))) : null);

    cat.fertList.forEach(function (f) {
      var at = entry.order.indexOf(f.id);

      order.appendChild(chip((at >= 0 ? (at + 1) + '. ' : '') + f.name, 'cropfert', entry.plant + '|' + f.id, at >= 0, null, f));
    });

    if (!entry.order.length && entry.whenOut === 'none') order.appendChild(badge(say('never'), 'fh-p-warn'));

    var del = plainButton('fh-ib fh-del', '\u00d7', 'cropdel', entry.plant, true);

    return put(el('div', 'fh-crow'), put(el('div', 'fh-cname'), icon(crop, 'fh-i22'), el('b', '', crop.name)), order,
      segment([['preset', say('woPreset')], ['none', say('woNone')]].map(function (c) { return [entry.plant + '|' + c[0], c[1]]; }),
        entry.plant + '|' + (entry.whenOut === 'none' ? 'none' : 'preset'), 'cropout'),
      time, del);
  }

  // A full pot of the crop with this fertilizer and no other bonus.
  function growHours(crop, speed) {
    return crop.grow / (1 + (speed || 0));
  }

  // Crops the preset plants come first; a search reaches every crop.
  function fillCropAdd() {
    var list = ui.croplist;

    if (!list || !state.draft || !state.isCropPicking || !list.parentNode) return;

    var d = state.draft;
    var planted = [];
    var query = state.cropSearch.trim().toLowerCase();

    Object.keys(d.seeds).forEach(function (cap) {
      d.seeds[cap].forEach(function (s) { planted.push(seedOf(s.item).plant); });
    });

    var used = d.perCrop.map(function (c) { return c.plant; });
    var found = cat.cropList.filter(function (c) {
      return used.indexOf(c.id) < 0 && (query ? c.name.toLowerCase().indexOf(query) >= 0 : planted.indexOf(c.id) >= 0);
    }).sort(byName);

    list.textContent = '';
    found.slice(0, MAX_ADD).forEach(function (c) {
      list.appendChild(put(act(el('div', 'fh-prow'), 'cropadd', c.id), icon(c, 'fh-i22'), el('span', 'fh-grow', c.name), el('span', 'fh-dim', hours(c.grow))));
    });

    if (!found.length) list.appendChild(el('div', 'fh-dim', say('nothingToAdd')));
    if (found.length > MAX_ADD) list.appendChild(el('div', 'fh-dim', say('more', found.length - MAX_ADD)));
  }

  function useChips(d) {
    var box = el('div', 'fh-chips');
    var uses = [['all', say('useAll')]];

    (state.data.floors || []).forEach(function (f) { uses.push(['f' + f, say('floor', f)]); });
    (state.data.rooms || []).forEach(function (r) { uses.push(['a' + r.area, (r.name || say('otherRoom')) + ' \u00b7 ' + say('floor', r.floor)]); });
    uses.forEach(function (use) { box.appendChild(chip(use[1], 'use', use[0], d.uses.indexOf(use[0]) >= 0)); });
    box.style.padding = '0 4px 6px';

    return box;
  }

  // The preset in numbers: for each size, the pots using it and how many plantings the seeds above the keep counts
  // still give, and the crop each size gets next.
  function fillPlan(plan) {
    var d = state.draft;
    var users = d.id ? pots().filter(function (p) { return p.preset === d.id; }) : [];
    var outs = [];

    plan.appendChild(section(say('willDo')));

    if (!users.length) plan.appendChild(el('div', 'fh-note', say('noPotsUse')));

    cat.caps.forEach(function (cap) {
      var steps = d.seeds[cap] || [];
      var count = users.filter(function (p) { return kindOf(p.kind).cap === cap; }).length;
      var left = steps.reduce(function (sum, s) { return sum + plantings(s, cap); }, 0);
      var verdict = !count ? null : left >= 2 * count ? badge(say('ok'), 'fh-p-good') : left ? badge(say('low'), 'fh-p-warn') : badge(say('out'), 'fh-p-urgent');

      steps.forEach(function (s) {
        if (perPot(s.item, cap) && !plantings(s, cap) && outs.indexOf(s.item) < 0) outs.push(s.item);
      });

      if (!steps.length && !count) return;

      plan.appendChild(put(el('div', 'fh-prow2'), el('b', '', sizeName(cap)), el('span', 'fh-dim', (count === 1 ? say('onePot') : say('nPots', count))),
        el('span', 'fh-grow fh-dim', say('plantingsLeft', left)), verdict));
    });
    plan.appendChild(el('div', 'fh-note', say('planNote')));
    plan.appendChild(section(say('nextBySize')));

    cat.caps.forEach(function (cap) {
      var steps = d.seeds[cap] || [];
      var at = firstPlantable(steps, cap);

      if (!steps.length) return;

      if (at < 0) {
        plan.appendChild(put(el('div', 'fh-prow2'), el('b', 'fh-grow', sizeName(cap)), el('span', 'fh-red', say('nothingLeft'))));

        return;
      }

      var crop = cropOfSeed(steps[at].item);

      plan.appendChild(put(el('div', 'fh-prow2'), icon(crop, 'fh-i22'), el('span', 'fh-grow', sizeName(cap) + ': ' + crop.name), el('span', 'fh-dim', say('toRipe', hours(crop.grow)))));
    });

    outs.forEach(function (item) { plan.appendChild(el('div', 'fh-banner', say('seedOut', cropOfSeed(item).name))); });
  }

  function editDraft(change) {
    if (!state.draft) return;

    change(state.draft);
    render();
  }

  function markDirty(scope) {
    var mark = (scope || ui.body).querySelector('.fh-dirty');

    if (mark) mark.style.display = isDirty() ? '' : 'none';
  }

  function moveIn(list, i, by) {
    editDraft(function () {
      var j = i + by;

      if (j < 0 || j >= list.length) return;

      var item = list[i];
      list[i] = list[j];
      list[j] = item;
    });
  }

  function capAndIndex(args) {
    var parts = String(args).split('|');

    return { cap: +parts[0], i: +parts[1] };
  }

  function moveSeed(args, by) {
    var at = capAndIndex(args);

    moveIn(stepsFor(at.cap), at.i, by);
  }

  function removeSeed(args) {
    var at = capAndIndex(args);

    editDraft(function () { stepsFor(at.cap).splice(at.i, 1); });
  }

  function changeKeep(args, by) {
    var at = capAndIndex(args);

    editDraft(function () {
      var step = stepsFor(at.cap)[at.i];

      if (step) step.keep = Math.max(0, Math.min(MAX_KEEP, step.keep + by));
    });
  }

  function openSeedPicker(cap) {
    state.addCap = state.addCap === +cap ? 0 : +cap;
    state.addSearch = '';
    render();
  }

  function addSeed(args) {
    var at = capAndIndex(args);
    var id = at.i;

    editDraft(function () {
      var steps = stepsFor(at.cap);

      if (steps.every(function (s) { return s.item !== id; })) steps.push({ item: id, keep: 0 });
    });
  }

  function addFert(itemId) {
    var id = +itemId;

    editDraft(function (d) {
      if (d.ferts.indexOf(id) < 0) d.ferts.push(id);
    });
  }

  function cropEntryOf(plantId) {
    return find(state.draft.perCrop, function (c) { return c.plant === plantId; });
  }

  // A new crop entry starts with the fertilizer that grows it fastest.
  function addCrop(plantId) {
    var id = +plantId;
    var fastest = cat.fertList.slice().sort(function (a, b) { return b.speed - a.speed; })[0];

    editDraft(function (d) {
      if (!cropEntryOf(id)) d.perCrop.push({ plant: id, order: fastest ? [fastest.id] : [], whenOut: 'preset' });
    });
  }

  function removeCrop(plantId) {
    var id = +plantId;

    editDraft(function (d) { d.perCrop = d.perCrop.filter(function (c) { return c.plant !== id; }); });
  }

  function toggleCropFert(args) {
    var parts = String(args).split('|');
    var fertId = +parts[1];

    editDraft(function () {
      var entry = cropEntryOf(+parts[0]);

      if (!entry) return;

      var at = entry.order.indexOf(fertId);

      if (at >= 0) entry.order.splice(at, 1);
      else entry.order.push(fertId);
    });
  }

  function setCropOut(args) {
    var parts = String(args).split('|');

    editDraft(function () {
      var entry = cropEntryOf(+parts[0]);

      if (entry) entry.whenOut = parts[1] === 'none' ? 'none' : 'preset';
    });
  }

  function toggleUse(use) {
    editDraft(function (d) {
      var at = d.uses.indexOf(use);

      if (at >= 0) d.uses.splice(at, 1);
      else d.uses.push(use);
    });
  }

  function pickPreset(id) {
    state.presetId = id;
    state.draft = null;
    state.addCap = 0;
    state.isCropPicking = false;
    render();
  }

  function newPreset() {
    state.presetId = '';
    state.draftOf = '';
    state.addCap = 0;
    state.isCropPicking = false;
    state.draft = { id: '', name: say('newPreset'), seeds: {}, ferts: [], perCrop: [], skipUnsuitable: true, fertilize: true, autoReplant: false, uses: [] };
    render();
  }

  function savePreset() {
    if (!state.draft) return;

    // A new preset gets its id from the plugin; the next view shows which one is new, and the editor moves onto it.
    if (!state.draft.id) state.pendingNewFrom = presets().map(function (p) { return p.id; });

    sendAction('savepreset|' + JSON.stringify(state.draft));
  }

  // ---------- Robots tab ----------

  function renderRobots() {
    var well = scroller('robots', 'fh-grow');
    var robots = state.data.robots || [];
    var isOn = !!(state.data.settings && state.data.settings.robots);
    var grid = el('div', 'fh-rgrid');

    well.appendChild(setRow(say('keepStocked'), say('keepNote'), switchButton(isOn, 'robots', isOn ? '0' : '1')));

    if (!robots.length) well.appendChild(el('div', 'fh-none', say('noRobots')));

    robots.forEach(function (robot) { grid.appendChild(robotCard(robot)); });
    well.appendChild(grid);
    ui.body.appendChild(well);
  }

  function robotCard(robot) {
    var box = el('div', 'fh-rcard');
    var battery = Math.max(0, Math.min(100, robot.battery | 0));
    var track = el('span', 'fh-bar' + (battery < 20 ? ' fh-red' : battery < 50 ? ' fh-gold' : ''));
    var fill = el('i');
    var head = put(el('div', 'fh-hd'),
      put(el('div', 'fh-mid'), el('div', 'fh-nm', robot.name), put(el('div', 'fh-meta'), text(say('floor', robot.floor)), track, text(say('battery', battery)))),
      robot.on ? badge(say('working'), 'fh-p-good') : badge(say('robotOff'), 'fh-p-urgent'));

    fill.style.width = battery + '%';
    track.appendChild(fill);
    put(box, head, put(el('div', 'fh-tier fh-thead'), el('span', '', say('colSize')), el('span', '', say('colSeed')), el('span', '', say('fertWord')), el('span'), el('span')));
    robot.tiers.forEach(function (tier, i) {
      var isEditing = state.tierEdit && state.tierEdit.robot === robot.id && state.tierEdit.tier === i;

      box.appendChild(tierRow(robot, tier, i));

      put(box, tier.follow ? followNote(robot, tier) : pickedNote(robot, tier));

      if (isEditing) box.appendChild(tierEditor(robot, tier));
    });

    return box;
  }

  function tierRow(robot, tier, i) {
    var seed = tier.seed ? seedOf(tier.seed) : null;
    var fert = tier.fert ? fertOf(tier.fert) : null;
    var isShort = seed && owned(tier.seed) < perPot(tier.seed, tier.cap);
    var verdict = !seed ? badge(say('noSeed'), 'fh-p-urgent') : isShort ? badge(say('out'), 'fh-p-urgent') : badge(say('ok'), 'fh-p-good');

    return put(el('div', 'fh-tier'), el('b', '', sizeName(tier.cap)),
      put(el('span', 'fh-tcell'), seed ? icon(seed, 'fh-i22') : null, el('span', seed ? '' : 'fh-dim', seed ? cropOfSeed(tier.seed).name : say('noSeed')),
        seed ? el('span', isShort ? 'fh-red' : 'fh-dim', say('nSeeds', owned(tier.seed))) : null),
      put(el('span', 'fh-tcell'), fert ? icon(fert, 'fh-i22') : null, el('span', fert ? '' : 'fh-dim', fert ? fert.name : say('noFert'))),
      verdict, button(say('change'), 'tieredit', robot.id + '|' + i, true, 'fh-sm'));
  }

  // Under a tier with no seed of its own: the preset it sows from and that preset's seeds for the pot size, in order.
  function followNote(robot, tier) {
    var preset = presetById(robot.preset);
    var steps = preset ? (preset.seeds || {})[tier.cap] || [] : [];
    var names = steps.map(function (s) { return cropOfSeed(s.item).name; }).join(', ');

    if (!preset) return null;

    var isOut = !steps.some(function (s) { return plantings(s, tier.cap) > 0; });

    if (!steps.length) return el('div', 'fh-tnote', say('followNone', preset.name));

    return el('div', 'fh-tnote', say(isOut ? 'followOut' : 'followSeeds', preset.name, names));
  }

  // Under a seed the player picked on a floor with a preset: how to hand the tier back to it.
  function pickedNote(robot, tier) {
    return tier.seed && presetById(robot.preset) ? el('div', 'fh-tnote', say('pickedNote')) : null;
  }

  // The seeds that fit the pot size, owned first, and the fertilizer, both chosen here and sent with one press. The
  // first choice hands the tier back to the pot-size seeds of the floor's preset, which bring their own fertilizer, so
  // it hides the fertilizers. On a floor with no preset it means no seed at all.
  function tierEditor(robot, tier) {
    var edit = state.tierEdit;
    var hasPreset = !!presetById(robot.preset);
    var isSizeSeeds = edit.seed === 0;
    var seeds = el('div', 'fh-chips');
    var ferts = el('div', 'fh-chips');
    var fits = cat.seedList.filter(function (s) { return perPot(s.id, tier.cap) > 0 && (owned(s.id) > 0 || s.id === edit.seed); })
      .sort(function (a, b) { return owned(b.id) - owned(a.id) || byName(a, b); });

    seeds.appendChild(chip(say(hasPreset ? 'sizeSeeds' : 'noSeed'), 'tierseed', 0, isSizeSeeds));
    fits.slice(0, MAX_ADD).forEach(function (s) { seeds.appendChild(chip(cropOfSeed(s.id).name, 'tierseed', s.id, edit.seed === s.id, owned(s.id), s)); });
    ferts.appendChild(chip(say('noFert'), 'tierfert', 0, edit.fert === 0));
    cat.fertList.forEach(function (f) { ferts.appendChild(chip(f.name, 'tierfert', f.id, edit.fert === f.id, owned(f.id), f)); });

    return put(el('div', 'fh-tedit'), el('span', 'fh-dim', say('seed')), seeds,
      isSizeSeeds ? (hasPreset ? el('span', 'fh-dim', say('sizeSeedsNote')) : null) : put(el('div', 'fh-tedit-fert'), el('span', 'fh-dim', say('fertWord')), ferts),
      put(el('div', 'fh-row2'), button(say('save'), 'settier', null, true, 'fh-gold'), button(say('cancel'), 'tierdone', null, true)));
  }

  function editTier(args) {
    var parts = String(args).split('|');
    var robot = find(state.data.robots || [], function (r) { return r.id === parts[0]; });
    var tier = robot && robot.tiers[+parts[1]];

    if (!tier) return;

    state.tierEdit = { robot: robot.id, tier: +parts[1], seed: tier.follow ? 0 : tier.seed, fert: tier.follow ? 0 : tier.fert };
    render();
  }

  function pickTierPart(part, id) {
    if (!state.tierEdit) return;

    state.tierEdit[part] = id;
    render();
  }

  function setTier() {
    var edit = state.tierEdit;

    if (!edit) return;

    state.tierEdit = null;
    sendAction('robottier|' + edit.robot + '|' + edit.tier + '|' + edit.seed + '|' + (edit.seed === 0 ? 0 : edit.fert));
  }

  // ---------- stock, on the presets tab ----------

  // Each seed the presets plant, with how many pots of each size what the player owns would fill, then the
  // fertilizers the presets use.
  function fillStock(well) {
    var rows = {};
    var ferts = [];
    var columns = 'minmax(0,1fr) 48px' + cat.caps.map(function () { return ' 36px'; }).join('');
    var head = put(el('div', 'fh-trow fh-thead'), el('span', '', say('colItem')), el('span', 'fh-tc', say('colHave')));

    presets().forEach(function (p) {
      Object.keys(p.seeds || {}).forEach(function (cap) {
        p.seeds[cap].forEach(function (s) {
          var row = rows[s.item] || (rows[s.item] = { item: s.item, caps: {} });

          if (perPot(s.item, +cap)) row.caps[cap] = Math.max(row.caps[cap] || 0, plantings(s, +cap));
        });
      });
      [p.ferts || []].concat((p.perCrop || []).map(function (c) { return c.order; })).forEach(function (order) {
        order.forEach(function (id) {
          if (ferts.indexOf(id) < 0) ferts.push(id);
        });
      });
    });

    head.style.gridTemplateColumns = columns;
    cat.caps.forEach(function (cap) { head.appendChild(el('span', 'fh-tc', SIZE_SHORT[cap] ? say(SIZE_SHORT[cap]) : String(cap))); });
    put(well, section(say('stockHead')), head);

    Object.keys(rows).map(function (k) { return rows[k]; }).sort(function (a, b) { return owned(a.item) - owned(b.item); }).forEach(function (row) {
      var line = put(el('div', 'fh-trow'), put(el('span', 'fh-cname'), icon(seedOf(row.item), 'fh-i22'), el('span', 'fh-cut', cropOfSeed(row.item).name)),
        el('span', 'fh-tc' + (owned(row.item) ? '' : ' fh-red'), String(owned(row.item))));

      line.style.gridTemplateColumns = columns;
      cat.caps.forEach(function (cap) {
        var fills = row.caps[cap];

        line.appendChild(el('span', 'fh-tc' + (fills === 0 ? ' fh-red' : fills == null ? ' fh-dim' : ''), fills == null ? '-' : String(fills)));
      });
      well.appendChild(line);
    });

    ferts.forEach(function (id) {
      var line = put(el('div', 'fh-trow'), put(el('span', 'fh-cname'), icon(fertOf(id), 'fh-i22'), el('span', 'fh-cut', fertOf(id).name)),
        el('span', 'fh-tc' + (owned(id) ? '' : ' fh-red'), String(owned(id))));

      line.style.gridTemplateColumns = columns;
      well.appendChild(line);
    });
    well.appendChild(el('div', 'fh-note', say('stockNote')));
  }

  // ---------- Settings tab ----------

  function renderSettings() {
    var well = scroller('settings', 'fh-grow');
    var settings = state.data.settings || {};

    put(well,
      section(say('keysHead')),
      state.key === GAME_TEND_KEY ? el('div', 'fh-warnbox', say('gTwice')) : null,
      setRow(say('hotkey'), state.key ? say('keyOpen', state.key) : say('keyNone'), state.key ? el('span', 'fh-kbd', state.key) : null),
      section(say('warnHead')),
      setRow(say('rotWarn'), say('rotWarnNote'), segment(ROT_WARNS.map(function (h) { return [String(h), say('hours', h)]; }), String(settings.rotWarn), 'rotwarn')),
      section(say('otherHead')),
      el('div', 'fh-note', say('presetFile')));
    ui.body.appendChild(well);
  }

  // ---------- shared ----------

  function icon(item, sizeClass) {
    var box = el('div', 'fh-icon ' + sizeClass);
    var letter = String(item.name || '?').charAt(0).toUpperCase();

    if (item.icon) {
      var img = el('img');
      img.setAttribute('data-letter', letter);
      img.src = item.icon;
      box.appendChild(img);
    } else {
      box.textContent = letter;
    }

    return box;
  }

  function open(data) {
    build();
    state.data = data || state.data;
    state.isOpen = true;
    state.isTyping = false;
    shownShape = shapeOf(state.data);
    ui.status.textContent = '';
    ui.root.style.display = 'flex';
    render();
  }

  // Called about once a second while the window is open. Most calls only move the clocks.
  function setData(data) {
    state.data = data || state.data;

    var shape = shapeOf(state.data);

    if (ui && shape === shownShape) {
      updateLive();

      return;
    }

    shownShape = shape;
    render();
  }

  function close() {
    state.isOpen = false;
    state.armed = '';
    state.tierEdit = null;

    if (state.isTyping) send('focus|0');

    state.isTyping = false;

    if (ui) ui.root.style.display = 'none';
  }

  function status(message) {
    build();
    clearBusy();

    // With the window closed the plugin shows the line as an Exiled's Toolkit toast instead.
    if (state.isOpen) ui.status.textContent = message;
  }

  // Whether a game with a home is loaded, and the key that opens the window ('' when none): the HUD button is only
  // shown in such a game, and both it and the window name the key.
  function setLive(isLive, key) {
    state.isLive = !!isLive;
    state.key = key || '';

    if (ui && state.isOpen) renderKeys();

    refreshHud();
  }

  // The number on the HUD button and the Pots tab: pots with a chore waiting and empty pots a press of Plant would fill.
  function setBadge(count) {
    state.badge = Math.max(0, count | 0);

    if (ui && state.isOpen) renderTabs();

    refreshHud();
  }

  // ---------- HUD button ----------

  // The Toolkit's page API (window.ExiledToolkit). The game can run its script after this one, so this stays null
  // until it has; the HUD button, the plant panel and the messages to the plugin start then.
  var etk = null;
  // The Farm Hub button in the HUD's tool column. The Toolkit keeps it there and writes changes into it in place.
  var hudButton = null;
  // What the button was last told, so it is told again only when something changed.
  var shownHud = { isLive: null, key: null, badge: null, words: null };
  // The Toolkit's check of the HUD page, which hands that page to patchHud.
  var hudWatch = null;

  // The HUD frame and every node placed in its document. Kept while the Toolkit hands over the same document, so a
  // check that finds nothing changed searches nothing; a new HUD document gets a new record.
  var hudAt = null;

  // Runs once the Toolkit's page API is there: at once when its script ran first, else when it runs.
  function startWithToolkit(api) {
    etk = api;

    // A window drawn before the Toolkit arrived gets its style now.
    if (ui) addStyle();

    hudButton = etk.addHudButton({
      id: HUD_CLASS, order: DOCK_ORDER, icon: SPROUT_PATH, title: 'Farm Hub', badgeClass: 'fh-hud-badge', shown: false,
      onClick: function () { send('toggle'); },
      onPlace: noteHudPlaced
    });
    showHudState();
    hudWatch = etk.watchPages(HUD_PAGE, patchHud, { everyCheck: true });
    send('ready');
  }

  // What the plugin sent changed what the HUD shows: the button hears it at once, and the plant panel is patched now
  // rather than at the next check.
  function refreshHud() {
    showHudState();

    if (hudWatch) hudWatch.refresh();
  }

  // The HUD page, handed over at every check. Outside a game nothing is shown, so there is nothing to look for unless
  // something placed earlier must go.
  function patchHud(doc, frame) {
    showHudState();

    if (!state.isLive && !(hudAt && hudAt.placed)) return;

    if (!hudAt || hudAt.doc !== doc) hudAt = newHudPage(frame, doc);

    patchPlantPanel(hudAt);
  }

  // Anything of ours already in the document is taken out once, so the nodes in the record are the only ones there.
  // The Toolkit takes out a button an older copy of this script left.
  function newHudPage(frame, doc) {
    ['.fh-hudadd', '#' + FILTER_STYLE_ID].forEach(function (selector) {
      doc.querySelectorAll(selector).forEach(function (node) { node.remove(); });
    });

    return {
      frame: frame, doc: doc, app: null, row: null, replant: null, box: null, boxInput: null, tip: null, plan: null, list: null,
      strip: null, empties: null, more: null, filter: null, rows: [], growing: 0, firstGrowing: null, placed: false, hasPanelStyle: false
    };
  }

  // Shown only in a game, with the count, the key and the tip's words; each is written only when it changed.
  function showHudState() {
    if (!hudButton) return;

    var words = wordsKey();

    if (shownHud.isLive === state.isLive && shownHud.key === state.key && shownHud.badge === state.badge && shownHud.words === words) return;

    shownHud = { isLive: state.isLive, key: state.key, badge: state.badge, words: words };
    hudButton.update({ shown: state.isLive, key: state.key, badge: state.badge, tip: say('tip') });
  }

  function noteHudPlaced() {
    if (!hasReportedHudPlaced) send('diag|HUD button placed in the tool column');
    hasReportedHudPlaced = true;
  }

  function sprout(doc) {
    var svg = doc.createElementNS(SVG_NS, 'svg');
    var path = doc.createElementNS(SVG_NS, 'path');

    svg.setAttribute('class', 'icon-svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    path.setAttribute('d', SPROUT_PATH);
    svg.appendChild(path);

    return svg;
  }

  // ---------- the game's plant panel ----------

  // Additions to the HUD's own Plants panel (the list under the ripe-crop counter): a "from presets" box beside the
  // game's Replant box, the Tend Garden plan in the patrol button's tip, a strip with the plan and a Needs you / All
  // filter, a second line under each row, and the empty pots, which the game does not list. Only our own nodes are
  // added (class fh-hudadd); the game's nodes and classes are never changed, so Vue keeps drawing its rows as before.
  var PANEL_STYLE_ID = 'fh-panel-style';
  var FILTER_STYLE_ID = 'fh-filter-style';
  var SHOWN_GROWING = 3;
  var PANEL_CSS = [
    '.mature-panel .mature-popover{max-width:340px}',
    '.mature-panel .mature-item-row{flex-wrap:wrap}',
    '#maturePatrolBtn .entry-tip{width:260px;max-width:300px;white-space:normal;left:-16px;transform:none;z-index:30}',
    // A hovered toggle row gets a brightness filter, which makes it a stacking context of its own, so the Tend Garden
    // tip inside it drew under the trap panel below. A z-index on the hovered panel puts it on top of its neighbours.
    '.mature-panel:hover{z-index:1}',
    // The HUD's top-left panel is a stacking context at z-index 100 and the tool column (.vertical-tools) sits at 101,
    // so nothing inside the panel can draw over the column whatever its own z-index. While the plant list is open or
    // the plant row is under the mouse, the panel is lifted just above the column.
    '.top-left-panel:has(.mature-toggle-row.is-expanded),.top-left-panel:has(.mature-panel:hover){z-index:102}',
    '.fh-sub{flex-basis:100%;display:flex;align-items:center;gap:5px;padding-left:18px;font-size:9.5px;color:rgba(255,255,255,0.55);white-space:nowrap;text-shadow:1px 1px 2px black}',
    '.fh-sub img,.fh-empty img.fh-pot{width:11px;height:11px;object-fit:contain}',
    '.fh-sub .fh-right{margin-left:auto;display:inline-flex;align-items:center;gap:4px}',
    '.fh-gold{color:#ffd54f}',
    '.fh-red{color:#ef9a9a;font-weight:bold}',
    '.fh-orange{color:#ffb74d}',
    '.fh-sep{color:rgba(255,255,255,0.25)}',
    '.fh-strip{display:flex;flex-wrap:wrap;align-items:center;gap:3px 5px;padding:2px 4px 3px;font-size:9.5px;color:rgba(255,255,255,0.75);text-shadow:1px 1px 2px black}',
    '.fh-strip b{color:#ffd54f}',
    '.fh-strip .fh-plan-line{flex-basis:100%;white-space:normal}',
    '.fh-strip .fh-warn{color:#ffb74d}',
    '.fh-pick{cursor:pointer;padding:0 5px;border-radius:7px;border:1px solid transparent}',
    '.fh-pick.fh-on{border-color:rgba(255,193,7,0.45);color:#ffd54f}',
    '.fh-lnk{margin-left:auto;color:#ffd54f;border:1px solid rgba(255,193,7,0.45);border-radius:8px;padding:0 7px;cursor:pointer}',
    '.fh-lnk:hover,.fh-pick:hover{background:rgba(255,193,7,0.16)}',
    '.fh-sec{font-size:9px;letter-spacing:1px;text-transform:uppercase;color:rgba(255,193,7,0.8);padding:4px 2px 1px;text-shadow:1px 1px 2px black}',
    '.fh-empties{display:flex;flex-direction:column;gap:2px}',
    '.fh-more{font-size:9.5px;color:rgba(255,255,255,0.55);padding:2px 4px;cursor:pointer;text-shadow:1px 1px 2px black}',
    '.fh-more:hover{color:#ffd54f}',
    '.fh-plan{margin-top:4px;font-size:11px;color:#ddd}',
    '.fh-plan-list{margin:4px 0 2px;display:grid;grid-template-columns:16px 1fr auto;gap:2px 8px;align-items:center}',
    '.fh-plan-list img{width:16px;height:16px;object-fit:contain}',
    '.fh-plan-list .fh-n{color:#ffd54f;font-family:Consolas,monospace;text-align:right}',
    '.fh-plan-h{font-size:10px;color:rgba(255,193,7,0.85);letter-spacing:1px;margin-top:6px;text-transform:uppercase}',
    '.fh-plan-foot{margin-top:5px;padding-top:4px;border-top:1px solid rgba(255,255,255,0.12);font-size:10.5px;color:#cfd3d5}',
    '.fh-plan-foot b{color:#ffd54f}',
    '.fh-plan-foot.fh-warn{color:#ffb74d}'
  ].join('\n');
  // In Needs you, growing rows past the first few are hidden. The game lists growing pots last, so this is every
  // growing row after the third.
  var NEEDS_CSS = '.mature-popover .is-growing ~ .is-growing ~ .is-growing ~ .is-growing{display:none}';

  var hudData = { ready: false };
  // Each listed pot by its HUD id as { pot, key }; a pot whose data did not change keeps the same entry, so its row's
  // line is not drawn again.
  var hudById = {};
  // The data without the pots and the empty pots, and the empty pots alone, as text; each seq is bumped when its text
  // changes, so the plan, the strip and the empty pots are drawn again only then.
  var hudPlanKey = '';
  var hudEmptyKey = '';
  var planSeq = 0;
  var emptySeq = 0;
  var panelShow = 'needs';
  var hasReportedVue = false;
  var hasReportedPanel = false;

  // What the plugin sends for the panel when it changes (HudView): each listed pot's room and what it needs, the
  // empty pots, and the Tend Garden plan.
  function hud(data) {
    var before = hudById;

    hudData = data && data.ready ? data : { ready: false };
    hudById = {};
    (hudData.pots || []).forEach(function (p) {
      var key = JSON.stringify(p);
      var kept = before[p.show];

      hudById[p.show] = kept && kept.key === key ? kept : { pot: p, key: key };
    });

    var planKey = JSON.stringify(hudData, function (key, value) { return key === 'pots' || key === 'empty' ? undefined : value; });
    var emptyKey = JSON.stringify(hudData.empty || []);

    if (planKey !== hudPlanKey) planSeq++;
    if (emptyKey !== hudEmptyKey) emptySeq++;

    hudPlanKey = planKey;
    hudEmptyKey = emptyKey;
    refreshHud();
  }

  // The HUD page's Vue state, which holds the plant rows' ids in the order the rows are drawn, and whether the panel,
  // its list and Tend Garden are shown. Null if a game update moved it, and then the rows get no second line and the
  // panel is looked for on every poll; the rest still works.
  function vueState(page) {
    try {
      if (!page.app || !page.app.isConnected) page.app = page.doc.getElementById('app');

      var app = page.app;
      var setup = app && app._vnode && app._vnode.component && app._vnode.component.setupState;

      return (setup && setup.state) || null;
    } catch (e) {
      return null;
    }
  }

  // One of the Vue state's switches: true or false, or null when it is not there to read.
  function vueFlag(vue, name) {
    return vue && typeof vue[name] === 'boolean' ? vue[name] : null;
  }

  // Works from the nodes found last time while they are still in the page, and searches only for one that is gone
  // and that the Vue state says is shown. A poll where nothing changed searches nothing and draws nothing.
  function patchPlantPanel(page) {
    if (!state.isLive || !hudData.ready) {
      clearPanel(page);

      return;
    }

    var vue = vueState(page);

    if (vue && vueFlag(vue, 'maturePlantsActive') !== null && !(vue.maturePlantsActive && vue.maturePlants && vue.maturePlants.length > 0)) {
      clearPanel(page);

      return;
    }

    if (!page.row || !page.row.isConnected) page.row = page.doc.querySelector('.mature-toggle-row');

    if (!page.row) {
      clearPanel(page);

      return;
    }

    if (!vue && !hasReportedVue) send('diag|the HUD plant panel has no Vue state to read, so its rows get no second line');
    hasReportedVue = hasReportedVue || !vue;
    page.placed = true;

    if (!page.hasPanelStyle) {
      etk.addStyle(page.doc, PANEL_STYLE_ID, PANEL_CSS);
      page.hasPanelStyle = true;
    }

    placePresetsBox(page, vue);
    placePlan(page, vue);

    // The list is drawn only while the panel is open; one already found is used until the game takes it away.
    if (page.list && !page.list.isConnected) page.list = null;
    if (!page.list && vueFlag(vue, 'maturePlantsExpanded') !== false) page.list = (page.row.parentNode || page.doc).querySelector('.mature-popover');

    var list = page.list;

    setFilterStyle(page, !!list && panelShow === 'needs');

    if (list) {
      var rows = readRows(page, list);
      var needs = rows.length - page.growing + (hudData.empty || []).length;
      var all = rows.length + (hudData.empty || []).length;

      placeStrip(page, list, vue, needs, all);
      placeRowLines(page, rows, vue);
      placeEmpties(page, list);
      placeMore(page, list);
    }

    if (!hasReportedPanel) send('diag|Farm Hub added to the HUD plant panel');
    hasReportedPanel = true;
  }

  // Takes out everything added to the panel, once: after that there is nothing placed and nothing to look for.
  function clearPanel(page) {
    if (!page.placed) return;

    page.placed = false;
    page.doc.querySelectorAll('.fh-hudadd').forEach(function (node) { node.remove(); });
    setFilterStyle(page, false);
    page.row = page.replant = page.box = page.boxInput = page.tip = page.plan = page.list = page.strip = page.empties = page.more = null;
  }

  // The game's rows, in order, from one walk of the list's children; the growing ones are counted on the way.
  function readRows(page, list) {
    var rows = page.rows;
    var children = list.children;

    rows.length = 0;
    page.growing = 0;
    page.firstGrowing = null;

    for (var i = 0; i < children.length; i++) {
      var node = children[i];

      if (!node.classList.contains('mature-item-row') || node.classList.contains('fh-hudadd')) continue;

      rows.push(node);

      if (node.classList.contains('is-growing')) {
        page.growing++;
        page.firstGrowing = page.firstGrowing || node;
      }
    }

    return rows;
  }

  function isReplantOn(vue) { return !!(vue && vue.matureReplantOn); }

  // Beside the game's Replant box, only while the game shows that box (Tend Garden unlocked). Built once.
  function placePresetsBox(page, vue) {
    var row = page.row;
    var isFromPresets = !!hudData.fromPresets;

    if (page.replant && page.replant.parentNode !== row) page.replant = null;
    if (!page.replant && vueFlag(vue, 'maturePatrolUnlocked') !== false) page.replant = row.querySelector('#matureReplantChk');

    if (!page.replant) {
      if (page.box) page.box.remove();

      page.box = page.boxInput = null;

      return;
    }

    if (!page.box || page.box.parentNode !== row) {
      if (page.box) page.box.remove();

      page.box = presetsBox(page);
      row.insertBefore(page.box, row.querySelector('.mature-toggle-chevron'));
    }

    if (page.boxInput.checked !== isFromPresets) page.boxInput.checked = isFromPresets;
  }

  function presetsBox(page) {
    var doc = page.doc;
    var label = make(doc, 'label', 'mature-replant-chk fh-presets-chk fh-hudadd', '');
    var input = doc.createElement('input');

    page.boxInput = input;
    label.setAttribute('data-interactive', '');
    input.type = 'checkbox';
    label.appendChild(input);
    label.appendChild(make(doc, 'span', '', say('fromPresets')));
    // The click must not reach the toggle row, which would open or close the list.
    label.addEventListener('click', function (e) { e.stopPropagation(); });
    input.addEventListener('change', function () {
      hudData.fromPresets = !!input.checked;
      send('frompresets|' + (input.checked ? '1' : '0'));
    });

    return label;
  }

  // The stamina Tend Garden spends: one chore's cost per chore, two more for each pot it replants (till, then plant),
  // and with the presets one more for each empty pot it plants and each growing pot it fertilizes.
  function plan(vue) {
    var d = hudData;
    var c = d.chores || {};
    var chores = (c.harvest || 0) + (c.water || 0) + (c.pests || 0) + (c.weeds || 0) + (c.clean || 0);
    var replant = isReplantOn(vue) ? d.replant || 0 : 0;
    var extra = d.fromPresets ? (d.plant || 0) + (d.fert || 0) : 0;
    var units = chores + replant * 2 + extra;
    var need = units * (d.cost || 0);

    return { chores: chores, replant: replant, units: units, need: need, hasStamina: d.stamina >= 0 && d.cost > 0, isShort: d.stamina >= 0 && d.cost > 0 && need > d.stamina };
  }

  // Our part of the game's Tend Garden tip, after its title and key.
  function placePlan(page, vue) {
    if (page.tip && !page.tip.isConnected) page.tip = null;

    if (!page.tip && vueFlag(vue, 'maturePatrolUnlocked') !== false) {
      var patrol = page.row.querySelector('#maturePatrolBtn');

      page.tip = patrol && patrol.querySelector('.entry-tip');
    }

    var tip = page.tip;

    if (!tip) return;

    var key = planSeq + '|' + isReplantOn(vue) + '|' + catSeq + '|' + wordsKey();
    var old = page.plan;

    if (old && old.parentNode === tip && old.getAttribute('data-key') === key) return;
    if (old) old.remove();

    page.plan = planTip(page.doc, vue);
    page.plan.setAttribute('data-key', key);
    tip.appendChild(page.plan);
  }

  function planTip(doc, vue) {
    var d = hudData;
    var c = d.chores || {};
    var p = plan(vue);
    var block = make(doc, 'div', 'fh-plan fh-hudadd', '');
    var ic = d.icons || {};
    var lines = [['tHarvest', c.harvest, cropOf(ic.harvest)], ['tWater', c.water, cropOf(ic.water)], ['tPests', c.pests, cropOf(ic.pests)], ['tWeeds', c.weeds, cropOf(ic.weeds)], ['hClean', c.clean, kindOf(ic.clean)]]
      .filter(function (l) { return l[1] > 0; });

    if (lines.length) block.appendChild(planList(doc, lines));

    if (d.fromPresets) {
      var then = [['hReplant', p.replant, seedOf(ic.replant)], ['hPlantEmpty', d.plant, seedOf(ic.plant)], ['fertilize', d.fert, fertOf(ic.fert)]].filter(function (l) { return l[1] > 0; });

      if (then.length) {
        block.appendChild(make(doc, 'div', 'fh-plan-h', say('hThenHead')));
        block.appendChild(planList(doc, then));
      }
    }

    if (!lines.length && !(d.fromPresets && (p.replant || d.plant || d.fert))) {
      block.appendChild(make(doc, 'div', 'fh-plan-foot', say('hIdle')));

      return block;
    }

    var foot = make(doc, 'div', 'fh-plan-foot', '');

    foot.appendChild(boldParts(doc, 'hChores', [p.units]));

    if (p.hasStamina) {
      foot.appendChild(make(doc, 'span', 'fh-sep', ' \u00b7 '));
      foot.appendChild(boldParts(doc, 'hStamina', [p.need, d.stamina]));
    }

    block.appendChild(foot);

    if (p.isShort) block.appendChild(make(doc, 'div', 'fh-plan-foot fh-warn', say('hShort')));
    if (d.fromPresets && d.noSeed > 0) block.appendChild(make(doc, 'div', 'fh-plan-foot fh-warn', noSeedWords()));

    return block;
  }

  function noSeedWords() {
    return hudData.noSeed > 1 ? say('hNoSeedMore', hudData.noSeedName, hudData.noSeed - 1) : say('hNoSeedFor', hudData.noSeedName);
  }

  function planList(doc, lines) {
    var list = make(doc, 'div', 'fh-plan-list', '');

    lines.forEach(function (l) {
      list.appendChild(hudIcon(doc, l[2]));
      list.appendChild(make(doc, 'span', '', say(l[0])));
      list.appendChild(make(doc, 'span', 'fh-n', String(l[1])));
    });

    return list;
  }

  // A phrase with its numbers in bold, in whichever order the language puts them.
  function boldParts(doc, key, numbers) {
    var node = make(doc, 'span', '', '');

    say(key).split(/(\{\d\})/).forEach(function (part) {
      var m = /^\{(\d)\}$/.exec(part);

      if (m) node.appendChild(make(doc, 'b', '', String(numbers[+m[1]])));
      else if (part) node.appendChild(doc.createTextNode(part));
    });

    return node;
  }

  // "Next Tend: 6 chores, 18 of 40 stamina, then plant 2, fertilize 1", the filter, and the way into Farm Hub.
  function placeStrip(page, list, vue, needs, all) {
    var doc = page.doc;
    var key = planSeq + '|' + isReplantOn(vue) + '|' + panelShow + '|' + needs + '|' + all + '|' + wordsKey();
    var strip = page.strip;

    if (strip && strip.parentNode === list && list.firstChild === strip && strip.getAttribute('data-key') === key) return;
    if (strip) strip.remove();

    var d = hudData;
    var p = plan(vue);
    var line = make(doc, 'span', 'fh-plan-line', say('hNextTend') + ' ');

    line.appendChild(boldParts(doc, 'hChores', [p.chores + p.replant * 2]));

    if (p.hasStamina) {
      line.appendChild(make(doc, 'span', 'fh-sep', ' \u00b7 '));
      line.appendChild(make(doc, 'span', p.isShort ? 'fh-warn' : '', '')).appendChild(boldParts(doc, 'hStamina', [p.need, d.stamina]));
    }

    if (d.fromPresets && (d.plant > 0 || d.fert > 0)) {
      line.appendChild(make(doc, 'span', 'fh-sep', ' \u00b7 '));
      line.appendChild(boldParts(doc, 'hThen', [d.plant || 0, d.fert || 0]));
    }

    strip = make(doc, 'div', 'fh-strip fh-hudadd', '');
    strip.setAttribute('data-key', key);
    strip.appendChild(line);
    strip.appendChild(make(doc, 'span', '', say('hShow')));
    strip.appendChild(pick(doc, 'needs', say('hNeeds', needs)));
    strip.appendChild(make(doc, 'span', 'fh-sep', '\u00b7'));
    strip.appendChild(pick(doc, 'all', say('hAll', all)));

    var link = make(doc, 'span', 'fh-lnk', 'Farm Hub \u2197');

    link.setAttribute('data-interactive', '');
    link.addEventListener('click', function (e) {
      e.stopPropagation();
      send('toggle');
    });
    strip.appendChild(link);
    list.insertBefore(strip, list.firstChild);
    page.strip = strip;
  }

  function pick(doc, mode, words) {
    var node = make(doc, 'span', 'fh-pick' + (panelShow === mode ? ' fh-on' : ''), words);

    node.setAttribute('data-interactive', '');
    node.setAttribute('data-show', mode);
    node.addEventListener('click', function (e) {
      e.stopPropagation();
      setShow(mode);
    });

    return node;
  }

  function setShow(mode) {
    panelShow = mode;
    refreshHud();
  }

  function setFilterStyle(page, isOn) {
    if (!isOn) {
      if (page.filter) page.filter.remove();

      page.filter = null;

      return;
    }

    if (page.filter && page.filter.isConnected) return;

    var doc = page.doc;
    var style = doc.createElement('style');

    style.id = FILTER_STYLE_ID;
    style.textContent = NEEDS_CSS;
    (doc.head || doc.body).appendChild(style);
    page.filter = style;
  }

  // Rows are drawn in the order of the Vue state's plant list, so row i is that list's plant i. Each row remembers its
  // line; the line is drawn again only when that pot's data, the Replant box, the catalog or the words changed.
  function placeRowLines(page, rows, vue) {
    var plants = (vue && vue.maturePlants) || [];
    var replantOn = isReplantOn(vue);
    var shared = replantOn + '|' + !!hudData.fromPresets + '|' + catSeq + '|' + wordsKey();

    for (var i = 0; i < rows.length; i++) {
      var row = rows[i];
      var plant = plants[i];
      var entry = plant ? hudById[String(plant.showInstanceId)] || null : null;
      var sub = row.__fhSub && row.__fhSub.parentNode === row ? row.__fhSub : null;

      if (sub ? sub.__fhEntry === entry && sub.__fhShared === shared : !entry) continue;
      if (sub) sub.remove();

      row.__fhSub = null;

      if (!entry) continue;

      sub = rowLine(page.doc, entry.pot, replantOn);
      sub.__fhEntry = entry;
      sub.__fhShared = shared;
      row.appendChild(sub);
      row.__fhSub = sub;
    }
  }

  // Where the pot is, then the one thing that matters most about it: rotting soon, what it is replanted with, or the
  // chore or fertilizer it waits for.
  function rowLine(doc, p, replantOn) {
    var sub = make(doc, 'span', 'fh-sub fh-hudadd', '');
    var right = make(doc, 'span', 'fh-right', '');

    sub.appendChild(make(doc, 'span', '', place(p)));

    if (p.warn && p.rot != null) {
      right.appendChild(make(doc, 'span', 'fh-red', say('hRotsIn', hours(p.rot))));
    } else if (p.need === 'harvest' && p.then > 0 && hudData.fromPresets && replantOn) {
      var crop = cropOfSeed(p.then);

      right.appendChild(make(doc, 'span', '', say('hThenCrop')));
      right.appendChild(hudIcon(doc, seedOf(p.then).icon ? seedOf(p.then) : crop));
      right.appendChild(make(doc, 'span', 'fh-gold', crop.name));
    } else if (p.need === 'clear' || p.need === 'uproot') {
      right.appendChild(make(doc, 'span', 'fh-orange', say('hClean')));
    } else if (CHORE_WORD[p.need] && p.need !== 'harvest') {
      right.appendChild(make(doc, 'span', 'fh-orange', say(CHORE_WORD[p.need])));
    } else if (p.need === 'feed') {
      right.appendChild(make(doc, 'span', 'fh-orange', say('hNeedsFert')));
    }

    if (right.children.length) sub.appendChild(right);

    return sub;
  }

  function place(p) {
    return p.area ? p.area + ' \u00b7 ' + p.kind : p.kind;
  }

  // The empty pots go between the pots that need the player and the growing ones, as the game sorts growing last. The
  // block is rebuilt only when the empty pots, the catalog or the words changed, and moved back if the game's rows moved
  // around it.
  function placeEmpties(page, list) {
    var empties = hudData.empty || [];
    var block = page.empties;
    var firstGrowing = page.firstGrowing;

    if (!empties.length && !firstGrowing) {
      if (block) block.remove();

      page.empties = null;

      return;
    }

    var key = emptySeq + '|' + (firstGrowing ? 'g' : '') + '|' + catSeq + '|' + wordsKey();

    if (!block || block.__fhKey !== key) {
      if (block) block.remove();

      block = emptiesBlock(page.doc, empties, !!firstGrowing);
      block.__fhKey = key;
      page.empties = block;
    }

    if (block.parentNode !== list || block.nextElementSibling !== firstGrowing) list.insertBefore(block, firstGrowing);
  }

  function emptiesBlock(doc, empties, hasGrowing) {
    var block = make(doc, 'div', 'fh-empties fh-hudadd', '');

    if (empties.length) {
      block.appendChild(make(doc, 'div', 'fh-sec', say('hEmptyHead')));
      empties.forEach(function (e) { block.appendChild(emptyRow(doc, e)); });
    }

    if (hasGrowing) block.appendChild(make(doc, 'div', 'fh-sec', say('hGrowingHead')));

    return block;
  }

  function emptyRow(doc, e) {
    var row = make(doc, 'div', 'mature-item-row fh-empty fh-hudadd', '');
    var kind = kindOf(e.kindId);
    var pot = hudIcon(doc, kind);
    var sub = make(doc, 'span', 'fh-sub', '');

    pot.className = 'fh-pot';
    row.setAttribute('data-interactive', '');
    row.setAttribute('data-pot', e.id);
    row.appendChild(pot);
    row.appendChild(make(doc, 'span', 'mature-item-name', say('hEmptyPot')));

    if (e.seed > 0) {
      row.appendChild(make(doc, 'span', 'mature-item-remain', say('hPlantSeed', cropOfSeed(e.seed).name)));
    } else {
      row.appendChild(make(doc, 'span', 'mature-item-remain fh-orange', say('hNoSeed')));
    }

    sub.appendChild(make(doc, 'span', '', place(e)));

    if (e.seed > 0) {
      var right = make(doc, 'span', 'fh-right', '');

      right.appendChild(hudIcon(doc, seedOf(e.seed)));
      right.appendChild(make(doc, 'span', '', say('hSeeds', e.count)));
      sub.appendChild(right);
    }

    row.appendChild(sub);
    row.addEventListener('click', function (ev) {
      ev.stopPropagation();

      if (e.seed > 0) sendAction('plant|' + e.id);
      else send('toggle');
    });

    return row;
  }

  // "+ 4 more growing" under the list while Needs you hides them; a click shows them all.
  function placeMore(page, list) {
    var hidden = panelShow === 'needs' ? Math.max(0, page.growing - SHOWN_GROWING) : 0;
    var more = page.more;
    var key = hidden + '|' + wordsKey();

    if (!hidden) {
      if (more) more.remove();

      page.more = null;

      return;
    }

    if (!more || more.__fhKey !== key) {
      if (more) more.remove();

      more = make(page.doc, 'div', 'fh-more fh-hudadd', say('hMore', hidden));
      more.__fhKey = key;
      more.setAttribute('data-interactive', '');
      more.addEventListener('click', function (e) {
        e.stopPropagation();
        setShow('all');
      });
      page.more = more;
    }

    if (more.parentNode !== list || more.nextElementSibling !== null) list.appendChild(more);
  }

  // An item's picture, or nothing when the game has none. Icon paths are relative to the root document's folder and
  // the HUD page sits in a folder of its own, so they are made absolute first.
  function hudIcon(doc, item) {
    var img = doc.createElement('img');

    if (item && item.icon) img.src = rootUrl(item.icon);
    else img.style.visibility = 'hidden';

    return img;
  }

  function rootUrl(path) {
    if (/^([a-z]+:|\/)/i.test(path)) return path;

    return String(location.href || '').replace(/[^\/]*([?#].*)?$/, '') + path;
  }

  function make(doc, tag, className, words) {
    var node = doc.createElement(tag);

    if (className) node.className = className;
    if (words) node.textContent = words;

    return node;
  }

  window.__slfarmhub = { open: open, setData: setData, close: close, status: status, live: setLive, catalog: catalog, badge: setBadge, hud: hud };
  (window.ExiledToolkitReady = window.ExiledToolkitReady || []).push(startWithToolkit);
})();
