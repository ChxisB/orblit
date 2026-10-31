# Orblit in C#: packages, commands and what differs from Dart

Orblit also ships as .NET packages, under `dotnet/` in `ChxisB/orblit`, with
Avalonia for the window. They follow the Dart packages one for one, and the
Dart text in this skill stays the guide to what each package is for. This file
covers what is different in C#.

The packages are pre-alpha like the rest, so check a name before you write it.
The source is `dotnet/Orblit.<Name>/*.cs` in the checkout. The `///` comments
are the reference. The docs site shows a C# block beside each Dart block that
has one, at https://orblitengine.com/docs/guides/paths/ for example.

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
`using Orblit.Pathing;`. Physics is built from `orblit-physics` and has no pack
script yet.

The renderer has drawn on Apple silicon Macs only. Windows and Linux build the
packages, but no frame has drawn there. The packages that do not draw run
anywhere .NET does. Say so plainly when a user is on Windows or Linux.

## Dart package to C# package

The id is `Orblit.` and the Dart name in PascalCase, with these exceptions.

| Dart | C# |
| --- | --- |
| `orblit_filament` | `Orblit.Filament`, and `Orblit.Filament.Avalonia` for the control |
| `orblit_stage` | `Orblit.Stage`, and `Orblit.Stage.Avalonia` |
| `orblit_ui` | `Orblit.Ui`, and `Orblit.Ui.Avalonia` |
| `orblit_path` | `Orblit.Pathing` |
| `orblit_audio_device` | `Orblit.AudioDevice` |
| `vector_math` | `Orblit.VectorMath` |

A type that shares a name with its package is renamed, because a namespace
wins over a type of the same name. `Orblit.Noise` holds `NoiseField`.
`Orblit.Sequence` holds `CutSequence`. `Orblit.Effect` holds `TimedEffect`.

## The renderer

| Dart | C# |
| --- | --- |
| `OrblitView` widget | `RendererViewport`, an Avalonia `Control` |
| `OrblitScene` | `RenderScene` |
| `OrblitObject` | `RenderObject` |
| `OrblitLight`, `OrblitLightKind` | `RenderLight`, `RenderLightKind` |
| `OrblitCamera` | `RenderCamera` |
| `OrblitSky` | `Sky` |
| `OrblitMaterial` | `RenderMaterial` |

The model is the same. A scene is a value that lists everything, and keys
decide what is the same object from frame to frame. What changes is how it is
sent.

- **A scene is a `sealed record`.** Build a new one, or change one with
  `with`. `Objects` and `Camera` are `required`. `Lights` and the rest default
  to empty.
- **A frame is a call.** `viewport.Frame(scene, seconds)` describes the scene,
  draws it and shows the result. It returns false when nothing was drawn,
  because the control has no size yet or the screen is behind. Call it from
  `RequestAnimationFrame`.
- **Keys are `long`.** Objects, lights, probes and decals share one key space,
  as in Dart.
- **`Vector3` is `Orblit.VectorMath.Vector3`, a mutable class of doubles.** It
  is not `System.Numerics.Vector3`. A `with` copy shares the instance, so do
  not edit a vector that two scenes hold. `Clone()` makes a copy.
- **Colour is linear RGB**, as in Dart.
- **Members are PascalCase**, and a Dart named argument is an object
  initialiser: `new RenderLight { Key = 10, Kind = RenderLightKind.Directional }`.
- **A Dart `abstract interface class Foo` is `IFoo`.**

A window that draws a cube is the quickstart at
https://orblitengine.com/docs/start/installing-dotnet/. It builds and runs.

## Interfaces

Interfaces are Avalonia controls. Lay them out in XAML or in code, and put a
`RendererViewport` in the same tree where the scene should show.
`Orblit.Ui.Avalonia` and `Orblit.Stage.Avalonia` hold the controls the engine
ships.

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
  Read the count, because zero is not a pass.
- `--filter-class` and `--filter-method` together select the intersection,
  which is often nothing. Run them as two calls.
- Before a pack, check `dotnet/native/runtimes/osx-arm64/native` holds
  `liborblit_filament.dylib`. A pack without it ships no library and clears the
  cached copy that had one.
- A package repacked at the same version is not re-read by NuGet.
  `pack_local.sh` clears the cache entry of each package it packs. A
  hand-copied `.nupkg` does not, so bump the version.
