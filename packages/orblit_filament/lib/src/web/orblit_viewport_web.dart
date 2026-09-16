// One renderer drawing into one <canvas> — the web's OrblitViewport.kt.
//
// The shape is forced by how a Flutter web platform view actually arrives,
// which is not how a texture does. On Apple and Android `create` can build the
// renderer there and then, because the texture registry hands out a surface
// synchronously. Here the canvas does not exist yet when `create` is answered:
// it is made later, by the view factory, when the widget carrying the
// `HtmlElementView` is built — and even then it is detached from the document,
// with no size, until Flutter has laid it out.
//
// So `create` mints an id and nothing else, and everything real happens once
// the canvas is both attached and measured. A scene that arrives before then
// is held (`_pending`) and applied the moment the renderer starts, which is
// what makes a static scene set before the first layout still appear — the
// same guarantee `_sync` gives on the other platforms by awaiting `create`.
library;

import 'dart:async';
import 'dart:js_interop';
import 'dart:typed_data';

import 'package:web/web.dart' as web;

import '../device_profile.dart';
import '../models.dart' show OrblitAssetInfo;
import 'orblit_module.dart';
import 'orblit_scene_web.dart';

/// WebGL 2, which is the only backend this build's materials were compiled
/// for. `OrblitBackend` in orblit_renderer.h: 3 is OPENGL.
const int _backendOpenGl = 3;

/// One viewport: a canvas, a module instance, and the frame loop over them.
class OrblitWebViewport {
  OrblitWebViewport(this.id, {Map<String, Uint8List> resources = const {}})
    : _resources = resources;

  /// Everything provided so far, shared with the plugin that owns it, so a
  /// module that starts late is handed what arrived before it.
  final Map<String, Uint8List> _resources;

  /// This viewport's number — the "textureId" of the channel contract, minted
  /// by the plugin rather than by a texture registry.
  final int id;

  /// The element id the canvas is given, and the CSS selector the renderer is
  /// handed. `OrblitSurfaceWeb.cpp` keeps that selector for the swap chain's
  /// whole life and Filament's `PlatformWebGL` carries it as an opaque
  /// identity, so it has to stay unique per canvas and stay valid.
  String get elementId => 'orblit-filament-$id';

  web.HTMLCanvasElement? _canvas;
  OrblitModule? _module;
  int _renderer = 0;
  bool _starting = false;
  bool _disposed = false;

  /// The size the host last asked for, in physical pixels.
  int _width = 1;
  int _height = 1;

  /// What the canvas's backing store currently is, so the renderer is only
  /// told about a size that actually moved.
  int _backingWidth = 0;
  int _backingHeight = 0;

  double? _startedAt;

  /// A scene that arrived before the renderer existed, applied at start.
  OrblitSceneWeb? _pending;

  /// Every call still waiting for a scene to be applied: one per scene that
  /// arrived before the renderer existed, the newest of which is [_pending].
  ///
  /// Answered only once a scene is really in. The view marks a population's,
  /// a splat cloud's or a sprite layer's bytes as delivered when this call
  /// returns, and sends them again only when their revision moves — so
  /// answering straight away, for a scene about to be replaced by the next
  /// frame's, told it bytes had arrived that never did. A sprite backdrop sent
  /// once came up empty on the web and nowhere else. Held like this, every
  /// scene sent before the renderer starts still carries its bytes, and the
  /// one applied at start has all of them.
  final List<Completer<Map<String, String>>> _waiting = [];

  /// The notes from the last scene applied, as `setScene` answers with.
  Map<String, String> _notes = const {};

  bool get isRunning => _renderer != 0;

  /// Makes the canvas this viewport draws into. Called by the view factory,
  /// which runs inside the engine with the element still detached — so this
  /// only builds the element and starts watching for it to be laid out.
  web.HTMLCanvasElement createCanvas() {
    final canvas = web.document.createElement('canvas') as web.HTMLCanvasElement
      ..id = elementId;
    // Flutter sizes the platform view's box; the canvas fills it. Its
    // backing store is a separate matter entirely — see _fitBackingStore.
    canvas.style
      ..width = '100%'
      ..height = '100%'
      ..display = 'block';
    _canvas = canvas;
    _whenLaidOut();
    return canvas;
  }

  /// Polls with `requestAnimationFrame` until the canvas is actually in the
  /// document and has a size, then starts the renderer.
  ///
  /// A platform view's element is detached when the factory returns it, and
  /// `Engine::create` needs a canvas it can find by selector — the swap chain
  /// is built from `document.querySelector(selector)` on Filament's side. A
  /// renderer built against a detached or zero-sized canvas either fails to
  /// start or starts at the HTML default 300x150 and never corrects.
  void _whenLaidOut() {
    if (_disposed) return;
    final canvas = _canvas;
    if (canvas == null) return;
    if (canvas.isConnected &&
        canvas.clientWidth > 0 &&
        canvas.clientHeight > 0) {
      unawaited(_start());
      return;
    }
    web.window.requestAnimationFrame(
      (JSNumber _) {
        _whenLaidOut();
      }.toJS,
    );
  }

