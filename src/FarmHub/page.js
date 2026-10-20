// Farm Hub's web half. Runs in Root.html, the document every game page iframe sits in. Draws the Farm Hub window over
// all of them, styled like the game's own panels, and puts a plant button with a count badge in the HUD's tool column
// (bottom left, above the phone and bag buttons), built like the game's own buttons there. Talks to the plugin through
// vuplex.postMessage("slmod|farmhub|<verb>") and is driven by window.__slfarmhub.
(function () {
  if (window.__slfarmhub) return;

  var PREFIX = 'slmod|farmhub|';
  var HUD_CLASS = 'fh-hud-btn';
  // Farm Hub's place in the HUD's tool column when Exiled's Toolkit lays it out in columns of four.
  var DOCK_ORDER = '30';
  var HUD_STYLE_ID = 'fh-hud-style';
  var BUSY_TIMEOUT_MS = 5000;
  var TOAST_MS = 3500;
  var MAX_ADD = 30;
  var MAX_KEEP = 99;
  var MAX_NEEDS = 14;
  var FORECAST_HOURS = 48;
  var FORECAST_STEP = 6;
  // How wide a forecast chip is, as a share of the timeline, when deciding which lane it fits in.
  var CHIP_SHARE = 17;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A sprout: a stem with two leaves over a pot.
  var SPROUT_PATH = 'M11 21v-7.2C8.4 13.6 6 11.4 6 8c2.9 0 5 1.6 5 4.4V12h2v.4C13 9.6 15.1 8 18 8c0 3.4-2.4 5.6-5 5.8V21h-2zM6 18h12l-1 4H7l-1-4z';
  var TABS = ['pots', 'presets', 'robots', 'forecast', 'settings'];
  var TAB_WORD = { pots: 'tabPots', presets: 'tabPresets', robots: 'tabRobots', forecast: 'tabForecast', settings: 'tabSettings' };
  var STATE_WORD = { empty: 'stEmpty', growing: 'stGrowing', ripe: 'stRipe', withered: 'stWithered', poor: 'stPoor' };
  var STATE_BADGE = { empty: 'fh-b-dim', growing: '', ripe: 'fh-b-green', withered: 'fh-b-red', poor: 'fh-b-red' };
  var STATE_ORDER = ['ripe', 'withered', 'poor', 'growing', 'empty'];
  var PROBLEM_WORD = { pests: 'pPests', weeds: 'pWeeds', dry: 'pDry', dark: 'pDark', bright: 'pBright', cold: 'pCold', feed: 'pFeed' };
  var PROBLEM_BADGE = { pests: 'fh-b-amber', weeds: 'fh-b-amber', dry: 'fh-b-blue', dark: 'fh-b-red', bright: 'fh-b-red', cold: 'fh-b-red', feed: 'fh-b-amber' };
  var CHORE_WORD = { harvest: 'tHarvest', clear: 'tClear', uproot: 'tUproot', pests: 'tPests', weeds: 'tWeeds', water: 'tWater' };
  var FROM_WORD = { pot: 'fromPot', room: 'fromRoom', floor: 'fromFloor', all: 'fromAll' };
  var SIZE_WORD = { 1: 'szSmall', 2: 'szMedium', 4: 'szLarge' };
  var SHOWS = [['all', 'fAll'], ['ripe', 'fRipe'], ['rot', 'fRot'], ['pests', 'fPests'], ['dry', 'fDry'], ['care', 'fCare'], ['empty', 'fEmpty'], ['norobot', 'fNoRobot']];
  var GROUPS = [['room', 'gRoom'], ['crop', 'gCrop'], ['state', 'gState']];
  var SORTS = [['rot', 'sRot'], ['ripe', 'sRipe'], ['name', 'sName']];
  var ROT_WARNS = [6, 12, 24, 48];

  var LOCAL = {
    tab: pickTab, pick: pickPot, check: checkPot, clearchecks: clearChecks,
    filter: function (v) { state.show = v; render(); },
    fcrop: function (v) { state.crop = state.crop === +v ? 0 : +v; render(); },
    froom: function (v) { state.room = state.room === v ? '' : v; render(); },
    group: function (v) { state.group = v; render(); },
    sort: function (v) { state.sort = v; render(); },
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
    tabForecast: 'Forecast',
    tabSettings: 'Settings',
    sumPots: '{0} pots',
    cRipe: '{0} ripe',
    cRot: '{0} rotting soon',
    cPests: '{0} pests or weeds',
    cDry: '{0} dry',
    cEmpty: '{0} empty',
    plantAll: 'Plant empty pots ({0})',
    fertAll: 'Fertilize ({0})',
    tendAll: 'Tend everything',
    tHarvest: 'Harvest',
    tClear: 'Clear withered crop',
    tUproot: 'Uproot poor crop',
    tPests: 'Remove Pests',
    tWeeds: 'Remove Weeds',
    tWater: 'Water',
    show: 'Show',
    fAll: 'All',
    fRipe: 'Ripe',
    fRot: 'Rotting soon',
    fPests: 'Pests or weeds',
    fDry: 'Dry',
    fCare: 'Needs care',
    fEmpty: 'Empty',
    fNoRobot: 'No robot',
    crops: 'Crop',
    rooms: 'Room',
    groupBy: 'Group by',
    gRoom: 'Room',
    gCrop: 'Crop',
    gState: 'State',
    sortBy: 'Sort',
    sRot: 'Rots first',
    sRipe: 'Ripe first',
    sName: 'Name',
    noMatch: 'No pot matches these filters.',
    floor: 'Floor {0}',
    here: 'your floor',
    otherRoom: 'Elsewhere',
    emptyPots: 'Empty pots',
    nPots: '{0} pots',
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
    preset: 'Planting preset',
    fromPot: 'This pot\'s own choice',
    fromRoom: 'From its room',
    fromFloor: 'From its floor',
    fromAll: 'From the preset used everywhere',
    useDefault: 'Room or floor preset',
    empty: 'No pots placed at home yet.',
    needsYou: 'Needs you',
    allFine: 'Nothing needs you right now.',
    pickPot: 'Click a pot to see its timing, next planting and preset.',
    pot: 'Pot',
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
    selected: '{0} selected',
    clear: 'Clear',
    assignTo: 'Preset:',
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
    skipNote: 'A seed whose crop needs more light or heat than the pot gives is passed over.',
    fertilizeOn: 'Fertilize when planting',
    fertilizeNote: 'Uses the fertilizer order above.',
    autoReplant: 'Replant empty pots by themselves',
    autoNote: 'Plants an empty pot from this preset on its own, as if you pressed Plant.',
    usedFor: 'Used for',
    useAll: 'Everywhere',
    usedNote: 'A pot uses its own choice, else its room\'s, else its floor\'s, else the one used everywhere.',
    willDo: 'What it will do',
    plantingsLeft: '{0} plantings left',
    ok: 'OK',
    low: 'Low',
    out: 'Out',
    planNote: 'Plantings left counts seeds above the keep counts. Two rounds for every pot is OK.',
    nextBySize: 'Next crop for each size',
    nothingLeft: 'No seed left',
    seedOut: '{0}: not enough seed, skipped.',
    noPotsUse: 'No pot uses this preset yet.',
    keepStocked: 'Keep robots stocked',
    keepNote: 'When a robot runs out of the seed for a pot size, switch it to the next seed in its floor\'s preset.',
    noRobots: 'No planter robots at home.',
    battery: 'Battery {0}%',
    working: 'Working',
    robotOff: 'Switched off',
    colSize: 'Size',
    colSeed: 'Seed now',
    noSeed: 'No seed',
    change: 'Change',
    cancel: 'Cancel',
    seed: 'Seed',
    stockHead: 'Seeds and fertilizer',
    stockNote: 'Per size: how many pots what you own would fill, after the keep counts.',
    colItem: 'Item',
    colHave: 'Have',
    comingUp: 'Next 48 hours',
    now: 'now',
    rots: '{0} rots',
    nothingSoon: 'Nothing ripens or rots in the next 48 hours.',
    rotWarn: 'Rotting soon means within',
    rotWarnNote: 'Pots this close to rotting are marked red and counted on the plant button.',
    hotkey: 'Hotkey',
    keyOpen: '{0} opens and closes this window.',
    keyNone: 'No key is set; use the plant button on the left of the screen.',
    presetFile: 'Presets are kept in BepInEx/config/farm-hub.json, shared by every save.',
    escClose: 'Esc to close',
    tip: 'Your pots: ripe crops, problems and planting'
  };

  var state = {
    data: { ready: false, note: '' }, isOpen: false, isLive: false, key: '', badge: 0, tab: 'pots', sel: '', checks: {},
    show: 'all', crop: 0, room: '', group: 'room', sort: 'rot',
    presetId: '', draft: null, draftOf: '', pendingNewFrom: null, addCap: 0, addSearch: '', isCropPicking: false, cropSearch: '',
    tierEdit: null, armed: '', isTyping: false, isStale: false
  };
  var cat = { kinds: {}, crops: {}, seeds: {}, ferts: {}, seedList: [], fertList: [], cropList: [], caps: [] };
  var ui = null;
  var shownShape = '';
  var toastTimer = 0;
  var isBusy = false;
  var busyTimer = 0;
  var hudVersion = 0;
  var hasReportedPageAccess = false;
  var hasReportedHudPlaced = false;

  function say(key) {
    var words = window.__slfarmhubWords || ENGLISH;
    var text = words[key] || ENGLISH[key] || key;

    for (var i = 1; i < arguments.length; i++) text = text.split('{' + (i - 1) + '}').join(String(arguments[i]));

    return text;
  }

  // A phrase with its number picked out, as in "5 ripe": the number is bold and coloured, the rest is plain.
  function sayCount(key, count, colorClass) {
    var parts = say(key).split('{0}');
    var node = el('span');

    put(node, text(parts[0]), el('b', colorClass, String(count)), text(parts.slice(1).join('{0}')));

    return node;
  }

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
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

  // A row of choices where one is on, like the game's tab strips.
  function segment(choices, current, action) {
    var seg = el('div', 'fh-seg');

    choices.forEach(function (c) { seg.appendChild(act(el('button', 'fh-sg' + (c[0] === current ? ' fh-on' : ''), c[1]), action, c[0])); });

    return seg;
  }

  function switchButton(isOn, action, args) {
    return act(el('button', 'fh-tog' + (isOn ? ' fh-on' : '')), action, args);
  }

  function badge(content, colorClass) {
    return el('span', 'fh-badge' + (colorClass ? ' ' + colorClass : ''), content);
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
    '#fh-root{position:fixed;inset:0;z-index:2147483000;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.45);font-family:"Noto Sans SC","Microsoft YaHei","PingFang SC",sans-serif;color:#eee;user-select:none;font-size:14px}',
    '#fh-root *{box-sizing:border-box}',
    '#fh-root button{font:inherit}',
    '.fh-plate{width:min(1600px,96vw);height:min(940px,92vh);display:flex;flex-direction:column;gap:12px;padding:18px 22px;border-radius:10px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(30,30,33,.95),rgba(18,18,20,.97));box-shadow:0 10px 40px rgba(0,0,0,.6)}',
    '.fh-head{display:flex;align-items:center;gap:14px}',
    '.fh-title{font-size:22px;font-weight:700;letter-spacing:1px;text-shadow:0 2px 6px rgba(0,0,0,.6)}',
    '.fh-sum{flex:1;font-size:14px;color:rgba(238,238,238,.6);display:flex;gap:6px;flex-wrap:wrap;align-items:baseline}',
    '.fh-sum b{font-weight:700;color:#eee}.fh-sum .fh-dot{color:rgba(238,238,238,.3)}',
    '.fh-cg{color:#aed581!important}.fh-cr{color:#ff8a80!important}.fh-ca{color:#EBA308!important}.fh-cb{color:#90caf9!important}',
    '.fh-tabs{display:flex;gap:4px;border-bottom:1px solid rgba(255,255,255,.14)}',
    '.fh-tab{font-size:15px;color:#b8b8b8;padding:8px 18px 9px;border:1px solid transparent;border-bottom:none;border-radius:7px 7px 0 0;background:none;position:relative;top:1px;display:flex;gap:8px;align-items:center;cursor:pointer}',
    '.fh-tab:hover{color:#fff}.fh-tab.fh-on{color:#EBA308;background:rgba(10,10,12,.72);border-color:rgba(255,255,255,.14);font-weight:600}',
    '.fh-tab .fh-n{font-size:11px;padding:1px 6px;border-radius:7px;background:#D32F2F;color:#fff;font-weight:700}',
    '.fh-body{flex:1;min-height:0;display:flex;gap:12px}',
    '.fh-well{background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);border-radius:8px;min-height:0;overflow-y:auto}',
    '.fh-well::-webkit-scrollbar{width:6px}.fh-well::-webkit-scrollbar-thumb{background:rgba(235,163,8,.35);border-radius:3px}',
    '.fh-col{display:flex;flex-direction:column;gap:10px;min-height:0;min-width:0}',
    '.fh-sect{font-size:12px;color:#EBA308;letter-spacing:2px;text-transform:uppercase;display:flex;align-items:center;gap:10px;padding:12px 12px 6px}',
    '.fh-sect::after{content:"";flex:1;height:1px;background:linear-gradient(90deg,rgba(235,163,8,.25),transparent)}',
    '.fh-sect .fh-r{order:2;text-transform:none;letter-spacing:0;color:#888;font-size:12px}',
    '.fh-sect .fh-badge{letter-spacing:0;text-transform:none}',
    '.fh-side{width:250px;flex:none;padding:0 10px 12px}',
    '.fh-chips{display:flex;gap:6px;flex-wrap:wrap;padding:2px 2px 4px}',
    '.fh-chip{display:inline-flex;align-items:center;gap:6px;padding:4px 9px;border-radius:7px;background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);color:#eee;font-size:13px;cursor:pointer;white-space:nowrap}',
    '.fh-chip.fh-ic{padding-left:4px}.fh-chip:hover{border-color:rgba(235,163,8,.6)}',
    '.fh-chip.fh-on{border-color:#EBA308;color:#EBA308;background:rgba(235,163,8,.12)}',
    '.fh-chip .fh-n{color:#EBA308;font-weight:600}.fh-chip.fh-zero{opacity:.45}',
    '.fh-seg{display:inline-flex;border:1px solid rgba(255,255,255,.14);border-radius:7px;overflow:hidden;flex:none}',
    '.fh-sg{font-size:13px;padding:5px 11px;color:#b8b8b8;background:none;border:none;border-right:1px solid rgba(255,255,255,.08);cursor:pointer;white-space:nowrap}',
    '.fh-sg:last-child{border-right:none}.fh-sg:hover{color:#fff}.fh-sg.fh-on{background:rgba(235,163,8,.18);color:#EBA308}',
    '.fh-mid{flex:1;min-width:0;display:flex;flex-direction:column;gap:10px}',
    '.fh-acts{display:flex;align-items:center;gap:8px;flex-wrap:wrap;padding:8px 10px;border-radius:8px;border:1px solid rgba(255,255,255,.14);background:rgba(10,10,12,.72)}',
    '.fh-acts.fh-bulk{border-color:rgba(235,163,8,.6);background:rgba(235,163,8,.07)}.fh-acts .fh-grow{flex:1}',
    '.fh-cards{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:6px;padding:0 8px 8px}',
    '.fh-card{position:relative;display:flex;gap:12px;align-items:center;padding:10px 10px 10px 12px;border-radius:7px;background:rgba(255,255,255,.04);border:1px solid transparent;cursor:pointer;min-height:78px}',
    '.fh-card:hover{background:rgba(255,255,255,.07)}.fh-card.fh-hot{border-color:rgba(229,57,53,.5)}',
    '.fh-card.fh-sel{border-color:rgba(235,163,8,.8);background:rgba(235,163,8,.08)}',
    '.fh-chk{position:absolute;left:4px;top:4px;width:18px;height:18px;padding:0;border-radius:4px;border:1px solid rgba(255,255,255,.35);background:rgba(0,0,0,.6);color:#111;font-size:12px;line-height:16px;font-weight:700;cursor:pointer;opacity:0;z-index:1}',
    '.fh-card:hover .fh-chk,.fh-checking .fh-chk{opacity:1}.fh-chk.fh-on{opacity:1;background:#EBA308;border-color:#EBA308}',
    '.fh-cmain{flex:1;min-width:0;display:flex;flex-direction:column;gap:4px}',
    '.fh-nm{font-size:15px;font-weight:600;display:flex;flex-wrap:wrap;gap:4px 6px;align-items:center;white-space:nowrap}',
    '.fh-ln{font-size:13px;color:#b8b8b8;display:flex;gap:8px;align-items:center;white-space:nowrap;overflow:hidden}',
    '.fh-sub{font-size:12px;color:#888;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-crt{display:flex;flex-direction:column;align-items:flex-end;gap:6px;flex:none}',
    '.fh-badge{flex:none;font-size:11px;padding:2px 7px;border-radius:5px;background:rgba(255,255,255,.1);color:rgba(238,238,238,.85);white-space:nowrap;font-weight:400}',
    '.fh-b-red{background:rgba(229,57,53,.28);color:#ff8a80}.fh-b-amber{background:rgba(255,193,7,.22);color:#EBA308}.fh-b-green{background:rgba(124,179,66,.24);color:#aed581}',
    '.fh-b-blue{background:rgba(100,181,246,.2);color:#90caf9}.fh-b-dim{background:rgba(255,255,255,.06);color:#888}',
    '.fh-track{width:110px;height:8px;border-radius:4px;background:rgba(255,255,255,.1);overflow:hidden;flex:none}',
    '.fh-fill{display:block;height:100%;border-radius:4px;background:#7cb342}.fh-fill.fh-amber{background:#EBA308}.fh-fill.fh-red{background:#e53935}.fh-fill.fh-blue{background:#64b5f6}',
    '.fh-detail{width:440px;flex:none;padding:14px;display:flex;flex-direction:column;gap:12px}',
    '.fh-dhead{display:flex;gap:12px;align-items:center}.fh-dname{flex:1;min-width:0}',
    '.fh-big{font-size:20px;font-weight:700}.fh-dim{color:#888;font-size:12px}',
    '.fh-bar{display:flex;align-items:center;gap:8px;flex-wrap:wrap}',
    '.fh-box{border:1px solid rgba(255,255,255,.08);border-radius:8px;padding:10px 12px;background:rgba(255,255,255,.025);display:flex;flex-direction:column;gap:6px}',
    '.fh-box h4{margin:0 0 2px;display:flex;align-items:center;gap:8px;font-size:12px;color:#EBA308;letter-spacing:2px;text-transform:uppercase;font-weight:600}',
    '.fh-kv{display:grid;grid-template-columns:auto 1fr;gap:5px 16px;font-size:13px;color:#b8b8b8}.fh-kv b{color:#eee;font-weight:600}',
    '.fh-nrow{display:flex;align-items:center;gap:10px;font-size:13px;padding:5px 8px;border-radius:6px}',
    '.fh-nrow.fh-next{background:rgba(124,179,66,.13)}.fh-nrow.fh-out{opacity:.45}.fh-nrow .fh-grow{flex:1;min-width:0}',
    '.fh-ordn{width:16px;text-align:center;color:#888;font-size:12px}',
    '.fh-row{display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:7px;background:rgba(255,255,255,.04);border:1px solid transparent;font-size:13px;cursor:pointer}',
    '.fh-row:hover{background:rgba(255,255,255,.07)}.fh-row .fh-grow{flex:1;min-width:0}',
    '.fh-rows{display:flex;flex-direction:column;gap:6px}',
    '.fh-num{font-weight:600;color:#EBA308;font-variant-numeric:tabular-nums}',
    '.fh-red{color:#ff8a80}.fh-green{color:#aed581}',
    '.fh-btn{font-size:13px;color:#dde2f0;padding:7px 12px;border-radius:7px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(58,58,60,.9),rgba(38,38,40,.92));cursor:pointer;white-space:nowrap}',
    '.fh-btn:hover:not(:disabled){background:linear-gradient(180deg,rgba(72,66,48,.92),rgba(50,46,32,.95));border-color:rgba(255,193,7,.75)}',
    '.fh-btn.fh-hot{border-color:rgba(235,163,8,.75);color:#EBA308}',
    '.fh-btn.fh-danger:hover:not(:disabled),.fh-btn.fh-armed{background:linear-gradient(180deg,rgba(84,44,42,.92),rgba(58,30,28,.95));border-color:rgba(229,57,53,.75);color:#ff8a80}',
    '.fh-btn.fh-sm{font-size:12px;padding:4px 9px}.fh-btn:disabled{opacity:.4;cursor:default}',
    '.fh-btn.fh-x{width:32px;height:32px;padding:0;font-size:18px;line-height:30px;text-align:center}',
    '.fh-ib{width:20px;height:20px;padding:0;border-radius:5px;border:1px solid rgba(255,255,255,.1);background:rgba(255,255,255,.05);color:#b8b8b8;font-size:11px;line-height:18px;text-align:center;cursor:pointer}',
    '.fh-ib:hover:not(:disabled){border-color:#EBA308;color:#EBA308}.fh-ib:disabled{opacity:.2;cursor:default}.fh-ib.fh-del:hover{border-color:#e53935;color:#ff8a80}',
    '.fh-tog{width:40px;height:22px;padding:0;border-radius:11px;background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.14);position:relative;flex:none;cursor:pointer}',
    '.fh-tog::after{content:"";position:absolute;top:3px;left:3px;width:14px;height:14px;border-radius:50%;background:#999;transition:left .15s}',
    '.fh-tog.fh-on{background:rgba(235,163,8,.35);border-color:#EBA308}.fh-tog.fh-on::after{left:21px;background:#EBA308}',
    '.fh-stepper{display:inline-flex;align-items:center;border:1px solid rgba(255,255,255,.14);border-radius:7px;overflow:hidden;font-size:13px;flex:none}',
    '.fh-stp{padding:1px 8px;color:#999;background:rgba(255,255,255,.04);border:none;cursor:pointer}.fh-stp:hover:not(:disabled){color:#EBA308}.fh-stp:disabled{opacity:.3;cursor:default}',
    '.fh-stepper b{padding:1px 6px;color:#EBA308;font-weight:600;min-width:28px;text-align:center}',
    '.fh-setrow{display:flex;align-items:center;gap:14px;padding:10px 14px;border-radius:7px;background:rgba(255,255,255,.04)}',
    '.fh-lbl{flex:1;min-width:0}.fh-lbl b{display:block;font-size:14px;font-weight:600}.fh-lbl small{display:block;font-size:12px;color:#888;margin-top:2px}',
    '.fh-note{font-size:12px;color:#888;padding:4px 12px}',
    '.fh-banner{padding:8px 12px;border-radius:7px;border:1px solid rgba(255,193,7,.6);background:rgba(255,193,7,.1);color:#EBA308;font-size:13px}',
    '.fh-plist{width:270px;flex:none;display:flex;flex-direction:column;gap:10px}.fh-plist .fh-well{flex:1}',
    '.fh-pitem{display:flex;align-items:center;gap:8px;padding:10px 12px;margin:6px;border-radius:7px;background:rgba(255,255,255,.04);border:1px solid transparent;cursor:pointer}',
    '.fh-pitem:hover{background:rgba(255,255,255,.07)}.fh-pitem.fh-sel{border-color:rgba(235,163,8,.75);background:rgba(235,163,8,.08)}',
    '.fh-pitem .fh-lbl b{font-size:15px}.fh-pitem small{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.fh-icons{display:flex;gap:2px;flex:none}',
    '.fh-editor{flex:1;min-width:0;padding:12px 14px 16px;display:flex;flex-direction:column;gap:8px}',
    '.fh-inp{font:inherit;font-size:15px;font-weight:600;color:#fff;background:rgba(0,0,0,.5);border:1px solid rgba(255,255,255,.14);border-radius:7px;padding:6px 10px;user-select:text}',
    '.fh-inp:focus{outline:none;border-color:rgba(235,163,8,.75)}.fh-inp.fh-small{font-size:13px;font-weight:400;padding:4px 8px;width:100%}',
    '.fh-capcols{display:grid;gap:8px}',
    '.fh-capcol{border:1px solid rgba(255,255,255,.08);border-radius:8px;padding:8px;background:rgba(255,255,255,.02);display:flex;flex-direction:column;gap:6px;min-width:0}',
    '.fh-caphead{display:flex;align-items:center;gap:8px;font-size:14px;font-weight:600;padding:2px 2px 4px}.fh-caphead .fh-grow{flex:1}',
    '.fh-step{display:grid;grid-template-columns:20px 30px minmax(0,1fr) auto;gap:7px;align-items:center;padding:6px 7px;border-radius:7px;background:rgba(255,255,255,.04);border-left:2px solid transparent}',
    '.fh-step.fh-short{border-left-color:#e53935}.fh-step.fh-go{border-left-color:#7cb342}.fh-step.fh-bad{opacity:.55}',
    '.fh-ord{width:20px;height:20px;border-radius:50%;display:grid;place-items:center;background:rgba(235,163,8,.18);color:#EBA308;font-weight:700;font-size:11px}',
    '.fh-sinfo{min-width:0}.fh-sinfo b{font-size:13px;font-weight:600;display:flex;gap:6px;align-items:center;white-space:nowrap;overflow:hidden}',
    '.fh-sinfo small{display:block;font-size:11px;color:#888;line-height:1.35}',
    '.fh-sctl{display:flex;flex-direction:column;align-items:flex-end;gap:4px}.fh-moves{display:flex;gap:3px}',
    '.fh-dash{font-size:13px;color:#b8b8b8;padding:6px;border-radius:7px;border:1px dashed rgba(255,255,255,.18);background:none;cursor:pointer;text-align:center}',
    '.fh-dash:hover,.fh-dash.fh-on{color:#EBA308;border-color:rgba(235,163,8,.6)}',
    '.fh-picker{display:flex;flex-direction:column;gap:3px;padding:6px;border-radius:7px;background:rgba(0,0,0,.35);border:1px solid rgba(235,163,8,.4)}',
    '.fh-prow{display:flex;align-items:center;gap:8px;font-size:12px;padding:4px;border-radius:5px;cursor:pointer}.fh-prow:hover{background:rgba(235,163,8,.12)}.fh-prow .fh-grow{flex:1;min-width:0}',
    '.fh-chain{display:flex;flex-wrap:wrap;align-items:center;gap:8px;padding:0 4px}',
    '.fh-link{display:inline-flex;align-items:center;gap:6px;padding:4px 6px 4px 4px;border-radius:7px;background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.1);font-size:13px}',
    '.fh-link.fh-out .fh-lname{text-decoration:line-through;color:#888}.fh-arrow{color:#888}',
    '.fh-ctable{display:flex;flex-direction:column;gap:4px}',
    '.fh-crow{display:grid;grid-template-columns:190px minmax(0,1fr) 170px 120px 22px;gap:10px;align-items:center;padding:7px 10px;border-radius:7px;background:rgba(255,255,255,.04);font-size:13px}',
    '.fh-crow.fh-chead{background:none;padding:2px 10px;color:#888;font-size:11px;letter-spacing:1px;text-transform:uppercase}',
    '.fh-cname{display:flex;align-items:center;gap:8px;min-width:0;white-space:nowrap;overflow:hidden}',
    '.fh-plan{width:360px;flex:none;padding:0 12px 14px;display:flex;flex-direction:column;gap:8px}',
    '.fh-prow2{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:7px;background:rgba(255,255,255,.04);font-size:13px}.fh-prow2 .fh-grow{flex:1;min-width:0}',
    '.fh-robots{flex:1;padding:12px;display:flex;flex-direction:column;gap:12px}',
    '.fh-robot{border:1px solid rgba(255,255,255,.1);border-radius:8px;padding:12px 14px;background:rgba(255,255,255,.025);display:flex;flex-direction:column;gap:8px}',
    '.fh-rhead{display:flex;gap:14px;align-items:center}',
    '.fh-rgrid{display:grid;grid-template-columns:repeat(auto-fill,minmax(620px,1fr));gap:12px;align-items:start}',
    '.fh-tier{display:grid;grid-template-columns:100px minmax(0,1.2fr) minmax(0,1fr) auto;gap:12px;align-items:center;padding:8px 10px;border-radius:7px;background:rgba(255,255,255,.04);font-size:13px}',
    '.fh-tier.fh-thead{background:none;padding:2px 10px;color:#888;font-size:11px;letter-spacing:1px;text-transform:uppercase}',
    '.fh-tcell{display:flex;align-items:center;gap:8px;min-width:0;white-space:nowrap;overflow:hidden}',
    '.fh-tedit{padding:10px 12px;border-radius:7px;border:1px solid rgba(235,163,8,.5);background:rgba(235,163,8,.05);display:flex;flex-direction:column;gap:8px}',
    '.fh-stock{width:560px;flex:none;padding:0 10px 12px}',
    '.fh-trow{display:grid;gap:8px;align-items:center;padding:5px 6px;border-bottom:1px solid rgba(255,255,255,.05);font-size:13px}',
    '.fh-trow.fh-thead{color:#888;font-size:12px}.fh-trow .fh-tc{text-align:right;font-variant-numeric:tabular-nums}',
    '.fh-time{flex:1;min-width:0;padding:0 16px 14px}',
    '.fh-axis{position:relative;height:28px;margin:6px 0 0;border-bottom:1px solid rgba(255,255,255,.14)}',
    '.fh-tick{position:absolute;top:6px;transform:translateX(-50%);font-size:11px;color:#888;white-space:nowrap}',
    '.fh-lanes{position:relative;margin-top:12px}',
    '.fh-gline{position:absolute;top:-12px;bottom:0;width:1px;background:rgba(255,255,255,.05)}.fh-gline.fh-now{width:2px;background:#EBA308}',
    '.fh-ev{position:absolute;display:inline-flex;align-items:center;gap:6px;padding:2px 9px 2px 3px;border-radius:12px;font-size:12px;white-space:nowrap;background:rgba(18,18,20,.95);border:1px solid rgba(124,179,66,.6);cursor:pointer}',
    '.fh-ev.fh-rot{border-color:rgba(229,57,53,.75)}.fh-ev .fh-dim{font-size:11px}',
    '.fh-icon{flex:none;display:flex;align-items:center;justify-content:center;border-radius:6px;background:rgba(255,255,255,.05);overflow:hidden;color:rgba(238,238,238,.7);font-weight:600}',
    '.fh-icon img{width:100%;height:100%;object-fit:contain}',
    '.fh-i56{width:56px;height:56px;font-size:20px}.fh-i40{width:40px;height:40px;font-size:15px}.fh-i30{width:30px;height:30px;font-size:12px}.fh-i22{width:22px;height:22px;font-size:11px}.fh-i18{width:18px;height:18px;font-size:10px;border-radius:4px}',
    '.fh-empty{padding:24px;color:#888;font-size:14px;text-align:center}',
    '.fh-lockmsg{padding:10px 14px;border-radius:7px;border:1px solid rgba(255,193,7,.75);background:rgba(255,193,7,.12);color:#EBA308;font-size:14px}',
    '.fh-foot{display:flex;gap:12px;font-size:13px;color:rgba(238,238,238,.7);align-items:center}.fh-status{flex:1}',
    '.fh-kbd{font-size:12px;color:#ccc;border:1px solid #555;border-bottom-width:2px;border-radius:4px;padding:0 6px;margin:0 4px}',
    '#fh-toast{position:fixed;left:50%;bottom:120px;transform:translateX(-50%);z-index:2147483001;pointer-events:none;display:none;max-width:640px;text-align:center;padding:10px 18px;border-radius:8px;border:1px solid rgba(255,193,7,.75);background:linear-gradient(180deg,rgba(30,30,33,.94),rgba(18,18,20,.96));color:#eee;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = el('div');
    var plate = el('div', 'fh-plate');
    var head = el('div', 'fh-head');
    var sum = el('div', 'fh-sum');
    var tabs = el('div', 'fh-tabs');
    var body = el('div', 'fh-body');
    var foot = el('div', 'fh-foot');
    var status = el('div', 'fh-status');
    var keys = el('div');
    var toast = el('div');

    root.id = 'fh-root';
    toast.id = 'fh-toast';
    put(head, el('div', 'fh-title', 'Farm Hub'), sum, button('\u00d7', 'close', null, true, 'fh-x'));
    put(foot, status, keys);
    put(plate, head, tabs, body, foot);
    root.appendChild(plate);
    document.body.appendChild(root);
    document.body.appendChild(toast);

    root.addEventListener('click', onClick);
    root.addEventListener('input', onInput);
    root.addEventListener('focusin', onFocus);
    root.addEventListener('focusout', onBlur);
    root.addEventListener('keydown', onKey);
    // Clicks inside the plate must not reach the game pages underneath; the mask itself closes nothing on purpose.
    root.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    // A broken icon path swaps to the first-letter placeholder; error events do not bubble, so capture them.
    root.addEventListener('error', function (e) {
      var img = e.target;

      if (img && img.tagName === 'IMG' && img.parentNode) img.parentNode.textContent = img.getAttribute('data-letter') || '?';
    }, true);

    ui = { root: root, sum: sum, tabs: tabs, body: body, status: status, keys: keys, toast: toast, live: [], cards: null };
  }

  // The style outlives a lost window root, so a rebuild must not stack another copy of it.
  function addStyle() {
    if (document.getElementById('fh-style')) return;

    var style = el('style');
    style.id = 'fh-style';
    style.textContent = CSS;
    document.head.appendChild(style);
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

  function ripeness(pot) {
    if (pot.state === 'ripe') return [0, pot.rotIn == null ? 1e6 : pot.rotIn];
    if (pot.state === 'growing') return [1, pot.ripeIn == null ? 1e6 : pot.ripeIn];

    return [2 + STATE_ORDER.indexOf(pot.state), 0];
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
    ui.cards = null;
    renderHead();
    renderTabs();
    ui.body.textContent = '';

    if (state.tab === 'settings') renderSettings();
    else if (!state.data.ready) ui.body.appendChild(lockMessage(state.data.note));
    else if (state.tab === 'presets') renderPresets();
    else if (state.tab === 'robots') renderRobots();
    else if (state.tab === 'forecast') renderForecast();
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
    var node = el('div', 'fh-well' + (className ? ' ' + className : ''));
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
    var parts = [];

    ui.sum.textContent = '';

    if (!state.data.ready || !c) return;

    parts.push(sayCount('sumPots', c.total, ''));

    if (c.ripe) parts.push(sayCount('cRipe', c.ripe, 'fh-cg'));
    if (c.rot) parts.push(sayCount('cRot', c.rot, 'fh-cr'));
    if (c.pests) parts.push(sayCount('cPests', c.pests, 'fh-ca'));
    if (c.dry) parts.push(sayCount('cDry', c.dry, 'fh-cb'));
    if (c.empty) parts.push(sayCount('cEmpty', c.empty, ''));

    parts.forEach(function (part, i) {
      if (i) ui.sum.appendChild(el('span', 'fh-dot', '\u00b7'));

      ui.sum.appendChild(part);
    });
  }

  function renderTabs() {
    ui.tabs.textContent = '';

    TABS.forEach(function (tab) {
      var node = act(el('button', 'fh-tab' + (state.tab === tab ? ' fh-on' : ''), say(TAB_WORD[tab])), 'tab', tab);

      if (tab === 'pots' && state.badge > 0) node.appendChild(el('span', 'fh-n', String(state.badge)));

      ui.tabs.appendChild(node);
    });
  }

  function renderKeys() {
    ui.keys.textContent = '';

    if (state.key) put(ui.keys, text(say('hotkey')), el('span', 'fh-kbd', state.key), text('/ '));

    ui.keys.appendChild(text(say('escClose')));
  }

  function pickTab(tab) {
    if (TABS.indexOf(tab) < 0) return;

    state.tab = tab;
    render();
  }

  // ---------- Pots tab ----------

  function renderPots() {
    var mid = el('div', 'fh-mid');
    var detail = scroller('detail', 'fh-detail');

    ui.acts = el('div', 'fh-acts');
    ui.potList = potList();
    ui.detail = detail;
    fillActs();
    fillDetail();
    put(mid, ui.acts, ui.potList);
    put(ui.body, potSide(), mid, detail);
  }

  function shows(pot, show) {
    switch (show) {
      case 'ripe':
        return pot.state === 'ripe';
      case 'rot':
        return !!pot.rotSoon;
      case 'pests':
        return hasProblem(pot, ['pests', 'weeds']);
      case 'dry':
        return hasProblem(pot, ['dry']);
      case 'care':
        return needsCare(pot);
      case 'empty':
        return pot.state === 'empty';
      case 'norobot':
        return !pot.robot;
      default:
        return true;
    }
  }

  function roomKey(pot) {
    return pot.floor + '|' + pot.area;
  }

  function isShown(pot) {
    return shows(pot, state.show) && (!state.crop || pot.plant === state.crop) && (!state.room || roomKey(pot) === state.room);
  }

  // Filters with their counts, the crops and rooms there are, and how the list is grouped and sorted.
  function potSide() {
    var side = scroller('side', 'fh-side');
    var showChips = el('div', 'fh-chips');
    var cropChips = el('div', 'fh-chips');
    var roomChips = el('div', 'fh-chips');
    var crops = {};
    var rooms = [];

    SHOWS.forEach(function (s) {
      var count = pots().filter(function (p) { return shows(p, s[0]); }).length;
      var node = chip(say(s[1]), 'filter', s[0], state.show === s[0], count);

      if (!count && state.show !== s[0]) setClass(node, 'fh-zero', true);

      showChips.appendChild(node);
    });

    pots().forEach(function (p) {
      if (p.plant) crops[p.plant] = (crops[p.plant] || 0) + 1;
      if (!find(rooms, function (r) { return r.key === roomKey(p); })) rooms.push({ key: roomKey(p), pot: p, count: 0 });

      find(rooms, function (r) { return r.key === roomKey(p); }).count++;
    });
    Object.keys(crops).map(function (id) { return cropOf(+id); }).sort(byName).forEach(function (crop) {
      cropChips.appendChild(chip(crop.name, 'fcrop', crop.id, state.crop === crop.id, crops[crop.id], crop));
    });
    rooms.forEach(function (r) { roomChips.appendChild(chip(roomTitle(r.pot), 'froom', r.key, state.room === r.key, r.count)); });

    put(side, section(say('show')), showChips);

    if (cropChips.firstChild) put(side, section(say('crops')), cropChips);
    if (rooms.length > 1) put(side, section(say('rooms')), roomChips);

    put(side, section(say('groupBy')), segment(GROUPS.map(function (g) { return [g[0], say(g[1])]; }), state.group, 'group'),
      section(say('sortBy')), segment(SORTS.map(function (s) { return [s[0], say(s[1])]; }), state.sort, 'sort'));

    return side;
  }

  function roomTitle(pot) {
    return (pot.room || say('otherRoom')) + ' \u00b7 ' + say('floor', pot.floor);
  }

  // The big buttons, or with pots checked, what can be done to just those.
  function fillActs() {
    var ids = checkedIds();
    var c = state.data.counts || {};

    ui.acts.textContent = '';
    setClass(ui.acts, 'fh-bulk', ids.length > 0);
    setClass(ui.potList, 'fh-checking', ids.length > 0);

    if (!ids.length) {
      // The bulk buttons stay pressable with nothing to do: a press then says so.
      put(ui.acts, button(say('plantAll', c.plant || 0), 'plantall', null, true, 'fh-hot'), button(say('fertAll', c.fert || 0), 'fertall', null, true),
        button(say('tendAll'), 'tendall', null, true));

      return;
    }

    var list = ids.join(',');

    put(ui.acts, el('b', '', say('selected', ids.length)), button(say('plant'), 'plant', list, true, 'fh-sm fh-hot'), button(say('fertilize'), 'fert', list, true, 'fh-sm'),
      el('span', 'fh-dim', say('assignTo')), chip(say('useDefault'), 'assign', '|' + list, false));
    presets().forEach(function (p) { ui.acts.appendChild(chip(p.name, 'assign', p.id + '|' + list, false)); });
    put(ui.acts, el('span', 'fh-grow'), button(say('clear'), 'clearchecks', null, true, 'fh-sm'));

    if (!state.data.saved) {
      ui.acts.querySelectorAll('[data-act]').forEach(function (node) {
        if (node.getAttribute('data-act') === 'assign') node.disabled = true;
      });
      ui.acts.appendChild(el('span', 'fh-dim', say('noSaveYet')));
    }
  }

  function checkedIds() {
    return Object.keys(state.checks).filter(function (id) { return state.checks[id] && potById(id); });
  }

  function potList() {
    var well = scroller('pots');
    var shown = pots().filter(isShown);

    ui.cards = {};

    if (!pots().length) {
      well.appendChild(el('div', 'fh-empty', say('empty')));

      return well;
    }

    if (!shown.length) {
      well.appendChild(el('div', 'fh-empty', say('noMatch')));

      return well;
    }

    groupsOf(shown).forEach(function (group) {
      var cards = el('div', 'fh-cards');

      sorted(group.pots).forEach(function (pot) { cards.appendChild(potCard(pot)); });
      put(well, groupHead(group), cards);
    });

    return well;
  }

  // Groups keep the order the plugin sends (floor, then room), so rooms read top floor to bottom as the house does.
  function groupsOf(list) {
    var groups = [];

    list.forEach(function (pot) {
      var key = groupKey(pot);
      var group = find(groups, function (g) { return g.key === key; });

      if (!group) groups.push(group = { key: key, first: pot, pots: [] });

      group.pots.push(pot);
    });

    if (state.group === 'state') groups.sort(function (a, b) { return STATE_ORDER.indexOf(a.key) - STATE_ORDER.indexOf(b.key); });
    if (state.group === 'crop') groups.sort(function (a, b) { return (a.key === 0) - (b.key === 0) || byName(cropOf(a.key), cropOf(b.key)); });

    return groups;
  }

  function groupKey(pot) {
    if (state.group === 'crop') return pot.plant || 0;
    if (state.group === 'state') return pot.state;

    return roomKey(pot);
  }

  function groupHead(group) {
    var pot = group.first;
    var right = el('span', 'fh-r', say('nPots', group.pots.length));
    var head;

    if (state.group === 'crop') head = section(pot.plant ? cropOf(pot.plant).name : say('emptyPots'), right);
    else if (state.group === 'state') head = section(say(STATE_WORD[pot.state] || pot.state), right);
    else {
      var preset = presetById(pot.preset);

      head = section(roomTitle(pot), right);

      if (pot.floor === state.data.floor) head.insertBefore(badge(say('here'), 'fh-b-amber'), right);
      if (preset) right.textContent = say('nPots', group.pots.length) + ' \u00b7 ' + preset.name;
    }

    return head;
  }

  function sorted(list) {
    var keyed = list.map(function (pot, i) { return { pot: pot, i: i }; });

    keyed.sort(function (a, b) {
      var order = 0;

      if (state.sort === 'rot') order = compareKeys(urgency(a.pot), urgency(b.pot));
      else if (state.sort === 'ripe') order = compareKeys(ripeness(a.pot), ripeness(b.pot));
      else order = byName({ name: potName(a.pot) }, { name: potName(b.pot) });

      return order || a.i - b.i;
    });

    return keyed.map(function (k) { return k.pot; });
  }

  function stateBadge(pot) {
    return badge(say(STATE_WORD[pot.state] || pot.state), STATE_BADGE[pot.state]);
  }

  function potBadges(pot, line) {
    if (pot.rotSoon) line.appendChild(badge(say('rotSoon'), 'fh-b-red'));

    (pot.problems || []).forEach(function (p) { line.appendChild(badge(say(PROBLEM_WORD[p] || p), PROBLEM_BADGE[p])); });

    return line;
  }

  function progressBar(pot, width) {
    var track = el('span', 'fh-track');
    var fill = el('span', 'fh-fill');

    if (width) track.style.width = width;

    track.appendChild(fill);

    return live(track, pot.id, function (node, p) { fill.style.width = Math.max(0, Math.min(100, p.progress | 0)) + '%'; });
  }

  function potCard(pot) {
    var kind = kindOf(pot.kind);
    var crop = pot.plant ? cropOf(pot.plant) : null;
    var preset = presetById(pot.preset);
    var isChecked = !!state.checks[pot.id];
    var card = act(el('div', 'fh-card' + (pot.id === state.sel ? ' fh-sel' : '') + (pot.rotSoon ? ' fh-hot' : '')), 'pick', pot.id);
    var check = act(el('button', 'fh-chk' + (isChecked ? ' fh-on' : ''), isChecked ? '\u2713' : ''), 'check', pot.id);
    var main = put(el('div', 'fh-cmain'), potBadges(pot, put(el('div', 'fh-nm'), el('span', '', crop ? crop.name : say('stEmpty')), stateBadge(pot))),
      timeLine(pot), el('div', 'fh-sub', kind.name + ' \u00b7 ' + (preset ? preset.name : say('noPreset'))));
    var right = el('div', 'fh-crt');

    if (pot.robot) right.appendChild(badge(say('robot'), 'fh-b-blue'));
    if (pot.pending) right.appendChild(badge(say('queued'), 'fh-b-green'));
    if (!pot.powered) right.appendChild(badge(say('unpowered'), 'fh-b-red'));
    if (pot.chore) right.appendChild(button(say(CHORE_WORD[pot.chore]), 'tend', pot.id, !pot.pending, 'fh-sm' + (pot.chore === 'harvest' ? ' fh-hot' : '')));

    put(card, check, icon(crop || kind, 'fh-i40'), main, right);
    ui.cards[pot.id] = card;

    return card;
  }

  function timeLine(pot) {
    var line = el('div', 'fh-ln');

    if (pot.state === 'growing') return put(line, progressBar(pot), live(el('span'), pot.id, growingText));
    if (pot.state === 'ripe') return put(line, live(el('span'), pot.id, ripeText));
    if (pot.state === 'empty') return put(line, nextPlantLine(pot));

    return line;
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

  function fillDetail() {
    var pot = potById(state.sel);
    var well = ui.detail;

    well.textContent = '';

    if (pot) potDetail(well, pot);
    else needsList(well);
  }

  // With no pot picked: everything waiting on the player, soonest first.
  function needsList(well) {
    var waiting = pots().filter(needsYou).sort(function (a, b) { return compareKeys(urgency(a), urgency(b)); });
    var rows = el('div', 'fh-rows');

    put(well, el('h4', 'fh-sect', say('needsYou')));

    if (!waiting.length) well.appendChild(el('div', 'fh-empty', say('allFine')));

    waiting.slice(0, MAX_NEEDS).forEach(function (pot) {
      var what = pot.rotSoon ? live(el('span'), pot.id, ripeText) : el('span', 'fh-ca', pot.chore ? say(CHORE_WORD[pot.chore]) : problemText(pot));

      rows.appendChild(put(act(el('div', 'fh-row'), 'pick', pot.id), icon(pot.plant ? cropOf(pot.plant) : kindOf(pot.kind), 'fh-i30'),
        put(el('div', 'fh-grow'), el('div', '', potName(pot)), el('div', 'fh-dim', roomTitle(pot))), what));
    });

    put(well, rows, el('div', 'fh-note', say('pickPot')));
  }

  function problemText(pot) {
    return (pot.problems || []).map(function (p) { return say(PROBLEM_WORD[p] || p); }).join(', ');
  }

  function potDetail(well, pot) {
    var kind = kindOf(pot.kind);
    var crop = pot.plant ? cropOf(pot.plant) : null;
    var head = put(el('div', 'fh-dhead'), icon(crop || kind, 'fh-i56'),
      put(el('div', 'fh-dname'), el('div', 'fh-big', crop ? crop.name : say('stEmpty')), el('div', 'fh-dim', kind.name + ' \u00b7 ' + roomTitle(pot))),
      stateBadge(pot));
    var acts = put(el('div', 'fh-bar'),
      pot.chore ? button(say(CHORE_WORD[pot.chore]), 'tend', pot.id, !pot.pending, 'fh-hot') : null,
      button(say('plant'), 'plant', pot.id, pot.canPlant),
      button(say('fertilize'), 'fert', pot.id, (pot.nextFert || 0) > 0));

    put(well, head, acts, timingBox(pot), nextPlantingBox(pot, kind), presetBox(pot), kindBox(kind, pot));
  }

  function kv(grid, key, value) {
    put(grid, el('span', '', key), typeof value === 'string' ? el('b', '', value) : value);
  }

  function timingBox(pot) {
    var grid = el('div', 'fh-kv');
    var stateLine = potBadges(pot, put(el('b', 'fh-bar'), text(say(STATE_WORD[pot.state] || pot.state))));

    kv(grid, say('state'), stateLine);

    if (pot.state === 'growing') {
      kv(grid, say('grown'), put(el('b', 'fh-bar'), progressBar(pot, '160px'), live(el('span'), pot.id, function (n, p) { n.textContent = p.progress + '%'; })));
      kv(grid, say('ripeIn'), live(el('b'), pot.id, function (n, p) { n.textContent = p.ripeIn != null ? hours(p.ripeIn) : '-'; }));
    }

    if (pot.state === 'ripe') kv(grid, say('rotIn'), live(el('b'), pot.id, function (n, p) { n.textContent = p.rotIn != null ? hours(p.rotIn) : say('noRot'); n.className = p.rotSoon ? 'fh-red' : ''; }));
    if (pot.state !== 'empty') kv(grid, say('fertWord'), pot.fert ? fertOf(pot.fert).name : say('noFert'));
    if ((pot.nextFert || 0) > 0) kv(grid, say('fertNext'), fertOf(pot.nextFert).name);
    if (pot.pending) kv(grid, '', say('queued'));

    return put(el('div', 'fh-box'), el('h4', '', say('timing')), grid);
  }

  // The pot's preset order for its size, with what the player owns and the seed the next planting will use.
  function nextPlantingBox(pot, kind) {
    var preset = presetById(pot.preset);
    var steps = preset ? (preset.seeds || {})[kind.cap] || [] : [];
    var box = put(el('div', 'fh-box'), el('h4', '', say('nextPlanting')));
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
        isNext ? badge(say('nextBadge'), 'fh-b-green') : null, el('span', isOut ? 'fh-red' : 'fh-dim', fits ? say('nSeeds', owned(step.item)) : say('tooBig'))));
    });
    box.appendChild(el('div', 'fh-dim', say('plantNote')));

    return box;
  }

  // The pot's preset: its room's or floor's, or its own, with where it comes from.
  function presetBox(pot) {
    var preset = presetById(pot.preset);
    var source = put(el('div', 'fh-ln'), el('b', '', preset ? preset.name : say('none')), preset ? el('span', 'fh-dim', say(FROM_WORD[pot.from] || 'fromAll')) : null);
    var pick = el('div', 'fh-chips');
    var isOwn = pot.from === 'pot';

    pick.appendChild(chip(say('useDefault'), 'assign', '|' + pot.id, !isOwn));
    presets().forEach(function (p) { pick.appendChild(chip(p.name, 'assign', p.id + '|' + pot.id, isOwn && p.id === pot.preset)); });

    if (!state.data.saved) pick.querySelectorAll('[data-act]').forEach(function (node) { node.disabled = true; });

    return put(el('div', 'fh-box'), el('h4', '', say('preset')), source, pick, state.data.saved ? null : el('div', 'fh-dim', say('noSaveYet')));
  }

  function kindBox(kind, pot) {
    var grid = el('div', 'fh-kv');
    var bonus = [];

    if (kind.light) bonus.push(say('light', kind.light));
    if (kind.heat) bonus.push(say('heat', kind.heat));
    if (kind.faster) bonus.push(say('faster', Math.round(kind.faster * 100)));

    kv(grid, say('pot'), kind.name);
    kv(grid, say('size'), sizeName(kind.cap));
    kv(grid, say('bonus'), bonus.length ? bonus.join(', ') : say('none'));

    if (kind.power) kv(grid, say('power'), pot.powered ? say('ok') : say('unpowered'));
    if (pot.robot) kv(grid, say('robot'), say('ok'));

    return put(el('div', 'fh-box'), el('h4', '', say('pot')), grid);
  }

  // Picking and checking only touch the cards involved and the panes beside them; the list is not rebuilt.
  function pickPot(id) {
    state.sel = state.sel === id ? '' : id;

    if (!ui.cards || !ui.detail) {
      render();

      return;
    }

    Object.keys(ui.cards).forEach(function (key) { setClass(ui.cards[key], 'fh-sel', key === state.sel); });
    fillDetail();
  }

  function checkPot(id) {
    state.checks[id] = !state.checks[id];

    var card = ui.cards && ui.cards[id];

    if (!card) {
      render();

      return;
    }

    var check = card.querySelector('.fh-chk');

    setClass(check, 'fh-on', state.checks[id]);
    check.textContent = state.checks[id] ? '\u2713' : '';
    fillActs();
  }

  function clearChecks() {
    state.checks = {};
    render();
  }

  // ---------- Planting presets tab ----------

  function renderPresets() {
    syncDraft();

    var side = el('div', 'fh-plist');
    var list = scroller('plist');
    var editor = scroller('editor', 'fh-editor');
    var plan = scroller('plan', 'fh-plan');

    list.appendChild(section(say('presetsHead')));
    presets().forEach(function (p) { list.appendChild(presetItem(p)); });
    put(side, list, button('+ ' + say('newPreset'), 'newpreset', null, true));

    if (state.draft) {
      fillEditor(editor);
      fillPlan(plan);
    }

    put(ui.body, side, editor, plan);
  }

  function presetItem(p) {
    var icons = el('div', 'fh-icons');
    var seen = [];

    Object.keys(p.seeds || {}).forEach(function (cap) {
      p.seeds[cap].forEach(function (s) {
        var crop = cropOfSeed(s.item);

        if (seen.length < 4 && seen.indexOf(crop.id) < 0) {
          seen.push(crop.id);
          icons.appendChild(icon(crop, 'fh-i18'));
        }
      });
    });

    return put(act(el('div', 'fh-pitem' + (p.id === state.presetId ? ' fh-sel' : '')), 'preset', p.id),
      put(el('div', 'fh-lbl'), el('b', '', p.name), el('small', '', (p.uses || []).length ? p.uses.map(useName).join(', ') : say('usedNowhere'))), icons);
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
    var name = el('input', 'fh-inp');
    var isArmed = state.armed === 'deletepreset|' + d.id;

    name.setAttribute('data-field', 'name');
    name.setAttribute('placeholder', say('name'));
    name.value = d.name;
    name.style.width = '300px';

    put(editor,
      put(el('div', 'fh-bar'), name, button(say('save'), 'savepreset', null, true, 'fh-hot'),
        d.id ? button(isArmed ? say('confirm') : say('delete'), 'deletepreset', d.id, true, 'fh-danger' + (isArmed ? ' fh-armed' : '')) : null,
        el('span', 'fh-grow'), el('span', 'fh-badge fh-b-amber fh-dirty', say('unsaved'))));
    markDirty(editor);
    seedSection(editor);
    fertSection(editor);
    cropSection(editor);
    put(editor, section(say('usedFor'), el('span', 'fh-r', say('usedNote'))), useChips(d),
      setRow(say('skipUnsuitable'), say('skipNote'), switchButton(d.skipUnsuitable, 'skip')),
      setRow(say('fertilizeOn'), say('fertilizeNote'), switchButton(d.fertilize, 'fertilizeon')),
      setRow(say('autoReplant'), say('autoNote'), switchButton(d.autoReplant, 'autoreplant')));
  }

  // One column per pot size, each with its own seed order and its own add button.
  function seedSection(editor) {
    var cols = el('div', 'fh-capcols');

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
    var col = put(el('div', 'fh-capcol'), put(el('div', 'fh-caphead'), el('span', 'fh-grow', sizeName(cap)), el('span', 'fh-dim', say('keep'))));

    steps.forEach(function (step, i) { col.appendChild(seedStep(step, i, cap, steps.length, i === nextAt)); });

    var isAdding = state.addCap === cap;

    col.appendChild(act(el('button', 'fh-dash' + (isAdding ? ' fh-on' : ''), say('addSeed')), 'addopen', cap));

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
    var row = el('div', 'fh-step' + (!each ? ' fh-bad' : !plantings(step, cap) ? ' fh-short' : isNext ? ' fh-go' : ''));
    var detail = each ? say('nSeeds', have) + ' \u00b7 ' + hours(crop.grow) : say('tooBig');
    var args = cap + '|' + i;
    var info = put(el('div', 'fh-sinfo'), el('b', '', crop.name),
      el('small', !each || have - step.keep < each ? 'fh-red' : isNext ? 'fh-green' : '', isNext ? say('nextBadge') + ' \u00b7 ' + detail : detail));
    var stepper = put(el('div', 'fh-stepper'), plainButton('fh-stp', '-', 'keepless', args, step.keep > 0), el('b', '', String(step.keep)),
      plainButton('fh-stp', '+', 'keepmore', args, step.keep < MAX_KEEP));
    var moves = put(el('div', 'fh-moves'), plainButton('fh-ib', '\u2191', 'seedup', args, i > 0), plainButton('fh-ib', '\u2193', 'seeddown', args, i < count - 1),
      plainButton('fh-ib fh-del', '\u00d7', 'seeddel', args, true));

    put(row, el('span', 'fh-ord', String(i + 1)), icon(seed, 'fh-i30'), info, put(el('div', 'fh-sctl'), moves, stepper));

    return row;
  }

  function picker(field, value, listId) {
    var box = el('div', 'fh-picker');
    var search = el('input', 'fh-inp fh-small');
    var list = el('div', 'fh-col');

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
    table.appendChild(act(el('button', 'fh-dash' + (state.isCropPicking ? ' fh-on' : ''), say('addCrop')), 'cropopen'));

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

    if (!entry.order.length && entry.whenOut === 'none') order.appendChild(badge(say('never'), 'fh-b-amber'));

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
      var verdict = !count ? null : left >= 2 * count ? badge(say('ok'), 'fh-b-green') : left ? badge(say('low'), 'fh-b-amber') : badge(say('out'), 'fh-b-red');

      steps.forEach(function (s) {
        if (perPot(s.item, cap) && !plantings(s, cap) && outs.indexOf(s.item) < 0) outs.push(s.item);
      });

      if (!steps.length && !count) return;

      plan.appendChild(put(el('div', 'fh-prow2'), el('b', '', sizeName(cap)), el('span', 'fh-dim', say('nPots', count)),
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
    var well = scroller('robots', 'fh-robots');
    var robots = state.data.robots || [];
    var isOn = !!(state.data.settings && state.data.settings.robots);

    well.appendChild(setRow(say('keepStocked'), say('keepNote'), switchButton(isOn, 'robots', isOn ? '0' : '1')));

    if (!robots.length) well.appendChild(el('div', 'fh-empty', say('noRobots')));

    var grid = el('div', 'fh-rgrid');

    robots.forEach(function (robot) { grid.appendChild(robotCard(robot)); });
    well.appendChild(grid);
    ui.body.appendChild(well);
  }

  function robotCard(robot) {
    var box = el('div', 'fh-robot');
    var battery = Math.max(0, Math.min(100, robot.battery | 0));
    var track = el('span', 'fh-track');
    var head = put(el('div', 'fh-rhead'), icon({ name: 'R' }, 'fh-i56'),
      put(el('div', 'fh-dname'), el('div', 'fh-big', robot.name), put(el('div', 'fh-bar fh-dim'), text(say('floor', robot.floor)), track, text(say('battery', battery)))),
      robot.on ? badge(say('working'), 'fh-b-green') : badge(say('robotOff'), 'fh-b-red'));

    track.appendChild(el('span', 'fh-fill' + (battery < 20 ? ' fh-red' : battery < 50 ? ' fh-amber' : ' fh-blue')));
    track.firstChild.style.width = battery + '%';
    put(box, head, put(el('div', 'fh-tier fh-thead'), el('span', '', say('colSize')), el('span', '', say('colSeed')), el('span', '', say('fertWord')), el('span')));
    robot.tiers.forEach(function (tier, i) {
      var isEditing = state.tierEdit && state.tierEdit.robot === robot.id && state.tierEdit.tier === i;

      box.appendChild(tierRow(robot, tier, i));

      if (isEditing) box.appendChild(tierEditor(tier));
    });

    return box;
  }

  function tierRow(robot, tier, i) {
    var seed = tier.seed ? seedOf(tier.seed) : null;
    var fert = tier.fert ? fertOf(tier.fert) : null;
    var isShort = seed && owned(tier.seed) < perPot(tier.seed, tier.cap);

    return put(el('div', 'fh-tier'), el('b', '', sizeName(tier.cap)),
      put(el('span', 'fh-tcell'), seed ? icon(seed, 'fh-i22') : null, el('span', seed ? '' : 'fh-dim', seed ? cropOfSeed(tier.seed).name : say('noSeed')),
        seed ? el('span', isShort ? 'fh-red' : 'fh-dim', say('nSeeds', owned(tier.seed))) : null),
      put(el('span', 'fh-tcell'), fert ? icon(fert, 'fh-i22') : null, el('span', fert ? '' : 'fh-dim', fert ? fert.name : say('noFert'))),
      button(say('change'), 'tieredit', robot.id + '|' + i, true, 'fh-sm'));
  }

  // The seeds that fit the pot size, owned first, and the fertilizer, both chosen here and sent with one press.
  function tierEditor(tier) {
    var edit = state.tierEdit;
    var seeds = el('div', 'fh-chips');
    var ferts = el('div', 'fh-chips');
    var fits = cat.seedList.filter(function (s) { return perPot(s.id, tier.cap) > 0 && (owned(s.id) > 0 || s.id === edit.seed); })
      .sort(function (a, b) { return owned(b.id) - owned(a.id) || byName(a, b); });

    seeds.appendChild(chip(say('noSeed'), 'tierseed', 0, edit.seed === 0));
    fits.slice(0, MAX_ADD).forEach(function (s) { seeds.appendChild(chip(cropOfSeed(s.id).name, 'tierseed', s.id, edit.seed === s.id, owned(s.id), s)); });
    ferts.appendChild(chip(say('noFert'), 'tierfert', 0, edit.fert === 0));
    cat.fertList.forEach(function (f) { ferts.appendChild(chip(f.name, 'tierfert', f.id, edit.fert === f.id, owned(f.id), f)); });

    return put(el('div', 'fh-tedit'), el('span', 'fh-dim', say('seed')), seeds, el('span', 'fh-dim', say('fertWord')), ferts,
      put(el('div', 'fh-bar'), button(say('save'), 'settier', null, true, 'fh-hot'), button(say('cancel'), 'tierdone', null, true)));
  }

  function editTier(args) {
    var parts = String(args).split('|');
    var robot = find(state.data.robots || [], function (r) { return r.id === parts[0]; });
    var tier = robot && robot.tiers[+parts[1]];

    if (!tier) return;

    state.tierEdit = { robot: robot.id, tier: +parts[1], seed: tier.seed, fert: tier.fert };
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
    sendAction('robottier|' + edit.robot + '|' + edit.tier + '|' + edit.seed + '|' + edit.fert);
  }

  // ---------- Forecast tab ----------

  function renderForecast() {
    var stock = scroller('stock', 'fh-stock');
    var time = scroller('coming', 'fh-time');

    fillStock(stock);
    fillComing(time);
    put(ui.body, stock, time);
  }

  // What ripens or rots in the next two days on a timeline, each in the first lane it fits in.
  function fillComing(well) {
    var events = [];
    var axis = el('div', 'fh-axis');
    var lanes = el('div', 'fh-lanes');
    var laneEnds = [];

    pots().forEach(function (pot) {
      if (pot.ripeIn != null && pot.ripeIn <= FORECAST_HOURS) events.push({ pot: pot, at: pot.ripeIn, isRot: false });
      if (pot.rotIn != null && pot.rotIn <= FORECAST_HOURS) events.push({ pot: pot, at: pot.rotIn, isRot: true });
    });
    events.sort(function (a, b) { return a.at - b.at; });

    for (var h = 0; h < FORECAST_HOURS; h += FORECAST_STEP) {
      var tick = el('span', 'fh-tick', h ? '+' + say('hours', h) : say('now'));
      var line = el('span', 'fh-gline' + (h ? '' : ' fh-now'));

      tick.style.left = at(h) + '%';
      line.style.left = at(h) + '%';
      axis.appendChild(tick);
      lanes.appendChild(line);
    }

    events.forEach(function (e) {
      var crop = cropOf(e.pot.plant);
      var x = at(e.at);
      var lane = 0;

      while (laneEnds[lane] != null && laneEnds[lane] > x) lane++;

      laneEnds[lane] = x + CHIP_SHARE;

      var chipNode = put(act(el('div', 'fh-ev' + (e.isRot ? ' fh-rot' : '')), 'pickat', e.pot.id), icon(crop, 'fh-i18'),
        el('span', '', e.isRot ? say('rots', crop.name) : crop.name), el('span', 'fh-dim', hours(e.at)));

      chipNode.style.left = Math.min(x, 100 - CHIP_SHARE) + '%';
      chipNode.style.top = lane * 34 + 'px';
      lanes.appendChild(chipNode);
    });

    lanes.style.height = Math.max(1, laneEnds.length) * 34 + 10 + 'px';
    put(well, section(say('comingUp')), axis, lanes);

    if (!events.length) well.appendChild(el('div', 'fh-empty', say('nothingSoon')));
  }

  function at(h) {
    return Math.round(h / FORECAST_HOURS * 1000) / 10;
  }

  // Each seed the presets plant, with how many pots of each size what the player owns would fill, then the
  // fertilizers the presets use.
  function fillStock(well) {
    var rows = {};
    var ferts = [];
    var columns = 'minmax(0,1fr) 60px' + cat.caps.map(function () { return ' 72px'; }).join('');
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
    cat.caps.forEach(function (cap) { head.appendChild(el('span', 'fh-tc', sizeName(cap))); });
    put(well, section(say('stockHead')), head);

    Object.keys(rows).map(function (k) { return rows[k]; }).sort(function (a, b) { return owned(a.item) - owned(b.item); }).forEach(function (row) {
      var line = put(el('div', 'fh-trow'), put(el('span', 'fh-cname'), icon(seedOf(row.item), 'fh-i22'), text(cropOfSeed(row.item).name)),
        el('span', 'fh-tc' + (owned(row.item) ? '' : ' fh-red'), String(owned(row.item))));

      line.style.gridTemplateColumns = columns;
      cat.caps.forEach(function (cap) {
        var fills = row.caps[cap];

        line.appendChild(el('span', 'fh-tc' + (fills === 0 ? ' fh-red' : fills == null ? ' fh-dim' : ''), fills == null ? '-' : String(fills)));
      });
      well.appendChild(line);
    });

    ferts.forEach(function (id) {
      var line = put(el('div', 'fh-trow'), put(el('span', 'fh-cname'), icon(fertOf(id), 'fh-i22'), text(fertOf(id).name)),
        el('span', 'fh-tc' + (owned(id) ? '' : ' fh-red'), String(owned(id))));

      line.style.gridTemplateColumns = columns;
      well.appendChild(line);
    });
    well.appendChild(el('div', 'fh-note', say('stockNote')));
  }

  // ---------- Settings tab ----------

  function renderSettings() {
    var well = scroller('settings', 'fh-robots');
    var settings = state.data.settings || {};

    put(well,
      setRow(say('rotWarn'), say('rotWarnNote'), segment(ROT_WARNS.map(function (h) { return [String(h), say('hours', h)]; }), String(settings.rotWarn), 'rotwarn')),
      setRow(say('hotkey'), state.key ? say('keyOpen', state.key) : say('keyNone'), state.key ? el('span', 'fh-kbd', state.key) : null),
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

  // A forecast chip opens its pot on the Pots tab.
  LOCAL.pickat = function (id) {
    state.tab = 'pots';
    state.sel = id;
    render();
  };

  function open(data) {
    build();
    state.data = data || state.data;
    state.isOpen = true;
    state.isTyping = false;
    shownShape = shapeOf(state.data);
    ui.status.textContent = '';
    ui.toast.style.display = 'none';
    ui.root.style.display = 'flex';
    render();
  }

  // Called about once a second while the window is open. Most calls only move the clocks.
  function setData(data) {
    state.data = data || state.data;

    var shape = shapeOf(state.data);

    if (ui && shape === shownShape && state.tab !== 'forecast') {
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

    if (state.isOpen) {
      ui.status.textContent = message;

      return;
    }

    // An empty status only clears the line; as a toast it would be an empty box.
    if (!message) {
      ui.toast.style.display = 'none';

      return;
    }

    ui.toast.textContent = message;
    ui.toast.style.display = 'block';
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { ui.toast.style.display = 'none'; }, TOAST_MS);
  }

  // Whether a game with a home is loaded, and the key that opens the window ('' when none): the HUD button is only
  // shown in such a game, and both it and the window name the key.
  function setLive(isLive, key) {
    state.isLive = !!isLive;
    state.key = key || '';
    hudVersion++;

    if (ui && state.isOpen) renderKeys();

    patchPagesSafely();
  }

  // The number on the HUD button and the Pots tab: pots with a chore waiting and empty pots a press of Plant would fill.
  function setBadge(count) {
    state.badge = Math.max(0, count | 0);
    hudVersion++;

    if (ui && state.isOpen) renderTabs();

    patchPagesSafely();
  }

  // ---------- HUD button ----------

  var HUD_CSS = [
    '.fh-hud-btn{position:relative}',
    '.fh-hud-btn .fh-hud-badge{position:absolute;top:-6px;right:-6px;min-width:18px;height:18px;padding:0 5px;border-radius:9px;background:#e53935;color:#fff;font-size:11px;font-weight:700;line-height:18px;text-align:center;pointer-events:none;box-shadow:0 1px 4px rgba(0,0,0,.6)}'
  ].join('\n');

  // The HUD is a same-origin iframe, so its document can be reached from here; polled because the game creates and
  // drops that iframe whenever it likes.
  function patchPages() {
    var frames = document.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) {
      var src = frames[i].src || '';

      if (!/CoreUI1\.html/.test(src)) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc) {
        if (!hasReportedPageAccess) send('diag|cannot reach the HUD document (root ' + location.protocol + ', iframe ' + src + ')');
        hasReportedPageAccess = true;
        continue;
      }

      placeHudButton(doc);
    }
  }

  function patchPagesSafely() {
    try {
      patchPages();
    } catch (e) {}
  }

  // First in the tool column: the column is anchored at the bottom, so the first child sits on top and the game's own
  // buttons do not move. The column hides itself in build mode, and the button with it.
  function placeHudButton(doc) {
    var column = doc.querySelector('.vertical-tools');
    var existing = doc.querySelector('.' + HUD_CLASS);

    if (!column || !state.isLive) {
      if (existing) existing.remove();

      return;
    }

    if (existing && existing.parentNode === column && existing.getAttribute('data-version') === String(hudVersion)) return;
    if (existing) existing.remove();

    addHudStyle(doc);
    var node = hudButton(doc);

    node.setAttribute('data-etk-dock', DOCK_ORDER);
    column.insertBefore(node, column.firstChild);

    if (!hasReportedHudPlaced) send('diag|HUD button placed in the tool column');
    hasReportedHudPlaced = true;
  }

  function addHudStyle(doc) {
    if (doc.getElementById(HUD_STYLE_ID)) return;

    var style = doc.createElement('style');
    style.id = HUD_STYLE_ID;
    style.textContent = HUD_CSS;
    (doc.head || doc.body).appendChild(style);
  }

  // The same markup as the game's bag button: its classes, an svg icon, and the hover tip its stylesheet shows. The
  // HUD page watches its body for new buttons and tells the game where they are, so a click on this one reaches it.
  function hudButton(doc) {
    var node = doc.createElement('button');
    node.className = 'ui-btn icon-sq interactive-btn has-entry-tip ' + HUD_CLASS;
    node.setAttribute('data-interactive', '');
    node.setAttribute('data-version', String(hudVersion));
    node.appendChild(sprout(doc));

    if (state.badge > 0) {
      var count = doc.createElement('span');
      count.className = 'fh-hud-badge';
      count.textContent = state.badge > 99 ? '99+' : String(state.badge);
      node.appendChild(count);
    }

    node.appendChild(entryTip(doc));
    node.addEventListener('click', function (e) {
      e.stopPropagation();
      send('toggle');
    });

    return node;
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

  function entryTip(doc) {
    var tip = doc.createElement('div');
    var title = doc.createElement('div');
    var body = doc.createElement('div');

    tip.className = 'entry-tip';
    title.className = 'entry-tip-title';
    title.textContent = 'Farm Hub';
    body.className = 'entry-tip-body';
    body.textContent = say('tip');

    if (state.key) {
      var key = doc.createElement('span');
      key.className = 'entry-tip-key';
      key.textContent = state.key;
      title.appendChild(key);
    }

    tip.appendChild(title);
    tip.appendChild(body);

    return tip;
  }

  setInterval(patchPagesSafely, 400);

  window.__slfarmhub = { open: open, setData: setData, close: close, status: status, live: setLive, catalog: catalog, badge: setBadge };
  send('ready');
})();
