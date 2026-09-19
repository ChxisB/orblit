import 'cook.dart';
import 'importer.dart';

/// The platforms a project can be cooked for, and what each of them samples.
///
/// A cook has to know a device's limits without the device being there, so
/// these are the conservative floor for each platform rather than what the
/// best machine running it could do. Cooking for the floor costs a little
/// size on a good device; cooking for the ceiling means an app that does not
/// run on half the phones it was sold to.
///
/// This is the table that makes "each app build contains only its own target's
/// formats" true. An iOS bundle carries ASTC and no BC, because no iOS device
/// reads BC and shipping it would be dead weight in an app-store download.
abstract final class CookTargets {
  /// macOS, Windows and Linux: desktop GPUs read BC, and always have.
  static const CookTarget macos = CookTarget(
    name: 'macos',
    properties: {
      'textureFamilies': [TextureFamily.bc],
      'maxTextureSize': 16384,
      'halfFloatTextures': true,
    },
  );

  static const CookTarget windows = CookTarget(
    name: 'windows',
    properties: {
      'textureFamilies': [TextureFamily.bc],
      'maxTextureSize': 16384,
      'halfFloatTextures': true,
    },
  );

  static const CookTarget linux = CookTarget(
    name: 'linux',
    properties: {
      'textureFamilies': [TextureFamily.bc],
      'maxTextureSize': 16384,
      'halfFloatTextures': true,
    },
  );

  /// iOS: every Metal device reads ASTC, so there is nothing else to carry.
  /// The size limit is the A7's, because the floor is the point.
  static const CookTarget ios = CookTarget(
    name: 'ios',
    properties: {
      'textureFamilies': [TextureFamily.astc],
      'maxTextureSize': 8192,
      'halfFloatTextures': true,
    },
  );

  /// Android: ASTC where there is ASTC, ETC2 where there is not. ETC2 is
  /// guaranteed by OpenGL ES 3.0, which is the floor the engine targets, and
  /// carrying both is the price of one binary running on both.
  static const CookTarget android = CookTarget(
    name: 'android',
    properties: {
      'textureFamilies': [TextureFamily.astc, TextureFamily.etc2],
      'maxTextureSize': 4096,
      'halfFloatTextures': true,
    },
  );

  /// The web, where a cook cannot know what it is running on.
  ///
  /// Basis Universal rather than a compressed family, because the page is
  /// served to whatever opens it and transcoding on load is the only way one
  /// file works everywhere. It costs decode time at startup and saves a
  /// download that would otherwise carry three formats to use one.
  static const CookTarget web = CookTarget(
    name: 'web',
    properties: {
      'textureFamilies': [TextureFamily.basis],
      'maxTextureSize': 4096,
      'halfFloatTextures': false,
    },
  );

  static const Map<String, CookTarget> byName = {
    'macos': macos,
    'windows': windows,
    'linux': linux,
    'ios': ios,
    'android': android,
    'web': web,
  };

  static const List<String> names = [
    'macos',
    'windows',
    'linux',
    'ios',
    'android',
    'web',
  ];

  /// The target called [name], or null.
  static CookTarget? find(String name) => byName[name.toLowerCase()];
}
