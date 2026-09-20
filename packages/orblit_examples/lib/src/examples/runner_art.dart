import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

/// The runner's world, made here rather than shipped.
///
/// Every model in the game is a handful of boxes, balls and cones, and every
/// texture is a few lines of noise and paint. Written out as ordinary glTF
/// and PNG files and handed to the renderer by name, they go down the same
/// path a downloaded model does — so the game shows the engine drawing
/// models, and still has nothing to download and nothing in the repository
/// but this file.
///
/// Made once, the first time the game is shown. The whole set is a few
/// hundred kilobytes and takes a blink to build.
class RunnerArt {
  RunnerArt._(this.files);

  /// Everything, by the resource name the scene uses for it.
  final Map<String, Uint8List> files;

  /// Bumped whenever what any file holds changes.
  ///
  /// A resource name stands for bytes that never change — a renderer that
  /// has loaded a name does not load it again — so art that changed under the
  /// same name would not be seen until the application restarted.
  static const version = 2;

  /// The name the scene uses for [file].
  static String name(String file) => 'orblit:resource/runner/v$version/$file';

  static final road = name('road.glb');
  static final ground = name('ground.glb');
  static final asphalt = name('asphalt.png');
  static final grass = name('grass.png');
  static final pine = name('pine.glb');
  static final oak = name('oak.glb');
  static final house = name('house.glb');
  static final darkHouse = name('dark_house.glb');
  static final bush = name('bush.glb');
  static final redBush = name('red_bush.glb');
  static final rock = name('rock.glb');
  static final billboard = name('billboard.glb');
  static final hill = name('hill.glb');
  static final coin = name('coin.glb');
  static final hurdle = name('hurdle.glb');
  static final bar = name('bar.glb');
  static final container = name('container.glb');
  static final body = name('body.glb');
  static final hand = name('hand.glb');
  static final foot = name('foot.glb');
  static final cloud = name('cloud.glb');

  /// How much road there is behind and ahead of wherever the road is put.
  ///
  /// The road is one long strip, moved along in whole repeats of its paint
  /// so the move cannot be seen; ahead has to reach far enough that its end is
  /// lost in the haze.
  static const roadBehind = 120.0;
  static const roadAhead = 480.0;

  /// Half the width of the road, kerb to kerb.
  static const roadHalfWidth = 4.4;

  /// How far along the road one repeat of its paint covers: a dash and a gap.
  static const roadRepeat = 8.0;

  /// How far one repeat of the grass texture covers.
  static const grassRepeat = 4.0;

  /// The length of one container, and so of one car of a train.
  static const containerLength = 10.0;

  /// Builds every file.
  factory RunnerArt.build() {
    return RunnerArt._({
      road: _road(),
      ground: _ground(),
      asphalt: _asphaltTexture(),
      grass: _grassTexture(),
      pine: _pine(),
      oak: _oak(),
      house: _house(
        walls: 0xFFF3F0E8,
        roof: 0xFF7ED9B7,
        door: 0xFF3D4B5E,
        windows: 0xFF78A8CF,
      ),
      darkHouse: _house(
        walls: 0xFF474B5C,
        roof: 0xFFA83A33,
        door: 0xFF26262C,
        windows: 0xFFF2CB6A,
      ),
      bush: _bush(leaves: 0xFF3E9C39, flowers: 0xFFE0413F),
      redBush: _bush(leaves: 0xFFC7302C, flowers: 0xFFF08A3A),
      rock: _rock(),
      billboard: _billboard(),
      hill: _hill(),
      coin: _coin(),
      hurdle: _hurdle(),
      bar: _bar(),
      container: _container(),
      body: _body(),
      hand: _hand(),
      foot: _foot(),
      cloud: _cloud(),
    });
  }

  // ---- the ground ----

  static Uint8List _road() {
    final model = RunnerModel();
    final top = model._surface(0xFFFFFFFF, 0.9, 0);
    // In twenty-metre pieces rather than one: a triangle six hundred metres
    // long is correct and gives every per-vertex quantity nothing to vary
    // along.
    for (var z = roadBehind; z > -roadAhead; z -= 20) {
      final far = math.max(z - 20, -roadAhead);
      model._quad(
        top,
        Vector3(-roadHalfWidth, 0, z),
        Vector3(roadHalfWidth, 0, z),
        Vector3(roadHalfWidth, 0, far),
        Vector3(-roadHalfWidth, 0, far),
        Vector3(0, 1, 0),
        // Across the road is across the texture, and along it is one repeat
        // of the paint every eight metres.
        [
          Vector2(0, -z / roadRepeat),
          Vector2(1, -z / roadRepeat),
          Vector2(1, -far / roadRepeat),
          Vector2(0, -far / roadRepeat),
        ],
      );
      // The edges, so the road stands a hand's width proud of the grass
      // rather than being painted on it.
      for (final side in const [-1.0, 1.0]) {
        final x = side * roadHalfWidth;
        model._quad(
          top,
          Vector3(x, 0, z),
          Vector3(x, 0, far),
          Vector3(x, -0.12, far),
          Vector3(x, -0.12, z),
          Vector3(side, 0, 0),
          [
            Vector2(0.99, 0),
            Vector2(0.99, 1),
            Vector2(0.99, 1),
            Vector2(0.99, 0),
          ],
        );
      }
    }
    return model.toGlb();
  }

  static Uint8List _ground() {
    final model = RunnerModel();
    final grass = model._surface(0xFFFFFFFF, 0.95, 0);
    const wide = 400.0;
    for (var z = roadBehind; z > -roadAhead; z -= 40) {
      final far = math.max(z - 40, -roadAhead);
      for (var x = -wide; x < wide; x += 80) {
        model._quad(
          grass,
          Vector3(x, 0, z),
          Vector3(x + 80, 0, z),
          Vector3(x + 80, 0, far),
          Vector3(x, 0, far),
          Vector3(0, 1, 0),
          [
            Vector2(x / grassRepeat, -z / grassRepeat),
            Vector2((x + 80) / grassRepeat, -z / grassRepeat),
            Vector2((x + 80) / grassRepeat, -far / grassRepeat),
            Vector2(x / grassRepeat, -far / grassRepeat),
          ],
        );
      }
    }
    return model.toGlb();
  }

