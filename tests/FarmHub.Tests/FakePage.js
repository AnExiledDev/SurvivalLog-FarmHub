// Just enough of Root.html and the game's HUD page (UI/CoreUI/CoreUI1.html) for page.js and Exiled's Toolkit's page
// API (api.js) to run in Jint: the root document with its page iframes, the vuplex bridge, and clicks and other events
// that bubble. Test code only; nothing here ships.
var window = this;

var location = { protocol: 'https:' };

var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

// One run of every interval the scripts set, which is how the Toolkit's 250 ms page check runs.
function poll() {
  intervals.slice().forEach(function (callback) { callback(); });
}

// Held and never fired: the tests answer every press themselves, as the plugin does.
var timers = {};
var nextTimer = 1;

function setTimeout(callback) {
  timers[nextTimer] = callback;

  return nextTimer++;
}

function clearTimeout(id) {
  delete timers[id];
}

var FakeElement = class {
  constructor(tagName) {
    var element = this;

    // Browsers report HTML tag names in capitals, and page.js checks them that way.
    this.tagName = tagName.charAt(0) === '#' ? tagName : tagName.toUpperCase();
    this.id = '';
    this.parentElement = null;
    this.children = [];
    this.text = '';
    this.attrs = {};
    this.classes = [];
    this.listeners = {};
    this.style = {};
    this.value = '';
    this.disabled = false;
    this.scrollTop = 0;
    this.classList = {
      contains: function (name) { return element.classes.indexOf(name) >= 0; },
      add: function (name) { if (element.classes.indexOf(name) < 0) element.classes.push(name); }
    };
  }

  get parentNode() { return this.parentElement; }

  get firstChild() { return this.children.length ? this.children[0] : null; }

  // In the page while some ancestor is a document's head or body.
  get isConnected() {
    for (var element = this; element; element = element.parentElement) {
      if (element.isRoot) return true;
    }

    return false;
  }

  get nextElementSibling() {
    var siblings = this.parentElement ? this.parentElement.children : [];

    for (var i = siblings.indexOf(this) + 1; i > 0 && i < siblings.length; i++) {
      if (siblings[i].tagName !== '#text') return siblings[i];
    }

    return null;
  }

  get className() { return this.classes.join(' '); }

  set className(value) { this.classes = String(value).split(' ').filter(function (name) { return name !== ''; }); }

  // As in a browser: an element's text is all of its descendants' text, and setting it replaces the children.
  get textContent() {
    return this.text + this.children.map(function (child) { return child.textContent; }).join('');
  }

  set textContent(value) {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
    this.text = value == null ? '' : String(value);
  }

  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name); }

  getAttribute(name) { return this.hasAttribute(name) ? this.attrs[name] : null; }

  setAttribute(name, value) { this.attrs[name] = String(value); }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  // A fragment's children move into the parent, as in a browser.
  appendChild(child) {
    if (child.tagName === '#fragment') {
      child.children.slice().forEach(function (moved) { this.appendChild(moved); }, this);

      return child;
    }

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.push(child);

    return child;
  }

  insertBefore(child, before) {
    if (!before) return this.appendChild(child);

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.splice(this.children.indexOf(before), 0, child);

    return child;
  }

  replaceChild(child, old) {
    if (child.parentElement) child.parentElement.removeChild(child);

    this.children[this.children.indexOf(old)] = child;
    child.parentElement = this;
    old.parentElement = null;

    return old;
  }

  removeChild(child) {
    this.children = this.children.filter(function (c) { return c !== child; });
    child.parentElement = null;

    return child;
  }

  remove() {
    if (this.parentElement) this.parentElement.removeChild(this);
  }

  contains(other) {
    for (var element = other; element; element = element.parentElement) {
      if (element === this) return true;
    }

    return false;
  }

  matches(selector) {
    var element = this;

    return selectorSteps(selector).every(function (step) { return matchesStep(element, step); });
  }

  closest(selector) {
    for (var element = this; element; element = element.parentElement) {
      if (element.matches(selector)) return element;
    }

    return null;
  }

  querySelectorAll(selector) {
    searches++;

    return descendantsOf([this]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Every querySelector, querySelectorAll and getElementById the script runs, so a test can see that a poll where nothing
// changed searched nothing.
var searches = 0;

// Compound selectors of a tag, classes, ids and attributes ("iframe", ".fh-hud-btn", "[data-act]"); the script uses
// nothing else.
function selectorSteps(selector) {
  var steps = selector.match(/^[a-z]+|\.[\w-]+|#[\w-]+|\[[\w-]+\]/g) || [];

  if (steps.join('') !== selector) throw new Error('the fake page does not know the selector "' + selector + '"');

  return steps;
}

function matchesStep(element, step) {
  switch (step.charAt(0)) {
    case '.':
      return element.classList.contains(step.slice(1));
    case '#':
      return element.id === step.slice(1);
    case '[':
      return element.hasAttribute(step.slice(1, -1));
    default:
      return element.tagName === step.toUpperCase();
  }
}

function descendantsOf(roots) {
  var found = [];

  roots.forEach(function (root) {
    root.children.forEach(function walk(child) {
      found.push(child);
      child.children.forEach(walk);
    });
  });

  return found;
}

// href is the page's address; the Toolkit's page check skips a document without one, as it skips the about:blank a new
// iframe holds first.
var FakeDocument = class {
  constructor(href) {
    this.head = new FakeElement('head');
    this.body = new FakeElement('body');
    this.head.isRoot = true;
    this.body.isRoot = true;
    this.defaultView = { location: { href: href || 'about:blank' } };
  }

  createElement(tagName) { return new FakeElement(tagName); }

  createElementNS(namespace, tagName) { return new FakeElement(tagName); }

  createTextNode(text) {
    var node = new FakeElement('#text');

    node.textContent = text;

    return node;
  }

  createDocumentFragment() { return new FakeElement('#fragment'); }

  getElementById(id) {
    searches++;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.id === id; })[0] || null;
  }

  querySelectorAll(selector) {
    searches++;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }

  // Not counted as a search: in a browser this is a live list the document keeps up to date, which is what the
  // Toolkit's page check reads each time.
  getElementsByTagName(tagName) {
    var wanted = tagName.toUpperCase();

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.tagName === wanted; });
  }
};

