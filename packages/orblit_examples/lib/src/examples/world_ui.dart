import 'dart:math' as math;

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import 'surface.dart' show linearOf;

/// An interface that belongs to a place in the scene.
///
/// A screen on a wall is a real widget tree, drawn off screen and shown on a
/// quad. A click on the quad becomes a click in the tree. Over the crowd float
/// two hundred names. Each distinct name is drawn once into one picture, and a
/// label is a quad that shows its rectangle of it.
class WorldUiExample extends Example {
  WorldUiExample();

  @override
  String get name => 'Interface in the world';

  @override
  ExampleSection get section => ExampleSection.content;

  @override
  String get blurb =>
      'A screen on a wall that takes a click and a drag, and two hundred names '
      'that face the camera.';

  @override
  ViewPoint get viewpoint =>
      const ViewPoint(yaw: 0, pitch: 0.3, distance: 9, height: 2);

  /// How many times the button on the wall was pressed.
  int presses = 0;

  /// The slider on the wall. It sets how bright the sun is.
  double level = 0.5;

  /// How many names float over the crowd, up to [_crowd].
  int labels = 200;

  /// Whether every name is its own text. Off, twenty names are shared by the
  /// crowd and the picture holds twenty rectangles. On, it holds two hundred.
  bool distinct = false;

  static const int _crowd = 200;
  static const int _columns = 20;
  static const Size _screen = Size(800, 450);
  static const int _wallObject = 1;
  static const int _floorObject = 2;
  static const int _screenKey = 3;
  static const int _figures = 1000;

  static const List<String> _names = [
    'Ada', 'Bram', 'Cleo', 'Dov', 'Edda', 'Finn', 'Gia', 'Hugo', 'Ines',
    'Jonas', 'Kira', 'Leo', 'Mina', 'Nils', 'Orla', 'Pavel', 'Quinn', 'Rosa',
    'Sven', 'Tilda', //
  ];

  /// The screen's place on the wall. It is the cube's front face, six metres
  /// by three and a bit and a hair thick.
  static final Matrix4 _onWall = Matrix4.identity()
    ..setTranslation(Vector3(0, 2.25, -2.49))
    ..scaleByDouble(3, 1.6875, 0.005, 1);

  late final UiPanel _ui = _openPanel();
  late final WorldPanel _wall = WorldPanel(
    id: 'world_ui',
    panel: _ui,
    transform: _onWall,
    objectKey: _screenKey,
    materialKey: _screenKey,
  );
  late final Nameplates _plates = _openPlates();
  final LabelAtlas _atlas = LabelAtlas(
    style: const TextStyle(
      color: Colors.white,
      fontSize: 32,
      fontWeight: FontWeight.w600,
    ),
  );

  OrblitCamera? _camera;
  VoidCallback? _changed;

  // shortcut: one queue for the screen and the names, and a failed sync stops
  // it. Upgrade when something outside this example depends on the pictures.
  Future<void> _busy = Future.value();

  /// Completes when every picture asked for so far has reached the renderer.
  @visibleForTesting
  Future<void> get idle => _busy;

  /// Lets go of the pictures and the panel. The gallery keeps its examples for
  /// as long as it runs, so only a test closes one.
  @visibleForTesting
  Future<void> close() async {
    await _busy;
    await _wall.dispose();
    await _plates.dispose();
    _ui.dispose();
  }

  UiPanel _openPanel() {
    final panel = UiPanel(size: _screen, child: _face());
    panel.addListener(() => _later(_drawScreen));
    _later(_drawScreen);
    return panel;
  }

  // A method and not `_wall.sync`, because this runs while `_wall` is being
  // made and a tear-off would read it too early.
  Future<void> _drawScreen() => _wall.sync();

  Nameplates _openPlates() {
    final plates = Nameplates(atlas: _atlas, fadeStart: 6, fadeEnd: 14);
    _later(_drawNames);
    return plates;
  }

