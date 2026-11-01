# Contributing

Orblit is early enough that the most useful thing you can do is try something
and report where it fell apart. A change that fixes what you hit is welcome.
So is an issue that only describes it.

## Before a pull request

Talk about anything large first, in an issue or on the
[Discord](https://discord.gg/5DH7HuDUtJ). The engine is moving quickly, and
there is a real chance the thing you want to build is half-built on a branch
already, or about to be made unnecessary by something else. Small fixes need no
ceremony: just open the pull request.

## Checking your work

```sh
./tool/check_dotnet.sh   # build, test and format-check everything under dotnet/
```

Two things to know about `check_dotnet.sh`. First, it opens no window, so
what a viewport looks like is checked by running an example. Second, a green
`check_dotnet.sh` is not a green CI. CI runs the same script on Linux, macOS
and Windows, so a change can pass locally and still fail on push. Expect that,
and watch the run.

The script builds `dotnet/Orblit.sln`, so a project that is not in it is never
built or tested.

Networking and the examples live in their own repositories and check
themselves. See [orblit-net](https://github.com/ChxisB/orblit-net) and
[orblit-examples](https://github.com/ChxisB/orblit-examples).

## Setting up

[Installing](https://orblitengine.com/docs/start/installing/) has what each
machine needs, and which machine can build for what. The native side needs one
setup step per platform. It downloads Google's Filament SDK and compiles the
materials.

## Licence

Orblit is under MPL-2.0. Opening a pull request means you are offering your
change under that same licence, and that you wrote it or otherwise have the
right to contribute it.

There is no CLA to sign and no copyright to assign. You keep the copyright on
what you write. It is simply licensed the same way as the rest of the
repository. In practice MPL asks nothing extra of you as a contributor. It
matters to people who fork Orblit, not to people who send changes to it.