// Root.html's own document, which holds the page iframes.
var document = new FakeDocument();

// What the script posts to the plugin.
var posted = [];

window.vuplex = {
  postMessage: function (message) { posted.push(message); }
};

// The game showing its HUD: a fresh CoreUI1 iframe in Root.html with the tool column the bag and phone buttons sit in.
function openHud() {
  var frame = document.body.appendChild(new FakeElement('iframe'));
  var column = new FakeElement('div');

  frame.src = 'https://game.local/UI/CoreUI/CoreUI1.html';
  frame.contentDocument = new FakeDocument(frame.src);
  column.className = 'vertical-tools';
  column.appendChild(new FakeElement('button'));
  frame.contentDocument.body.appendChild(column);

  return frame;
}

function hudPage() {
  return document.querySelector('iframe').contentDocument;
}

function hudButton() {
  return hudPage().querySelector('.fh-hud-btn');
}

function panel() {
  return document.getElementById('fh-root');
}

// The first button in the window for an action, or for an action and its arguments.
function panelButton(action, args) {
  return document.querySelectorAll('[data-act]').filter(function (node) {
    return node.getAttribute('data-act') === action && (args === undefined || node.getAttribute('data-args') === args);
  })[0];
}

function panelButtons(action) {
  return document.querySelectorAll('[data-act]').filter(function (node) { return node.getAttribute('data-act') === action; });
}

function field(name) {
  return document.querySelectorAll('input').filter(function (node) { return node.getAttribute('data-field') === name; })[0];
}

// An event of any type, bubbling from the target as focusin, focusout and input do in a browser.
function fire(target, type, extra) {
  var isStopped = false;
  var event = { type: type, target: target, stopPropagation: function () { isStopped = true; } };

  Object.keys(extra || {}).forEach(function (key) { event[key] = extra[key]; });

  for (var node = target; node && !isStopped; node = node.parentElement) {
    (node.listeners[type] || []).slice().forEach(function (listener) { listener(event); });
  }
}

