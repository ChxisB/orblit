// Building a kit store from a folder of entries, where there is a file system.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot be imported
// when compiling for the web.
export 'kit_store_stub.dart' if (dart.library.io) 'kit_store_io.dart';
