import '../importer.dart';

/// The file name of the pack an app ships for [target], such as
/// `game.macos.opack`.
///
/// The target is in the name so that each build writes a file only that
/// target reads. A build hook is skipped when nothing it watched has changed,
/// so a fixed name would hold whatever the last build for any platform wrote,
/// and a macOS build that followed an iOS one would ship the iOS pack.
///
/// The build writes the pack under this name and the running app looks for
/// it under the same one, which is why both call this.
String packFileName(CookTarget target, {String name = 'game'}) =>
    '$name.${target.name}.opack';