// Typing into a text box: it takes focus, its value changes, and the page hears the input.
function type(input, text) {
  fire(input, 'focusin');
  input.value = text;
  fire(input, 'input');
}

// A click as the game's click-through delivers one: dispatched on whatever is under the pointer, greyed buttons
// included, bubbling up until a listener stops it (webui-core.js:33-36).
function click(target) {
  var isStopped = false;
  var event = { target: target, stopPropagation: function () { isStopped = true; } };

  for (var node = target; node && !isStopped; node = node.parentElement) {
    (node.listeners.click || []).slice().forEach(function (listener) { listener(event); });
  }
}

// The game's plant panel in the HUD (CoreUI1.html 2296-2400), opened: the toggle row with Tend Garden and its tip and
// the Replant box, both only while Tend Garden is unlocked (hasReplant not false), and one row per plant in the order
// its Vue state lists them. rows are [showInstanceId, status class] pairs; the Vue state sits where the page script
// reads it, with the switches the game's v-if lines read.
function openPlantPanel(rows, isReplantOn, hasReplant) {
  var doc = hudPage();
  var app = doc.getElementById('app') || doc.body.appendChild(new FakeElement('div'));
  var panel = new FakeElement('div');
  var toggle = new FakeElement('div');
  var patrol = new FakeElement('button');
  var tip = new FakeElement('div');
  var title = new FakeElement('div');
  var chevron = new FakeElement('svg');
  var list = new FakeElement('div');

  app.id = 'app';
  app._vnode = { component: { setupState: { state: {
    maturePlants: rows.map(function (r) { return { showInstanceId: r[0] }; }),
    matureReplantOn: !!isReplantOn,
    maturePlantsActive: true,
    maturePlantsExpanded: true,
    maturePatrolUnlocked: hasReplant !== false
  } } } };
  panel.className = 'mature-panel';
  toggle.className = 'mature-toggle-row is-expanded';
  patrol.id = 'maturePatrolBtn';
  patrol.className = 'mature-patrol-btn has-entry-tip';
  tip.className = 'entry-tip';
  title.className = 'entry-tip-title';
  title.textContent = 'Tend Garden';
  tip.appendChild(title);
  patrol.appendChild(tip);

  if (hasReplant !== false) {
    var replant = new FakeElement('label');

    toggle.appendChild(patrol);

    replant.id = 'matureReplantChk';
    replant.className = 'mature-replant-chk';
    replant.appendChild(new FakeElement('input'));
    toggle.appendChild(replant);
  }

  chevron.className = 'mature-toggle-chevron';
  toggle.appendChild(chevron);
  list.className = 'mature-popover';
  rows.forEach(function (r) {
    var row = new FakeElement('div');
    var name = new FakeElement('span');

    row.className = 'mature-item-row ' + r[1];
    name.className = 'mature-item-name';
    name.textContent = 'plant ' + r[0];
    row.appendChild(new FakeElement('img'));
    row.appendChild(name);
    list.appendChild(row);
  });
  panel.appendChild(toggle);
  panel.appendChild(list);
  app.appendChild(panel);

  return panel;
}

// The game's plant rows, ours left out.
function gameRowsNow() {
  return hudPage().querySelectorAll('.mature-item-row').filter(function (n) { return !n.classList.contains('fh-hudadd'); });
}

// What a row's second line says.
function rowSub(i) {
  var sub = gameRowsNow()[i].querySelectorAll('.fh-sub')[0];

  return sub ? sub.textContent : '';
}

// The node of a row's second line, to tell a line kept from one drawn again.
function rowSubNode(i) {
  return gameRowsNow()[i].querySelectorAll('.fh-sub')[0] || null;
}

// The player closing the plant list: the game's Vue state says so and the list leaves the page.
function closePlantList() {
  var doc = hudPage();

  doc.getElementById('app')._vnode.component.setupState.state.maturePlantsExpanded = false;
  doc.querySelector('.mature-popover').remove();
}

// The game dropping its HUD page and making a new one, as it does when it reloads the HUD.
function swapHud() {
  document.querySelector('iframe').remove();

  return openHud();
}
