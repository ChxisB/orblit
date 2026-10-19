import 'dart:typed_data';

import 'reason.dart';

/// One save: the game's bytes, why they were saved and when.
final class SaveRecord {
  /// Copies [bytes], so the caller may reuse its buffer. [at] keeps its
  /// milliseconds and nothing finer, which is what a file stores.
  SaveRecord({
    required Uint8List bytes,
    required this.reason,
    required DateTime at,
  }) : bytes = Uint8List.fromList(bytes),
       at = DateTime.fromMillisecondsSinceEpoch(
         at.millisecondsSinceEpoch,
         isUtc: true,
       );

  final Uint8List bytes;
  final SaveReason reason;
  final DateTime at;
}