  // ---- the scenery ----

  static Uint8List _pine() {
    final model = RunnerModel();
    model.tube(place(0, 0, 0, sx: 0.17, sy: 0.9, sz: 0.17), 0xFF6B4A2F);
    const tiers = [
      (y: 0.7, radius: 1.25, height: 1.5, colour: 0xFF2E7A38),
      (y: 1.45, radius: 1.0, height: 1.4, colour: 0xFF368A3E),
      (y: 2.2, radius: 0.72, height: 1.6, colour: 0xFF41994A),
    ];
    for (final tier in tiers) {
      model.tube(
        place(0, tier.y, 0, sx: tier.radius, sy: tier.height, sz: tier.radius),
        tier.colour,
        top: 0,
        segments: 9,
        flat: true,
      );
    }
    return model.toGlb();
  }

  static Uint8List _oak() {
    final model = RunnerModel();
    model.tube(place(0, 0, 0, sx: 0.2, sy: 1.5, sz: 0.2), 0xFF7A5234);
    model.tube(
      place(0.1, 1.1, 0, sx: 0.09, sy: 0.7, sz: 0.09, roll: -0.7),
      0xFF7A5234,
    );
    const crowns = [
      (x: 0.0, y: 2.3, z: 0.0, r: 1.25, colour: 0xFF5CB23A),
      (x: 0.7, y: 2.75, z: 0.25, r: 0.85, colour: 0xFF6BC244),
      (x: -0.6, y: 2.7, z: -0.3, r: 0.9, colour: 0xFF55A836),
      (x: 0.1, y: 3.15, z: -0.1, r: 0.7, colour: 0xFF74CA4C),
    ];
    for (final crown in crowns) {
      model.ball(
        place(
          crown.x,
          crown.y,
          crown.z,
          sx: crown.r,
          sy: crown.r * 0.9,
          sz: crown.r,
        ),
        crown.colour,
        segments: 12,
        rings: 8,
        flat: true,
      );
    }
    return model.toGlb();
  }

  /// A house whose front, with the door in it, faces -z.
  static Uint8List _house({
    required int walls,
    required int roof,
    required int door,
    required int windows,
  }) {
    final model = RunnerModel();
    model.box(place(0, 0.12, 0, sx: 4.5, sy: 0.24, sz: 4.5), 0xFF9C9C98);
    model.box(place(0, 1.64, 0, sx: 4, sy: 2.8, sz: 4), walls);
    // The eaves, a little wider and deeper than the walls.
    model.roof(place(0, 3.04, 0, sx: 4.7, sy: 1.7, sz: 4.6), roof);
    model.box(place(1.15, 3.9, 0.9, sx: 0.5, sy: 1.3, sz: 0.5), 0xFFB0523D);
    model.box(place(0, 1.1, -2.02, sx: 0.95, sy: 1.75, sz: 0.1), door);
    model.box(place(0, 2.02, -2.03, sx: 1.15, sy: 0.12, sz: 0.12), 0xFFFFFFFF);
    for (final x in const [-1.3, 1.3]) {
      model.box(place(x, 1.9, -2.02, sx: 0.85, sy: 0.85, sz: 0.08), windows);
      model.box(place(x, 1.42, -2.05, sx: 1.0, sy: 0.1, sz: 0.14), 0xFFFFFFFF);
    }
    for (final side in const [-1.0, 1.0]) {
      for (final z in const [-0.9, 0.9]) {
        model.box(
          place(side * 2.02, 1.9, z, sx: 0.08, sy: 0.85, sz: 0.85),
          windows,
        );
      }
    }
    return model.toGlb();
  }

  static Uint8List _bush({required int leaves, required int flowers}) {
    final model = RunnerModel();
    const lumps = [
      (x: 0.0, y: 0.42, z: 0.0, r: 0.62),
      (x: 0.55, y: 0.34, z: 0.15, r: 0.46),
      (x: -0.5, y: 0.32, z: -0.1, r: 0.44),
      (x: 0.1, y: 0.3, z: 0.5, r: 0.4),
    ];
    for (final lump in lumps) {
      model.ball(
        place(
          lump.x,
          lump.y,
          lump.z,
          sx: lump.r,
          sy: lump.r * 0.85,
          sz: lump.r,
        ),
        leaves,
        segments: 10,
        rings: 7,
        flat: true,
      );
    }
    final chance = math.Random(3);
    for (var i = 0; i < 9; i++) {
      final lump = lumps[i % lumps.length];
      final yaw = chance.nextDouble() * math.pi * 2;
      final up = 0.2 + chance.nextDouble() * 0.9;
      model.ball(
        place(
          lump.x + math.cos(yaw) * math.cos(up) * lump.r,
          lump.y + math.sin(up) * 0.85 * lump.r,
          lump.z + math.sin(yaw) * math.cos(up) * lump.r,
          sx: 0.08,
          sy: 0.08,
          sz: 0.08,
        ),
        flowers,
        segments: 6,
        rings: 4,
      );
    }
    return model.toGlb();
  }

  static Uint8List _rock() {
    final model = RunnerModel();
    model.ball(
      place(0, 0.25, 0, sx: 1, sy: 0.7, sz: 0.85),
      0xFF8E9196,
      segments: 7,
      rings: 5,
      flat: true,
      lumpy: 0.28,
      roughness: 0.95,
    );
    return model.toGlb();
  }

  /// A billboard whose face is towards +z, to be read by somebody coming
  /// along the road.
  static Uint8List _billboard() {
    final model = RunnerModel();
    for (final x in const [-1.6, 1.6]) {
      model.box(place(x, 1.6, 0.1, sx: 0.2, sy: 3.2, sz: 0.2), 0xFF5A5F68);
    }
    model.box(place(0, 3.3, 0, sx: 4.4, sy: 2.4, sz: 0.2), 0xFFEFEFEA);
    model.box(place(0, 3.3, 0.11, sx: 4.05, sy: 2.05, sz: 0.04), 0xFF2F9BDB);
    model.box(
      place(-0.35, 3.85, 0.14, sx: 2.9, sy: 0.32, sz: 0.03),
      0xFFFFFFFF,
    );
    model.box(place(-0.75, 3.35, 0.14, sx: 2.1, sy: 0.2, sz: 0.03), 0xFFFFD21F);
    model.box(place(-0.95, 2.85, 0.14, sx: 1.7, sy: 0.2, sz: 0.03), 0xFFFFFFFF);
    model.ball(
      place(1.3, 2.95, 0.12, sx: 0.55, sy: 0.55, sz: 0.06),
      0xFFFFD21F,
      segments: 16,
      rings: 8,
    );
    return model.toGlb();
  }

