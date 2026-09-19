// The directory-backed cook cache, where there is a file system.
//
// The same one line as directory.dart, and for the same reason: `dart:io`
// cannot be imported at all when compiling for the web, so one import of it
// anywhere in the package would stop every web app that uses a cook key from
// building, whether or not it ever touched a directory.
export 'cook_cache_stub.dart' if (dart.library.io) 'cook_cache_io.dart';
