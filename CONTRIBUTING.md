# Contributing

Orblit is early enough that the most useful thing you can do is try something
and report where it fell apart. A change that fixes what you hit is welcome;
so is an issue that only describes it.

## Before a pull request

Talk about anything large first, in an issue or on the
[Discord](https://discord.gg/5DH7HuDUtJ). The engine is moving quickly and
there is a real chance the thing you want to build is half-built on a branch
already, or is about to be made unnecessary by something else. Small fixes
need no ceremony — open the pull request.

## Checking your work

```sh
./tool/format.sh   # formatting
./tool/check.sh    # analyze and test everything that needs no window
```

Two things to know about `check.sh`. It deliberately skips the renderer,
which needs Flutter, a macOS host and a Filament download, and is checked by
being built instead. And a green `check.sh` is not a green CI: CI also draws
frames on real devices and builds the native side for each platform, so a
change can pass locally and still fail on push. Expect that, and watch the
run.

If you add a package, add it to the `PACKAGES` list in `tool/check.sh` too.
That list is how four packages once went a long while with three hundred tests
nobody was running. The script checks what is on disk against the list, so it
will tell you if you forget.

Networking, scripting and the examples live in their own repositories and
check themselves — see [orblit-net](https://github.com/ChxisB/orblit-net),
[orblit-script](https://github.com/ChxisB/orblit-script) and
[orblit-examples](https://github.com/ChxisB/orblit-examples).

## Setting up

[Installing](https://orblitengine.com/docs/start/installing/) has what each
machine needs and which machine can build for what. The native side needs one
setup step per platform, which downloads Google's Filament SDK and compiles
the materials.

## Licence

Orblit is under MPL-2.0. Opening a pull request means you are offering your
change under that same licence, and that you wrote it or otherwise have the
right to contribute it.

There is no CLA to sign and no copyright to assign. You keep the copyright on
what you write; it is simply licensed the same way as the rest of the
repository. In practice MPL asks nothing extra of you as a contributor — it
matters to people who fork Orblit, not to people who send changes to it.
