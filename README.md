# Orblit Engine

A Dart-first 3D game engine. Game logic and UI in Flutter, an entity-component
core in C++, and Google's [Filament](https://github.com/google/filament) doing
the rendering.

```sh
./tool/check.sh    # analyze and test everything that needs no window
```

| Package | What it is |
| --- | --- |
| `orblit_core` | Archetype entity-component store in C++ behind a C ABI, with a transform hierarchy. Component data reaches Dart as views, not copies. |
| `orblit_codegen` | Turns annotated component classes into registration and a manifest other front ends read without compiling this package. |
| `orblit_filament` | Filament rendering composited by Flutter's texture registry. macOS so far. |

## The rest of Orblit

| Repository | What it is |
| --- | --- |
| [`orblit-net`](https://github.com/ChxisB/orblit-net) | Multiplayer. Replicates component columns, with ownership rules and interpolation. |
| [`orblit-script`](https://github.com/ChxisB/orblit-script) | TypeScript scripting on QuickJS, as a peer of Dart over the same core. |
| [`orblit-examples`](https://github.com/ChxisB/orblit-examples) | Worked examples of what the engine does, and how. |

Design notes live outside these repositories, as Claude artifacts, so a
checkout carries what it needs to build and run and nothing else.

Pre-alpha. Nothing here is stable.

## Licence

FSL-1.1-MIT, © 2026 Chris Beckett — the Functional Source License, with an MIT
future. Fork it, change it, send changes back, and ship games made with it,
commercial ones included. What it rules out is offering Orblit, or a renamed
copy of it, as a competing product. Each release becomes plain MIT two years
after it's published.

Builds link Filament, which carries its own Apache 2.0 licence, and a prebuilt
Filament.xcframework is committed here — so a full copy of that licence travels
with it. See [LICENSE](LICENSE).
