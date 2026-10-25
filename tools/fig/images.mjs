import fs from 'node:fs';

const { message, schema } = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const changes = message.nodeChanges;
const key = (g) => `${g.sessionID}:${g.localID}`;
const byKey = new Map(changes.map(c => [key(c.guid), c]));
const children = new Map();
for (const c of changes) {
  if (!c.parentIndex) continue;
  const p = key(c.parentIndex.guid);
  if (!children.has(p)) children.set(p, []);
  children.get(p).push(c);
}
for (const l of children.values()) l.sort((a, b) => a.parentIndex.position - b.parentIndex.position);

function hashHex(h) {
  if (h === undefined || h === null) return '?';
  if (typeof h === 'string') return h;
  if (Array.isArray(h)) return Buffer.from(h).toString('hex');
  if (h instanceof Uint8Array) return Buffer.from(h).toString('hex');
  if (typeof h === 'object') {
    // fig kiwi may encode as { bytes: [...] } or nested
    const vals = Object.values(h).filter(v => typeof v === 'number');
    if (vals.length) return Buffer.from(vals).toString('hex');
  }
  return JSON.stringify(h);
}

function path(c) {
  const parts = [c.name || c.type];
  let p = c.parentIndex ? byKey.get(key(c.parentIndex.guid)) : null;
  let guard = 0;
  while (p && guard++ < 12) { parts.unshift(p.name || p.type); p = p.parentIndex ? byKey.get(key(p.parentIndex.guid)) : null; }
  return parts.join(' / ');
}

const usage = new Map();
for (const c of changes) {
  const paints = [...(c.fillPaints ?? []), ...(c.strokePaints ?? []), ...(c.backgroundPaints ?? [])];
  for (const p of paints) {
    if (!p.image) continue;
    const h = hashHex(p.image.hash);
    if (!usage.has(h)) usage.set(h, []);
    usage.get(h).push({
      node: c.type, name: c.name, size: c.size, scale: p.image.scaleMode ?? p.image.scale,
      transform: !!p.image.transform, parent: path(c), guid: c.guid.localID,
    });
  }
}
for (const [h, list] of usage) {
  console.log('### image ' + h + '  (' + list.length + ' uses)');
  for (const u of list.slice(0, 12)) {
    console.log(`    ${u.node} "${u.name}" ${u.size ? Math.round(u.size.x) + 'x' + Math.round(u.size.y) : ''} scale=${u.scale} xf=${u.transform} | ${u.parent}`);
  }
  if (list.length > 12) console.log('    ... +' + (list.length - 12));
}
