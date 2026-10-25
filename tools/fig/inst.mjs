import fs from 'node:fs';
const { message } = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const cs = message.nodeChanges;
const k = (g) => `${g.sessionID}:${g.localID}`;
const idx = new Map(cs.map(c => [k(c.guid), c]));
const name = process.argv[3];
const filter = process.argv[4];

function chain(c) {
  const out = [];
  let p = c.parentIndex ? idx.get(k(c.parentIndex.guid)) : null;
  let guard = 0;
  while (p && guard++ < 20) { out.unshift(p.name || p.type); p = p.parentIndex ? idx.get(k(p.parentIndex.guid)) : null; }
  return out;
}
function texts(o) {
  const out = [];
  const rec = (v, path) => {
    if (v === null || typeof v !== 'object') return;
    if (Array.isArray(v)) return v.forEach(x => rec(x, path));
    if (typeof v.characters === 'string' && v.characters !== '') out.push([path, v.characters]);
    for (const [key, val] of Object.entries(v)) rec(val, path + '/' + key);
  };
  rec(o, '');
  return out;
}
for (const c of cs) {
  if (c.type !== 'INSTANCE' || c.name !== name) continue;
  const ch = chain(c);
  if (filter && !ch.some(x => x.includes(filter))) continue;
  console.log(`${c.guid.localID} ${c.size ? Math.round(c.size.x) + 'x' + Math.round(c.size.y) : ''} | ${ch.slice(-4).join(' / ')}`);
  const seen = new Set();
  for (const [, s] of texts(c)) if (!seen.has(s)) { seen.add(s); console.log('    >> ' + JSON.stringify(s)); }
  if (!seen.size) console.log('    (no text overrides)');
}
