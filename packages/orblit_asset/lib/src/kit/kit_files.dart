// Packing a folder as a kit and writing a kit into a project, where there is
// a file system.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot be imported
// when compiling for the web.
export 'kit_files_stub.dart' if (dart.library.io) 'kit_files_io.dart';
