import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

ContentHash hashOf(String text) => ContentHash.of(utf8.encode(text));

AssetId id(String name) => AssetId.parse(name);

KitUpdatePlan plan({
  Map<String, String> locked = const {},
  Map<String, String> incoming = const {},
  Map<String, String?> onDisk = const {},
}) => planKitUpdate(
  locked: {for (final e in locked.entries) id(e.key): hashOf(e.value)},
  incoming: {for (final e in incoming.entries) id(e.key): hashOf(e.value)},
  onDisk: {
    for (final e in onDisk.entries)
      id(e.key): e.value == null ? null : hashOf(e.value!),
  },
);

List<String> names(List<AssetId> ids) => [for (final i in ids) '$i'];

void main() {
  test('replaces a file that is as the lock has it and changed', () {
    final result = plan(
      locked: {'a/x': '1'},
      incoming: {'a/x': '2'},
      onDisk: {'a/x': '1'},
    );
    expect(names(result.replace), ['a/x']);
    expect(result.kept, isEmpty);
  });

  test('keeps and lists a file the person edited', () {
    final result = plan(
      locked: {'a/x': '1'},
      incoming: {'a/x': '2'},
      onDisk: {'a/x': 'mine'},
    );
    expect(result.replace, isEmpty);
    expect(names(result.kept), ['a/x']);
  });

  test('says nothing of an edited file the release did not change', () {
    final result = plan(
      locked: {'a/x': '1'},
      incoming: {'a/x': '1'},
      onDisk: {'a/x': 'mine'},
    );
    expect(result.isEmpty, isTrue);
  });

  test('leaves a file the person deleted and lists it', () {
    final result = plan(
      locked: {'a/x': '1'},
      incoming: {'a/x': '2'},
      onDisk: {'a/x': null},
    );
    expect(result.replace, isEmpty);
    expect(names(result.kept), ['a/x']);
  });

  test('removes a dropped file only when it is unchanged', () {
    final result = plan(
      locked: {'a/gone': '1', 'a/edited': '1', 'a/missing': '1'},
      onDisk: {'a/gone': '1', 'a/edited': 'mine', 'a/missing': null},
    );
    expect(names(result.remove), ['a/gone']);
    expect(names(result.kept), ['a/edited']);
  });

  test('adds a new file unless another one is in its place', () {
    final result = plan(
      incoming: {'a/new': '1', 'a/taken': '1', 'a/same': '1'},
      onDisk: {'a/new': null, 'a/taken': 'mine', 'a/same': '1'},
    );
    expect(names(result.add), ['a/new']);
    expect(names(result.kept), ['a/taken']);
  });
}
