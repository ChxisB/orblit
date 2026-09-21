import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';

/// A scene read from a file, with no editor anywhere.
///
/// Everything else in this gallery builds its scene in Dart. This one parses
/// one — the same `.oscene` text the editor saves — and hands what comes out
/// to the renderer. That is the whole point of the format living in a package
/// of its own: a scene is a document, and reading one should not require the
/// tool that wrote it.
///
/// Both scenes here are version-four documents, which is the shape entities
/// and components arrived in. The 3D one is lit geometry under weather; the 2D
/// one is sprites, which the same renderer draws and which are placed by the
/// same transforms. Neither mentions a file on disk, so this example works in
/// a browser and in a sandbox.
///
/// The slider is the part worth watching. Moving it does not rebuild the
/// scene: it edits the document, diffs it against the one before, and hands
/// the diff to the view — which rebuilds the one entity that moved and leaves
/// the other twenty alone.
class SceneFilesExample extends Example {
  SceneFilesExample() {
    _load();
  }

  @override
  String get name => 'Scene files';

  @override
  ExampleSection get section => ExampleSection.basics;

  @override
  String get blurb =>
      'A .oscene document read and drawn, in 2D and in 3D, with an edit '
      'applied as a diff rather than a rebuild.';

  @override
  ViewPoint get viewpoint => flat
      ? const ViewPoint(yaw: 0, pitch: 0, distance: 14)
      : const ViewPoint(distance: 16);

  /// Which of the two documents is showing.
  ///
  /// A setter rather than a field because changing it has to re-read the file:
  /// the two scenes are different documents, not two views of one, and a
  /// toggle that swapped the flag while leaving the parsed document in place
  /// would show the 3D scene and label it the 2D one.
  bool get flat => _flat;

  set flat(bool wanted) {
    if (wanted == _flat) return;
    _flat = wanted;
    _load();
  }

  bool _flat = false;

  /// How high the thing that moves is lifted, in metres.
  double lift = 0;

  /// What the last edit cost, as operations rather than as entities.
  int lastOperations = 0;

  OrblitDocumentView? _view;
  SceneDocument? _current;

  void _load() {
    final load = SceneDocument.decode(flat ? _flatScene : _solidScene);
    // A file that could not be fully read still draws, and says what it lost.
    note = load.hasProblems ? load.problems.join(' ') : null;
    _current = load.document;
    _view = OrblitDocumentView(load.document);
    lift = 0;
    lastOperations = 0;
  }

  /// Raises one entity by editing the document, not the scene.
  ///
  /// What the slider does, and public so that it can be driven by something
  /// other than a finger.
  void liftTo(double metres) {
    final view = _view;
    final document = _current;
    if (view == null || document == null) return;

    final id = flat ? 'coin' : 'crate';
    final entity = document[id];
    if (entity == null) return;

    final was = entity[SceneComponents.transform];
    final position = was is TransformComponent
        ? was.position.clone()
        : Vector3.zero();
    final next = document.withEntity(
      id,
      entity.withComponent(
        SceneComponents.transform,
        TransformComponent(
          position: Vector3(position.x, metres, position.z),
          rotation: was is TransformComponent ? was.rotation : null,
          scale: was is TransformComponent ? was.scale : null,
        ),
      ),
    );

    final diff = SceneDiff.between(document, next);
    lastOperations = diff.operations.length;
    view.apply(diff);
    _current = view.document;
    lift = metres;
  }

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    final view = _view;
    if (view == null) {
      return OrblitScene(objects: const [], camera: camera);
    }

