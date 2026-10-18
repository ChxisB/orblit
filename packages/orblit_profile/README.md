# orblit_profile

Where a frame went on the Dart side. Named spans, gauges, counters, a ring of
frame times and a heap trend, as a snapshot a tool can read.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_profile:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_profile
```

Needs Dart alone. Runs anywhere Dart runs, the web included.

```dart
final profiler = Profiler(heap: () => ProcessInfo.currentRss);

void onFrame() {
  profiler.frame();
  final started = profiler.begin();
  update();
  profiler.end('update', started);
  profiler.gauge('entities', entities.length.toDouble());
}
```

`Profiler.off()` takes the same calls and records nothing. The snapshot is
plain JSON.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
