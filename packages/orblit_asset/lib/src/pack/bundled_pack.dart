// The pack an app was built with, found on disk where there is a file system.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot be imported
// when compiling for the web.
export 'bundled_pack_stub.dart' if (dart.library.io) 'bundled_pack_io.dart';
