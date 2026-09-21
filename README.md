<p align="center">
  <a href="https://orblitengine.com">
    <img alt="Orblit" width="220px" src="https://raw.githubusercontent.com/ChxisB/orblit/main/brand/orblit-logo-full.png">
  </a>
</p>

<p align="center"><b>A 3D game engine you write in Dart</b></p>

<p align="center">Game logic and interface in Flutter. An archetype entity-component core in C++. Google&rsquo;s Filament does the rendering. The viewport is a widget, so the menu over it is an ordinary <code>Column</code>.</p>

<p align="center">
  <a title="CI" href="https://github.com/ChxisB/orblit/actions/workflows/ci.yaml?query=event%3Apush+branch%3Amain"><img src="https://github.com/ChxisB/orblit/actions/workflows/ci.yaml/badge.svg?branch=main&event=push"/></a>
  <a title="Licence" href="LICENSE"><img src="https://img.shields.io/badge/licence-MPL--2.0-blue"/></a>
  <img alt="Pre-alpha" src="https://img.shields.io/badge/status-pre--alpha-orange"/>
  <a title="Discord" href="https://discord.gg/5DH7HuDUtJ"><img src="https://img.shields.io/badge/Discord-Join-5865F2?logo=discord&logoColor=white"/></a>
</p>

<p align="center"><a href="https://orblitengine.com">Website</a> · <a href="https://orblitengine.com/docs/">Docs</a> · <a href="https://orblitengine.com/docs/start/installing/">Install</a> · <a href="https://orblitengine.com/docs/gallery/basics/">Gallery</a> · <a href="https://github.com/ChxisB/orblit-examples">Examples</a> · <a href="#showcases">Showcases</a> · <a href="https://discord.gg/5DH7HuDUtJ">Discord</a></p>

<p align="center">
  <img alt="The Amazon Lumberyard Bistro at night, a hundred point lights standing where the artist put the lamps, drawn by Orblit" width="600px" src="https://orblitengine.com/showcase/hero-bistro.jpg">
</p>

<p align="center">
  <img alt="A hundred thousand instanced objects animating at once" width="600px" src="https://orblitengine.com/showcase/a-hundred-thousand.jpg">
</p>

<p align="center">
  <img alt="A robot running and jumping hurdles in the runner example, played by its own autopilot" width="600px" src="https://orblitengine.com/showcase/runner.jpg">
</p>

## At a glance

