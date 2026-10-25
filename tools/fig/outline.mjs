import fs from 'node:fs';

const { message } = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const changes = message.nodeChanges;

const key = (g) => `${g.sessionID}:${g.localID}`;
const children = new Map();
for (const c of changes) {
  if (!c.parentIndex) continue;
  const p = key(c.parentIndex.guid);
  if (!children.has(p)) children.set(p, []);
  children.get(p).push(c);
}
for (const list of children.values()) list.sort((a, b) => a.parentIndex.position - b.parentIndex.position);

const px = (n) => (n === undefined || n === null) ? '' : Math.round(n * 100) / 100;
const hx = (v) => Math.round((v ?? 0) * 255).toString(16).padStart(2, '0');
function fmtColor(c) {
  if (!c) return '';
  const a = c.a ?? 1;
  return `#${hx(c.r)}${hx(c.g)}${hx(c.b)}${a < 1 ? '@' + Math.round(a * 100) / 100 : ''}`;
}
function paint(p) {
  let s = p.type;
  if (p.color) s += fmtColor(p.color);
  if (p.stops) s += '(' + p.stops.map(x => fmtColor(x.color) + '@' + Math.round(x.position * 100) / 100).join(',') + ')';
  if (p.image) s += 'img#' + String(p.image.hash).slice(0, 8);
  if (p.opacity !== undefined) s += ' o=' + Math.round(p.opacity * 100) / 100;
  if (p.visible === false) s += ' HIDDEN';
  if (p.rotation !== undefined) s += ' rot=' + px(p.rotation);
  if (p.filters) s += ' filters=' + JSON.stringify(p.filters);
  return s;
}
function fills(n) { return (n.fillPaints && n.fillPaints.length) ? ' fill=[' + n.fillPaints.map(paint).join(' | ') + ']' : ''; }
function strokes(n) {
  if (!n.strokePaints || !n.strokePaints.length) return '';
  return ' stroke=[' + n.strokePaints.map(paint).join(' | ') + ']' + (n.strokeWeight !== undefined ? ' sw=' + px(n.strokeWeight) : '') + (n.strokeAlign ? ' sa=' + n.strokeAlign : '') + (n.strokeDashes ? ' dash=' + JSON.stringify(n.strokeDashes) : '');
}
function effects(n) {
  if (!n.effects || !n.effects.length) return '';
  return ' fx=[' + n.effects.map(e => `${e.type}${e.radius !== undefined ? ' r=' + px(e.radius) : ''}${e.offset ? ' off=' + px(e.offset.x) + ',' + px(e.offset.y) : ''}${e.color ? ' ' + fmtColor(e.color) : ''}${e.spread !== undefined ? ' sp=' + px(e.spread) : ''}${e.visible === false ? ' HIDDEN' : ''}`).join(' ') + ']';
}
function layout(n) {
  const p = [];
  if (n.stackMode) p.push(`stack=${n.stackMode}${n.stackSpacing !== undefined ? ' gap=' + px(n.stackSpacing) : ''}${n.stackPadding !== undefined ? ' pad=' + px(n.stackPadding) : ''}${n.stackPrimaryAxisSizing ? ' pas=' + n.stackPrimaryAxisSizing : ''}${n.stackCounterAxisSizing ? ' cas=' + n.stackCounterAxisSizing : ''}${n.stackPrimaryAlignItems ? ' pai=' + n.stackPrimaryAlignItems : ''}${n.stackCounterAlignItems ? ' cai=' + n.stackCounterAlignItems : ''}${n.stackWrap ? ' wrap=' + n.stackWrap : ''}`);
  if (n.stackHorizontalPadding !== undefined) p.push(`padH=${px(n.stackHorizontalPadding)}`);
  if (n.stackVerticalPadding !== undefined) p.push(`padV=${px(n.stackVerticalPadding)}`);
  if (n.horizontalConstraint || n.verticalConstraint) p.push(`con=${n.horizontalConstraint ?? '-'}/${n.verticalConstraint ?? '-'}`);
  return p.length ? ' ' + p.join(' ') : '';
}
function style(n) {
  const p = [];
  if (n.cornerRadius !== undefined) p.push('r=' + px(n.cornerRadius));
  if (n.rectangleCornerRadii) p.push('radii=' + n.rectangleCornerRadii.map(px).join(','));
  if (n.fontSize !== undefined) p.push('fs=' + n.fontSize);
  if (n.fontName) p.push(`font=${n.fontName.family}/${n.fontName.style}`);
  if (n.textAlignHorizontal) p.push('align=' + n.textAlignHorizontal);
  if (n.textAlignVertical) p.push('valign=' + n.textAlignVertical);
  if (n.lineHeight) p.push(`lh=${n.lineHeight.value}${n.lineHeight.units}`);
  if (n.letterSpacing) p.push(`ls=${px(n.letterSpacing.value)}${n.letterSpacing.units}`);
  if (n.opacity !== undefined && n.opacity !== 1) p.push('op=' + Math.round(n.opacity * 100) / 100);
  if (n.visible === false) p.push('HIDDEN');
  if (n.isMask) p.push('MASK');
  if (n.arcData) p.push(`arc=${px(n.arcData.startingAngle)},${px(n.arcData.endingAngle)}`);
  if (n.backgroundPaints) p.push('bg=[' + n.backgroundPaints.map(paint).join('|') + ']');
  if (n.autoRename) p.push('autoRename');
  if (n.styleIdForFill) p.push('styleFill=' + n.styleIdForFill.localID);
  if (n.styleIdForText) p.push('styleText=' + n.styleIdForText.localID);
  if (n.effectStyleId) p.push('styleFx');
  if (n.isStateGroup) p.push('stateGroup');
  return p.length ? ' [' + p.join(' ') + ']' : '';
}
function sz(n) { return n.size ? `${px(n.size.x)}x${px(n.size.y)}` : ''; }
function pos(n) { return n.transform ? `@${px(n.transform.m02)},${px(n.transform.m12)}` : ''; }

const out = [];
function walk(c, depth) {
  const n = c;
  const pad = '  '.repeat(depth);
  let l = `${pad}${n.type}${n.name ? ' "' + n.name + '"' : ''} ${sz(n)} ${pos(n)}`;
  l += style(n) + layout(n) + fills(n) + strokes(n) + effects(n);
  if (n.type === 'TEXT' && n.textData) l += `\n${pad}   >> ${JSON.stringify(n.textData.characters)}`;
  if (n.textData && n.textData.characterStyleIDs) l += ` cs=${n.textData.characterStyleIDs.length}`;
  out.push(l);
  for (const ch of (children.get(key(c.guid)) ?? [])) walk(ch, depth + 1);
}

const roots = changes.filter(c => !c.parentIndex);
for (const r of roots) {
  out.push(`=== ${r.type} "${r.name}" ${sz(r)} guid=${r.guid.localID}`);
  for (const ch of (children.get(key(r.guid)) ?? [])) walk(ch, 1);
}
fs.writeFileSync(process.argv[3], out.join('\n'));
console.log('roots=' + roots.length + ' lines=' + out.length);
