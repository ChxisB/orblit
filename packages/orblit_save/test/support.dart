import 'dart:typed_data';

import 'package:orblit_save/orblit_save.dart';

Uint8List bytesOf(List<int> values) => Uint8List.fromList(values);

SaveRecord recordOf(
  List<int> values, {
  SaveReason reason = const Autosave(),
  int at = 1000,
}) => SaveRecord(
  bytes: bytesOf(values),
  reason: reason,
  at: DateTime.fromMillisecondsSinceEpoch(at, isUtc: true),
);

DateTime fixedClock() => DateTime.fromMillisecondsSinceEpoch(5000, isUtc: true);
