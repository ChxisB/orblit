import 'dart:typed_data';

import 'history.dart';
import 'history_store.dart';
import 'reason.dart';
import 'record.dart';

/// What [SaveTools.restorePlan] found: the versions it would write, and the
/// keys it left alone because a player is using them.
final class RestorePlan {
  const RestorePlan({required this.changes, required this.live});

  final List<HistoryEntry> changes;
  final List<String> live;
}

/// What an operator does to saves: restore a version, restore many, edit
/// one. A save that [isLive] says a player is using is never touched,
/// because the game would write over it.
final class SaveTools {
  SaveTools({
    required this.store,
    required this.isLive,
    required DateTime Function() clock,
  }) : _clock = clock;

  final HistoryStore store;
  final bool Function(String key) isLive;
  final DateTime Function() _clock;

  void _refuseLive(String key) {
    if (isLive(key)) throw StateError('The save "$key" is in use');
  }

  /// Makes the version of [key] saved at [at] the current save. The save it
  /// replaces stays in the history as `pre-restore`.
  Future<void> restore(String key, DateTime at) async {
    _refuseLive(key);
    final versions = await store.history.list(key);
    final entry = versions.where((e) => e.at == at).firstOrNull;
    if (entry == null) throw StateError('"$key" has no version at $at');
    await _restoreEntry(entry);
  }

  Future<void> _restoreEntry(HistoryEntry entry) async {
    final current = await store.read(entry.key);
    if (current != null) {
      await store.history.record(
        entry.key,
        SaveRecord(
          bytes: current.bytes,
          reason: Custom('pre-restore'),
          at: _clock(),
        ),
      );
    }
    final version = await store.history.load(entry);
    await store.write(
      entry.key,
      SaveRecord(bytes: version.bytes, reason: Custom('restore'), at: _clock()),
    );
  }

  /// What restoring every save to its newest version at or before [before]
  /// would change. Writes nothing.
  Future<RestorePlan> restorePlan(DateTime before) async {
    final changes = <HistoryEntry>[];
    final live = <String>[];
    for (final key in await store.history.keys()) {
      final versions = await store.history.list(key);
      final target = versions.where((e) => !e.at.isAfter(before)).firstOrNull;
      if (target == null) continue;
      final current = await store.read(key);
      if (current != null && hashOf(current.bytes) == target.hash) continue;
      if (isLive(key)) {
        live.add(key);
      } else {
        changes.add(target);
      }
    }
    return RestorePlan(changes: changes, live: live);
  }

  /// Writes every version in [plan]. A key that has gone live since the plan
  /// was made stops it with a [StateError], and the keys before it stay done.
  Future<void> applyRestore(RestorePlan plan) async {
    for (final entry in plan.changes) {
      _refuseLive(entry.key);
      await _restoreEntry(entry);
    }
  }

  /// Saves what [change] makes of the current bytes of [key], for [reason].
  Future<void> edit(
    String key,
    Uint8List Function(Uint8List bytes) change,
    SaveReason reason,
  ) async {
    _refuseLive(key);
    final current = await store.read(key);
    if (current == null) throw StateError('There is no save "$key"');
    await store.write(
      key,
      SaveRecord(bytes: change(current.bytes), reason: reason, at: _clock()),
    );
  }
}
