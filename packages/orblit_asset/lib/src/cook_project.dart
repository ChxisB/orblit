// Cooking a whole project, where there is a file system.
//
// Split for the same reason `directory.dart` is: `dart:io` cannot even be
// imported when compiling for the web.

export 'cook_project_stub.dart' if (dart.library.io) 'cook_project_io.dart';
