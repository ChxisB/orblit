// Runs a pack off the caller's own thread, so a big folder of sprites never
// stalls a frame. `dart:isolate` cannot even be imported when compiling for
// the web, so the import moves behind this one line, the same way
// `orblit_asset`'s directory.dart keeps `dart:io` out of a web build.
// Wherever an `Isolate` exists this is atlas_pack_isolate_io.dart, and on the
// web it is atlas_pack_isolate_stub.dart, which packs in place instead.
export 'atlas_pack_isolate_stub.dart'
    if (dart.library.io) 'atlas_pack_isolate_io.dart';
