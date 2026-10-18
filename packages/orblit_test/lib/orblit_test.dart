/// Tests for a game that run with no window.
///
/// A [TestStage] steps sampled time one frame at a time and holds the controls
/// a player would. Finders and matchers read a scene document and recorded
/// sound, and [describeChange] writes what a frame changed as text, so a game
/// can compare it with a file instead of with pixels.
///
/// Nothing here draws and nothing here needs Flutter.
library;

export 'src/capture.dart' show describeChange, matchesTextFile;
export 'src/controls.dart' show ScriptedControls;
export 'src/finders.dart' show SceneFinders;
export 'src/matchers.dart'
    show hasComponent, hasPlayed, isPositionedNear, isSounding;
export 'src/stage.dart' show Frame, TestStage;
