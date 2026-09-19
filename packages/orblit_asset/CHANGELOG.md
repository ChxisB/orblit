# Changelog

## 0.3.0

- **Importers, and the cook that drives them.** `Importer` is the contract: a
  name, a version, the extensions it claims, the settings it reads and the
  other assets it depends on. `Cook` puts the steps in the only order that
  saves anything — settings resolved, dependencies found, key built, cache
  asked, and only then the work — and reports what happened per asset rather
  than throwing, so one unreadable texture does not end a build of a thousand.
- `CookTarget` is what a cook is cooking *for*, as plain data rather than a
  device profile, so `orblit_asset` still builds where the renderer does not.
  Only put things in it that change the bytes: a device's thread count changes
  how fast a cook runs and never what it writes, and putting it in the target
  would split the cache for nothing.
- `GltfImporter` finds the buffers and images a `.gltf` points at, reading a
  `.glb`'s JSON chunk for the same answer, and passes the bytes through —
  Filament's loader reads glTF well, and re-encoding it here would be work that
  changes nothing. `SceneImporter` collects what a scene places, matching keys
  by shape rather than by a fixed list, so a component added later is found
  without this being edited.
- `TextureImporter`, `ModelImporter`, `SplatImporter` and `EnvironmentImporter`
  drive `orblit_texture_cook`, `orblit_import`, `orblit_splat_cook` and
  Filament's `cmgen`. The flags come from `.import.json` and the target instead
  of from a file-name convention — `tool/cook_textures.sh` had to guess that a
  name ending `_normal` was a normal map, and a guess is wrong for exactly the
  assets whose names came from somewhere else.
- The native importers are absent on the web, the same way `DirectoryCookCache`
  is: a browser cannot start a program, and it does not need to, because it
  loads what a build machine cooked with `--target web`.

## 0.2.0

- **A cook cache.** `CookKey` writes down everything that decided what a cooked
  asset is — the source bytes, the files it read, which importer ran and its
  version, the resolved settings and the target — as canonical JSON, and hashes
  that. `CookCache` files results under it, so a rebuild with nothing changed
  cooks nothing and editing one texture recooks that texture alone. Raising an
  importer's version is what reaches caches that already exist, since a fix to
  an importer changes nothing on disk.
- `DirectoryCookCache` is the cache a machine shares between builds: bytes in a
  content store, a JSON index written whole or not at all, and a file lock so
  two builds cooking at once do not lose each other's entries. Give it
  `limitBytes` and it evicts oldest-first, counting shared bytes once and never
  taking a file out from under an entry that still points at it.
  `MemoryCookCache` is the same contract for tests.
- **Import settings.** An `.import.json` beside an asset says how to cook it,
  and one in a folder says it for everything under that folder — a folder of
  sprites is all pixel art — with the nearer file winning key by key.
  `ImportSettingsReader` works out what applies to an asset and reads each file
  once.

## 0.1.0

- First cut of `orblit_asset`. Pre-alpha: everything is subject to change.
