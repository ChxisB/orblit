// The importers that drive native programs, where there is a file system and
// a way to start one.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot even be
// imported when compiling for the web, so the web gets the other half.

export 'native_importers_stub.dart'
    if (dart.library.io) 'native_importers_io.dart';