  static Uint8List _hill() {
    final model = RunnerModel();
    model.ball(
      place(0, -0.08, 0, sx: 1, sy: 0.42, sz: 1),
      0xFF6AB944,
      segments: 22,
      rings: 12,
      roughness: 0.95,
    );
    return model.toGlb();
  }

  /// A fair-weather cloud, in lumps like the trees' crowns so that it looks
  /// as if it belongs to the same world. About two units across.
  static Uint8List _cloud() {
    final model = RunnerModel();
    const lumps = [
      (x: 0.0, y: 0.25, z: 0.0, r: 0.55),
      (x: 0.5, y: 0.15, z: 0.1, r: 0.42),
      (x: -0.52, y: 0.12, z: -0.05, r: 0.4),
      (x: 0.2, y: 0.5, z: -0.1, r: 0.4),
      (x: -0.25, y: 0.42, z: 0.12, r: 0.36),
      (x: 0.85, y: 0.05, z: -0.05, r: 0.28),
      (x: -0.88, y: 0.02, z: 0.05, r: 0.26),
    ];
    for (final lump in lumps) {
      model.ball(
        place(
          lump.x,
          lump.y,
          lump.z,
          sx: lump.r,
          sy: lump.r * 0.85,
          sz: lump.r,
        ),
        0xFFFFFFFF,
        segments: 10,
        rings: 7,
        flat: true,
        roughness: 1,
      );
    }
    return model.toGlb();
  }

  // ---- what is on the road ----

  /// A coin standing on its edge, its faces towards ±z.
  static Uint8List _coin() {
    final model = RunnerModel();
    Matrix4 disc(double radius, double thickness) => Matrix4.identity()
      ..rotateX(math.pi / 2)
      ..scaleByDouble(radius, thickness, radius, 1)
      ..translateByDouble(0, -0.5, 0, 1);
    model.tube(disc(0.42, 0.08), 0xFFFFC21A, segments: 22, roughness: 0.3);
    model.tube(disc(0.3, 0.11), 0xFFFFC21A, segments: 22, roughness: 0.3);
    return model.toGlb();
  }

  /// What is jumped: a striped board on two legs, a metre tall, one lane
  /// wide.
  static Uint8List _hurdle() {
    final model = RunnerModel();
    for (final x in const [-1.05, 1.05]) {
      model.box(place(x, 0.5, 0, sx: 0.12, sy: 1.0, sz: 0.12), 0xFF34363C);
      model.box(place(x, 0.04, 0, sx: 0.16, sy: 0.08, sz: 0.7), 0xFF34363C);
    }
    const stripes = 7;
    const wide = 2.36;
    for (var i = 0; i < stripes; i++) {
      final x = -wide / 2 + (i + 0.5) * wide / stripes;
      model.box(
        place(x, 0.74, 0, sx: wide / stripes, sy: 0.5, sz: 0.14),
        i.isEven ? 0xFFFFD21F : 0xFF232323,
        roughness: 0.6,
      );
    }
    return model.toGlb();
  }

  /// What is slid under: a striped board on tall posts, with a gap beneath
  /// it that only something ducking fits through.
  static Uint8List _bar() {
    final model = RunnerModel();
    for (final x in const [-1.22, 1.22]) {
      model.box(place(x, 0.95, 0, sx: 0.14, sy: 1.9, sz: 0.14), 0xFFE8E8E4);
      model.box(place(x, 0.04, 0, sx: 0.2, sy: 0.08, sz: 0.6), 0xFF5A5F68);
    }
    const stripes = 6;
    const wide = 2.5;
    for (var i = 0; i < stripes; i++) {
      final x = -wide / 2 + (i + 0.5) * wide / stripes;
      model.box(
        place(x, 1.3, 0, sx: wide / stripes, sy: 0.5, sz: 0.12),
        i.isEven ? 0xFFE8322C : 0xFFF6F6F2,
        roughness: 0.6,
      );
    }
    return model.toGlb();
  }

  /// What is gone round: a shipping container, its near end at z = 0 and
  /// the rest of it towards -z.
  static Uint8List _container() {
    final model = RunnerModel();
    const length = containerLength;
    const green = 0xFF2E9E62;
    const dark = 0xFF217A4A;
    model.box(place(0, 1.55, -length / 2, sx: 2.4, sy: 2.9, sz: length), green);
    // Ribs down both sides, which is what makes a box read as a container
    // rather than as a box.
    for (var i = 1; i < 14; i++) {
      final z = -i * length / 14;
      for (final side in const [-1.0, 1.0]) {
        model.box(
          place(side * 1.22, 1.55, z, sx: 0.07, sy: 2.7, sz: 0.16),
          dark,
        );
      }
    }
    for (final side in const [-1.0, 1.0]) {
      model.box(
        place(side * 1.2, 3.0, -length / 2, sx: 0.12, sy: 0.12, sz: length),
        dark,
      );
      model.box(
        place(side * 1.2, 0.12, -length / 2, sx: 0.14, sy: 0.24, sz: length),
        0xFF2B2E33,
      );
    }
    // The doors, on the end that faces whoever is running at it.
    model.box(place(0, 1.55, 0.02, sx: 2.3, sy: 2.8, sz: 0.06), dark);
    for (final x in const [-0.55, -0.2, 0.2, 0.55]) {
      model.box(place(x, 1.55, 0.07, sx: 0.07, sy: 2.6, sz: 0.06), 0xFFB7C0C4);
    }
    model.box(place(0, 1.55, 0.06, sx: 0.04, sy: 2.75, sz: 0.06), 0xFF1B5E39);
    return model.toGlb();
  }