  Future<void> _start() async {
    if (_disposed || _starting || _renderer != 0) return;
    _starting = true;
    final canvas = _canvas;
    if (canvas == null) return;
    try {
      _fitBackingStore();
      // One module instance per canvas: Emscripten's GL emulation binds its
      // WebGL context to whatever `Module.canvas` was, so two viewports need
      // two instances rather than one shared one.
      final module = await loadOrblitModule(canvas);
      if (_disposed) return;
      _module = module;
      for (final provided in _resources.entries) {
        _provideTo(module, provided.key, provided.value);
      }

      // Straight through orblit_web_create_on_canvas, which makes the WebGL 2
      // context current before calling the unmodified orblit_renderer_create —
      // nothing else in this build creates one (see orblit_web_host.cpp).
      final heap = OrblitHeap(module);
      try {
        _renderer = orblitCall(module, 'orblit_web_create_on_canvas', [
          _backendOpenGl,
          heap.string('#$elementId'),
          _backingWidth,
          _backingHeight,
        ]);
      } finally {
        heap.free();
      }
      if (_renderer == 0) {
        _answerWaiting(const {});
        web.console.error(
          '[orblit] the renderer would not start on #$elementId — see the '
                  'console for what Filament refused and why.'
              .toJS,
        );
        return;
      }

      final pending = _pending;
      _pending = null;
      if (pending != null) _answerWaiting(_applyNow(pending));

      web.window.requestAnimationFrame(_frame.toJS);
    } finally {
      _starting = false;
    }
  }

  /// Matches the canvas's backing store to its laid-out size.
  ///
  /// Flutter resizes the platform view's CSS box and never touches the
  /// element's `width`/`height` attributes, which are what WebGL actually
  /// draws into. Left alone they stay at the HTML default of 300x150 however
  /// large the view is on screen, so this is checked every frame rather than
  /// only when the host calls `resize`.
  bool _fitBackingStore() {
    final canvas = _canvas;
    if (canvas == null) return false;
    final ratio = web.window.devicePixelRatio;
    var width = (canvas.clientWidth * ratio).round();
    var height = (canvas.clientHeight * ratio).round();
    // Before layout there is nothing to measure; fall back to what the host
    // asked for rather than to the HTML default.
    if (width <= 0 || height <= 0) {
      width = _width;
      height = _height;
    }
    if (width == _backingWidth && height == _backingHeight) return false;
    _backingWidth = width;
    _backingHeight = height;
    canvas.width = width;
    canvas.height = height;
    return true;
  }

  void _frame(JSNumber timestamp) {
    if (_disposed || _renderer == 0) return;
    final module = _module;
    if (module == null) return;

    if (_fitBackingStore()) {
      orblitCall(module, 'orblit_renderer_resize', [
        _renderer,
        _backingWidth,
        _backingHeight,
      ]);
    }

    // Seconds since this viewport's first frame, the same quantity Android's
    // Choreographer delta and Apple's CFAbsoluteTimeGetCurrent offset are.
    // A scene's own animation rides on the camera's `at` instead.
    final now = timestamp.toDartDouble / 1000.0;
    _startedAt ??= now;
    orblitCall(module, 'orblit_renderer_draw', [_renderer, now - _startedAt!]);

    web.window.requestAnimationFrame(_frame.toJS);
  }

  /// The host's requested size, in physical pixels. The canvas's own laid-out
  /// size still wins each frame; this only matters before the first layout.
  void resize(int width, int height) {
    if (width <= 0 || height <= 0) return;
    _width = width;
    _height = height;
  }

  /// Applies a scene, or holds it until the renderer exists and answers then.
  Future<Map<String, String>> applyScene(OrblitSceneWeb scene) {
    if (_renderer == 0 || _module == null) {
      _pending = scene;
      final applied = Completer<Map<String, String>>();
      _waiting.add(applied);
      return applied.future;
    }
    return Future.value(_applyNow(scene));
  }

  void _answerWaiting(Map<String, String> notes) {
    for (final waiting in _waiting) {
      waiting.complete(notes);
    }
    _waiting.clear();
  }

  Map<String, String> _applyNow(OrblitSceneWeb scene) {
    final module = _module!;
    scene.applyTo(module, _renderer);
    final was = _notes;
    _notes = _readNotes(module);
    // Only when they change. A scene is published on every frame of an
    // animation, and a note that is still true sixty times a second says
    // nothing the first one did not — it only buries everything else in the
    // console, including the refusals above.
    if (!_sameNotes(was, _notes)) {
      for (final note in _notes.entries) {
        // A description of a model is not a refusal, and a skinned
        // character's is kilobytes of joint names; it reaches the host through
        // OrblitView.onAssetInfo, not the console.
        if (note.key.startsWith(OrblitAssetInfo.notePrefix)) continue;
        // The same mechanism every other host reads a refusal through, said
        // where a browser capture records it beside the frame.
        web.console.warn('[orblit] [${note.key}] ${note.value}'.toJS);
      }
    }
    return _notes;
  }

