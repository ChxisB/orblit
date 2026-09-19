# Changelog

## 0.5.0

- **Asking where an asset stands without cooking it.** `Cook.stateOf` answers
  `cooked`, `stale`, `failed` or `ignored` for one asset, and `stateOfAll` for
  many at once, sharing the dependency hashes so a texture forty models depend
  on is read once. This is for the editor, which wants a mark beside every
  asset in a list and cannot run a build to get one.
- It reaches that answer down the same path a cook does — resolve the settings,
  pick the importer, hash the source and everything under it, build the key —
  and stops at the cache lookup instead of running the importer. An editor that
  worked the answer out its own way would drift from the build the first time
  an importer changed how it resolves a setting, and a stale mark on a fresh
  asset is worse than no mark at all.
- `CookProject.statesOf` asks the same question for a whole project, or for a
  few assets of it, through the project's own assets folder, cache and
  importers — so the mark an editor shows is decided by what the build will
  actually do.
- **Cooks remember what would not cook.** A cook that fails files the reason in
  the cache against the key it failed under, and `CookCache.lookUpFailure`
  reads it back, so an editor can mark a broken asset without running the
  importer that breaks on it. Recording is not refusing: no cook consults one
  before working, because importers fail for reasons that are not in the key —
  a tool that was not installed, a full disk, a killed process — and a cache
  that treated those as settled would turn a bad afternoon into a project that
  never builds again. Cooking a key clears its failure, and so does changing
  the asset, since that is different work.

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
- **Packing sprites into atlases, as part of the cook.** `AtlasImporter`
  claims a small document — `ui.atlas.json` — naming the sprites it wants
  packed, and runs `orblit_sprite`'s packer over them. An atlas is not a file
  somebody has but one they want made, so the document is the asset and the
  sprites are its dependencies: adding a sprite edits the document, editing a
  sprite changes a dependency, and either repacks while nothing else moves.
- Sprites are listed rather than globbed. A glob would make the cook's answer
  depend on what happened to be in a folder, which is the one thing a
  content-addressed cache cannot key on, and it would pick up the stray `.png`
  somebody left there mid-export.
- Pages come out as PNG and a descriptor per page, not as compressed textures.
  Packing and encoding are separate jobs, and the pages want cooking per target
  like any other picture — doing it here would mean doing it twice.
- A sprite too large for a page fails the atlas rather than being noted. An
  atlas that packed nine of ten sprites gives a game that draws nothing where
  the tenth was, and the first anyone hears of it is at run time.
- **Importing a file a user hands the app.** `RuntimeImport` is the same cook
  with the parts a project supplies replaced by the parts a running app has:
  the device's cache, the device's own target, and one file somebody chose
  instead of a folder full of them. Because the key covers the file's bytes,
  importing the same file twice is free, and so is importing one the build
  machine already cooked.
- `RuntimeImport.nameFor` turns whatever a file picker hands back — a
  container path, a Windows path, a URL with a query on it — into an asset id,
  keeping the part the user would recognise and dropping the machine's
  directory layout, which is the thing an id exists not to carry.
- A `.gltf` a user picked on its own is a fragment, since it names its buffers
  and images in other files. `alongside` is where those go, so the same
  importer that works in a build works on a folder somebody dropped in.
- Importers that drive a command-line tool are still registered at runtime,
  because a desktop app is a running app and has them. A phone gets a failure
  naming the tool it could not find rather than a file that imported to
  nothing.
- `currentCookTarget` is what the machine this is running on wants, since a
  runtime import has no target to choose and offering the choice would only be
  a way to get it wrong.
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