  Future<void> _drawNames() =>
      _plates.sync([for (var i = 0; i < labels; i++) _text(i)]);

  void _later(Future<void> Function() job) {
    _busy = _busy.then((_) => job()).whenComplete(() => _changed?.call());
  }

  String _text(int i) {
    final name = _names[i % _columns];
    return distinct ? '$name ${i ~/ _columns + 1}' : name;
  }

  Widget _face() => ColoredBox(
    color: const Color(0xFF16222E),
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text('Ticket booth', style: _title),
          Text('Pressed $presses times', style: _body),
          const SizedBox(height: 16),
          Expanded(
            child: FilledButton(
              onPressed: _press,
              child: const Text('Press', style: _title),
            ),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 64,
            child: Row(
              children: [
                const Text('Daylight', style: _body),
                Expanded(
                  child: Slider(value: level, onChanged: _slide),
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );

  static const TextStyle _title = TextStyle(
    color: Colors.white,
    fontSize: 56,
    fontWeight: FontWeight.w700,
  );
  static const TextStyle _body = TextStyle(color: Colors.white, fontSize: 36);

  void _press() {
    presses++;
    _ui.update(_face());
  }

  void _slide(double value) {
    level = value;
    _ui.update(_face());
  }

  /// Whether a press at [at] in a view of [view] pixels lands on the screen.
  bool hits(Offset at, Size view) {
    final ray = _ray(at, view);
    return ray != null && _wall.hits(ray);
  }

  /// A pointer that went down, moved or came up at [at] in a view of [view]
  /// pixels. They reach the screen only if they began on it.
  void down(Offset at, Size view) => _route(at, view, _wall.down);
  void move(Offset at, Size view) => _route(at, view, _wall.move);
  void up(Offset at, Size view) => _route(at, view, _wall.up);

  void _route(Offset at, Size view, void Function(PointerRay) send) {
    final ray = _ray(at, view);
    if (ray != null) send(ray);
  }

  PointerRay? _ray(Offset at, Size view) {
    final camera = _camera;
    return camera == null ? null : PointerRay.fromCamera(camera, at, view);
  }

  /// Where the name at [i] stands.
  Vector3 _stand(int i, double seconds) {
    final bob = 0.06 * math.sin(seconds * 2 + i);
    return Vector3((i % _columns - 9.5) * 0.8, bob, (i ~/ _columns) * 0.55);
  }

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    _camera = camera;
    final stands = [for (var i = 0; i < _crowd; i++) _stand(i, seconds)];
    final names = _plates.build(camera, [
      for (var i = 0; i < labels; i++)
        Nameplate(
          id: i,
          text: _text(i),
          position: stands[i] + Vector3(0, 1.3, 0),
        ),
    ]);
    return OrblitScene(
      objects: [
        _box(
          _floorObject,
          Vector3(0, -0.05, 2),
          Vector3(14, 0.05, 8),
          0xFF3B424C,
        ),
        _box(
          _wallObject,
          Vector3(0, 2.25, -2.6),
          Vector3(5, 2.25, 0.1),
          0xFFB8B2A7,
        ),
        _wall.object,
        for (var i = 0; i < _crowd; i++)
          _box(
            _figures + i,
            stands[i] + Vector3(0, 0.45, 0),
            Vector3(0.2, 0.45, 0.2),
            _palette[i % _palette.length],
          ),
        ...names.objects,
      ],
      materials: [_wall.material, ...names.materials],
      lights: [
        OrblitLight(
          key: 10,
          kind: OrblitLightKind.directional,
          intensity: 20000 + 100000 * level,
          colour: linearOf(const Color(0xFFFFF3E0)),
          direction: Vector3(-0.3, -1, -0.6)..normalize(),
        ),
      ],
      sky: OrblitSky(colour: linearOf(const Color(0xFF1B222C)), ambient: 9000),
      camera: camera,
    );
  }

  static const List<int> _palette = [
    0xFFD9634F, 0xFFE5B84F, 0xFF5FA8D3, 0xFF6FBF73, 0xFFE8E4DC, //
  ];

  OrblitObject _box(int key, Vector3 at, Vector3 half, int colour) =>
      OrblitObject(
        key: key,
        transform: Matrix4.identity()
          ..setTranslation(at)
          ..scaleByVector3(half),
        colour: linearOf(Color(colour)),
      );

  /// The screen takes the pointer only when it begins on the screen, so a drag
  /// anywhere else still orbits the camera underneath.
  @override
  Widget? overlay(BuildContext context, VoidCallback changed) {
    _changed = changed;
    return LayoutBuilder(
      builder: (context, box) {
        final view = box.biggest;
        return RawGestureDetector(
          behavior: HitTestBehavior.translucent,
          gestures: {
            _Claim: GestureRecognizerFactoryWithHandlers<_Claim>(
              _Claim.new,
              (claim) => claim.wants = (at) => hits(at, view),
            ),
          },
          child: Listener(
            behavior: HitTestBehavior.translucent,
            onPointerDown: (event) => down(event.localPosition, view),
            onPointerMove: (event) => move(event.localPosition, view),
            onPointerUp: (event) => up(event.localPosition, view),
          ),
        );
      },
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Setting(
        label: 'Names',
        value: labels.toDouble(),
        min: 0,
        max: _crowd.toDouble(),
        decimals: 0,
        onChanged: (value) {
          labels = value.round();
          _later(_drawNames);
          changed();
        },
      ),
      Toggle(
        label: 'Distinct text',
        value: distinct,
        note: 'Off: twenty rectangles. On: every name has its own.',
        onChanged: (value) {
          distinct = value;
          _later(_drawNames);
          changed();
        },
      ),
    ],
  );

  @override
  String get code => '''
// A screen is a widget tree drawn without a window.
final panel = UiPanel(size: const Size(800, 450), child: face());
final wall = WorldPanel(
  id: 'booth',
  panel: panel,
  // The cube's front face, squashed thin.
  transform: Matrix4.identity()
    ..setTranslation(Vector3(0, 2.25, -2.49))
    ..scaleByDouble(3, 1.6875, 0.005, 1),
  objectKey: 3,
  materialKey: 3,
);
panel.addListener(wall.sync); // draw again after a change

// Put it in the scene.
OrblitScene(objects: [wall.object], materials: [wall.material], ...);

// A click is a ray from the camera. It reaches the tree only if it lands on
// the face.
final ray = PointerRay.fromCamera(camera, point, view);
wall.down(ray);
wall.move(ray);
wall.up(ray);

// Two hundred names share one picture.
final plates = Nameplates(atlas: LabelAtlas(), fadeStart: 6, fadeEnd: 14);
await plates.sync(texts);
final named = plates.build(camera, [Nameplate(id: 1, text: 'Ada', position: p)]);
OrblitScene(objects: named.objects, materials: named.materials, ...);

// The renderer loads a name once, so each new picture has a new name and the
// one before is released. That suits a screen somebody presses. It does not
// suit a screen that changes every frame.
''';
}

/// Takes a pointer at once if it began on the screen, so the camera's drag
/// underneath loses the arena to it.
final class _Claim extends OneSequenceGestureRecognizer {
  _Claim();

  bool Function(Offset at) wants = (_) => false;

  @override
  bool isPointerAllowed(PointerDownEvent event) =>
      wants(event.localPosition) && super.isPointerAllowed(event);

  @override
  void addAllowedPointer(PointerDownEvent event) {
    startTrackingPointer(event.pointer, event.transform);
    resolve(GestureDisposition.accepted);
  }

  @override
  void handleEvent(PointerEvent event) {
    if (event is PointerUpEvent || event is PointerCancelEvent) {
      stopTrackingPointer(event.pointer);
    }
  }

  @override
  void didStopTrackingLastPointer(int pointer) {}

  @override
  String get debugDescription => 'claim';
}
