// The backend for the platform being compiled for.
//
// `linux.dart` needs `dart:ffi` and `web.dart` needs `dart:js_interop`, and
// neither can be imported at all on the other side, so the choice is made by
// which file is compiled in rather than by a check at run time.
export 'platform_io.dart' if (dart.library.js_interop) 'platform_web.dart';
