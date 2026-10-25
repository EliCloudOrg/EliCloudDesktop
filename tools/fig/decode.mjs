import fs from 'node:fs';
import zlib from 'node:zlib';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const kiwiSchema = require('kiwi-schema');

const buf = fs.readFileSync(process.argv[2]);
let off = 0;
const magic = buf.slice(0, 8).toString('latin1');
if (magic !== 'fig-kiwi') throw new Error('bad magic: ' + magic);
off = 8;
const version = buf.readUInt32LE(off); off += 4;
const files = [];
while (off + 4 <= buf.length) {
  const size = buf.readUInt32LE(off); off += 4;
  if (off + size > buf.length) { console.error('truncated at', off, size, buf.length); break; }
  files.push(buf.slice(off, off + size)); off += size;
}
console.error(`version=${version} files=${files.length} sizes=${files.map(f => f.length).join(',')} endOffset=${off}/${buf.length}`);

const schemaBin = zlib.inflateRawSync(files[0]);          // schema chunk: raw deflate
const messageBin = zlib.zstdDecompressSync(files[1]);    // message chunk: zstd
console.error('schemaBin=' + schemaBin.length + ' messageBin=' + messageBin.length);
const schema = kiwiSchema.decodeBinarySchema(schemaBin);
const compiled = kiwiSchema.compileSchema(schema);
const message = compiled.decodeMessage(messageBin);

fs.writeFileSync(process.argv[3], JSON.stringify({ version, schema, message }, null, 1));
console.error('message keys: ' + Object.keys(message).join(', '));
console.error('nodeChanges: ' + (message.nodeChanges ? message.nodeChanges.length : 'n/a'));
console.error('blobs: ' + (message.blobs ? message.blobs.length : 'n/a'));
