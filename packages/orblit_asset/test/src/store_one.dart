// A second process for cook_cache_test.dart, which is the only way to check
// the claim the file lock actually makes: that two builds cooking at once do
// not lose each other's entries. Two objects in one process prove the queue,
// not the lock.
//
//   dart run test/src/store_one.dart <cache root> <source name>
import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';

Future<void> main(List<String> arguments) async {
  final [root, name] = arguments;
  await DirectoryCookCache(root).store(
    CookKey(
      importer: 'texture',
      importerVersion: 1,
      source: ContentHash.of(utf8.encode(name)),
    ),
    {'out': utf8.encode('cooked $name')},
  );
}
