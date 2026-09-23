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
library;

export 'package:orblit_sequence/orblit_sequence.dart'
    show Easing, Hold, Key, Mark, WhenDone;

export 'src/clip.dart'
    show
        ClipChannel,
        ClipDocument,
        ClipFormatException,
        ClipLoad,
        ClipMigration,
        RootMotion,
        clipExtension;
export 'src/frame.dart' show BoneLocal, ClipFrame;
export 'src/import.dart' show ClipsImported, clipsFromGltf;
export 'src/kind.dart' show ChannelKind;
export 'src/player.dart' show ClipPlayer, ClipStep;
export 'src/root.dart' show RootStep;
export 'src/scene.dart' show ClipScope, sceneOpsFor;