  // ---- the runner ----
  //
  // A robot, in four floating pieces: a body, two hands and two boots, with
  // nothing joining them. There is no skeleton here and no skinning, so an
  // arm that bent would need bones and a rig; a hand that simply travels
  // where an arm would have carried it needs a matrix, and reads as a run
  // all the same. It is also why the parts can be five draws of five meshes
  // rather than one of a rigged one.
  //
  // Slate and steel with a hot orange down one side of it, which is the
  // engine's own colouring, against a world that is otherwise all greens and
  // blues.

  /// Slate, for everything that is a panel of the machine.
  ///
  /// Light enough to be a colour rather than a hole: this is a bright world
  /// of greens and whites, seen from behind and often against black asphalt,
  /// and a darker machine than this read as a silhouette on it.
  static const _shell = 0xFF5C6A82;

  /// The lighter steel of the joints and the plating over them.
  static const _steel = 0xFFB4BECE;

  /// The darker trim, which keeps the shapes apart where two panels meet.
  static const _trim = 0xFF2E3546;

  /// The hot orange of the chest, the visor's surround and the soles.
  static const _ember = 0xFFE5893F;

  /// What is behind the visor, which the glass only half hides.
  static const _glow = 0xFF7FE4FF;

  /// The runner's body: a machine facing -z, its middle at the origin,
  /// standing about a metre and a quarter from its hips to the top of its
  /// head.
  static Uint8List _body() {
    final model = RunnerModel();

    // The chest, a box with its corners taken off by a slightly smaller box
    // turned through an eighth -- cheaper than rounding it, and at this size
    // the eye reads the result as a bevel.
    model.box(place(0, 0.04, 0, sx: 0.52, sy: 0.62, sz: 0.40), _shell,
        roughness: 0.42);
    model.box(
      place(0, 0.04, 0, sx: 0.50, sy: 0.58, sz: 0.46, yaw: math.pi / 8),
      _shell,
      roughness: 0.42,
    );

    // The plate across the front, and the lamp set into it. The lamp is the
    // one thing on the runner that is meant to be looked at, so it is the
    // brightest thing on it and sits at the height a camera behind will hold
    // in the middle of the frame.
    model.box(place(0, 0.08, -0.19, sx: 0.34, sy: 0.40, sz: 0.06), _steel,
        roughness: 0.3);
    model.ball(
      place(0, 0.10, -0.23, sx: 0.09, sy: 0.09, sz: 0.05),
      _ember,
      segments: 14,
      rings: 10,
      roughness: 0.18,
    );

    // The shoulders, which no arm hangs from: they are what the hands swing
    // around, and the eye supplies the rest.
    for (final side in const [-1.0, 1.0]) {
      model.ball(
        place(side * 0.29, 0.26, 0, sx: 0.14, sy: 0.13, sz: 0.14),
        _steel,
        segments: 14,
        rings: 10,
        roughness: 0.35,
      );
      model.ball(
        place(side * 0.34, 0.26, 0, sx: 0.06, sy: 0.07, sz: 0.07),
        _ember,
        segments: 10,
        rings: 8,
        roughness: 0.25,
      );
    }

    // The waist and the hips, narrower than the chest, so that the shape
    // tapers to where the boots are rather than stopping flat.
    model.tube(
      place(0, -0.30, 0, sx: 0.17, sy: 0.12, sz: 0.15),
      _steel,
      top: 1.35,
      segments: 12,
      roughness: 0.4,
    );
    model.box(place(0, -0.44, 0, sx: 0.38, sy: 0.16, sz: 0.32), _shell,
        roughness: 0.45);

    // The back, which is the side of this that anybody playing ever sees:
    // the camera is behind the runner for the whole game. A pack with a vent
    // down each side of it, lit, so that what follows the runner down the
    // road is two orange lights rather than the back of a box.
    model.box(place(0, 0.06, 0.20, sx: 0.36, sy: 0.46, sz: 0.14), _trim,
        roughness: 0.4);
    for (final side in const [-1.0, 1.0]) {
      model.box(
        place(side * 0.11, 0.06, 0.27, sx: 0.09, sy: 0.34, sz: 0.04),
        _ember,
        roughness: 0.25,
      );
    }
    model.box(place(0, 0.26, 0.27, sx: 0.30, sy: 0.05, sz: 0.04), _steel,
        roughness: 0.3);

    // The neck and the head. The head is turned an eighth the other way from
    // the chest, which is enough to stop the two boxes reading as one.
    model.tube(
      place(0, 0.36, 0, sx: 0.10, sy: 0.06, sz: 0.10),
      _steel,
      segments: 10,
      roughness: 0.35,
    );
    model.box(
      place(0, 0.58, 0, sx: 0.40, sy: 0.34, sz: 0.38, yaw: -math.pi / 16),
      _shell,
      roughness: 0.4,
    );

    // The visor: a dark glass band round the front of the head with the
    // light behind it showing at the sides, and an orange brow over it.
    model.box(place(0, 0.58, -0.19, sx: 0.30, sy: 0.15, sz: 0.06), 0xFF13161C,
        roughness: 0.12);
    model.box(place(0, 0.58, -0.205, sx: 0.22, sy: 0.07, sz: 0.04), _glow,
        roughness: 0.1);
    model.box(place(0, 0.70, -0.17, sx: 0.34, sy: 0.05, sz: 0.08), _ember,
        roughness: 0.3);

    // The plates over the ears, and the one aerial, which is what the back
    // of it has instead of a face.
    for (final side in const [-1.0, 1.0]) {
      model.tube(
        Matrix4.identity()
          ..translateByDouble(side * 0.21, 0.57, 0.01, 1)
          ..rotateZ(side * math.pi / 2)
          ..scaleByDouble(0.09, 0.04, 0.09, 1),
        _steel,
        segments: 10,
        roughness: 0.3,
      );
    }
    // A light on the back of the head as well, at the height the eye goes to.
    model.box(place(0, 0.60, 0.19, sx: 0.16, sy: 0.07, sz: 0.04), _glow,
        roughness: 0.15);

    model.tube(
      place(0.10, 0.82, 0.06, sx: 0.022, sy: 0.16, sz: 0.022, roll: -0.22),
      _steel,
      top: 0.5,
      segments: 6,
    );
    model.ball(
      place(0.06, 0.96, 0.09, sx: 0.045, sy: 0.045, sz: 0.045),
      _ember,
      segments: 10,
      rings: 8,
      roughness: 0.2,
    );

    return model.toGlb();
  }

