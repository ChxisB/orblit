// The directory-backed source and store, where there is a file system.
//
// Both need `dart:io`, and `dart:io` cannot even be imported when compiling
// for the web — one import of it anywhere in the package would stop every web
// app that uses an asset id from building, whether or not it ever touched a
// directory. So the import moves behind this one line. Wherever `dart:io`
// exists this is directory_io.dart, and on the web it is directory_stub.dart,
// whose classes have the same shape and refuse to be constructed.
export 'directory_stub.dart' if (dart.library.io) 'directory_io.dart';
