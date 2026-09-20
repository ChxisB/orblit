// The Cache Storage-backed store and records, where there is a browser.
//
// Both need `dart:js_interop`, which cannot be imported at all when compiling
// for anything but the web — one import of it in this package would stop
// every native app that uses an asset id from building. So the import moves
// behind this one line, the same way directory.dart handles `dart:io`.
export 'cache_storage_stub.dart'
    if (dart.library.js_interop) 'cache_storage_web.dart';
