using System.Diagnostics.CodeAnalysis;
using Orblit.Filament;
using Orblit.Scene;
using Orblit.Stage;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A scene read from a file, with no editor anywhere.
/// </summary>
/// <remarks>
/// Everything else in this gallery builds its scene in code. This one parses
/// one, the same <c>.oscene</c> text the editor saves, and hands what comes out
/// to the renderer. That is the whole point of the format living in a package
/// of its own: a scene is a document, and reading one should not require the
/// tool that wrote it.
///
/// Both scenes here are version-four documents, which is the shape entities
/// and components arrived in. The 3D one is lit geometry under weather. The 2D
/// one is sprites, which the same renderer draws and the same transforms
/// place. Neither names a file on disk, so this example works in a sandbox.
///
/// The slider is the part worth watching. Moving it does not rebuild the
/// scene. It edits the document, diffs it against the one before, and hands
/// the diff to the view, which rebuilds the one entity that moved and leaves
/// the others alone.
/// </remarks>
public sealed class SceneFilesExample : Example
{
    private const string Solid = "3D scene";
    private const string Sprites = "2D scene";

    private DocumentView _view;

    /// <summary>Which of the two documents is showing.</summary>
    private bool _flat;

    /// <summary>How high the thing that moves is lifted, in metres.</summary>
    private double _lift;

    /// <summary>What the last edit cost, as operations rather than as entities.</summary>
    private int _lastOperations;

    public SceneFilesExample() => Load();

    public override string Name => "Scene files";

    public override string Blurb =>
        "A .oscene document read and drawn, in 2D and in 3D, with an edit applied as a diff rather than a rebuild.";

    public override ExampleSection Section => ExampleSection.Basics;

    public override ViewPoint ViewPoint => _flat ? new() { Yaw = 0, Pitch = 0, Distance = 14 } : new() { Distance = 16 };

    /// <summary>
    /// The gallery owns the camera so that dragging works. A document can name
    /// its own, and the staged scene carries it. This example hands the
    /// gallery's back for the 3D case and flattens the 2D one, because a sprite
    /// scene wants to be looked at square on rather than orbited.
    /// </summary>
    public override RenderScene Scene(RenderCamera camera, double seconds) => _view.Scene with
    {
        Camera = _flat
            ? new RenderCamera { Position = new Vector3(0, 0, 12), Target = Vector3.Zero(), Orthographic = true, ViewHeight = 14 }
            : camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("", [Solid, Sprites], _flat ? Sprites : Solid, option => Show(option == Sprites)),
        new Setting(_flat ? "Lift the coin" : "Lift the crate", _lift, 0, 5, LiftTo) { Unit = " m", Decimals = 1 },
        new Caption(_lastOperations switch
        {
            0 => "Edits are applied as diffs.",
            1 => "Last edit: 1 operation, and only the entity it named was rebuilt.",
            var many => $"Last edit: {many} operations, and only the entity it named was rebuilt.",
        }),
    ];

    public override string Code => """
        // The file, as the editor saves it.
        var load = SceneDocument.Decode(text);
        foreach (var problem in load.Problems)
        {
            Console.WriteLine(problem);   // what could not be read, and why
        }

        // Staged for the renderer: entities become objects, lights, sprite layers.
        var view = new DocumentView(load.Document, projectRoot: root);

        // Every frame. The lists are assembled. The objects in them are kept.
        RenderScene Scene(RenderCamera camera, double seconds) => view.Scene;

        // An edit. Diffed against what was there, so only the entity that moved is
        // rebuilt. The others are the objects the renderer already has.
        var next = document.WithEntity(id, moved);
        view.Apply(SceneDiff.Between(document, next));
        """;

    /// <summary>
    /// Re-reads the file. The two scenes are different documents, not two views
    /// of one, and swapping the flag while leaving the parsed document in place
    /// would show the 3D scene and label it the 2D one.
    /// </summary>
    private void Show(bool flat)
    {
        if (flat == _flat)
        {
            return;
        }

        _flat = flat;
        Load();
    }

    [MemberNotNull(nameof(_view))]
    private void Load()
    {
        var load = SceneDocument.Decode(_flat ? FlatScene : SolidScene);
        // A file that could not be fully read still draws, and says what it lost.
        Note = load.HasProblems ? string.Join(' ', load.Problems) : null;
        _view = new DocumentView(load.Document);
        _lift = 0;
        _lastOperations = 0;
    }

    /// <summary>Raises one entity by editing the document, not the scene.</summary>
    private void LiftTo(double metres)
    {
        var document = _view.Document;
        var id = _flat ? "coin" : "crate";
        if (document[id] is not { } entity)
        {
            return;
        }

        var was = entity[SceneComponents.Transform] as TransformComponent;
        var position = was?.Position ?? Vector3.Zero();
        var moved = new TransformComponent(new Vector3(position.X, metres, position.Z), was?.Rotation, was?.Scale);
        var next = document.WithEntity(id, entity.WithComponent(SceneComponents.Transform, moved));

        var diff = SceneDiff.Between(document, next);
        _lastOperations = diff.Operations.Count;
        _view.Apply(diff);
        _lift = metres;
    }

    /// <summary>
    /// A lit 3D scene: a floor, three crates on it, a sun and some haze. Written
    /// out in full rather than built with the API, because what this example
    /// shows is the file. An <c>.oscene</c> is meant to be readable by whoever
    /// opens it in a diff, and this is what one looks like.
    /// </summary>
    private const string SolidScene = """
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
        """;

    /// <summary>
    /// A 2D scene: sprite entities, placed by the same transforms. No texture is
    /// named, so each draws as a rectangle of its own colour. That is the
    /// renderer's own behaviour for a layer with no picture, and it keeps this
    /// example free of any file at all.
    /// </summary>
    private const string FlatScene = """
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
        """;
}