  /// A hand, centred on its own origin so that the game can put it wherever
  /// the swing of an arm would have taken it.
  static Uint8List _hand() {
    final model = RunnerModel();
    model.ball(
      place(0, 0, -0.01, sx: 0.115, sy: 0.115, sz: 0.13),
      _steel,
      segments: 14,
      rings: 10,
      roughness: 0.45,
    );
    // A steel cuff at the back of it, towards the shoulder, which gives the
    // hand a front and a back and so a direction of travel.
    model.tube(
      Matrix4.identity()
        ..translateByDouble(0, 0, 0.09, 1)
        ..rotateX(math.pi / 2)
        ..scaleByDouble(0.085, 0.04, 0.085, 1),
      _ember,
      segments: 10,
      roughness: 0.3,
    );
    return model.toGlb();
  }

  /// A boot, pointing -z, centred on its own origin for the same reason.
  static Uint8List _foot() {
    final model = RunnerModel();
    model.box(place(0, 0.02, -0.03, sx: 0.17, sy: 0.13, sz: 0.28), _shell,
        roughness: 0.5);
    model.box(place(0, -0.06, -0.04, sx: 0.19, sy: 0.05, sz: 0.30), _ember,
        roughness: 0.35);
    // The ankle, which is what shows from behind when the boot is thrown
    // forward on a slide.
    model.ball(
      place(0, 0.09, 0.06, sx: 0.09, sy: 0.08, sz: 0.09),
      _steel,
      segments: 12,
      rings: 8,
      roughness: 0.35,
    );
    return model.toGlb();
  }

  // ---- the textures ----

  /// Asphalt, one road wide and one repeat of the paint long: the lane
  /// dashes, the edge lines and the kerb.
  static Uint8List _asphaltTexture() {
    const size = 256;
    final pixels = Uint8List(size * size * 3);
    final chance = math.Random(11);
    final lumps = _PeriodicNoise(8, seed: 5);
    for (var row = 0; row < size; row++) {
      final along = (row + 0.5) / size * roadRepeat;
      final dashed = along < roadRepeat / 2;
      for (var column = 0; column < size; column++) {
        final x = -roadHalfWidth + (column + 0.5) / size * roadHalfWidth * 2;
        final ax = x.abs();

        var shade =
            88.0 +
            (lumps.at(column / size, row / size) - 0.5) * 12 +
            (chance.nextDouble() - 0.5) * 18;
        // Darker where the wheels go, which is what makes a road look used.
        for (final lane in const [-2.6, 0.0, 2.6]) {
          for (final wheel in const [-0.75, 0.75]) {
            final off = (x - lane - wheel).abs();
            if (off < 0.3) shade -= (1 - off / 0.3) * 7;
          }
        }
        var r = shade - 3, g = shade + 1, b = shade + 9;

        final lane = (ax - 1.3).abs() < 0.07 && dashed;
        final edge = (ax - 4.02).abs() < 0.08;
        if (lane || edge) {
          final paint = 228 + (chance.nextDouble() - 0.5) * 20;
          r = paint;
          g = paint;
          b = paint - 6;
        } else if (ax > 4.22) {
          final kerb = 176 + (chance.nextDouble() - 0.5) * 16;
          r = kerb;
          g = kerb - 2;
          b = kerb - 8;
        }

        final at = (row * size + column) * 3;
        pixels[at] = r.round().clamp(0, 255);
        pixels[at + 1] = g.round().clamp(0, 255);
        pixels[at + 2] = b.round().clamp(0, 255);
      }
    }
    return encodePng(size, size, pixels);
  }

  /// Grass, one repeat square, bright and mottled, and seamless so the
  /// repeat is not a grid across the fields.
  static Uint8List _grassTexture() {
    const size = 256;
    final pixels = Uint8List(size * size * 3);
    final chance = math.Random(17);
    final broad = _PeriodicNoise(4, seed: 1);
    final middle = _PeriodicNoise(12, seed: 2);
    final fine = _PeriodicNoise(40, seed: 3);
    for (var row = 0; row < size; row++) {
      for (var column = 0; column < size; column++) {
        final u = column / size, v = row / size;
        final mottle =
            broad.at(u, v) * 0.5 +
            middle.at(u, v) * 0.32 +
            fine.at(u, v) * 0.18;
        final speck = (chance.nextDouble() - 0.5) * 22;
        // From a deep green in the hollows to a sunlit yellow-green on top.
        final r = 92 + mottle * 92 + speck;
        final g = 150 + mottle * 62 + speck;
        final b = 26 + mottle * 22 + speck * 0.4;
        final at = (row * size + column) * 3;
        pixels[at] = r.round().clamp(0, 255);
        pixels[at + 1] = g.round().clamp(0, 255);
        pixels[at + 2] = b.round().clamp(0, 255);
      }
    }
    return encodePng(size, size, pixels);
  }
}

/// A transform that puts a unit shape at ([x], [y], [z]), turned by [yaw]
/// about the vertical and [roll] about the forward axis, and stretched by
/// ([sx], [sy], [sz]).
Matrix4 place(
  double x,
  double y,
  double z, {
  double sx = 1,
  double sy = 1,
  double sz = 1,
  double yaw = 0,
  double roll = 0,
}) {
  return Matrix4.identity()
    ..translateByDouble(x, y, z, 1)
    ..rotateY(yaw)
    ..rotateZ(roll)
    ..scaleByDouble(sx, sy, sz, 1);
}

/// A model being made out of simple shapes, written out as a binary glTF.
///
/// Everything of one colour is one primitive, whatever shape it came from,
/// so a house of eleven boxes is five draws rather than eleven.
///
/// Triangles are kept separate rather than sharing corners. That costs
/// memory nobody here is short of, and buys faceted shapes that are faceted
/// on purpose — a low-poly pine is a pine, a smooth one is a green blob.
class RunnerModel {
  final _surfaces = <String, _Surface>{};

