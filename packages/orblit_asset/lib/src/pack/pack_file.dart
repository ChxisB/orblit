// A pack read from a file, where there is a file system.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot be imported
// when compiling for the web.
export 'pack_file_stub.dart' if (dart.library.io) 'pack_file_io.dart';