    // The gallery owns the camera so that dragging works. A document can name
    // its own, and the staged scene carries it — this example hands it back
    // for the 3D case and flattens the 2D one, because a sprite scene wants to
    // be looked at square on rather than orbited.
    return view.scene.copyWith(
      camera: flat
          ? OrblitCamera(
              position: Vector3(0, 0, 12),
              target: Vector3.zero(),
              orthographic: true,
              viewHeight: 14,
            )
          : camera,
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      SegmentedButton<bool>(
        segments: const [
          ButtonSegment(value: false, label: Text('3D scene')),
          ButtonSegment(value: true, label: Text('2D scene')),
        ],
        selected: {flat},
        onSelectionChanged: (wanted) {
          flat = wanted.first;
          changed();
        },
      ),
      const SizedBox(height: 16),
      Text(
        'Lift ${flat ? 'the coin' : 'the crate'}: '
        '${lift.toStringAsFixed(1)} m',
      ),
      Slider(
        value: lift,
        max: 5,
        onChanged: (value) {
          liftTo(value);
          changed();
        },
      ),
      Text(
        lastOperations == 0
            ? 'Edits are applied as diffs.'
            : 'Last edit: $lastOperations operation'
                  '${lastOperations == 1 ? '' : 's'}, and only the entity it '
                  'named was rebuilt.',
        style: Theme.of(context).textTheme.bodySmall,
      ),
    ],
  );

  @override
  String get code => '''
// The file, as the editor saves it.
final load = SceneDocument.decode(text);
for (final problem in load.problems) {
  print(problem);  // what could not be read, and why
}

// Staged for the renderer: entities become objects, lights, sprite layers.
final view = OrblitDocumentView(load.document, projectRoot: root);

// Every frame. The lists are assembled; the objects in them are kept.
OrblitScene scene(OrblitCamera camera, double seconds) => view.scene;

// An edit. Diffed against what was there, so only the entity that moved is
// rebuilt -- the other twenty are the objects the renderer already has.
final next = document.withEntity(id, moved);
view.apply(SceneDiff.between(document, next));
''';

  /// A lit 3D scene: a floor, three crates on it, a sun and some haze.
  ///
  /// Written out in full rather than built with the API, because what this
  /// example is showing is the file — an `.oscene` is meant to be readable by
  /// whoever opens it in a diff, and this is what one looks like.
  static const String _solidScene = '''
{
  "formatVersion": 4,
  "name": "Yard",
  "sky": "#2B3440",
  "ambient": 9000.0,
  "time": { "hour": 15.0, "cycle": false, "hoursPerSecond": 0.5 },
  "entities": [
    {
      "id": "sun",
      "name": "Sun",
      "components": {
        "transform": {
          "position": [0.0, 12.0, 6.0],
          "rotation": [-55.0, -30.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "light": {
          "kind": "sun",
          "power": 90.0,
          "colour": "#FFF2DC",
          "spotSize": 45.0,
          "spotBlend": 0.15,
          "sourceRadius": 0.1,
          "sunAngle": 0.526,
          "body": "sun",
          "castShadows": true
        }
      }
    },
    {
      "id": "floor",
      "name": "Floor",
      "components": {
        "transform": {
          "position": [0.0, -0.5, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [16.0, 1.0, 16.0]
        },
        "mesh": {
          "colour": "#6E7480",
          "castShadows": false,
          "receiveShadows": true
        }
      }
    },
    {
      "id": "stack",
      "name": "Stack",
      "components": {
        "transform": {
          "position": [-3.0, 0.5, 0.0],
          "rotation": [0.0, 22.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        }
      }
    },
    {
      "id": "lower",
      "name": "Lower crate",
      "parent": "stack",
      "components": {
        "transform": {
          "position": [0.0, 0.0, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "mesh": { "colour": "#B4703C" }
      }
    },
    {
      "id": "upper",
      "name": "Upper crate",
      "parent": "stack",
      "components": {
        "transform": {
          "position": [0.0, 1.05, 0.0],
          "rotation": [0.0, 18.0, 0.0],
          "scale": [0.8, 0.8, 0.8]
        },
        "mesh": { "colour": "#C98A4B" }
      }
    },
    {
      "id": "crate",
      "name": "Loose crate",
      "components": {
        "transform": {
          "position": [2.5, 0.5, 1.0],
          "rotation": [0.0, -14.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "mesh": { "colour": "#8E9BA8" }
      }
    },
    {
      "id": "air",
      "name": "Weather",
      "components": {
        "weather": {
          "condition": "hazy",
          "windDirection": 120.0,
          "transition": 8.0,
          "air": {
            "cover": 0.35,
            "colour": "#8C97A6",
            "density": 0.035,
            "height": 6.0,
            "falloff": 0.4,
            "mist": 0.2,
            "size": 26.0,
            "wind": 2.0,
            "cloudHeight": 900.0
          }
        }
      }
    }
  ]
}
''';

  /// A 2D scene: sprite entities, placed by the same transforms.
  ///
  /// No texture is named, so each draws as a rectangle of its own colour —
  /// which is the renderer's own behaviour for a layer with no picture, and
  /// keeps this example free of any file at all.
  static const String _flatScene = '''
{
  "formatVersion": 4,
  "name": "Flat",
  "sky": "#1B2430",
  "ambient": 16000.0,
  "time": { "hour": 10.0, "cycle": false, "hoursPerSecond": 0.5 },
  "entities": [
    {
      "id": "backdrop",
      "name": "Backdrop",
      "components": {
        "transform": {
          "position": [0.0, 0.0, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "sprite": {
          "width": 20.0,
          "height": 12.0,
          "depth": -10.0,
          "pivotX": 0.5,
          "pivotY": 0.5,
          "colour": "#243247",
          "opacity": 1.0
        }
      }
    },
    {
      "id": "ground",
      "name": "Ground",
      "components": {
        "transform": {
          "position": [0.0, -4.0, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "sprite": {
          "width": 20.0,
          "height": 4.0,
          "depth": -5.0,
          "pivotX": 0.5,
          "pivotY": 0.5,
          "colour": "#3E5533",
          "opacity": 1.0
        }
      }
    },
    {
      "id": "coin",
      "name": "Coin",
      "components": {
        "transform": {
          "position": [-2.0, 0.0, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "sprite": {
          "width": 1.2,
          "height": 1.2,
          "depth": 2.0,
          "pivotX": 0.5,
          "pivotY": 0.5,
          "colour": "#E8C34A",
          "opacity": 1.0
        }
      }
    },
    {
      "id": "spark",
      "name": "Spark",
      "parent": "coin",
      "components": {
        "transform": {
          "position": [0.0, 1.2, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "sprite": {
          "width": 0.6,
          "height": 0.6,
          "depth": 3.0,
          "pivotX": 0.5,
          "pivotY": 0.5,
          "colour": "#FFF0B0",
          "opacity": 0.9,
          "additive": true
        }
      }
    },
    {
      "id": "post",
      "name": "Post",
      "components": {
        "transform": {
          "position": [3.5, -1.0, 0.0],
          "rotation": [0.0, 0.0, 0.0],
          "scale": [1.0, 1.0, 1.0]
        },
        "sprite": {
          "width": 0.8,
          "height": 6.0,
          "depth": 1.0,
          "pivotX": 0.5,
          "pivotY": 0.5,
          "colour": "#6B4F3A",
          "opacity": 1.0
        }
      }
    }
  ]
}
''';
}
