// Writing a pack from a cooked bundle, where there is a file system.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot be imported
// when compiling for the web.
export 'pack_bundle_stub.dart' if (dart.library.io) 'pack_bundle_io.dart';
