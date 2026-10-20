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
  var FORECAST_HOURS = 48;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A sprout: a stem with two leaves over a pot.
  var SPROUT_PATH = 'M11 21v-7.2C8.4 13.6 6 11.4 6 8c2.9 0 5 1.6 5 4.4V12h2v.4C13 9.6 15.1 8 18 8c0 3.4-2.4 5.6-5 5.8V21h-2zM6 18h12l-1 4H7l-1-4z';
  var TABS = ['pots', 'presets', 'robots', 'forecast', 'settings'];
  var TAB_WORD = { pots: 'tabPots', presets: 'tabPresets', robots: 'tabRobots', forecast: 'tabForecast', settings: 'tabSettings' };
  var STATE_WORD = { empty: 'stEmpty', growing: 'stGrowing', ripe: 'stRipe', withered: 'stWithered', poor: 'stPoor' };
  var PROBLEM_WORD = { pests: 'pPests', weeds: 'pWeeds', dry: 'pDry', dark: 'pDark', bright: 'pBright', cold: 'pCold', feed: 'pFeed' };
  var CHORE_WORD = { harvest: 'tHarvest', clear: 'tClear', uproot: 'tUproot', pests: 'tPests', weeds: 'tWeeds', water: 'tWater' };
  var FROM_WORD = { pot: 'fromPot', room: 'fromRoom', floor: 'fromFloor', all: 'fromAll' };
  var FILTERS = [['all', 'fAll'], ['ripe', 'fRipe'], ['care', 'fCare'], ['empty', 'fEmpty']];
  var ROT_WARNS = [6, 12, 24, 48];

  var LOCAL = {
    tab: pickTab, pick: pickPot, check: checkPot, clearchecks: clearChecks, filter: function (v) { state.filter = v; render(); },
    preset: pickPreset, newpreset: newPreset, cap: function (v) { state.cap = +v; render(); },
    seedup: function (i) { moveIn(seedSteps(), +i, -1); }, seeddown: function (i) { moveIn(seedSteps(), +i, 1); },
    seeddel: function (i) { editDraft(function () { seedSteps().splice(+i, 1); }); },
    keepless: function (i) { changeKeep(+i, -1); }, keepmore: function (i) { changeKeep(+i, 1); }, seedadd: addSeed,
    fertup: function (i) { moveIn(state.draft.ferts, +i, -1); }, fertdown: function (i) { moveIn(state.draft.ferts, +i, 1); },
    fertdel: function (i) { editDraft(function (d) { d.ferts.splice(+i, 1); }); }, fertadd: addFert,
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
    fAll: 'All',
    fRipe: 'Ripe',
    fCare: 'Needs care',
    fEmpty: 'Empty',
    floor: 'Floor {0}',
    here: 'you are here',
    otherRoom: 'Elsewhere',
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
    ripeIn: 'Ripe in {0}',
    rotIn: 'Rots in {0}',
    rotSoon: 'Rotting soon',
    noRot: 'Does not rot',
    hours: '{0} h',
    fert: 'Fertilizer: {0}',
    noFert: 'No fertilizer',
    robotHas: 'Robot\'s',
    queued: 'Walk queued',
    unpowered: 'No power',
    nextPlant: 'Plant uses: {0} x{1}',
    nextFert: 'Fertilize uses: {0}',
    nextNone: 'No seed left for its preset',
    plant: 'Plant',
    fertilize: 'Fertilize',
    preset: 'Planting preset',
    fromPot: 'This pot\'s own choice',
    fromRoom: 'From its room',
    fromFloor: 'From its floor',
    fromAll: 'From the preset used everywhere',
    useDefault: 'Room or floor preset',
    none: 'None',
    empty: 'No pots placed at home yet.',
    pickPot: 'Pick a pot to see its timing, problems and next planting.',
    pot: 'Pot',
    slots: '{0} slots',
    light: 'Light +{0}',
    heat: 'Heat +{0}',
    power: 'Needs power',
    faster: 'Grows {0}% faster',
    timing: 'Timing',
    grown: 'Grown {0}%',
    selected: '{0} selected',
    clear: 'Clear',
    assignTo: 'Planting preset for the selected pots:',
    noSaveYet: 'Save the game once to pick presets per pot.',
    confirm: 'Sure?',
    newPreset: 'New preset',
    name: 'Name',
    save: 'Save',
    delete: 'Delete',
    unsaved: 'Unsaved changes',
    seedsFor: 'Seeds for {0}',
    seedNote: 'The first seed that fits the pot and that you have enough of is used.',
    noSteps: 'No seeds yet. Add some below.',
    keep: 'Keep at least',
    own: 'Own {0}',
    perPot: '{0} per pot',
    tooBig: 'Too big for this pot',
    addSeed: 'Add a seed',
    add: 'Add',
    search: 'Search',
    remove: 'Remove',
    more: '{0} more. Search to narrow the list.',
    ferts: 'Fertilizer order',
    fertNote: 'A crop without its own list below gets the first of these you have.',
    noFerts: 'No fertilizer in the order, so these crops are planted without any.',
    addFert: 'Add a fertilizer',
    perCrop: 'Fertilizer per crop',
    perCropNote: 'A crop listed here uses its own order first. Grow times are for a full pot with no other bonus.',
    whenOut: 'When none are left',
    woPreset: 'Use the preset order',
    woNone: 'No fertilizer',
    never: 'Never fertilized',
    addCrop: 'Add a crop',
    baseTime: 'No fertilizer: {0}',
    skipUnsuitable: 'Skip crops the pot\'s light or heat cannot grow',
    fertilizeOn: 'Fertilize when planting',
    autoReplant: 'Replant empty pots by themselves',
    autoNote: 'Plants an empty pot from this preset on its own, as if you pressed Plant.',
    usedFor: 'Used for',
    useAll: 'Everywhere',
    usedNote: 'A pot uses its own choice, else its room\'s, else its floor\'s, else the one used everywhere.',
    on: 'On',
    off: 'Off',
    robotsNote: 'Each planter robot sows three pot sizes, with one seed and one fertilizer for each.',
    noRobots: 'No planter robots at home.',
    battery: 'Battery {0}%',
    robotOff: 'Switched off',
    tierCap: '{0}-slot pots',
    noSeed: 'No seed',
    change: 'Change',
    done: 'Done',
    seed: 'Seed',
    fertWord: 'Fertilizer',
    keepStocked: 'Keep robots stocked',
    keepNote: 'When a robot runs out of the seed for a pot size, switch it to the next seed in its floor\'s preset.',
    comingUp: 'Next 48 hours',
    rots: 'Rots',
    nothingSoon: 'Nothing ripens or rots in the next 48 hours.',
    stock: 'Seeds in your presets',
    fills: 'Enough for {0} pot(s) of {1} slots',
    rotWarn: 'Rotting soon means within',
    keyOpen: '{0} opens and closes this window.',
    keyNone: 'No key is set; use the plant button on the left of the screen.',
    presetFile: 'Presets are kept in BepInEx/config/farm-hub.json, shared by every save.',
    escClose: 'Esc to close',
    tip: 'Your pots: ripe crops, problems and planting'
  };

  var state = {
    data: { ready: false, note: '' }, isOpen: false, isLive: false, key: '', badge: 0, tab: 'pots', sel: '', checks: {},
    filter: 'all', presetId: '', draft: null, draftOf: '', pendingNewFrom: null, cap: 0, addSearch: '', cropSearch: '',
    tierEdit: null, armed: '', isTyping: false, isStale: false
  };
  var cat = { kinds: {}, crops: {}, seeds: {}, ferts: {}, seedList: [], fertList: [], cropList: [], caps: [] };
  var ui = null;
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

  function el(tag, className, text) {
    var node = document.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function put(parent) {
    for (var i = 1; i < arguments.length; i++) {
      if (arguments[i]) parent.appendChild(arguments[i]);
    }

    return parent;
  }

  function button(text, action, args, isEnabled, extraClass) {
    var node = el('button', 'fh-btn' + (extraClass ? ' ' + extraClass : ''), text);
    node.setAttribute('data-act', action);

    if (args != null) node.setAttribute('data-args', String(args));
    if (isEnabled === false) node.disabled = true;

    return node;
  }

  function toggle(isOn, action) {
    return button(say(isOn ? 'on' : 'off'), action, null, true, 'fh-tog' + (isOn ? ' fh-on' : ''));
  }

  var CSS = [
    '#fh-root{position:fixed;inset:0;z-index:2147483000;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.4);font-family:"Microsoft YaHei","PingFang SC",sans-serif;color:#eee;user-select:none}',
    '#fh-root *{box-sizing:border-box}',
    '.fh-plate{width:min(1480px,95vw);height:min(880px,90vh);display:flex;flex-direction:column;gap:10px;padding:16px 20px;border-radius:10px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(30,30,33,.93),rgba(18,18,20,.95));box-shadow:0 10px 40px rgba(0,0,0,.6)}',
    '.fh-head{display:flex;align-items:center;gap:14px}',
    '.fh-title{font-size:20px;font-weight:600;letter-spacing:1px;text-shadow:0 2px 6px rgba(0,0,0,.6)}',
    '.fh-sub{flex:1;display:flex;gap:8px;flex-wrap:wrap}',
    '.fh-pill{font-size:12px;padding:3px 9px;border-radius:10px;background:rgba(255,255,255,.08);color:rgba(238,238,238,.8)}',
    '.fh-pill.fh-g{background:rgba(124,179,66,.22);color:#aed581}.fh-pill.fh-r{background:rgba(229,57,53,.24);color:#ff8a80}.fh-pill.fh-a{background:rgba(235,163,8,.22);color:#EBA308}.fh-pill.fh-b{background:rgba(100,181,246,.2);color:#90caf9}',
    '.fh-tabs{display:flex;gap:4px;border-bottom:1px solid rgba(255,255,255,.12)}',
    '.fh-tab{font:inherit;font-size:14px;color:rgba(238,238,238,.65);padding:8px 16px;border:none;border-bottom:2px solid transparent;background:none;cursor:pointer}',
    '.fh-tab:hover{color:#eee}.fh-tab.fh-on{color:#EBA308;border-bottom-color:#EBA308}',
    '.fh-body{flex:1;min-height:0;display:flex;gap:10px}',
    '.fh-well{min-height:0;overflow-y:auto;background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);border-radius:8px;padding:8px}',
    '.fh-col{display:flex;flex-direction:column;gap:8px;min-height:0;min-width:0}',
    '.fh-bar{display:flex;align-items:center;gap:8px;flex-wrap:wrap}',
    '.fh-bulk{display:flex;align-items:center;gap:8px;flex-wrap:wrap;padding:8px 10px;border-radius:8px;border:1px solid rgba(235,163,8,.5);background:rgba(235,163,8,.07);font-size:13px}',
    '.fh-sect{font-size:12px;color:#EBA308;letter-spacing:1.5px;text-transform:uppercase;padding:8px 4px 4px;display:flex;gap:8px;align-items:center}',
    '.fh-cards{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:6px}',
    '.fh-card{position:relative;display:grid;grid-template-columns:44px minmax(0,1fr) auto;gap:10px;align-items:center;padding:8px 10px;border-radius:7px;background:rgba(255,255,255,.04);border:1px solid transparent;cursor:pointer}',
    '.fh-card:hover{background:rgba(255,255,255,.07)}.fh-card.fh-sel{border-color:rgba(235,163,8,.75);background:rgba(235,163,8,.08)}.fh-card.fh-hot{box-shadow:inset 3px 0 0 #e53935}',
    '.fh-card .fh-chk{position:absolute;left:-1px;top:-1px;width:18px;height:18px;padding:0;font-size:11px;line-height:16px;border-radius:7px 0 5px 0}',
    '.fh-card .fh-chk.fh-on{background:#EBA308;color:#111;border-color:#EBA308}',
    '.fh-nm{font-size:14px;font-weight:600;display:flex;gap:6px;align-items:center;white-space:nowrap;overflow:hidden}',
    '.fh-ln{font-size:12px;color:rgba(238,238,238,.7);display:flex;gap:6px;align-items:center;margin-top:4px;white-space:nowrap;overflow:hidden}',
    '.fh-rt{display:flex;flex-direction:column;align-items:flex-end;gap:4px;font-size:12px;color:rgba(238,238,238,.55)}',
    '.fh-badge{flex:none;font-size:11px;padding:2px 7px;border-radius:5px;background:rgba(255,255,255,.1);color:rgba(238,238,238,.8)}',
    '.fh-b-ripe{background:rgba(124,179,66,.24);color:#aed581}.fh-b-withered,.fh-b-poor,.fh-b-bad{background:rgba(229,57,53,.26);color:#ff8a80}.fh-b-empty{background:rgba(255,255,255,.06);color:rgba(238,238,238,.5)}.fh-b-warn{background:rgba(235,163,8,.22);color:#EBA308}.fh-b-info{background:rgba(100,181,246,.2);color:#90caf9}',
    '.fh-track{width:100px;height:6px;border-radius:3px;background:rgba(255,255,255,.1);overflow:hidden;flex:none}',
    '.fh-fill{display:block;height:100%;border-radius:3px;background:#7cb342}',
    '.fh-detail{width:440px;flex:none;display:flex;flex-direction:column;gap:10px}',
    '.fh-dhead{display:flex;gap:12px;align-items:center}.fh-dhead .fh-big{font-size:18px;font-weight:700}.fh-dim{color:rgba(238,238,238,.55);font-size:12px}',
    '.fh-box{border:1px solid rgba(255,255,255,.1);border-radius:8px;padding:10px 12px;background:rgba(255,255,255,.025);display:flex;flex-direction:column;gap:6px}',
    '.fh-box h4{margin:0;font-size:12px;color:#EBA308;letter-spacing:1.5px;text-transform:uppercase;font-weight:600}',
    '.fh-row{display:flex;align-items:center;gap:8px;font-size:13px;padding:3px 4px;border-radius:6px}',
    '.fh-row .fh-grow{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}',
    '.fh-row.fh-out{opacity:.45}',
    '.fh-num{font-weight:600;color:#EBA308;font-variant-numeric:tabular-nums}',
    '.fh-red{color:#ff8a80}.fh-green{color:#aed581}',
    '.fh-plist{width:280px;flex:none}',
    '.fh-pitem{display:flex;flex-direction:column;gap:2px;padding:9px 10px;margin-bottom:4px;border-radius:7px;background:rgba(255,255,255,.04);border:1px solid transparent;cursor:pointer}',
    '.fh-pitem.fh-sel{border-color:rgba(235,163,8,.75);background:rgba(235,163,8,.08)}.fh-pitem b{font-size:14px}',
    '.fh-editor{flex:1}',
    '.fh-step{display:grid;grid-template-columns:24px 32px minmax(0,1fr) 90px 150px 150px;gap:8px;align-items:center;padding:6px 8px;border-radius:7px;background:rgba(255,255,255,.04);font-size:13px;margin-bottom:4px}',
    '.fh-step.fh-bad{background:rgba(211,47,47,.08)}.fh-step.fh-short{box-shadow:inset 3px 0 0 #D32F2F}',
    '.fh-ord{width:22px;height:22px;border-radius:50%;display:grid;place-items:center;background:rgba(235,163,8,.18);color:#EBA308;font-weight:700;font-size:12px}',
    '.fh-set{display:flex;align-items:center;gap:12px;padding:8px 4px;font-size:13px;border-top:1px solid rgba(255,255,255,.06)}.fh-set .fh-lbl{flex:1}',
    '.fh-seg{display:flex;gap:0}.fh-seg .fh-btn{border-radius:0}.fh-seg .fh-btn:first-child{border-radius:7px 0 0 7px}.fh-seg .fh-btn:last-child{border-radius:0 7px 7px 0}',
    '.fh-btn.fh-on{background:linear-gradient(180deg,rgba(235,163,8,.35),rgba(170,115,5,.35));border-color:rgba(235,163,8,.85);color:#fff}',
    '.fh-inp{font:inherit;font-size:13px;color:#eee;background:rgba(0,0,0,.4);border:1px solid rgba(255,255,255,.2);border-radius:6px;padding:6px 9px;user-select:text}',
    '.fh-inp:focus{outline:none;border-color:rgba(235,163,8,.75)}',
    '.fh-crop{border:1px solid rgba(255,255,255,.08);border-radius:7px;padding:8px;margin-bottom:6px;display:flex;flex-direction:column;gap:6px}',
    '.fh-robot{border:1px solid rgba(255,255,255,.1);border-radius:8px;padding:10px 12px;margin-bottom:8px;background:rgba(255,255,255,.025);display:flex;flex-direction:column;gap:6px}',
    '.fh-tier{display:grid;grid-template-columns:110px 30px minmax(0,1fr) minmax(0,1fr) auto;gap:8px;align-items:center;font-size:13px;padding:4px;border-radius:6px;background:rgba(255,255,255,.03)}',
    '.fh-note{font-size:12px;color:rgba(238,238,238,.55);padding:4px}',
    '.fh-lockmsg{padding:10px 14px;border-radius:7px;border:1px solid rgba(255,193,7,.75);background:rgba(255,193,7,.12);color:#EBA308;font-size:14px}',
    '.fh-icon{flex:none;display:flex;align-items:center;justify-content:center;border-radius:6px;background:rgba(255,255,255,.05);overflow:hidden;color:rgba(238,238,238,.7);font-weight:600}',
    '.fh-icon img{width:100%;height:100%;object-fit:contain}',
    '.fh-i56{width:56px;height:56px;font-size:20px}.fh-i44{width:44px;height:44px;font-size:18px}.fh-i30{width:30px;height:30px;font-size:13px}.fh-i22{width:22px;height:22px;font-size:11px}',
    '.fh-empty{padding:20px;color:rgba(238,238,238,.55);font-size:14px;text-align:center}',
    '.fh-btn{font:inherit;font-size:13px;color:#dde2f0;padding:6px 11px;border-radius:7px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(58,58,60,.9),rgba(38,38,40,.92));cursor:pointer;white-space:nowrap}',
    '.fh-btn:hover:not(:disabled){background:linear-gradient(180deg,rgba(72,66,48,.92),rgba(50,46,32,.95));border-color:rgba(255,193,7,.75)}',
    '.fh-btn.fh-danger:hover:not(:disabled),.fh-btn.fh-armed{background:linear-gradient(180deg,rgba(84,44,42,.92),rgba(58,30,28,.95));border-color:rgba(229,57,53,.75)}',
    '.fh-btn.fh-hot{border-color:rgba(124,179,66,.7)}',
    '.fh-btn.fh-sm{font-size:12px;padding:3px 8px}',
    '.fh-btn:disabled{opacity:.4;cursor:default}',
    '.fh-btn.fh-x{width:30px;height:30px;padding:0;font-size:18px;line-height:28px}',
    '.fh-foot{display:flex;gap:12px;font-size:13px;color:rgba(238,238,238,.7)}.fh-status{flex:1}',
    '#fh-toast{position:fixed;left:50%;bottom:120px;transform:translateX(-50%);z-index:2147483001;pointer-events:none;display:none;max-width:640px;text-align:center;padding:10px 18px;border-radius:8px;border:1px solid rgba(255,193,7,.75);background:linear-gradient(180deg,rgba(30,30,33,.94),rgba(18,18,20,.96));color:#eee;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = el('div');
    var plate = el('div', 'fh-plate');
    var head = el('div', 'fh-head');
    var sub = el('div', 'fh-sub');
    var tabs = el('div', 'fh-tabs');
    var body = el('div', 'fh-body');
    var foot = el('div', 'fh-foot');
    var status = el('div', 'fh-status');
    var keys = el('div');
    var toast = el('div');

    root.id = 'fh-root';
    toast.id = 'fh-toast';
    put(head, el('div', 'fh-title', 'Farm Hub'), sub, button('\u00d7', 'close', null, true, 'fh-x'));
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

    ui = { root: root, sub: sub, tabs: tabs, body: body, status: status, keys: keys, toast: toast };
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

    var act = target.getAttribute('data-act');
    var args = target.getAttribute('data-args');
    var armKey = act + '|' + (args || '');

    if (CONFIRMED[act] && state.armed !== armKey) {
      state.armed = armKey;
      render();

      return;
    }

    state.armed = '';

    if (LOCAL[act]) {
      LOCAL[act](args);

      return;
    }

    // Opening or shutting the window gets no status line back, so it must not hold the buttons busy.
    if (act === 'close' || act === 'toggle') {
      send(act);

      return;
    }

    if (act === 'savepreset') {
      savePreset();

      return;
    }

    if (act === 'settier') {
      setTier();

      return;
    }

    sendAction(args != null ? act + '|' + args : act);
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

  // ---------- frame ----------

  function render() {
    if (!ui || !state.isOpen) return;

    // Typing into a box must not lose the cursor to a refresh; the refresh shows when the box loses focus.
    if (state.isTyping) {
      state.isStale = true;

      return;
    }

    state.isStale = false;

    var scrolls = keepScrolls();

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

  function lockMessage(text) {
    var box = el('div', 'fh-col');
    box.style.flex = '1';
    box.appendChild(el('div', 'fh-lockmsg', text || ''));

    return box;
  }

  function renderHead() {
    var c = state.data.counts;
    ui.sub.textContent = '';

    if (!state.data.ready || !c) return;

    put(ui.sub,
      el('span', 'fh-pill' + (c.ripe ? ' fh-g' : ''), say('cRipe', c.ripe)),
      el('span', 'fh-pill' + (c.rot ? ' fh-r' : ''), say('cRot', c.rot)),
      el('span', 'fh-pill' + (c.pests ? ' fh-a' : ''), say('cPests', c.pests)),
      el('span', 'fh-pill' + (c.dry ? ' fh-b' : ''), say('cDry', c.dry)),
      el('span', 'fh-pill', say('cEmpty', c.empty)));
  }

  function renderTabs() {
    ui.tabs.textContent = '';

    TABS.forEach(function (tab) {
      var node = button(say(TAB_WORD[tab]), 'tab', tab, true);
      node.className = 'fh-tab' + (state.tab === tab ? ' fh-on' : '');
      ui.tabs.appendChild(node);
    });
  }

  function renderKeys() {
    ui.keys.textContent = state.key ? state.key + ' / ' + say('escClose') : say('escClose');
  }

  function pickTab(tab) {
    if (TABS.indexOf(tab) < 0) return;

    state.tab = tab;
    render();
  }

  // ---------- Pots tab ----------

  function renderPots() {
    var left = el('div', 'fh-col');
    left.style.flex = '1';

    put(left, potBar(state.data.counts), bulkBar(), potList());
    put(ui.body, left, potDetail(potById(state.sel)));
  }

  // The bulk buttons stay pressable with nothing to do: a press then says so.
  function potBar(c) {
    var seg = el('div', 'fh-seg');

    FILTERS.forEach(function (f) { seg.appendChild(button(say(f[1]), 'filter', f[0], true, 'fh-sm' + (state.filter === f[0] ? ' fh-on' : ''))); });

    return put(el('div', 'fh-bar'),
      button(say('plantAll', c.plant), 'plantall', null, true, 'fh-hot'),
      button(say('fertAll', c.fert), 'fertall', null, true),
      button(say('tendAll'), 'tendall', null, true),
      put(el('span', ''), seg));
  }

  function checkedIds() {
    return Object.keys(state.checks).filter(function (id) { return state.checks[id] && potById(id); });
  }

  function bulkBar() {
    var ids = checkedIds();

    if (!ids.length) return null;

    var list = ids.join(',');
    var bar = put(el('div', 'fh-bulk'), el('b', '', say('selected', ids.length)),
      button(say('plant'), 'plant', list, true, 'fh-sm fh-hot'), button(say('fertilize'), 'fert', list, true, 'fh-sm'),
      el('span', '', say('assignTo')));

    bar.appendChild(button(say('useDefault'), 'assign', '|' + list, state.data.saved, 'fh-sm'));
    presets().forEach(function (p) { bar.appendChild(button(p.name, 'assign', p.id + '|' + list, state.data.saved, 'fh-sm')); });
    bar.appendChild(button(say('clear'), 'clearchecks', null, true, 'fh-sm'));

    if (!state.data.saved) bar.appendChild(el('span', 'fh-dim', say('noSaveYet')));

    return bar;
  }

  function isShown(pot) {
    if (state.filter === 'ripe') return pot.state === 'ripe';
    if (state.filter === 'care') return !!pot.chore || (pot.problems || []).length > 0;
    if (state.filter === 'empty') return pot.state === 'empty';

    return true;
  }

  // Grouped by floor, then by room; a rotting crop sorts to the top of its room.
  function potList() {
    var well = scroller('pots');
    var list = pots().filter(isShown);
    var lastFloor = null;
    var lastRoom = null;
    var cards = null;

    if (!pots().length) {
      well.appendChild(el('div', 'fh-empty', say('empty')));

      return well;
    }

    list.forEach(function (pot) {
      if (pot.floor !== lastFloor) {
        lastFloor = pot.floor;
        lastRoom = null;

        var head = el('div', 'fh-sect', say('floor', pot.floor));

        if (pot.floor === state.data.floor) head.appendChild(el('span', 'fh-badge fh-b-warn', say('here')));

        well.appendChild(head);
      }

      if (pot.area !== lastRoom) {
        lastRoom = pot.area;
        cards = el('div', 'fh-cards');
        put(well, el('div', 'fh-dim', pot.room || say('otherRoom')), cards);
      }

      cards.appendChild(potCard(pot));
    });

    return well;
  }

  function stateBadge(pot) {
    return el('span', 'fh-badge fh-b-' + pot.state, say(STATE_WORD[pot.state] || pot.state));
  }

  function progressBar(percent) {
    var track = el('span', 'fh-track');
    var fill = el('span', 'fh-fill');
    fill.style.width = Math.max(0, Math.min(100, percent | 0)) + '%';
    track.appendChild(fill);

    return track;
  }

  function potCard(pot) {
    var kind = kindOf(pot.kind);
    var crop = pot.plant ? cropOf(pot.plant) : null;
    var card = el('div', 'fh-card' + (pot.id === state.sel ? ' fh-sel' : '') + (pot.rotSoon ? ' fh-hot' : ''));
    var main = el('div');
    var right = el('div', 'fh-rt');
    var check = button(state.checks[pot.id] ? '\u2713' : '', 'check', pot.id, true, 'fh-chk' + (state.checks[pot.id] ? ' fh-on' : ''));
    var name = put(el('div', 'fh-nm'), el('span', '', crop ? crop.name : kind.name), stateBadge(pot));

    card.setAttribute('data-act', 'pick');
    card.setAttribute('data-args', pot.id);

    if (pot.rotSoon) name.appendChild(el('span', 'fh-badge fh-b-bad', say('rotSoon')));

    (pot.problems || []).forEach(function (p) { name.appendChild(el('span', 'fh-badge fh-b-warn', say(PROBLEM_WORD[p] || p))); });

    put(main, name, el('div', 'fh-ln fh-dim', kind.name + ' \u00b7 ' + say('slots', kind.cap)), timeLine(pot));

    if (pot.robot) right.appendChild(el('span', 'fh-badge fh-b-info', say('robotHas')));
    if (pot.pending) right.appendChild(el('span', 'fh-badge fh-b-ripe', say('queued')));
    if (!pot.powered) right.appendChild(el('span', 'fh-badge fh-b-bad', say('unpowered')));
    if (pot.chore) right.appendChild(el('span', '', say(CHORE_WORD[pot.chore])));

    put(card, check, icon(crop || kind, 'fh-i44'), main, right);

    return card;
  }

  function timeLine(pot) {
    var line = el('div', 'fh-ln');

    if (pot.state === 'growing') return put(line, progressBar(pot.progress), el('span', '', pot.ripeIn != null ? say('ripeIn', hours(pot.ripeIn)) : say('grown', pot.progress)));
    if (pot.state === 'ripe') return put(line, el('span', pot.rotSoon ? 'fh-red' : '', pot.rotIn != null ? say('rotIn', hours(pot.rotIn)) : say('noRot')));
    if (pot.state === 'empty') return put(line, nextPlantLine(pot));

    return line;
  }

  function nextPlantLine(pot) {
    if (!pot.canPlant) return el('span', 'fh-dim', '');
    if (!pot.next) return el('span', 'fh-red', say('nextNone'));

    return el('span', 'fh-green', say('nextPlant', seedOf(pot.next.seed).name, pot.next.count));
  }

  function potDetail(pot) {
    var well = scroller('detail', 'fh-detail');

    if (!pot) {
      well.appendChild(el('div', 'fh-empty', say('pickPot')));

      return well;
    }

    var kind = kindOf(pot.kind);
    var crop = pot.plant ? cropOf(pot.plant) : null;
    var head = put(el('div', 'fh-dhead'), icon(crop || kind, 'fh-i56'),
      put(el('div', ''), el('div', 'fh-big', crop ? crop.name : kind.name), el('div', 'fh-dim', (pot.room || say('otherRoom')) + ' \u00b7 ' + say('floor', pot.floor))));
    var acts = put(el('div', 'fh-bar'),
      button(say('plant'), 'plant', pot.id, pot.canPlant, 'fh-hot'),
      button(say('fertilize'), 'fert', pot.id, (pot.nextFert || 0) > 0),
      pot.chore ? button(say(CHORE_WORD[pot.chore]), 'tend', pot.id, !pot.pending) : null);

    head.appendChild(stateBadge(pot));
    put(well, head, acts, timingBox(pot), problemBox(pot), presetBox(pot), kindBox(kind));

    return well;
  }

  function timingBox(pot) {
    var box = put(el('div', 'fh-box'), el('h4', '', say('timing')));

    if (pot.state === 'growing') {
      box.appendChild(put(el('div', 'fh-row'), progressBar(pot.progress), el('span', '', say('grown', pot.progress))));

      if (pot.ripeIn != null) box.appendChild(el('div', 'fh-row', say('ripeIn', hours(pot.ripeIn))));
    }

    if (pot.state === 'ripe') box.appendChild(el('div', 'fh-row' + (pot.rotSoon ? ' fh-red' : ''), pot.rotIn != null ? say('rotIn', hours(pot.rotIn)) : say('noRot')));
    if (pot.state === 'empty') box.appendChild(put(el('div', 'fh-row'), nextPlantLine(pot)));
    if (pot.state !== 'empty') box.appendChild(el('div', 'fh-row fh-dim', pot.fert ? say('fert', fertOf(pot.fert).name) : say('noFert')));
    if ((pot.nextFert || 0) > 0) box.appendChild(el('div', 'fh-row fh-green', say('nextFert', fertOf(pot.nextFert).name)));
    if (pot.robot) box.appendChild(el('div', 'fh-row fh-dim', say('robotHas')));
    if (pot.pending) box.appendChild(el('div', 'fh-row fh-green', say('queued')));

    return box;
  }

  function problemBox(pot) {
    var problems = pot.problems || [];

    if (!problems.length && pot.powered) return null;

    var box = el('div', 'fh-box');

    problems.forEach(function (p) { box.appendChild(el('div', 'fh-row fh-red', say(PROBLEM_WORD[p] || p))); });

    if (!pot.powered) box.appendChild(el('div', 'fh-row fh-red', say('unpowered')));

    return box;
  }

  // The pot's preset: its room's or floor's, or its own, with where it comes from.
  function presetBox(pot) {
    var box = put(el('div', 'fh-box'), el('h4', '', say('preset')));
    var pick = el('div', 'fh-bar');
    var isOwn = pot.from === 'pot';

    pick.appendChild(button(say('useDefault'), 'assign', '|' + pot.id, state.data.saved, 'fh-sm' + (!isOwn ? ' fh-on' : '')));
    presets().forEach(function (p) {
      pick.appendChild(button(p.name, 'assign', p.id + '|' + pot.id, state.data.saved, 'fh-sm' + (isOwn && p.id === pot.preset ? ' fh-on' : '')));
    });
    box.appendChild(pick);

    var preset = presetById(pot.preset);

    box.appendChild(el('div', 'fh-dim', preset ? preset.name + ' \u00b7 ' + say(FROM_WORD[pot.from] || 'fromAll') : say('none')));

    if (!state.data.saved) box.appendChild(el('div', 'fh-dim', say('noSaveYet')));

    return box;
  }

  function kindBox(kind) {
    var box = put(el('div', 'fh-box'), el('h4', '', say('pot')), el('div', 'fh-row', kind.name + ' \u00b7 ' + say('slots', kind.cap)));

    if (kind.light) box.appendChild(el('div', 'fh-row fh-dim', say('light', kind.light)));
    if (kind.heat) box.appendChild(el('div', 'fh-row fh-dim', say('heat', kind.heat)));
    if (kind.faster) box.appendChild(el('div', 'fh-row fh-dim', say('faster', Math.round(kind.faster * 100))));
    if (kind.power) box.appendChild(el('div', 'fh-row fh-dim', say('power')));

    return box;
  }

  function pickPot(id) {
    state.sel = state.sel === id ? '' : id;
    render();
  }

  function checkPot(id) {
    state.checks[id] = !state.checks[id];
    render();
  }

  function clearChecks() {
    state.checks = {};
    render();
  }

  // ---------- Planting presets tab ----------

  function renderPresets() {
    syncDraft();

    var list = scroller('plist', 'fh-plist');
    var editor = scroller('editor', 'fh-editor');

    presets().forEach(function (p) {
      var item = put(el('div', 'fh-pitem' + (p.id === state.presetId ? ' fh-sel' : '')), el('b', '', p.name),
        el('span', 'fh-dim', (p.uses || []).length ? say('usedFor') + ': ' + p.uses.map(useName).join(', ') : ''));

      item.setAttribute('data-act', 'preset');
      item.setAttribute('data-args', p.id);
      list.appendChild(item);
    });
    list.appendChild(button('+ ' + say('newPreset'), 'newpreset', null, true));

    if (state.draft) fillEditor(editor);

    put(ui.body, list, editor);
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

    if (!state.cap || cat.caps.indexOf(state.cap) < 0) state.cap = cat.caps[0] || 1;
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
    name.style.width = '280px';

    put(editor,
      put(el('div', 'fh-bar'), name, button(say('save'), 'savepreset', null, true, 'fh-hot'),
        d.id ? button(isArmed ? say('confirm') : say('delete'), 'deletepreset', d.id, true, 'fh-danger' + (isArmed ? ' fh-armed' : '')) : null,
        el('span', 'fh-red fh-dirty', isDirty() ? say('unsaved') : '')));

    seedSection(editor);
    fertSection(editor);
    cropSection(editor);
    put(editor,
      setting(say('skipUnsuitable'), toggle(d.skipUnsuitable, 'skip')),
      setting(say('fertilizeOn'), toggle(d.fertilize, 'fertilizeon')),
      setting(say('autoReplant'), toggle(d.autoReplant, 'autoreplant'), say('autoNote')),
      setting(say('usedFor'), useChips(d), say('usedNote')));
  }

  function setting(label, control, note) {
    var lbl = put(el('div', 'fh-lbl'), el('div', '', label));

    if (note) lbl.appendChild(el('div', 'fh-dim', note));

    return put(el('div', 'fh-set'), lbl, control);
  }

  // One seed order per pot size, picked with a row of size buttons.
  function seedSection(editor) {
    var sizes = el('div', 'fh-seg');
    var steps = seedSteps();

    cat.caps.forEach(function (cap) {
      var count = (state.draft.seeds[cap] || []).length;
      sizes.appendChild(button(say('tierCap', cap) + (count ? ' (' + count + ')' : ''), 'cap', cap, true, 'fh-sm' + (cap === state.cap ? ' fh-on' : '')));
    });

    put(editor, el('div', 'fh-sect', say('seedsFor', say('tierCap', state.cap))), sizes, el('div', 'fh-note', say('seedNote')));

    if (!steps.length) editor.appendChild(el('div', 'fh-note', say('noSteps')));

    steps.forEach(function (step, i) { editor.appendChild(seedStep(step, i, steps.length)); });

    put(editor, el('div', 'fh-sect', say('addSeed')), addPicker('addsearch', state.addSearch, 'addlist'));
    fillSeedAdd();
  }

  function seedSteps() {
    var d = state.draft;

    if (!d.seeds[state.cap]) d.seeds[state.cap] = [];

    return d.seeds[state.cap];
  }

  function perPot(seedId, cap) {
    var size = cropOfSeed(seedId).size || 1;

    return size > cap ? 0 : Math.floor(cap / size);
  }

  function seedStep(step, i, count) {
    var seed = seedOf(step.item);
    var have = owned(step.item);
    var each = perPot(step.item, state.cap);
    var row = el('div', 'fh-step' + (!each ? ' fh-bad' : have - step.keep < each ? ' fh-short' : ''));
    var label = put(el('div', ''), el('b', '', seed.name), el('div', each ? 'fh-dim' : 'fh-red', each ? say('perPot', each) : say('tooBig')));
    var keep = put(el('div', 'fh-bar'), el('span', 'fh-dim', say('keep')), button('-', 'keepless', i, step.keep > 0, 'fh-sm'),
      el('span', 'fh-num', String(step.keep)), button('+', 'keepmore', i, step.keep < MAX_KEEP, 'fh-sm'));
    var moves = put(el('div', 'fh-bar'), button('\u2191', 'seedup', i, i > 0, 'fh-sm'), button('\u2193', 'seeddown', i, i < count - 1, 'fh-sm'),
      button(say('remove'), 'seeddel', i, true, 'fh-sm fh-danger'));

    put(row, el('span', 'fh-ord', String(i + 1)), icon(seed, 'fh-i30'), label, el('span', 'fh-dim', say('own', have)), keep, moves);

    return row;
  }

  function addPicker(field, value, listId) {
    var box = el('div', 'fh-col');
    var search = el('input', 'fh-inp');
    var list = el('div', 'fh-col');

    search.setAttribute('data-field', field);
    search.setAttribute('placeholder', say('search'));
    search.value = value;
    list.id = 'fh-' + listId;
    put(box, search, list);
    ui[listId] = list;

    return box;
  }

  // Every seed that fits the pot size; with no search, only the ones the player owns.
  function fillSeedAdd() {
    var list = ui.addlist;

    if (!list || !state.draft) return;

    var used = seedSteps().map(function (s) { return s.item; });
    var query = state.addSearch.trim().toLowerCase();
    var found = cat.seedList.filter(function (s) {
      return used.indexOf(s.id) < 0 && perPot(s.id, state.cap) > 0 && (query ? s.name.toLowerCase().indexOf(query) >= 0 : owned(s.id) > 0);
    }).sort(function (a, b) { return (owned(b.id) > 0) - (owned(a.id) > 0) || byName(a, b); });

    list.textContent = '';
    found.slice(0, MAX_ADD).forEach(function (s) {
      list.appendChild(put(el('div', 'fh-row'), icon(s, 'fh-i22'), el('span', 'fh-grow', s.name), el('span', 'fh-dim', say('perPot', perPot(s.id, state.cap))),
        el('span', 'fh-dim', say('own', owned(s.id))), button(say('add'), 'seedadd', s.id, true, 'fh-sm')));
    });

    if (found.length > MAX_ADD) list.appendChild(el('div', 'fh-note', say('more', found.length - MAX_ADD)));
  }

  function fertSection(editor) {
    var d = state.draft;
    var adds = el('div', 'fh-bar');

    put(editor, el('div', 'fh-sect', say('ferts')), el('div', 'fh-note', say('fertNote')));

    if (!d.ferts.length) editor.appendChild(el('div', 'fh-note', say('noFerts')));

    d.ferts.forEach(function (id, i) {
      var fert = fertOf(id);

      editor.appendChild(put(el('div', 'fh-row'), el('span', 'fh-ord', String(i + 1)), icon(fert, 'fh-i22'), el('span', 'fh-grow', fert.name),
        el('span', 'fh-dim', say('own', owned(id))), button('\u2191', 'fertup', i, i > 0, 'fh-sm'), button('\u2193', 'fertdown', i, i < d.ferts.length - 1, 'fh-sm'),
        button(say('remove'), 'fertdel', i, true, 'fh-sm fh-danger')));
    });

    cat.fertList.filter(function (f) { return d.ferts.indexOf(f.id) < 0; }).forEach(function (f) {
      adds.appendChild(button('+ ' + f.name, 'fertadd', f.id, true, 'fh-sm'));
    });

    if (adds.firstChild) put(editor, put(el('div', 'fh-row'), el('span', 'fh-dim', say('addFert'))), adds);
  }

  // A crop with its own fertilizer order: a toggle per fertilizer (numbered in order), what to do when none are left,
  // and the grow time each choice gives a full pot.
  function cropSection(editor) {
    var d = state.draft;

    put(editor, el('div', 'fh-sect', say('perCrop')), el('div', 'fh-note', say('perCropNote')));

    d.perCrop.forEach(function (entry) { editor.appendChild(cropEntry(entry)); });

    put(editor, el('div', 'fh-row fh-dim', say('addCrop')), addPicker('cropsearch', state.cropSearch, 'croplist'));
    fillCropAdd();
  }

  function cropEntry(entry) {
    var crop = cropOf(entry.plant);
    var box = el('div', 'fh-crop');
    var order = el('div', 'fh-bar');
    var isNever = !entry.order.length && entry.whenOut === 'none';

    cat.fertList.forEach(function (f) {
      var at = entry.order.indexOf(f.id);
      var label = (at >= 0 ? (at + 1) + '. ' : '') + f.name + ' \u00b7 ' + hours(growHours(crop, f.speed));

      order.appendChild(button(label, 'cropfert', entry.plant + '|' + f.id, true, 'fh-sm' + (at >= 0 ? ' fh-on' : '')));
    });

    put(box,
      put(el('div', 'fh-row'), icon(crop, 'fh-i30'), el('b', 'fh-grow', crop.name), el('span', 'fh-dim', say('baseTime', hours(crop.grow))),
        isNever ? el('span', 'fh-badge fh-b-warn', say('never')) : null, button(say('remove'), 'cropdel', entry.plant, true, 'fh-sm fh-danger')),
      order,
      put(el('div', 'fh-row'), el('span', 'fh-dim', say('whenOut')),
        put(el('div', 'fh-seg'),
          button(say('woPreset'), 'cropout', entry.plant + '|preset', true, 'fh-sm' + (entry.whenOut !== 'none' ? ' fh-on' : '')),
          button(say('woNone'), 'cropout', entry.plant + '|none', true, 'fh-sm' + (entry.whenOut === 'none' ? ' fh-on' : '')))));

    return box;
  }

  // A full pot of the crop with this fertilizer and no other bonus.
  function growHours(crop, speed) {
    return crop.grow / (1 + (speed || 0));
  }

  // Crops the preset plants come first; a search reaches every crop.
  function fillCropAdd() {
    var list = ui.croplist;

    if (!list || !state.draft) return;

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
      list.appendChild(put(el('div', 'fh-row'), icon(c, 'fh-i22'), el('span', 'fh-grow', c.name), el('span', 'fh-dim', say('baseTime', hours(c.grow))),
        button(say('add'), 'cropadd', c.id, true, 'fh-sm')));
    });

    if (found.length > MAX_ADD) list.appendChild(el('div', 'fh-note', say('more', found.length - MAX_ADD)));
  }

  function useChips(d) {
    var box = el('div', 'fh-bar');
    var uses = [['all', say('useAll')]];

    (state.data.floors || []).forEach(function (f) { uses.push(['f' + f, say('floor', f)]); });
    (state.data.rooms || []).forEach(function (r) { uses.push(['a' + r.area, r.name || say('otherRoom')]); });
    uses.forEach(function (use) {
      box.appendChild(button(use[1], 'use', use[0], true, 'fh-sm' + (d.uses.indexOf(use[0]) >= 0 ? ' fh-on' : '')));
    });

    return box;
  }

  function editDraft(change) {
    if (!state.draft) return;

    change(state.draft);
    render();
  }

  function markDirty() {
    var mark = ui.body.querySelector('.fh-dirty');

    if (mark) mark.textContent = isDirty() ? say('unsaved') : '';
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

  function changeKeep(i, by) {
    editDraft(function () {
      var step = seedSteps()[i];

      if (step) step.keep = Math.max(0, Math.min(MAX_KEEP, step.keep + by));
    });
  }

  function addSeed(itemId) {
    var id = +itemId;

    editDraft(function () {
      var steps = seedSteps();

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
    render();
  }

  function newPreset() {
    state.presetId = '';
    state.draftOf = '';
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
    var well = scroller('robots');
    var robots = state.data.robots || [];
    var isOn = !!(state.data.settings && state.data.settings.robots);

    well.style.flex = '1';
    put(well, el('div', 'fh-note', say('robotsNote')),
      setting(say('keepStocked'), button(say(isOn ? 'on' : 'off'), 'robots', isOn ? '0' : '1', true, 'fh-tog' + (isOn ? ' fh-on' : '')), say('keepNote')));

    if (!robots.length) well.appendChild(el('div', 'fh-empty', say('noRobots')));

    robots.forEach(function (robot) { well.appendChild(robotCard(robot)); });
    ui.body.appendChild(well);
  }

  function robotCard(robot) {
    var box = el('div', 'fh-robot');
    var head = put(el('div', 'fh-row'), el('b', 'fh-grow', robot.name), el('span', 'fh-dim', say('floor', robot.floor)),
      el('span', 'fh-dim', say('battery', robot.battery)), robot.on ? null : el('span', 'fh-badge fh-b-bad', say('robotOff')));

    box.appendChild(head);
    robot.tiers.forEach(function (tier, i) {
      var isEditing = state.tierEdit && state.tierEdit.robot === robot.id && state.tierEdit.tier === i;

      box.appendChild(isEditing ? tierEditor(robot, tier, i) : tierRow(robot, tier, i));
    });

    return box;
  }

  function tierRow(robot, tier, i) {
    var seed = tier.seed ? seedOf(tier.seed) : null;
    var short = seed && owned(tier.seed) < perPot(tier.seed, tier.cap);

    return put(el('div', 'fh-tier'), el('span', 'fh-dim', say('tierCap', tier.cap)), seed ? icon(seed, 'fh-i22') : el('span', ''),
      el('span', seed ? (short ? 'fh-red' : '') : 'fh-dim', seed ? seed.name + ' \u00b7 ' + say('own', owned(tier.seed)) : say('noSeed')),
      el('span', 'fh-dim', tier.fert ? fertOf(tier.fert).name : say('noFert')),
      button(say('change'), 'tieredit', robot.id + '|' + i, true, 'fh-sm'));
  }

  // The seeds that fit the pot size, owned first, and the fertilizer, both chosen here and sent with one press.
  function tierEditor(robot, tier, i) {
    var edit = state.tierEdit;
    var box = el('div', 'fh-crop');
    var seeds = el('div', 'fh-bar');
    var ferts = el('div', 'fh-seg');
    var fits = cat.seedList.filter(function (s) { return perPot(s.id, tier.cap) > 0 && (owned(s.id) > 0 || s.id === edit.seed); })
      .sort(function (a, b) { return owned(b.id) - owned(a.id) || byName(a, b); });

    seeds.appendChild(button(say('noSeed'), 'tierseed', 0, true, 'fh-sm' + (edit.seed === 0 ? ' fh-on' : '')));
    fits.slice(0, MAX_ADD).forEach(function (s) {
      seeds.appendChild(button(s.name + ' (' + owned(s.id) + ')', 'tierseed', s.id, true, 'fh-sm' + (edit.seed === s.id ? ' fh-on' : '')));
    });
    ferts.appendChild(button(say('noFert'), 'tierfert', 0, true, 'fh-sm' + (edit.fert === 0 ? ' fh-on' : '')));
    cat.fertList.forEach(function (f) {
      ferts.appendChild(button(f.name, 'tierfert', f.id, true, 'fh-sm' + (edit.fert === f.id ? ' fh-on' : '')));
    });

    put(box, put(el('div', 'fh-row'), el('b', 'fh-grow', say('tierCap', tier.cap))),
      put(el('div', 'fh-row'), el('span', 'fh-dim', say('seed'))), seeds,
      put(el('div', 'fh-row'), el('span', 'fh-dim', say('fertWord'))), ferts,
      put(el('div', 'fh-bar'), button(say('save'), 'settier', null, true, 'fh-hot'), button(say('done'), 'tierdone', null, true)));

    return box;
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
    var left = scroller('coming', 'fh-col');
    var right = scroller('stock', 'fh-col');

    left.style.flex = '1';
    right.style.flex = '1';
    fillComing(left);
    fillStock(right);
    put(ui.body, left, right);
  }

  // What ripens or rots in the next two days, soonest first.
  function fillComing(well) {
    var events = [];

    pots().forEach(function (pot) {
      if (pot.ripeIn != null && pot.ripeIn <= FORECAST_HOURS) events.push({ pot: pot, at: pot.ripeIn, isRot: false });
      if (pot.rotIn != null && pot.rotIn <= FORECAST_HOURS) events.push({ pot: pot, at: pot.rotIn, isRot: true });
    });
    events.sort(function (a, b) { return a.at - b.at; });

    well.appendChild(el('div', 'fh-sect', say('comingUp')));

    if (!events.length) well.appendChild(el('div', 'fh-empty', say('nothingSoon')));

    events.forEach(function (e) {
      var crop = cropOf(e.pot.plant);

      well.appendChild(put(el('div', 'fh-row' + (e.isRot && e.pot.rotSoon ? ' fh-red' : '')), el('span', 'fh-num', hours(e.at)), icon(crop, 'fh-i22'),
        el('span', 'fh-grow', crop.name + ' \u00b7 ' + (e.pot.room || say('floor', e.pot.floor))),
        el('span', 'fh-badge ' + (e.isRot ? 'fh-b-bad' : 'fh-b-ripe'), say(e.isRot ? 'rots' : 'stRipe'))));
    });
  }

  // Each seed the presets plant, with how many pots of each size what the player owns would fill.
  function fillStock(well) {
    var rows = {};

    presets().forEach(function (p) {
      Object.keys(p.seeds || {}).forEach(function (cap) {
        p.seeds[cap].forEach(function (s) {
          var row = rows[s.item] || (rows[s.item] = { item: s.item, caps: {} });
          var each = perPot(s.item, +cap);

          if (each) row.caps[cap] = Math.max(row.caps[cap] || 0, Math.floor(Math.max(0, owned(s.item) - s.keep) / each));
        });
      });
    });

    well.appendChild(el('div', 'fh-sect', say('stock')));

    Object.keys(rows).map(function (k) { return rows[k]; }).sort(function (a, b) { return owned(a.item) - owned(b.item); }).forEach(function (row) {
      var seed = seedOf(row.item);
      var fills = Object.keys(row.caps).map(function (cap) { return say('fills', row.caps[cap], cap); }).join(' \u00b7 ');
      var isOut = Object.keys(row.caps).every(function (cap) { return !row.caps[cap]; });

      well.appendChild(put(el('div', 'fh-row' + (isOut ? ' fh-out' : '')), icon(seed, 'fh-i22'), el('span', 'fh-grow', seed.name),
        el('span', 'fh-dim', say('own', owned(row.item))), el('span', isOut ? 'fh-red' : 'fh-dim', fills)));
    });
  }

  // ---------- Settings tab ----------

  function renderSettings() {
    var well = scroller('settings');
    var settings = state.data.settings || {};
    var seg = el('div', 'fh-seg');

    ROT_WARNS.forEach(function (h) {
      seg.appendChild(button(say('hours', h), 'rotwarn', h, true, settings.rotWarn === h ? 'fh-on' : ''));
    });

    well.style.flex = '1';
    put(well, el('div', 'fh-sect', say('tabSettings')), setting(say('rotWarn'), seg),
      el('div', 'fh-note', state.key ? say('keyOpen', state.key) : say('keyNone')),
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
    ui.status.textContent = '';
    ui.toast.style.display = 'none';
    ui.root.style.display = 'flex';
    render();
  }

  function setData(data) {
    state.data = data || state.data;
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

  function status(text) {
    build();
    clearBusy();

    if (state.isOpen) {
      ui.status.textContent = text;

      return;
    }

    // An empty status only clears the line; as a toast it would be an empty box.
    if (!text) {
      ui.toast.style.display = 'none';

      return;
    }

    ui.toast.textContent = text;
    ui.toast.style.display = 'block';
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { ui.toast.style.display = 'none'; }, TOAST_MS);
  }

  // Whether a game with a home is loaded, and the key that opens the window ('' when none): the HUD button is only
  // shown in such a game, and both it and the window name the key.
  function live(isLive, key) {
    state.isLive = !!isLive;
    state.key = key || '';
    hudVersion++;

    if (ui && state.isOpen) renderKeys();

    patchPagesSafely();
  }

  // The number on the HUD button: pots with a chore waiting and empty pots a press of Plant would fill.
  function badge(count) {
    state.badge = Math.max(0, count | 0);
    hudVersion++;
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

  window.__slfarmhub = { open: open, setData: setData, close: close, status: status, live: live, catalog: catalog, badge: badge };
  send('ready');
})();
