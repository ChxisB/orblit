# Changelog

## 0.4.0

- **Cooking a whole project, from a command.** `dart run orblit_asset:cook
  --target ios` walks a project's assets, cooks each one for that target and
  writes a bundle: the files, and a manifest saying what each asset now is.
  Repeat `--target` and it writes one bundle per target, side by side, so an
  app build ships only the formats its own target can read — an iOS build
  carries ASTC and no BC, and a desktop build the reverse.
- `CookTargets` names the six platforms Orblit builds for and what each one
  wants: which texture families it can decode, how large a texture it will
  take, and whether it has half-float textures. They are ordinary `CookTarget`
  values, so a project that needs a seventh writes one rather than waiting for
  this to grow.
- `CookProject` is the same run without the command around it, for a build hook
  to call. It empties a bundle before writing it, so an asset deleted yesterday
  stops shipping today rather than lingering because nothing overwrote it.
- `bundleHash` is one hash over a whole bundle's manifest, which is how a build
  says "this is the same set of assets as last time" in one comparison. Two
  clean cooks of an unchanged project produce the same hash; changing one asset
  changes it.
- `cookDuringBuild` is the same cook for a build hook to call, taking the
  platform as the hook spells it and handing back every file it read so the
  hook re-runs on an edit and is skipped otherwise. It names no hook types, so
  depending on `orblit_asset` does not pull `hooks` and `code_assets` into a
  project that only reads assets. It does not produce a data asset: those are
  master-channel only, so a cooked bundle ships as an ordinary Flutter asset
  and the project lists it under `flutter: assets:` until that changes.
- The command's exit code is what a build reads: 0 when everything cooked, 1
  when an asset failed, and 2 when the arguments were wrong. Failures are
  repeated at the end rather than only where they happened, because the one
  line that matters should not be a thousand lines up.

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
