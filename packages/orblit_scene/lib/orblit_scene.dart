/// A scene as a document.
///
/// Entities with stable ids, a parent and a place in the order; components
/// that say what each entity is; an ordered list of migrations from every
/// older format; and diffs between two documents that can be applied and
/// inverted.
///
/// Nothing here draws anything and nothing here knows what a renderer is. That
/// is the point of it being its own package: the editor, the runtime, an
/// importer and a command-line cook step all need to agree on what a scene
/// *is*, and three of those four have no Flutter in them. It has none either.
library;

export 'src/component.dart'
    show ComponentReader, SceneComponent, SceneComponents, UnknownComponent;
export 'src/components/body.dart' show BodyComponent, BodyMotion, BodyShape;
export 'src/components/data.dart'
    show DataComponent, PrefabComponent, PrefabState;
export 'src/components/drawing.dart'
    show MaterialComponent, MeshComponent, SplatsComponent;
export 'src/components/flat.dart'
    show
        CanvasComponent,
        ParallaxComponent,
        ParallaxLayer,
        SpriteComponent,
        TilemapComponent;
export 'src/components/motion.dart' show MotionComponent;
export 'src/components/staging.dart'
    show CameraComponent, LightComponent, WeatherComponent;
export 'src/components/terrain.dart' show TerrainComponent;
export 'src/components/transform.dart' show TransformComponent;
export 'src/diff.dart'
    show
        AddEntity,
        RemoveEntity,
        Reorder,
        Reparent,
        SceneDiff,
        SceneOp,
        SetComponent,
        SetEntityName,
        SetField,
        SetSetting,
        SetVisible;
export 'src/document.dart'
    show
        SceneDocument,
        SceneFormatException,
        SceneLoad,
        SceneSettings,
        sceneExtension;
export 'src/entity.dart' show SceneEntity;
export 'src/export/export.dart' show SceneExport, SceneFormat, SceneWritten;
export 'src/export/gltf.dart' show GltfScene, sceneToGltf;
export 'src/export/graft.dart' show GraftFailure, Grafted, graft, regraft;
export 'src/export/obj.dart' show ObjScene, sceneToObj;
export 'src/import/gltf.dart' show SceneImported, gltfToScene;
export 'src/import/import.dart' show readSceneFrom;
export 'src/instances.dart'
    show
        PrefabEdit,
        PrefabException,
        PrefabSource,
        applyInstance,
        expandInstances,
        foldInstances,
        makePrefab,
        refreshInstances,
        revertInstance,
        unpackInstance;
export 'src/material.dart'
    show
        MaterialDocument,
        MaterialField,
        MaterialFields,
        MaterialKind,
        MaterialLibrary,
        MaterialLoad,
        ResolvedMaterial,
        materialExtension;
export 'src/migration.dart' show SceneMigration, SceneMigrations;
export 'src/path.dart' show EntityPath;
export 'src/prefab.dart' show PrefabDocument, PrefabLoad, prefabExtension;
export 'src/values.dart' show Values;
