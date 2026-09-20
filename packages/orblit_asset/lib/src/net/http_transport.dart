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
      // Open-ended: everything from here on. A resumed download wants the rest
      // of the file, and naming an end would mean having to trust the length
      // the last attempt was told.
      message.headers['range'] = 'bytes=${request.from}-';
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
      from: status == 206 ? request.from : null,
    );
  }

  @override
  void close() {
    if (_owned) _client.close();
  }

  /// How long the whole asset is, which is not the length of this reply.
  ///
  /// A resumed request answers 206 with a content-length of what is left and
  /// a content-range of `bytes 900-999/1000`. Reading the wrong one of those
  /// makes a progress bar that fills up to the last tenth and then claims to
  /// be finished, and makes a size limit that a large file walks through by
  /// being asked for in two halves.
  static int? _wholeLength(http.StreamedResponse reply) {
    final range = reply.headers['content-range'];
    if (range != null) {
      final slash = range.lastIndexOf('/');
      if (slash >= 0) {
        final whole = int.tryParse(range.substring(slash + 1).trim());
        if (whole != null) return whole;
      }
    }
    return reply.contentLength;
  }
}
