# Changelog

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