  _Surface _surface(int colour, double roughness, double metallic) =>
      _surfaces.putIfAbsent(
        '$colour/$roughness/$metallic',
        () => _Surface(colour, roughness, metallic),
      );

  /// A cube one unit across, centred on the origin, placed by [at].
  void box(Matrix4 at, int colour, {double roughness = 0.8}) {
    final into = _surface(colour, roughness, 0);
    const faces = [
      (n: (1.0, 0.0, 0.0), u: (0.0, 0.0, -1.0), v: (0.0, 1.0, 0.0)),
      (n: (-1.0, 0.0, 0.0), u: (0.0, 0.0, 1.0), v: (0.0, 1.0, 0.0)),
      (n: (0.0, 1.0, 0.0), u: (1.0, 0.0, 0.0), v: (0.0, 0.0, -1.0)),
      (n: (0.0, -1.0, 0.0), u: (1.0, 0.0, 0.0), v: (0.0, 0.0, 1.0)),
      (n: (0.0, 0.0, 1.0), u: (1.0, 0.0, 0.0), v: (0.0, 1.0, 0.0)),
      (n: (0.0, 0.0, -1.0), u: (-1.0, 0.0, 0.0), v: (0.0, 1.0, 0.0)),
    ];
    for (final face in faces) {
      final n = Vector3(face.n.$1, face.n.$2, face.n.$3);
      final u = Vector3(face.u.$1, face.u.$2, face.u.$3);
      final v = Vector3(face.v.$1, face.v.$2, face.v.$3);
      final centre = n * 0.5;
      _shape(
        into,
        at,
        [
          centre - u * 0.5 - v * 0.5,
          centre + u * 0.5 - v * 0.5,
          centre + u * 0.5 + v * 0.5,
          centre - u * 0.5 + v * 0.5,
        ],
        [n, n, n, n],
        [Vector2(0, 1), Vector2(1, 1), Vector2(1, 0), Vector2(0, 0)],
        const [0, 1, 2, 0, 2, 3],
        flat: true,
      );
    }
  }

  /// A ball of radius one about the origin, placed by [at].
  ///
  /// [lumpy] pushes each corner in or out by up to that fraction, the same
  /// way every time, which is a rock.
  void ball(
    Matrix4 at,
    int colour, {
    int segments = 16,
    int rings = 10,
    bool flat = false,
    double lumpy = 0,
    double roughness = 0.8,
  }) {
    final points = <Vector3>[];
    final normals = <Vector3>[];
    final uvs = <Vector2>[];
    for (var ring = 0; ring <= rings; ring++) {
      final polar = ring / rings * math.pi;
      for (var segment = 0; segment <= segments; segment++) {
        final around = segment / segments * math.pi * 2;
        final n = Vector3(
          math.sin(polar) * math.cos(around),
          math.cos(polar),
          math.sin(polar) * math.sin(around),
        );
        var radius = 1.0;
        if (lumpy > 0) {
          // By where the corner is rather than by which one it is, so the
          // copies at the seam and at the poles move together and the rock
          // stays closed.
          final key =
              (n.x * 7).round() * 131 +
              (n.y * 7).round() * 17 +
              (n.z * 7).round() * 3;
          final hash = math.sin(key * 12.9898) * 43758.5453;
          radius += (hash - hash.floorToDouble() - 0.5) * 2 * lumpy;
        }
        points.add(n * radius);
        normals.add(n);
        uvs.add(Vector2(segment / segments, ring / rings));
      }
    }
    final indices = <int>[];
    final across = segments + 1;
    for (var ring = 0; ring < rings; ring++) {
      for (var segment = 0; segment < segments; segment++) {
        final a = ring * across + segment;
        final c = a + across;
        indices.addAll([a, c, a + 1, a + 1, c, c + 1]);
      }
    }
    _shape(
      _surface(colour, roughness, 0),
      at,
      points,
      normals,
      uvs,
      indices,
      flat: flat,
    );
  }

  /// A cylinder of radius one from y = 0 to y = 1, placed by [at]; [top] is
  /// the radius at the top as a fraction of the bottom, so zero is a cone.
  void tube(
    Matrix4 at,
    int colour, {
    double top = 1,
    int segments = 12,
    bool flat = false,
    double roughness = 0.8,
  }) {
    final points = <Vector3>[];
    final normals = <Vector3>[];
    final uvs = <Vector2>[];
    final indices = <int>[];
    for (var segment = 0; segment <= segments; segment++) {
      final around = segment / segments * math.pi * 2;
      final c = math.cos(around), s = math.sin(around);
      final side = Vector3(c, 1 - top, s)..normalize();
      points
        ..add(Vector3(c, 0, s))
        ..add(Vector3(c * top, 1, s * top));
      normals
        ..add(side)
        ..add(side);
      uvs
        ..add(Vector2(segment / segments, 1))
        ..add(Vector2(segment / segments, 0));
    }
    for (var segment = 0; segment < segments; segment++) {
      final a = segment * 2;
      indices.addAll([a, a + 1, a + 2, a + 2, a + 1, a + 3]);
    }
    // The caps, each a fan round its own middle.
    for (final (y, radius, facing) in [(0.0, 1.0, -1.0), (1.0, top, 1.0)]) {
      if (radius <= 0) continue;
      final middle = points.length;
      points.add(Vector3(0, y, 0));
      normals.add(Vector3(0, facing, 0));
      uvs.add(Vector2(0.5, 0.5));
      for (var segment = 0; segment <= segments; segment++) {
        final around = segment / segments * math.pi * 2;
        points.add(
          Vector3(math.cos(around) * radius, y, math.sin(around) * radius),
        );
        normals.add(Vector3(0, facing, 0));
        uvs.add(
          Vector2(0.5 + math.cos(around) / 2, 0.5 + math.sin(around) / 2),
        );
      }
      for (var segment = 0; segment < segments; segment++) {
        indices.addAll([middle, middle + 1 + segment, middle + 2 + segment]);
      }
    }
    _shape(
      _surface(colour, roughness, 0),
      at,
      points,
      normals,
      uvs,
      indices,
      flat: flat,
    );
  }

