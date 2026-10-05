/// Clips: animation as an asset.
///
/// A clip is channels of keys, each moving one thing — a property of an
/// entity, or a bone of a model — named by its path from whoever plays the
/// clip, so one clip plays on every copy of a prefab. Sampling a clip is
/// stateless, like a sequence, so a timeline can scrub it; playing one is a
/// [ClipPlayer], which is where marks fire and root motion is measured.
///
/// Clips come from two places: written in the editor, and imported from a
/// glTF file's animations by [clipsFromGltf]. Both are the same `.oclip`
/// file, one key to a line so two takes diff as the keys that changed.
///
/// Which clips play, and how much each has to say, is a blend: a
/// [BlendDocument] of states, each playing a clip or a mix of clips along
/// one input or over two, and changes between them that fade. Where a blend
/// has got to is a [BlendPlace], a value that can be saved, sent and built
/// by hand, so a character can be restored mid-stride and a test can ask
/// what happens next without playing up to it.
///
/// A cutscene is the scene played as a whole: a [CutsceneDocument] of keys
/// on the scene's own ids, shots that say which camera is looked through
/// when, and sounds. It is a `.ocutscene` file written the way a clip is,
/// and played by a [Director] over its sequence.
///
/// A clip made for one skeleton can move another. [retargetClip] turns each
/// bone to stand in the world as the bone driving it did, given the
/// [RestSkeleton] of each, and [matchBones] pairs them by name.
library;

export 'package:orblit_sequence/orblit_sequence.dart'
    show Director, Easing, Hold, Key, Mark, ShotAt, SoundAt, WhenDone;

export 'src/blend.dart'
    show
        BlendChange,
        BlendDocument,
        BlendFade,
        BlendFormatException,
        BlendLoad,
        BlendMigration,
        BlendState,
        BlendStep,
        ClipWeight,
        blendExtension;
export 'src/blend_player.dart' show BlendPlayer;
export 'src/clip.dart'
    show
        ClipChannel,
        ClipDocument,
        ClipFormatException,
        ClipLoad,
        ClipMigration,
        RootMotion,
        clipExtension;
export 'src/cutscene.dart'
    show
        CutsceneDocument,
        CutsceneFormatException,
        CutsceneLoad,
        CutsceneMigration,
        CutsceneShot,
        CutsceneSound,
        cutsceneExtension;
export 'src/condition.dart' show BlendCondition;
export 'src/frame.dart' show BoneLocal, ClipFrame;
export 'src/import.dart'
    show ClipsImported, clipsFromGltf, restSkeletonsFromGltf;
export 'src/kind.dart' show ChannelKind;
export 'src/layer.dart'
    show
        BoneMask,
        ClipLayer,
        LayerPlace,
        LayerStep,
        addFrame,
        completeFrame,
        layerFrame,
        restFrame;
export 'src/place.dart' show BlendPlace, BlendRoute;
export 'src/player.dart' show ClipPlayer, ClipStep;
export 'src/rest.dart' show RestSkeleton;
export 'src/retarget.dart' show ClipRetargeted, matchBones, retargetClip;
export 'src/root.dart' show RootStep;
export 'src/scene.dart' show ClipScope, sceneOpsFor;
export 'src/snapshot.dart' show PoseSnapshot;
export 'src/source.dart'
    show
        BlendClip,
        BlendGraph,
        BlendLine,
        BlendPlane,
        BlendSource,
        LinePoint,
        PlanePoint;