  /// What the scene asked for that could not be given, through the same
  /// `orblit_renderer_notes`/`orblit_renderer_note` pair every other host reads
  /// it through.
  Map<String, String> _readNotes(OrblitModule module) {
    final count = orblitCall(module, 'orblit_renderer_notes', [_renderer]);
    if (count <= 0) return const {};
    final notes = <String, String>{};
    final heap = OrblitHeap(module);
    try {
      // Two `const char *` out-parameters, side by side.
      final out = heap.ints(const [0, 0]);
      for (var i = 0; i < count; i++) {
        final ok = orblitCall(module, 'orblit_renderer_note', [
          _renderer,
          i,
          out,
          out + 4,
        ]);
        if (ok != 0) continue;
        final heapU32 = module.HEAPU32.toDart;
        final about = module.UTF8ToString(heapU32[out >> 2]);
        final saying = module.UTF8ToString(heapU32[(out >> 2) + 1]);
        notes[about] = saying;
      }
    } finally {
      heap.free();
    }
    return notes;
  }

  /// Whether two snapshots of the notes say the same thing.
  static bool _sameNotes(Map<String, String> before, Map<String, String> now) {
    if (before.length != now.length) return false;
    for (final note in before.entries) {
      if (now[note.key] != note.value) return false;
    }
    return true;
  }

  /// Every orblit_capability, in order, or null until the renderer has
  /// started — which on the web is a frame or two after the view is laid out.
  List<int>? capabilities() {
    final module = _module;
    if (_renderer == 0 || module == null) return null;
    return [
      for (final which in OrblitCapability.values)
        orblitCall(module, 'orblit_renderer_capability', [
          _renderer,
          which.index,
        ]),
    ];
  }

  /// Hands [bytes] to this viewport's module if it has one; a module that
  /// starts later is handed them as it starts.
  void provideResource(String name, Uint8List bytes) {
    final module = _module;
    if (module != null) _provideTo(module, name, bytes);
  }

  void releaseResource(String name) {
    final module = _module;
    if (module == null) return;
    orblitCall(module, 'orblit_renderer_release_resource', [name]);
  }

  static void _provideTo(OrblitModule module, String name, Uint8List bytes) {
    final heap = OrblitHeap(module);
    try {
      // Copied into the module's heap for the call and copied again into the
      // store by the renderer, which keeps its own; this copy is freed below.
      orblitCall(module, 'orblit_renderer_provide_resource', [
        heap.string(name),
        heap.uint8s(bytes),
        bytes.length,
      ]);
    } finally {
      heap.free();
    }
  }

  /// The same three numbers `stats` answers with everywhere else.
  Map<String, Object?> stats() {
    final module = _module;
    if (_renderer == 0 || module == null) return const {};
    final heap = OrblitHeap(module);
    try {
      // orblit_stats: two doubles then three uint32s. Read through the heap
      // rather than by a per-field accessor, because the ABI hands the whole
      // struct back at once.
      final stats = heap.ints(const [0, 0, 0, 0, 0, 0, 0]);
      final ok = orblitCall(module, 'orblit_renderer_stats', [
        _renderer,
        stats,
      ]);
      if (ok != 0) return const {};
      final bytes = module.HEAPU8.toDart;
      final view = bytes.buffer.asByteData(bytes.offsetInBytes + stats);
      final gpu = view.getFloat64(0, Endian.little);
      final batched = view.getUint32(16, Endian.little);
      final groups = view.getUint32(20, Endian.little);
      final passCount = view.getUint32(24, Endian.little);

      final timings = <double>[];
      if (passCount > 0) {
        final milliseconds = heap.ints(List<int>.filled(passCount * 2, 0));
        final drawn = heap.ints(List<int>.filled(passCount, 0));
        final got = orblitCall(module, 'orblit_renderer_pass_timings', [
          _renderer,
          milliseconds,
          drawn,
          passCount,
        ]);
        final heapU8 = module.HEAPU8.toDart;
        final ms = heapU8.buffer.asByteData(
          heapU8.offsetInBytes + milliseconds,
        );
        final dr = heapU8.buffer.asByteData(heapU8.offsetInBytes + drawn);
        for (var i = 0; i < got; i++) {
          timings
            ..add(ms.getFloat64(i * 8, Endian.little))
            ..add(dr.getInt32(i * 4, Endian.little).toDouble());
        }
      }

      return {
        'gpuMilliseconds': gpu,
        'passTimings': timings,
        'batching': [batched, groups],
      };
    } finally {
      heap.free();
    }
  }

  void dispose() {
    _disposed = true;
    _pending = null;
    _answerWaiting(const {});
    final module = _module;
    if (module != null && _renderer != 0) {
      orblitCall(module, 'orblit_renderer_destroy', [_renderer]);
    }
    _renderer = 0;
    _module = null;
    // Flutter removes the platform view's own element; the canvas goes with
    // it. Dropping the reference is all this side owes.
    _canvas = null;
  }
}
