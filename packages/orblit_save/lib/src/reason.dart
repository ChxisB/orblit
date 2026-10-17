/// Why a save was made. It is stored beside the bytes, so an operator
/// reading history can tell a routine save from one made as a player fell off
/// the network.
sealed class SaveReason {
  const SaveReason();

  /// Reads what [label] wrote. Throws [FormatException] for anything else.
  factory SaveReason.parse(String label) => switch (label) {
    'autosave' => const Autosave(),
    'leave' => const Leave(),
    'disconnect' => const Disconnect(),
    'shutdown' => const Shutdown(),
    'error' => const Failed(),
    _ when label.startsWith(_customPrefix) => Custom(
      label.substring(_customPrefix.length),
    ),
    _ => throw FormatException('Not a save reason', label),
  };

  /// A short name that [SaveReason.parse] reads back.
  String get label;

  @override
  bool operator ==(Object other) => other is SaveReason && other.label == label;

  @override
  int get hashCode => label.hashCode;

  @override
  String toString() => label;
}

const String _customPrefix = 'custom:';

/// The longest text a [Custom] reason holds.
const int _maxCustomReason = 64;

/// A routine save of a player who is still playing.
final class Autosave extends SaveReason {
  const Autosave();

  @override
  String get label => 'autosave';
}

/// A player left on purpose.
final class Leave extends SaveReason {
  const Leave();

  @override
  String get label => 'leave';
}

/// A player's connection ended and the game gave up waiting.
final class Disconnect extends SaveReason {
  const Disconnect();

  @override
  String get label => 'disconnect';
}

/// The server is stopping.
final class Shutdown extends SaveReason {
  const Shutdown();

  @override
  String get label => 'shutdown';
}

/// The game saved while something was wrong.
final class Failed extends SaveReason {
  const Failed();

  @override
  String get label => 'error';
}

/// A reason the game names itself, such as `pre-restore`.
final class Custom extends SaveReason {
  Custom(this.text) {
    if (text.isEmpty || text.length > _maxCustomReason || text.contains('\n')) {
      throw ArgumentError.value(
        text,
        'text',
        'is empty, over $_maxCustomReason characters, or has a new line',
      );
    }
  }

  final String text;

  @override
  String get label => '$_customPrefix$text';
}
