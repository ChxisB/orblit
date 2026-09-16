// Bytes over HTTP, where that is how an example's files arrive.
//
// Only a browser reads an example's files this way: everywhere else they are
// on disk and read with `dart:io`. Flutter's NetworkAssetBundle looks like the
// portable answer and is not — it is built on `dart:io`'s HttpClient, which a
// browser does not have, so every read failed quietly and a sample that was
// sitting on the server never arrived. So the web gets the browser's own
// fetch, and every other platform a stand-in that says there is nothing.
export 'fetch_native.dart' if (dart.library.js_interop) 'fetch_web.dart';