  /// A roof: a triangular prism one unit wide in x and deep in z, standing
  /// one unit tall from y = 0, placed by [at].
  void roof(Matrix4 at, int colour, {double roughness = 0.7}) {
    final into = _surface(colour, roughness, 0);
    final left = Vector3(-0.5, 0, 0), right = Vector3(0.5, 0, 0);
    final ridge = Vector3(0, 1, 0);
    final back = Vector3(0, 0, 0.5), front = Vector3(0, 0, -0.5);
    final uv = [Vector2(0, 1), Vector2(1, 1), Vector2(1, 0), Vector2(0, 0)];
    _quad(
      into,
      left + front,
      ridge + front,
      ridge + back,
      left + back,
      Vector3(-1, 0.5, 0),
      uv,
      at: at,
    );
    _quad(
      into,
      right + back,
      ridge + back,
      ridge + front,
      right + front,
      Vector3(1, 0.5, 0),
      uv,
      at: at,
    );
    _quad(
      into,
      left + back,
      right + back,
      right + front,
      left + front,
      Vector3(0, -1, 0),
      uv,
      at: at,
    );
    for (final (end, facing) in [(front, -1.0), (back, 1.0)]) {
      final n = Vector3(0, 0, facing);
      _shape(
        into,
        at,
        [left + end, right + end, ridge + end],
        [n, n, n],
        [Vector2(0, 1), Vector2(1, 1), Vector2(0.5, 0)],
        const [0, 1, 2],
        flat: true,
      );
    }
  }

  void _quad(
    _Surface into,
    Vector3 a,
    Vector3 b,
    Vector3 c,
    Vector3 d,
    Vector3 facing,
    List<Vector2> uvs, {
    Matrix4? at,
  }) {
    _shape(
      into,
      at ?? Matrix4.identity(),
      [a, b, c, d],
      [facing, facing, facing, facing],
      uvs,
      const [0, 1, 2, 0, 2, 3],
      flat: true,
    );
  }

  /// Adds [indices]' triangles of [points], placed by [at].
  ///
  /// [normals] say which way is out. Each triangle is wound to agree with
  /// them, so no shape above has to get its winding right — and a placement
  /// that mirrors is harmless rather than turning the shape inside out. A
  /// flat shape then takes each triangle's own normal; a smooth one keeps
  /// the ones it was given.
  void _shape(
    _Surface into,
    Matrix4 at,
    List<Vector3> points,
    List<Vector3> normals,
    List<Vector2> uvs,
    List<int> indices, {
    required bool flat,
  }) {
    final turn = at.getNormalMatrix();
    final placed = [for (final point in points) at.transformed3(point)];
    final facing = [
      for (final normal in normals) turn.transformed(normal)..normalize(),
    ];
    for (var i = 0; i < indices.length; i += 3) {
      var a = indices[i], b = indices[i + 1], c = indices[i + 2];
      final face = (placed[b] - placed[a]).cross(placed[c] - placed[a]);
      // The corners of a ball's poles, which meet at a point.
      if (face.length2 < 1e-12) continue;
      if (face.dot(facing[a] + facing[b] + facing[c]) < 0) {
        final swap = b;
        b = c;
        c = swap;
        face.negate();
      }
      face.normalize();
      for (final corner in [a, b, c]) {
        final p = placed[corner];
        final n = flat ? face : facing[corner];
        into.positions
          ..add(p.x)
          ..add(p.y)
          ..add(p.z);
        into.normals
          ..add(n.x)
          ..add(n.y)
          ..add(n.z);
        into.uvs
          ..add(uvs[corner].x)
          ..add(uvs[corner].y);
      }
    }
  }

  /// The model as a binary glTF: one mesh, one primitive per colour.
  Uint8List toGlb() {
    final binary = BytesBuilder(copy: false);
    final views = <Map<String, Object>>[];
    final accessors = <Map<String, Object>>[];
    final materials = <Map<String, Object>>[];
    final primitives = <Map<String, Object>>[];

    int view(Uint8List bytes, int target) {
      final offset = binary.length;
      binary.add(bytes);
      // Every view starts on a four-byte boundary, which floats need.
      while (binary.length % 4 != 0) {
        binary.addByte(0);
      }
      views.add({
        'buffer': 0,
        'byteOffset': offset,
        'byteLength': bytes.length,
        'target': target,
      });
      return views.length - 1;
    }

    int floats(List<double> values, int width, String type) {
      final data = Float32List.fromList(values);
      final low = List.filled(width, double.infinity);
      final high = List.filled(width, double.negativeInfinity);
      for (var i = 0; i < data.length; i++) {
        low[i % width] = math.min(low[i % width], data[i]);
        high[i % width] = math.max(high[i % width], data[i]);
      }
      accessors.add({
        'bufferView': view(data.buffer.asUint8List(), 34962),
        'componentType': 5126,
        'count': values.length ~/ width,
        'type': type,
        // Asked for on positions, and harmless on the rest.
        'min': low,
        'max': high,
      });
      return accessors.length - 1;
    }

    for (final surface in _surfaces.values) {
      final count = surface.positions.length ~/ 3;
      if (count == 0) continue;
      final colour = surface.colour;
      materials.add({
        'pbrMetallicRoughness': {
          'baseColorFactor': [
            _linear((colour >> 16) & 0xFF),
            _linear((colour >> 8) & 0xFF),
            _linear(colour & 0xFF),
            1.0,
          ],
          'metallicFactor': surface.metallic,
          'roughnessFactor': surface.roughness,
        },
      });

      final positions = floats(surface.positions, 3, 'VEC3');
      final normals = floats(surface.normals, 3, 'VEC3');
      final uvs = floats(surface.uvs, 2, 'VEC2');
      final small = count < 65536;
      final order = [for (var i = 0; i < count; i++) i];
      accessors.add({
        'bufferView': view(
          small
              ? Uint16List.fromList(order).buffer.asUint8List()
              : Uint32List.fromList(order).buffer.asUint8List(),
          34963,
        ),
        'componentType': small ? 5123 : 5125,
        'count': count,
        'type': 'SCALAR',
      });
      primitives.add({
        'attributes': {
          'POSITION': positions,
          'NORMAL': normals,
          'TEXCOORD_0': uvs,
        },
        'indices': accessors.length - 1,
        'material': materials.length - 1,
      });
    }

    final bin = binary.takeBytes();
    final json = utf8.encode(
      jsonEncode({
        'asset': {'version': '2.0', 'generator': 'Orblit runner'},
        'scene': 0,
        'scenes': [
          {
            'nodes': [0],
          },
        ],
        'nodes': [
          {'mesh': 0},
        ],
        'meshes': [
          {'primitives': primitives},
        ],
        'materials': materials,
        'accessors': accessors,
        'bufferViews': views,
        'buffers': [
          {'byteLength': bin.length},
        ],
      }),
    );

    // The JSON is padded with spaces and the binary with zeros, each to a
    // four-byte boundary, as the format asks.
    final jsonLength = (json.length + 3) & ~3;
    final binLength = (bin.length + 3) & ~3;
    final total = 12 + 8 + jsonLength + 8 + binLength;
    final out = ByteData(total)
      ..setUint32(0, 0x46546C67, Endian.little)
      ..setUint32(4, 2, Endian.little)
      ..setUint32(8, total, Endian.little)
      ..setUint32(12, jsonLength, Endian.little)
      ..setUint32(16, 0x4E4F534A, Endian.little);
    final bytes = out.buffer.asUint8List();
    bytes.setRange(20, 20 + json.length, json);
    bytes.fillRange(20 + json.length, 20 + jsonLength, 0x20);
    final binAt = 20 + jsonLength;
    out
      ..setUint32(binAt, binLength, Endian.little)
      ..setUint32(binAt + 4, 0x004E4942, Endian.little);
    bytes.setRange(binAt + 8, binAt + 8 + bin.length, bin);
    return bytes;
  }

