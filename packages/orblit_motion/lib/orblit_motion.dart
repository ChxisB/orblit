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
library;

export 'package:orblit_sequence/orblit_sequence.dart'
    show Easing, Hold, Key, Mark, WhenDone;

export 'src/blend.dart'
    show
        BlendChange,
        BlendDocument,
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
export 'src/condition.dart' show BlendCondition;
export 'src/frame.dart' show BoneLocal, ClipFrame;
export 'src/import.dart' show ClipsImported, clipsFromGltf;
export 'src/kind.dart' show ChannelKind;
export 'src/place.dart' show BlendPlace;
export 'src/player.dart' show ClipPlayer, ClipStep;
export 'src/root.dart' show RootStep;
export 'src/scene.dart' show ClipScope, sceneOpsFor;
export 'src/source.dart'
    show BlendClip, BlendLine, BlendPlane, BlendSource, LinePoint, PlanePoint;
