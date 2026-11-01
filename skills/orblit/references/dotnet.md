# Orblit in C#: packages, commands and traps

Orblit ships as .NET packages, under `dotnet/` in `ChxisB/orblit`, with
Avalonia for the window. The packages are pre-alpha, so check a name before
you write it. The source is `dotnet/Orblit.<Name>/*.cs` in the checkout. The
`///` comments are the reference. The docs site shows the C# for each guide,
at https://orblitengine.com/docs/guides/paths/ for example.

## Setting up

You need the .NET 10 SDK, Git, CMake and a C++ compiler (Xcode on a Mac). The
packages are not on nuget.org. The engine packs them into a folder, and a
project restores from it.

```sh
git clone https://github.com/ChxisB/orblit.git
cd orblit
packages/orblit_filament/darwin/setup.sh   # fetches Filament, compiles the materials
tool/build_native.sh                       # builds the native libraries
tool/pack_local.sh                         # packs into ~/.orblit/feed, or $ORBLIT_FEED
```

Build the natives before packing. A package packed first carries no native
library and the renderer fails to load.

A project then adds the feed once and the packages by name:

```sh
dotnet new console -o game && cd game
dotnet nuget add source "$HOME/.orblit/feed" --name orblit
dotnet add package Avalonia.Desktop --version 12.1.4
dotnet add package Avalonia.Themes.Fluent --version 12.1.4
dotnet add package Orblit.Filament.Avalonia
```

The package id is the namespace. `dotnet add package Orblit.Pathing` gives
`using Orblit.Pathing;`. Two other repositories hold packages:

- Physics is in `orblit-physics`, with its own `tool/pack_local.sh` into the
  same feed. Run its `tool/build_native.sh` first, for the same reason.
- `Orblit.Net` is in `orblit-net`, which has no pack script. Reference its
  project with a `ProjectReference`.

A project that restores from several sources can pin a package to one with
`RestoreAdditionalProjectSources` in `Directory.Build.props`, which is how the
sibling repositories find the engine feed.

The renderer has drawn on Apple silicon Macs only. Windows and Linux build the
packages, but no frame has drawn there. The packages that do not draw run
anywhere .NET does. Say so plainly when a user is on Windows or Linux.
[platforms.md](platforms.md) has the per-system setup.

## Package names

The id is `Orblit.` and the area's name. These have more than one package, or
a name that is not obvious:

| Id | What it holds |
| --- | --- |
| `Orblit.Filament` | The renderer over its C API, and the scene types it draws |
| `Orblit.Filament.Avalonia` | `RendererViewport`, the control that shows a renderer |
| `Orblit.Stage` | A scene document staged for the renderer: its materials, terrain, models, particles, sounds and cutscenes |
| `Orblit.Stage.Avalonia` | Interface in a staged scene: the pointer ray, name labels and the quad a panel shows on |
| `Orblit.Ui` | A game's words in each language: strings tables, language files and plural rules |
| `Orblit.Ui.Avalonia` | Avalonia controls: flex layout, the image loader, touch controls and the label atlas |
| `Orblit.Pathing` | Splines, a grid and a navmesh to search, and the followers that walk what they find |
| `Orblit.AudioDevice` | Plays an `Orblit.Audio` game on a real output device |
| `Orblit.VectorMath` | `Vector3`, `Matrix4`, `Quaternion` and the other double precision maths |
| `Orblit.Remote` | A control surface for a running debug game, over a loopback socket |
| `Orblit.Remote.Tool` | Serves that control surface to an assistant, as tools |
| `Orblit.Save`, `Orblit.Save.History` | Saves for a game server, and the versions of a saved key |
| `Orblit.Cook`, `Orblit.Pack`, `Orblit.Kit` | Cooking a project, one `.opack` file, and kits |

A type that shares a name with its package gets another name, because a
namespace wins over a type of the same name. `Orblit.Noise` holds `NoiseField`.
`Orblit.Sequence` holds `CutSequence`. `Orblit.Effect` holds `TimedEffect`.
[packages.md](packages.md) says what each package does and where to start.

## The renderer

A scene is a value that lists everything, and keys decide what is the same
object from frame to frame. The types are `RenderScene`, `RenderObject`,
`RenderLight` with `RenderLightKind`, `RenderCamera`, `Sky` and
`RenderMaterial`. [scene-api.md](scene-api.md) has their fields.

- **A scene is a `sealed record`.** Build a new one, or change one with
  `with`. `Objects` and `Camera` are `required`. `Lights` and the rest default
  to empty.
- **A frame is a call.** `viewport.Frame(scene, seconds)` describes the scene,
  draws it and shows the result. It returns false when nothing was drawn,
  because the control has no size yet or the screen is behind. Call it from
  `RequestAnimationFrame`.
- **Keys are `long`.** Objects, lights, probes and decals share one key space.
- **`Vector3` is `Orblit.VectorMath.Vector3`, a mutable class of doubles.** It
  is not `System.Numerics.Vector3`. A `with` copy shares the instance, so do
  not edit a vector that two scenes hold. `Clone()` makes a copy.
- **Colour is linear RGB.**
- **Members are PascalCase.** The scene types take an object initialiser:
  `new RenderLight { Key = 10, Kind = RenderLightKind.Directional }`. Most
  other types take constructor arguments, called by name.
- **An interface starts with `I`**, as in `IPanel`.

A window that draws a cube is the quickstart at
https://orblitengine.com/docs/start/installing-dotnet/. It builds and runs.

## Interfaces

Interfaces are Avalonia controls. Lay them out in XAML or in code, and put a
`RendererViewport` in the same tree where the scene should show.
`Orblit.Ui.Avalonia` holds the controls the engine ships, and
`Orblit.Stage.Avalonia` puts interface inside a staged scene.

## Commands and gates

In a game, `dotnet build` is the check, and `dotnet test` runs its tests.

In the engine checkout, `tool/check_dotnet.sh` runs the native build,
`dotnet build`, `dotnet test` and `dotnet format --verify-no-changes`. The
analyzer rules are build errors there, so the build is the lint. Run the C#
blocks on the docs site with `tool/check_snippets.sh --require-csharp` in
`orblit-site`. Without the flag, a machine with no feed skips the C# half and
says so.

Traps from running them:

- Tests run on the Microsoft Testing Platform. `dotnet test` takes
  `--project <dir>`, not a bare path, and `--nologo` makes it report zero tests.
  `dotnet run --project dotnet/<Name>.Tests` runs one test project. Read the
  count, because zero is not a pass.
- `--filter-class` and `--filter-method` together select the intersection,
  which is often nothing. Run them as two calls.
- Before a pack, check `dotnet/native/runtimes/osx-arm64/native` holds
  `liborblit_filament.dylib`. A pack without it ships no library and clears the
  cached copy that had one.
- A package repacked at the same version is not re-read by NuGet.
  `pack_local.sh` clears the cache entry of each package it packs. A
  hand-copied `.nupkg` does not, so bump the version.
- A project with a `<Version>` needs a bump and a line in its `CHANGELOG.md`
  when its `.cs`, `.axaml` or `.csproj` changes. `tool/check_versions.sh`
  checks that against the merge base, so it needs a commit on the branch.
- Windows checks out CRLF by default. The engine's `.gitattributes` keeps
  `dotnet/**` on LF so the byte comparisons hold.
