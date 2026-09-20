// Runs the Khronos glTF-Validator over a directory of exports.
//
// The validator is the reference implementation of the specification, and is
// the only honest answer to "is this a valid glTF" — the format has enough
// rules about alignment, ranges and what may reference what that reading the
// spec and believing you have followed it is not the same thing.
//
// Warnings fail this as well as errors. A warning from this validator is a
// file that loads but is wrong in some way a loader is papering over, and a
// file we wrote is a file we can fix.
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { validateBytes } from 'gltf-validator';

const dir = process.argv[2];
if (!dir) {
  console.error('usage: validate_gltf.mjs <directory>');
  process.exit(2);
}

const names = (await readdir(dir))
  .filter((one) => one.endsWith('.glb') || one.endsWith('.gltf'))
  .sort();

if (names.length === 0) {
  console.error(`nothing to validate in ${dir}`);
  process.exit(2);
}

let bad = 0;
for (const name of names) {
  const bytes = new Uint8Array(await readFile(path.join(dir, name)));
  const report = await validateBytes(bytes, {
    uri: name,
    // A .gltf keeps its buffers and textures in files beside it.
    externalResourceFunction: (uri) =>
      readFile(path.join(dir, decodeURIComponent(uri))).then(
        (one) => new Uint8Array(one),
      ),
  });

  const { numErrors, numWarnings } = report.issues;
  const wrong = numErrors + numWarnings;
  console.log(`${wrong ? 'FAIL' : ' ok '}  ${name}`);
  for (const one of report.issues.messages) {
    if (one.severity > 1) continue;
    console.log(`        ${one.pointer || '/'} ${one.code}: ${one.message}`);
  }
  if (wrong) bad++;
}

if (bad) {
  console.error(`\n${bad} of ${names.length} exports are not valid glTF.`);
  process.exit(1);
}
console.log(`\n${names.length} exports, all valid.`);
