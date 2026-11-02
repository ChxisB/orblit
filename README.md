<p align="center">
  <a href="https://orblitengine.com">
    <img alt="Orblit" width="220px" src="https://raw.githubusercontent.com/ChxisB/orblit/main/brand/orblit-logo-full.png">
  </a>
</p>

<p align="center"><b>A 3D game engine you write in C#</b></p>

<p align="center">Game logic and interface in C# on Avalonia. An archetype entity-component core in C++. Google&rsquo;s Filament does the rendering. The viewport is a control, so the menu over it is an ordinary <code>StackPanel</code>.</p>

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
- **Simulation.** An archetype entity-component store written in C++ behind a C ABI, with a transform hierarchy. Component data reaches C# as a view over the store's own memory rather than a copy, so reading a column costs nothing to translate, and writing to one writes to the store.
- **The scene is a control.** `RendererViewport` sits in the same Avalonia tree as everything else, takes part in the same layout, and is drawn by the same compositor. On macOS the renderer draws into an IOSurface-backed buffer that the compositor reads as a texture: no readback, no copy through the CPU. A panel can overlap the viewport.
- **Games.** Steering behaviours and behaviour trees. Armatures with poses and bone constraints. Camera shots that say what to frame, and blend between them. Collision. Sampled time, so the frame rate does not change the simulation. Multiplayer that replicates component columns. And native C++ scripts that the engine loads as shared libraries, over the same core.
- **2D.** Sprites in layers, atlas packing, parallax and tile maps, in the same scene as the 3D.
- **Tooling.** A desktop editor. A gallery app that shows every technique one at a time, beside the lines that do it. A build-time asset pipeline that cooks KTX2 texture sets per device. And a Claude Code skill, so an assistant writes real API instead of guessing.
- **Formats.** glTF, with FBX and OBJ converted on the way in. An `.oscene` scene document with migrations and diffs. KTX2 compressed textures, HDR and EXR environments, and `.ply`, `.spz` and `.osplat` splat captures.
- **Platforms.** macOS, Windows and Linux first. Every change builds the renderer and measures a drawn frame on Apple silicon, Linux x64 and Windows x64. The Linux and Windows frames come from Mesa's software Vulkan driver, because a CI runner has no GPU, so no real graphics card there has drawn one. [Platform support](https://orblitengine.com/docs/reference/platform-support/) is fussy about that difference on purpose.

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

To open the gallery on one, from a checkout of
[`orblit-examples`](https://github.com/ChxisB/orblit-examples):

```sh
ORBLIT_EXAMPLE='runner' dotnet run --project dotnet/Orblit.Gallery -c Release
```

[Running the examples](https://orblitengine.com/docs/examples/running-them/)
covers getting there from a clean checkout.

## Getting started

The packages are not on NuGet yet. `tool/pack_local.sh` packs them into a
local feed, `~/.orblit/feed` or the folder `ORBLIT_FEED` names, and a project
restores from there:

```sh
git clone https://github.com/ChxisB/orblit.git
orblit/tool/build_native.sh   # the native core and renderer
orblit/tool/pack_local.sh     # packs the engine into ~/.orblit/feed
```

```xml
<PropertyGroup>
  <RestoreAdditionalProjectSources>$([System.Environment]::GetFolderPath(SpecialFolder.UserProfile))/.orblit/feed</RestoreAdditionalProjectSources>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Orblit.Filament.Avalonia" Version="0.1.0" />
</ItemGroup>
```

The renderer's native side needs one setup step per platform. It downloads
Google's Filament SDK and compiles the materials.
[Installing](https://orblitengine.com/docs/start/installing/) has what each
machine needs, and which machine can build for what.
[Your first scene](https://orblitengine.com/docs/start/your-first-scene/) is the
shortest path to a frame.

```sh
./tool/check_dotnet.sh    # build, test and format-check everything under dotnet/
```

## What is in here

| Package | What it is |
| --- | --- |
| `Orblit.Core` | Archetype entity-component store in C++ behind a C ABI, with a transform hierarchy. Component data reaches C# as views, not copies. |
| `Orblit.Codegen` | Turns annotated component classes into registration code, and a manifest other front ends can read without compiling this package. |
| `Orblit.Filament` | Filament rendering behind a C ABI, with the managed binding over it. |
| `Orblit.Filament.Avalonia` | A control that shows an Orblit renderer, by the fastest path each system offers. |

Those are the four the rest is built on. The others cover geometry, rigging,
animation, agents, cameras, lighting, sprites, scene files, UI and weather.
They are all listed in the
[package reference](https://orblitengine.com/docs/reference/packages/).

## The rest of Orblit

| Repository | What it is |
| --- | --- |
| [`orblit-net`](https://github.com/ChxisB/orblit-net) | Multiplayer. Replicates component columns, with ownership rules and interpolation. |
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

Builds link Filament, which carries its own Apache 2.0 licence. See
[LICENSE](LICENSE) and [NOTICE](NOTICE).
