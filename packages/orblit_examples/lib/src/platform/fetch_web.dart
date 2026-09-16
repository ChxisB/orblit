import 'dart:js_interop';
import 'dart:typed_data';

import 'package:web/web.dart' as web;

/// The bytes at [url], relative to the page, or null for anything but a
/// whole answer.
Future<Uint8List?> fetchBytes(String url) async {
  try {
    final response = await web.window.fetch(url.toJS).toDart;
    if (!response.ok) return null;
    final buffer = await response.arrayBuffer().toDart;
    return buffer.toDart.asUint8List();
  } on Object {
    return null;
  }
}
