# C# games on the Web

The editor exports a C# game to a browser folder. The engine runs as one wasm
module and the game runs as a second one, from the .NET `browser-wasm` bundle.
The page connects them. `game.mjs` is that connection.

This folder holds the managed side.

- `Orblit.Web` is the library a game references. `WebGame` is the game
  object. `WebScript` is a script on a scene entity. `WebHost` starts both.
  `WebEngine` calls the engine.
- `sample` is a game with a grid of cubes and three scene scripts. One leaves the
  stage after two seconds. One is not registered, to show the error.

## Setup

An export needs two toolchains, and they stay apart.

- Emscripten 6.0.13 builds the engine template. It is only needed to build the
  template yourself.
- The .NET SDK 10.0.302 with the `wasm-tools` workload publishes the game.
  The workload brings its own Emscripten 3.1.56. It cannot share the first one.

Install the SDK into a folder of its own, so it never touches a system install.
The export finds `dotnet` on the `PATH`.

```bash
export DOTNET_ROOT="$HOME/.cache/orblit-build/dotnet"
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 10.0.302 --install-dir "$DOTNET_ROOT"
"$DOTNET_ROOT/dotnet" workload install wasm-tools
export PATH="$DOTNET_ROOT:$PATH"
```

## Export a game

1. Put a project file named `Name.web.csproj` in the project root. It uses
   `Microsoft.NET.Sdk.WebAssembly` and references `Orblit.Web`. Copy
   `sample/SampleGame.web.csproj`. To use another path, set `dotnet/web_project` in
   the Web export preset.
2. Export with the Web preset. The editor runs `dotnet publish -c Release`, which
   takes about 20 s with a warm cache, and the editor waits for it.
3. Serve the folder from any static server that sends `.wasm` and `.mjs` files with
   the right types. The editor's Run in Browser server does not serve `_framework`.

A custom HTML shell must carry `$ORBLIT_GAME_GLUE`. Without it no .NET starts.

## Write a game

`Main` registers the game and the script classes.

```csharp
Orblit.Web.WebHost.Register<Spinner>();
Orblit.Web.WebHost.Run(new SampleGame());
```

The game is optional. A project of scene scripts only can skip `Run`.

A scene attaches `Spinner.cs` to an entity as usual. The class name is the file
name. The engine runs no script on it. It notes the class name on the entity,
and the page runs the C# class:

- `OnStart` runs once when the entity enters the stage.
- `OnFrame` runs each frame.
- `OnExit` runs once when the entity leaves the stage.
- `Position` reads and writes the entity position. A write goes to the engine
  once per frame, in one call for every script.

A scene script that no `Register` call names logs one error and the entity runs
no C#. Properties set in the scene for a script are not passed to the C# class.
A script set on an entity that is already in the stage is not attached.

## Entities and handles

The game holds no engine pointer. `WebEngine.BindEntity("Main/Cube")` returns an
integer handle. Zero means no entity. `WebEngine.Release(handle)` frees it. A
handle carries a generation, so a released or stale handle never reaches the entity
that took its slot, and a second release does nothing. A script's own `Handle`
belongs to the engine, so do not release it.

A call crosses between two wasm memories. A 64 KB batch costs about 20 to 34 µs.
A scalar call costs about 0.12 to 0.17 µs. Pass many entities in one call.
