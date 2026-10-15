// A folder of downloaded packs, where there is a file system.
//
// Split for the same reason `pack_file.dart` is: `dart:io` cannot be imported
// when compiling for the web.
export 'pack_folder_stub.dart' if (dart.library.io) 'pack_folder_io.dart';
