import 'dart:convert';

import 'package:vector_math/vector_math_64.dart';

import 'frame.dart';
import 'kind.dart';

/// A frozen pose with JSON transport and copies of every mutable value.
final class PoseSnapshot {
  PoseSnapshot(ClipFrame frame) : _frame = _checked(frame);

  final ClipFrame _frame;
  late final String _text = jsonEncode(_write(_frame));

  ClipFrame sample() => ClipFrame.mix([_frame], [1], at: _frame.at);

  Map<String, Object?> toJson() => _write(_frame);

  static PoseSnapshot? fromJson(Object? raw) {
    if (raw is! Map<String, Object?>) return null;
    final frame = _read(raw);
    return frame == null ? null : PoseSnapshot(frame);
  }

  static ClipFrame _checked(ClipFrame frame) {
    final copied = _read(_write(frame));
    if (copied == null) {
      throw ArgumentError('A snapshot needs finite, supported pose values.');
    }
    return copied;
  }

  static Map<String, Object?> _write(ClipFrame frame) => {
    'at': frame.at,
    'values': {
      for (final MapEntry(:key, :value) in frame.values.entries)
        key: {
          for (final MapEntry(:key, :value) in value.entries)
            key: _value(value),
        },
    },
    'bones': {
      for (final MapEntry(:key, :value) in frame.bones.entries)
        key: {
          for (final MapEntry(:key, :value) in value.entries)
            key: {
              if (value.position != null) 'position': _value(value.position!),
              if (value.rotation != null) 'rotation': _value(value.rotation!),
              if (value.scale != null) 'scale': _value(value.scale!),
            },
        },
    },
  };

  static Object _value(Object value) => switch (value) {
    final Vector3 v => [v.x, v.y, v.z],
    final Quaternion q => [q.x, q.y, q.z, q.w],
    _ => value,
  };

  static ClipFrame? _read(Map<String, Object?> raw) {
    final at = raw['at'];
    if (at is! num || !at.isFinite) return null;
    if (raw['values'] is! Map<String, Object?>) return null;
    final frame = ClipFrame(at.toDouble());
    if (raw['values'] case final Map<String, Object?> targets) {
      for (final MapEntry(:key, :value) in targets.entries) {
        if (value is! Map<String, Object?>) return null;
        frame.values[key] = {};
        for (final MapEntry(key: property, :value) in value.entries) {
          final decoded = _readValue(value);
          if (decoded == null) return null;
          frame.values[key]![property] = decoded;
        }
      }
    }
    if (!_readBones(raw['bones'], frame)) return null;
    return frame;
  }

  static bool _readBones(Object? raw, ClipFrame frame) {
    if (raw is! Map<String, Object?>) return false;
    for (final MapEntry(:key, :value) in raw.entries) {
      if (value is! Map<String, Object?>) return false;
      frame.bones[key] = {};
      for (final MapEntry(key: bone, :value) in value.entries) {
        final local = _readLocal(value);
        if (local == null) return false;
        frame.bones[key]![bone] = local;
      }
    }
    return true;
  }

  static BoneLocal? _readLocal(Object? raw) {
    if (raw is! Map<String, Object?>) return null;
    final position = _readValue(raw['position']);
    final rotation = _readValue(raw['rotation']);
    final scale = _readValue(raw['scale']);
    if (raw.containsKey('position') && position is! Vector3) return null;
    if (raw.containsKey('rotation') && rotation is! Quaternion) return null;
    if (raw.containsKey('scale') && scale is! Vector3) return null;
    return BoneLocal(
      position: position as Vector3?,
      rotation: rotation as Quaternion?,
      scale: scale as Vector3?,
    );
  }

  static Object? _readValue(Object? raw) {
    if (raw is bool) return raw;
    if (raw is num && raw.isFinite) return raw.toDouble();
    if (raw is! List) return null;
    return raw.length == 3
        ? ChannelKind.vector.read(raw)
        : ChannelKind.rotation.read(raw);
  }

  @override
  bool operator ==(Object other) =>
      other is PoseSnapshot && other._text == _text;

  @override
  int get hashCode => _text.hashCode;
}