- **Rendering.** Filament's physically based materials, and a tone mapper that behaves like a camera. Image-based lighting from HDR or EXR. Directional, point and spot lights, cascaded shadows, and irradiance fields for bounced light sampled through a volume. A post stack with bloom, volumetric light, motion blur, occlusion, decals and selection outlines. Gaussian splats, instancing and populations.
- **Simulation.** An archetype entity-component store written in C++ behind a C ABI, with a transform hierarchy. Component data reaches Dart as a view over the store's own memory rather than a copy, so reading a column costs nothing to translate, and writing to one writes to the store.
- **The scene is a widget.** `OrblitView` sits in the same widget tree as everything else, takes part in the same layout, and is drawn by the same compositor. On Apple platforms the renderer draws into an IOSurface-backed buffer that Flutter takes as it is: no readback, no copy through the CPU. A panel can overlap the viewport, and the inventory screen has a widget test.
- **Games.** Steering behaviours and behaviour trees. Armatures with poses and bone constraints. Camera shots that say what to frame, and blend between them. Collision. Sampled time, so the frame rate does not change the simulation. Multiplayer that replicates component columns. And TypeScript scripting on QuickJS, as a peer of Dart over the same core.
- **2D.** Sprites in layers, atlas packing, parallax and tile maps, in the same scene as the 3D.
- **Tooling.** A desktop editor. A gallery app that shows every technique one at a time, beside the lines that do it. A build-time asset pipeline that cooks KTX2 texture sets per device. And a Claude Code skill, so an assistant writes real API instead of guessing.
- **Formats.** glTF, with FBX and OBJ converted on the way in. An `.oscene` scene document with migrations and diffs. KTX2 compressed textures, HDR and EXR environments, and `.ply`, `.spz` and `.osplat` splat captures.
- **Platforms.** macOS, iOS, Android and the web draw today. Linux draws, though not yet on a real GPU. Windows builds on every change, and nothing has drawn on it yet. [Platform support](https://orblitengine.com/docs/reference/platform-support/) is fussy about that difference on purpose.

## Showcases

Every one of these is a page in the gallery app, running on a real machine. The
[showcases page](https://orblitengine.com/docs/gallery/showcases/) carries the
lines that do it, lifted from each example's own source.

| Showcase | What it is |
| --- | --- |
| **Blocks** | A landscape of sixty thousand cubes you can walk around and dig into, generated and sent once. |
| **Runner** | A runner you can play: dodge, jump, slide and pick up coins. An autopilot plays it well enough to show the game is fair. |
| **Bistro exterior** | Somebody else's street, lit by this engine. A hundred lights at night. |
| **Bistro interior** | The room, and the one place where missing bounced light shows. |

To open the gallery on one:

```sh
ORBLIT_EXAMPLE='runner' flutter run -d macos
```

[Running the examples](https://orblitengine.com/docs/examples/running-them/)
covers getting there from a clean checkout.

## Getting started

The packages are not on pub.dev yet, so they resolve from git:

```yaml
dependencies:
  flutter:
    sdk: flutter

  # Vectors and matrices. Orblit takes and returns these types rather than
  # defining its own, so it is a direct dependency of yours too.
  vector_math: ^2.1.4

  orblit_filament:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_filament
```

The renderer's native side needs one setup step per platform. It downloads
Google's Filament SDK and compiles the materials.
[Installing](https://orblitengine.com/docs/start/installing/) has what each
machine needs, and which machine can build for what.
[Your first scene](https://orblitengine.com/docs/start/your-first-scene/) is the
shortest path to a frame.

```sh
./tool/check.sh    # analyze and test everything that needs no window
```

## What is in here

| Package | What it is |
| --- | --- |
| `orblit_core` | Archetype entity-component store in C++ behind a C ABI, with a transform hierarchy. Component data reaches Dart as views, not copies. |
| `orblit_codegen` | Turns annotated component classes into registration code, and a manifest other front ends can read without compiling this package. |
| `orblit_filament` | Filament rendering composited by Flutter's texture registry. |

Those are the three the rest is built on. The other eighteen cover geometry,
rigging, agents, cameras, lighting, sprites, scene files, UI and weather. They
are all listed in the
[package reference](https://orblitengine.com/docs/reference/packages/).

## The rest of Orblit

| Repository | What it is |
| --- | --- |
| [`orblit-net`](https://github.com/ChxisB/orblit-net) | Multiplayer. Replicates component columns, with ownership rules and interpolation. |
| [`orblit-script`](https://github.com/ChxisB/orblit-script) | TypeScript scripting on QuickJS, as a peer of Dart over the same core. |
| [`orblit-examples`](https://github.com/ChxisB/orblit-examples) | Worked examples of what the engine does, and how. |

Design notes live outside these repositories, as Claude artifacts, so a
checkout carries what it needs to build and run, and nothing else.

Pre-alpha. Nothing here is stable.

## With an AI assistant

The documentation at [orblitengine.com](https://orblitengine.com) is also
served as Markdown for language models, starting from
[`llms.txt`](https://orblitengine.com/llms.txt). For Claude Code, this
repository is a plugin marketplace holding one skill,
[`skills/orblit`](skills/orblit). It carries the engine's model and its traps,
and makes the assistant check names against the source before it writes them.
In Claude Code:

```text
/plugin marketplace add ChxisB/orblit
/plugin install orblit@orblit
```

[Working with AI assistants](https://orblitengine.com/docs/start/working-with-ai/)
has the rest.

## Come and break it

It is early enough that the first thing you try is probably something nobody
has tried yet. Either way, we learn something. The
[Discord](https://discord.gg/5DH7HuDUtJ) is where that conversation happens, and
[CONTRIBUTING](CONTRIBUTING.md) has what to run before a pull request.

## Licence

MPL-2.0, © 2026 Chris Beckett. That is the Mozilla Public License, and it is
open source. Fork it, change it, and ship games made with it, including
commercial ones: your game is your own, and the licence does not reach into it.
What it asks is that changes to Orblit's own files ship under the same licence,
with source available to whoever you hand the result to, so engine work stays
in the open.

Builds link Filament, which carries its own Apache 2.0 licence, and a prebuilt
Filament.xcframework is committed here, so a full copy of that licence travels
with it. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
