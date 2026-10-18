import 'dart:ui' show Size;

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart';

/// One label hanging in the scene: a name over a head, a number over a hit.
final class Nameplate {
  const Nameplate({
    required this.id,
    required this.text,
    required this.position,
  });

  /// Tells this label from the others. It is added to [Nameplates.keyBase] to
  /// make the label's object and material keys.
  final int id;

  final String text;

  /// The middle of the label.
  final Vector3 position;
}

/// Many short labels drawn as quads that square up to the view.
///
/// A scene with two hundred names above two hundred heads cannot afford two
/// hundred widget trees. Every distinct string is drawn once into the shared
/// [atlas], and each label is a quad that shows its string's rectangle of that
/// one picture. Two labels with the same text share the rectangle.
///
/// [sync] and [build] are the two halves. [sync] adds strings to the picture
/// and hands the renderer the new one. [build] turns the labels of this frame
/// into scene objects and materials, nearest first, so it is a query that can
/// be called every frame.
///
/// A label fades out between [fadeStart] and [fadeEnd], measured from the
/// camera's position, and is dropped past [fadeEnd]. Past [maxLabels] only the
/// nearest are kept. A label whose text [sync] has not drawn yet is left out.
/// One added during a [sync] still in flight shows blank until it lands.
///
/// There is no test against walls, so a label behind one shows through it.
final class Nameplates {
  Nameplates({
    required this.atlas,
    required this.fadeStart,
    required this.fadeEnd,
    this.maxLabels = 256,
    this.metresPerPixel = 0.01,
    this.keyBase = 0x4e500000,
    this.id = 'nameplates',
  }) : assert(fadeStart < fadeEnd);

  /// The picture every label reads. The caller sets its style and width, and
  /// this adds strings to it.
  final LabelAtlas atlas;

  final double fadeStart;
  final double fadeEnd;
  final int maxLabels;

  /// How big a label's text is in the world, so a 32 pixel line is
  /// `32 * metresPerPixel` metres high.
  final double metresPerPixel;

  /// The first key this uses. Pick a range no other object or material is in.
  final int keyBase;

  /// Names this picture, so two sets of labels never share one.
  final String id;

  /// A quad is the cube squashed to this depth.
  static const double _depth = 0.001;

  String? _name;
  Size? _size;
  int _picture = 0;

  /// Draws any string [atlas] lacks and hands the renderer the new picture.
  /// Publish the scene again afterwards, because [build] names it.
  ///
  /// Await one call before making the next, and before [dispose].
  ///
  /// The old picture's bytes are released and its texture is not. The
  /// renderer keeps what it already made until it is disposed, so each new
  /// picture adds to memory. Add strings in batches and not one at a time.
  ///
  /// A string wider than the atlas, or one that no longer fits at its
  /// `maxHeight`, throws a [StateError]. Trim long names before they get here.
  Future<void> sync(Iterable<String> texts) async {
    texts.forEach(atlas.add);
    if (!atlas.isDirty) return;
    final size = atlas.size;
    final bytes = await atlas.paint();
    final next = OrblitResources.nameFor('panel/$id/${++_picture}.png');
    await OrblitResources.provide(next, bytes);
    final old = _name;
    _name = next;
    _size = size;
    if (old != null) await OrblitResources.release(old);
  }

  /// Lets go of the picture the renderer holds. The atlas stays the caller's.
  Future<void> dispose() async {
    final name = _name;
    _name = null;
    if (name != null) await OrblitResources.release(name);
  }

  /// The objects and materials to put in the scene for [labels] seen from
  /// [camera]. Empty until the first [sync] lands.
  ({List<OrblitObject> objects, List<OrblitMaterial> materials}) build(
    OrblitCamera camera,
    Iterable<Nameplate> labels,
  ) {
    final objects = <OrblitObject>[];
    final materials = <OrblitMaterial>[];
    final name = _name;
    final size = _size;
    if (name == null || size == null) {
      return (objects: objects, materials: materials);
    }
    final near = <(double, Nameplate)>[];
    for (final label in labels) {
      final distance = label.position.distanceTo(camera.position);
      if (distance < fadeEnd && atlas.rectOf(label.text) != null) {
        near.add((distance, label));
      }
    }
    near.sort((a, b) => a.$1.compareTo(b.$1));
    final turn = _squareTo(camera);
    for (final (distance, label) in near.take(maxLabels)) {
      final rect = atlas.rectOf(label.text)!; // checked in the loop above
      final key = keyBase + label.id;
      final alpha = (fadeEnd - distance) / (fadeEnd - fadeStart);
      objects.add(
        OrblitObject(
          key: key,
          material: key,
          transform: Matrix4.compose(
            label.position,
            turn,
            Vector3(
              rect.width * metresPerPixel / 2,
              rect.height * metresPerPixel / 2,
              _depth,
            ),
          ),
          colour: Vector3(1, 1, 1),
          castShadows: false,
          receiveShadows: false,
        ),
      );
      materials.add(
        OrblitMaterial(
          key: key,
          shading: OrblitShading.unlit,
          blend: OrblitBlend.fade,
          baseColour: Vector4(1, 1, 1, alpha.clamp(0.0, 1.0)),
          // The rectangle's rows run top down and the cube's v runs bottom up.
          tiling: Vector2(rect.width / size.width, -rect.height / size.height),
          offset: Vector2(rect.left / size.width, rect.bottom / size.height),
          wrap: OrblitWrap.clamp,
          baseColourMap: OrblitTexture(name),
        ),
      );
    }
    return (objects: objects, materials: materials);
  }

  /// A turn that puts a quad's front square to the view. It is the camera's
  /// own turn and not a turn toward the eye, so text stays upright and does
  /// not skew towards the edge of the screen.
  Quaternion _squareTo(OrblitCamera camera) {
    final back = (camera.position - camera.target)..normalize();
    // shortcut: up is world Y because the camera has no roll. Looking straight
    // up or down has no right-hand side, so labels there face along x. Upgrade
    // when the camera gains an up vector.
    final right = Vector3(0, 1, 0).cross(back);
    if (right.length2 < 1e-12) {
      right.setValues(1, 0, 0);
    } else {
      right.normalize();
    }
    return Quaternion.fromRotation(
      Matrix3.columns(right, back.cross(right), back),
    );
  }
}
