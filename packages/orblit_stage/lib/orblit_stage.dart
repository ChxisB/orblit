/// A scene document, staged for a renderer.
///
/// The document says what a scene *is*; this says what to do with it. Entities
/// and their components become objects, lights, clouds and sprite layers, the
/// scene's own settings and its weather become the sky and the air, and a
/// [SceneDiff] moves only what it touched.
///
/// Its own package because it is the one piece that has to know both halves.
/// `orblit_scene` has no renderer in it, on purpose — a runtime, an importer
/// and a command-line cook step all read scenes and none of them draw. And
/// `orblit_filament` has no artist units in it, on purpose — it takes light in
/// the units a renderer works in, and an app using it directly should not have
/// to carry a scene format, a set of migrations and a weather model to do so.
///
/// It also binds an `orblit_rig` armature to a model's skin, and turns an
/// `orblit_terrain` terrain, and what is scattered over it, into what the
/// renderer draws, for the same reason: a rig and a terrain know nothing about
/// renderers, and the renderer knows nothing about either.
///
/// And it plays cutscenes over a staged scene with [OrblitCutscenes]: an
/// `orblit_motion` cutscene knows which camera to look through, and this is
/// where that becomes the view.
library;

export 'src/cutscenes.dart'
    show OrblitCutsceneStep, OrblitCutscenes, blendCameras;
export 'src/document_view.dart' show OrblitDocumentView;
export 'src/material_view.dart' show materialFrom;
export 'src/scatter_view.dart' show scatterFrom;
export 'src/skin_binding.dart'
    show OrblitSkinBinding, armatureOfSkin, boneNamesOfSkin;
export 'src/terrain_view.dart' show terrainFrom;
