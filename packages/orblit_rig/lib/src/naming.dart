/// What a bone is for.
///
/// A generated rig holds far more bones than a skeleton does, and they are
/// told apart by a prefix rather than by a flag. That looks like a hack until
/// you have to debug one: every bone announces its job in the outliner, in an
/// error message, and in a file diff, without anything having to be loaded.
enum BoneRole {
  /// What an animator grabs. No prefix.
  control,

  /// The original skeleton, copied from the source rig. Drives nothing on its
  /// own; exists so a generated rig can be regenerated against what it came
  /// from.
  original,

  /// What the mesh is bound to. The only bones skinning ever sees.
  deform,

  /// Hidden plumbing — the bones that wire a control to a deform without
  /// either knowing about the other.
  mechanism;

  /// The prefix a bone of this role carries.
  String get prefix => switch (this) {
    BoneRole.control => '',
    BoneRole.original => 'ORG-',
    BoneRole.deform => 'DEF-',
    BoneRole.mechanism => 'MCH-',
  };
}

/// Which side of the body a bone is on.
enum Side {
  left('.L'),
  right('.R');

  const Side(this.suffix);

  final String suffix;

  Side get opposite => this == Side.left ? Side.right : Side.left;
}

/// Reading and writing the names a generated rig uses.
///
/// Conventions rather than data, so two tools that never speak can still agree
/// — which is the point of a convention.
abstract final class BoneNaming {
  static final RegExp _numeric = RegExp(r'\.\d{3}$');

  /// [names], made fit to be bone names: none empty and no two alike.
  ///
  /// A skeleton read from a file is free to leave a joint unnamed or to name
  /// two alike, and an armature is not: bones are found by name, so two
  /// answering to one would be a rig where posing one moves the other. An
  /// unnamed joint is called `joint` and its position in the list. A name
  /// already taken gets `.001`, `.002` and so on, which is what an artist's
  /// tools do to a duplicated bone and so what they will recognise. The first
  /// with a name keeps it, whatever comes after, so a renamed joint never
  /// takes a name from another.
  ///
  /// One function for everything that names joints, so an imported clip and
  /// the model it plays on agree about which bone is which.
  static List<String> unique(List<String> names) {
    final taken = <String>{};
    final out = List<String?>.filled(names.length, null);

    // Every name given is claimed before anything is renamed, so a renamed
    // joint cannot take the name of one that comes after it.
    for (var at = 0; at < names.length; at++) {
      final name = names[at];
      if (name.isNotEmpty && taken.add(name)) out[at] = name;
    }

    for (var at = 0; at < names.length; at++) {
      if (out[at] != null) continue;
      final base = names[at].isEmpty ? 'joint $at' : names[at];
      var name = base;
      for (var copy = 1; !taken.add(name); copy++) {
        name = '$base.${copy.toString().padLeft(3, '0')}';
      }
      out[at] = name;
    }

    return [for (final name in out) name!];
  }

  /// What a bone is for, from its prefix.
  static BoneRole roleOf(String name) {
    for (final role in BoneRole.values) {
      if (role.prefix.isNotEmpty && name.startsWith(role.prefix)) return role;
    }
    return BoneRole.control;
  }

  /// Which side a bone is on, or null if it is on the midline.
  ///
  /// A duplicate suffix is stripped first, so `hand.L.001` is still a left
  /// hand — Blender appends the number after the side and something that only
  /// checked the end of the string would call it sideless.
  static Side? sideOf(String name) {
    final trimmed = name.replaceAll(_numeric, '');
    for (final side in Side.values) {
      if (trimmed.endsWith(side.suffix)) return side;
    }
    return null;
  }

  /// The name with its role prefix and side suffix taken off.
  static String baseOf(String name) {
    var result = name.replaceAll(_numeric, '');

    final role = roleOf(result);
    if (role.prefix.isNotEmpty) {
      result = result.substring(role.prefix.length);
    }

    final side = sideOf(result);
    if (side != null) {
      result = result.substring(0, result.length - side.suffix.length);
    }
    return result;
  }

  /// Builds a name from its parts.
  static String compose(
    String base, {
    BoneRole role = BoneRole.control,
    Side? side,
  }) => '${role.prefix}$base${side?.suffix ?? ''}';

  /// The same bone, in the other role.
  static String asRole(String name, BoneRole role) =>
      compose(baseOf(name), role: role, side: sideOf(name));

  /// The bone on the other side of the body, or null for one on the midline.
  ///
  /// What every mirroring tool needs, and the reason sides are a suffix rather
  /// than a naming habit.
  static String? mirror(String name) {
    final side = sideOf(name);
    if (side == null) return null;
    return compose(baseOf(name), role: roleOf(name), side: side.opposite);
  }

  /// Whether two names describe the same bone on opposite sides.
  static bool areMirrors(String a, String b) => a != b && mirror(a) == b;
}
