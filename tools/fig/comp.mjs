import fs from 'node:fs';
const { message } = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const cs = message.nodeChanges;
const k = (g) => `${g.sessionID}:${g.localID}`;
const idx = new Map(cs.map(c => [k(c.guid), c]));
const children = new Map();
for (const c of cs) {
  if (!c.parentIndex) continue;
  const p = k(c.parentIndex.guid);
  if (!children.has(p)) children.set(p, []);
  children.get(p).push(c);
}
for (const l of children.values()) l.sort((a, b) => a.parentIndex.position - b.parentIndex.position);
function line(c, d) {
  let s = '  '.repeat(d) + c.type + ' "' + (c.name || '') + '" ' + (c.size ? Math.round(c.size.x) + 'x' + Math.round(c.size.y) : '');
  if (c.symbolData) s += ' sym=' + k(c.symbolData.symbolID);
  if (c.componentId) s += ' comp=' + c.componentId.sessionID + ':' + c.componentId.localID;
  if (c.type === 'TEXT' && c.textData) s += ' text=' + JSON.stringify(c.textData.characters);
  if (c.fillPaints) s += ' fills=' + c.fillPaints.map(p => p.type + (p.color ? '#' + [p.color.r, p.color.g, p.color.b].map(v => Math.round((v ?? 0) * 255).toString(16).padStart(2, '0')).join('') : '')).join(',');
  return s;
}
function walk(c, d, max) {
  console.log(line(c, d));
  if (d >= max) return;
  for (const ch of (children.get(k(c.guid)) || [])) walk(ch, d + 1, max);
}
const scope = process.argv[3];
if (scope.startsWith('sym:')) {
  // find any instance node whose symbolID matches, print its resolved component
  const target = scope.slice(4);
  const inst = cs.find(c => c.symbolData && k(c.symbolData.symbolID) === target);
  console.log('instance:', inst ? inst.name : 'none');
  const n = idx.get(target);
  console.log('component:', n ? n.type + ' "' + n.name + '"' : 'NONE (external library)');
  if (n) walk(n, 0, Number(process.argv[4] ?? 6));
} else {
  const n = idx.get(scope);
  console.log('node:', n ? n.type + ' "' + n.name + '"' : 'NONE');
  if (n) walk(n, 0, Number(process.argv[4] ?? 6));
}
