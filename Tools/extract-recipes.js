// Extract Logos Action / mneme / logogram data from the ffxiv-eureka.com Ember bundle.
const fs = require('fs');
const path = require('path');

const dir = __dirname;
const src = fs.readFileSync(path.join(dir, 'eureka-tracker.js'), 'utf8');

// --- 1. Records: {id:"N",type:"logos-action",attributes:{...}} -------------
// Brace-match from each `{id:"` so nested objects/arrays are captured whole.
function grabRecords(text) {
  const out = [];
  const re = /\{id:"(\d+)",type:"([a-z-]+)",attributes:\{/g;
  let m;
  while ((m = re.exec(text)) !== null) {
    const start = m.index;
    let depth = 0, i = start, inStr = false, q = null;
    for (; i < text.length; i++) {
      const c = text[i];
      if (inStr) {
        if (c === '\\') { i++; continue; }
        if (c === q) inStr = false;
        continue;
      }
      if (c === '"' || c === "'") { inStr = true; q = c; continue; }
      if (c === '{' || c === '[') depth++;
      else if (c === '}' || c === ']') { depth--; if (depth === 0) break; }
    }
    out.push(text.slice(start, i + 1));
  }
  return out;
}

// Quote bare identifier keys so JSON.parse accepts the object literal.
function toJson(literal) {
  return literal.replace(/([{,])([A-Za-z_][A-Za-z0-9_]*):/g, '$1"$2":');
}

const records = [];
const seen = new Set();
for (const lit of grabRecords(src)) {
  let obj;
  try { obj = JSON.parse(toJson(lit)); } catch { continue; }
  const key = obj.type + '#' + obj.id;
  if (seen.has(key)) continue;      // bundle contains the payload twice
  seen.add(key);
  records.push(obj);
}

// --- 2. i18n name maps ----------------------------------------------------
// Parse EVERY `name:{...}` map in the bundle, then pick the English ones by
// size + a known English string. The bundle ships several locales.
function allNameMaps() {
  const maps = [];
  const re = /name:\{(?=\d)/g;
  let m;
  while ((m = re.exec(src)) !== null) {
    let depth = 0, i = m.index + 5, inStr = false, q = null;
    const start = i;
    for (; i < src.length; i++) {
      const c = src[i];
      if (inStr) {
        if (c === '\\') { i++; continue; }
        if (c === q) inStr = false;
        continue;
      }
      if (c === '"' || c === "'") { inStr = true; q = c; continue; }
      if (c === '{') depth++;
      else if (c === '}') { depth--; if (depth === 0) break; }
    }
    const lit = src.slice(start, i + 1);
    try { maps.push(JSON.parse(lit.replace(/([{,])(\d+):/g, '$1"$2":'))); }
    catch { /* not a plain map */ }
  }
  return maps;
}

const maps = allNameMaps();
// The logogram map uses short names ("Conceptual"), not "Conceptual Logogram",
// which is why an earlier version of this test silently picked the German map.
const isEnglish = o => Object.values(o).some(v => /^Wisdom of the |^Protect L$|^Conceptual$/.test(v));
const pick = (size) => maps.find(o => Object.keys(o).length === size && isEnglish(o)) || {};

const names = {
  logosActions: pick(56),
  mnemes: pick(28),
  logograms: pick(9),
};
console.log('name maps found:', maps.length,
  '| sizes:', [...new Set(maps.map(o => Object.keys(o).length))].join(','));

const byType = t => records.filter(r => r.type === t);

const out = {
  logosActions: byType('logos-action').map(r => ({
    idx: Number(r.id),
    name: names.logosActions[r.id] || null,
    ...r.attributes,
  })).sort((a, b) => a.idx - b.idx),
  // relationships.logogram is what tells us which logogram yields each mneme.
  // It is the only source for that link, so it must not be dropped.
  mnemes: byType('mneme').map(r => ({
    idx: Number(r.id),
    name: names.mnemes[r.id] || null,
    logogramIdx: Number(r.relationships?.logogram?.data?.id ?? 0),
    ...r.attributes,
  })).sort((a, b) => a.idx - b.idx),
  logograms: byType('logogram').map(r => ({
    idx: Number(r.id),
    name: names.logograms[r.id] || null,
    ...r.attributes,
  })).sort((a, b) => a.idx - b.idx),
};

fs.writeFileSync(path.join(dir, 'eureka_extracted.json'), JSON.stringify(out, null, 2));

console.log('logosActions:', out.logosActions.length);
console.log('mnemes      :', out.mnemes.length);
console.log('logograms   :', out.logograms.length);
console.log('\nunnamed actions:', out.logosActions.filter(a => !a.name).length);
console.log('unnamed mnemes :', out.mnemes.filter(a => !a.name).length);
console.log('\n--- sample actions ---');
for (const a of out.logosActions.slice(0, 5)) console.log(JSON.stringify(a));
console.log('\n--- sample mnemes ---');
for (const a of out.mnemes.slice(0, 5)) console.log(JSON.stringify(a));
console.log('\n--- logograms ---');
for (const a of out.logograms) console.log(JSON.stringify(a));
console.log('\nmnemes with no logogram link:', out.mnemes.filter(m => !m.logogramIdx).length);
console.log('\n--- combination size histogram ---');
const hist = {};
for (const a of out.logosActions)
  for (const c of (a.combinations || [])) hist[c.length] = (hist[c.length] || 0) + 1;
console.log(hist);
