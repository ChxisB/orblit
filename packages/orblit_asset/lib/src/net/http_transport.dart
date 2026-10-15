import 'package:http/http.dart' as http;

import 'transport.dart';

/// The ordinary transport: `package:http`, which speaks to a server on every
/// platform Orblit runs on, browsers included.
///
/// Thin on purpose. Everything that decides *what* to ask for — the queue, the
/// retries, the range on a resume, the ETag on a revalidation — is in
/// [AssetFetcher], and this only turns one of those decisions into a request
/// and the answer back into a reply. A transport that made decisions would be
/// a second place they were made.
class HttpTransport implements AssetTransport {
  /// A transport over [client], or over one of its own.
  ///
  /// Pass a client to share connections with the rest of an app, to add
  /// authentication, or to pin a certificate. A transport that made its own is
  /// a second connection pool to the same server.
  HttpTransport({http.Client? client})
    : _client = client ?? http.Client(),
      _owned = client == null;

  final http.Client _client;
  final bool _owned;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    final message = http.Request('GET', request.url)
      ..followRedirects = true
      // Enough to follow a CDN's redirect to a region and no further. A chain
      // longer than this is either a loop or a server being used to bounce a
      // request somewhere it should not go.
      ..maxRedirects = 5;

    if (request.ifNoneMatch != null) {
      message.headers['if-none-match'] = request.ifNoneMatch!;
    }
    if (request.from != null) {
      // Open-ended unless the request names an end. A resumed download wants
      // the rest of the file, and naming an end would mean having to trust the
      // length the last attempt was told. A part of a file names both.
      message.headers['range'] =
          'bytes=${request.from}-${request.through ?? ''}';
    }

    final http.StreamedResponse reply;
    try {
      reply = await _client.send(message);
    } on http.ClientException catch (error) {
      // Named by this package so that the fetcher's message reads as a
      // connection failure rather than as the internals of a client.
      throw SocketFailure(error.message);
    }

    final status = reply.statusCode;
    if (status == 304 || status == 204) {
      return FetchReply.empty(status, etag: reply.headers['etag']);
    }

    return FetchReply(
      status: status,
      body: reply.stream,
      etag: reply.headers['etag'],
      length: _wholeLength(reply),
      from: status == 206 ? _startOf(reply) ?? request.from : null,
    );
  }

  @override
  void close() {
    if (_owned) _client.close();
  }

  /// The first byte this reply holds, read out of the content-range.
  ///
  /// The server's word for it, not an echo of what was asked. A server that
  /// answers a different range than the one sent is a reader that gets the
  /// wrong bytes without knowing, and this is what lets the caller see it.
  static int? _startOf(http.StreamedResponse reply) {
    final range = reply.headers['content-range'];
    if (range == null) return null;
    final match = RegExp(r'^\s*bytes\s+(\d+)-').firstMatch(range);
    return match == null ? null : int.tryParse(match.group(1)!);
  }

  /// How long the whole asset is, which is not the length of this reply.
  ///
  /// A resumed request answers 206 with a content-length of what is left and
  /// a content-range of `bytes 900-999/1000`. Reading the wrong one of those
  /// makes a progress bar that fills up to the last tenth and then claims to
  /// be finished, and makes a size limit that a large file walks through by
  /// being asked for in two halves.
  ///
  /// A `206` with no content-range gives null, not the content-length. A page
  /// on another origin cannot read content-range unless the server lists it in
  /// `Access-Control-Expose-Headers`, and the content-length it can read is
  /// the part's, which would pass for the whole file.
  static int? _wholeLength(http.StreamedResponse reply) {
    final range = reply.headers['content-range'];
    if (range != null) {
      final slash = range.lastIndexOf('/');
      if (slash >= 0) {
        final whole = int.tryParse(range.substring(slash + 1).trim());
        if (whole != null) return whole;
      }
    }
    return reply.statusCode == 206 ? null : reply.contentLength;
  }
}