  static double _linear(int channel) {
    final c = channel / 255;
    return c <= 0.04045
        ? c / 12.92
        : math.pow((c + 0.055) / 1.055, 2.4).toDouble();
  }
}

class _Surface {
  _Surface(this.colour, this.roughness, this.metallic);

  final int colour;
  final double roughness;
  final double metallic;
  final positions = <double>[];
  final normals = <double>[];
  final uvs = <double>[];
}

/// Smooth noise that repeats every [cells] cells, so a texture made of it
/// tiles without a seam.
class _PeriodicNoise {
  _PeriodicNoise(this.cells, {required int seed})
    : _values = _fill(cells * cells, math.Random(seed));

  final int cells;
  final List<double> _values;

  static List<double> _fill(int count, math.Random chance) =>
      List.generate(count, (_) => chance.nextDouble());

  /// The noise at ([u], [v]), each from zero to one across the texture.
  double at(double u, double v) {
    final x = u * cells, y = v * cells;
    final x0 = x.floor(), y0 = y.floor();
    final fx = _ease(x - x0), fy = _ease(y - y0);
    double corner(int i, int j) => _values[(j % cells) * cells + i % cells];
    final top = corner(x0, y0) + (corner(x0 + 1, y0) - corner(x0, y0)) * fx;
    final bottom =
        corner(x0, y0 + 1) + (corner(x0 + 1, y0 + 1) - corner(x0, y0 + 1)) * fx;
    return top + (bottom - top) * fy;
  }

  static double _ease(double t) => t * t * (3 - 2 * t);
}

/// [rgb] as a PNG, [width] by [height], eight bits a channel.
///
/// Stored rather than compressed: the files never leave the process, and a
/// PNG writer that needs no deflate is thirty lines rather than three
/// hundred — or a dependency, or `dart:io`, which the web does not have.
Uint8List encodePng(int width, int height, Uint8List rgb) {
  final rows = BytesBuilder(copy: false);
  for (var row = 0; row < height; row++) {
    rows
      ..addByte(0)
      ..add(Uint8List.sublistView(rgb, row * width * 3, (row + 1) * width * 3));
  }
  final raw = rows.takeBytes();

  // A zlib stream of stored blocks, each at most 65535 bytes.
  final zlib = BytesBuilder(copy: false)..add(const [0x78, 0x01]);
  var at = 0;
  do {
    final length = math.min(65535, raw.length - at);
    final last = at + length >= raw.length;
    zlib
      ..addByte(last ? 1 : 0)
      ..add([length & 0xFF, length >> 8, ~length & 0xFF, (~length >> 8) & 0xFF])
      ..add(Uint8List.sublistView(raw, at, at + length));
    at += length;
  } while (at < raw.length);
  var a = 1, b = 0;
  for (final byte in raw) {
    a = (a + byte) % 65521;
    b = (b + a) % 65521;
  }
  zlib.add(_bigEndian((b << 16) | a));

  final header = ByteData(13)
    ..setUint32(0, width)
    ..setUint32(4, height)
    ..setUint8(8, 8)
    ..setUint8(9, 2);

  final out = BytesBuilder(copy: false)
    ..add(const [137, 80, 78, 71, 13, 10, 26, 10]);
  void chunk(String type, List<int> data) {
    final typed = ascii.encode(type);
    out
      ..add(_bigEndian(data.length))
      ..add(typed)
      ..add(data)
      ..add(_bigEndian(_crc([...typed, ...data])));
  }

  chunk('IHDR', header.buffer.asUint8List());
  chunk('IDAT', zlib.takeBytes());
  chunk('IEND', const []);
  return out.takeBytes();
}

List<int> _bigEndian(int value) => [
  (value >> 24) & 0xFF,
  (value >> 16) & 0xFF,
  (value >> 8) & 0xFF,
  value & 0xFF,
];

final _crcTable = List<int>.generate(256, (n) {
  var c = n;
  for (var k = 0; k < 8; k++) {
    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
  }
  return c;
});

int _crc(List<int> bytes) {
  var c = 0xFFFFFFFF;
  for (final byte in bytes) {
    c = _crcTable[(c ^ byte) & 0xFF] ^ (c >> 8);
  }
  return c ^ 0xFFFFFFFF;
}
