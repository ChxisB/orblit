import 'dart:async';

/// One request for an asset's bytes.
///
/// Deliberately smaller than HTTP. Only three things vary between one asset
/// fetch and another, and a transport that took arbitrary headers and methods
/// would be a transport that could be asked to do something this package has
/// no way to check.
class FetchRequest {
  const FetchRequest(this.url, {this.ifNoneMatch, this.from});

  final Uri url;

  /// The ETag of the copy already held, if there is one.
  ///
  /// The server answers 304 when the copy is still current, which costs a
  /// round trip and no body. That is the difference between a cold start and
  /// a warm one on a connection where the bytes are the expensive part.
  final String? ifNoneMatch;

  /// The byte to resume from, for a download that stopped partway.
  final int? from;

  @override
  String toString() => [
    url,
    if (ifNoneMatch != null) 'if-none-match: $ifNoneMatch',
    if (from != null) 'from: $from',
  ].join(', ');
}

/// What a server said.
class FetchReply {
  const FetchReply({
    required this.status,
    required this.body,
    this.etag,
    this.length,
    this.from,
  });

  /// A reply with no body, for a 304 or a failure.
  FetchReply.empty(this.status, {this.etag})
    : body = const Stream.empty(),
      length = 0,
      from = null;

  final int status;

  /// The bytes, which may not have arrived yet.
  ///
  /// A stream rather than a list so that a fetch can be abandoned partway
  /// without waiting for the rest, and so that a length limit can stop one
  /// while it is still arriving rather than after. Cancelling the
  /// subscription is what cancels the request: every transport here closes
  /// the connection when nobody is listening any more.
  final Stream<List<int>> body;

  final String? etag;

  /// How many bytes the whole asset is, when the server says.
  ///
  /// The whole asset, not this reply: a resumed request answers 206 with the
  /// remaining bytes, and this is still the total, read out of the
  /// content-range. Callers checking a size limit want the total, and the two
  /// being different is exactly the mistake worth designing out.
  final int? length;

  /// The byte this reply's body starts at, for a resumed request.
  final int? from;

  bool get isNotModified => status == 304;

  /// Whether asking again later might work.
  ///
  /// A 5xx, a 408 or a 429 is a server having a moment. A 404 or a 403 is an
  /// answer: retrying it is a slower way to fail, and on a metered connection
  /// an expensive one.
  bool get mayWorkLater =>
      status >= 500 || status == 408 || status == 425 || status == 429;
}

/// The thing that actually talks to a server.
///
/// A seam rather than a hard dependency on one client, for three reasons that
/// each came up on their own: a test wants to answer without a network, an app
/// may already have a client carrying authentication or a certificate pin, and
/// a platform may have something better than the default — an HTTP/3 stack, a
/// game console's own downloader — that this package should not have to know
/// about.
abstract interface class AssetTransport {
  /// Sends [request] and answers with the headers, before the body arrives.
  ///
  /// Throwing means the request never got an answer — no connection, no
  /// route, a timeout. A status code is an answer, however unwelcome, and
  /// comes back as a reply.
  Future<FetchReply> send(FetchRequest request);

  /// Lets go of whatever is held open. Sending afterwards is a mistake.
  void close();
}

/// A transport that answers from a map, for tests and for offline demos.
///
/// Understands enough of the protocol to exercise the code that depends on
/// it: an ETag it answers 304 to, a range it honours, and a list of failures
/// to hand out before it starts succeeding.
class MapTransport implements AssetTransport {
  MapTransport(this.pages);

  /// What is served, by URL.
  final Map<Uri, TransportPage> pages;

  /// Every request that has been sent, in order, for a test to assert on.
  final List<FetchRequest> sent = [];

  var _closed = false;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    if (_closed) {
      throw StateError('This transport is closed.');
    }
    sent.add(request);

    final page = pages[request.url];
    if (page == null) return FetchReply.empty(404);

    final fault = page.take();
    if (fault != null) {
      if (fault.status != null) return FetchReply.empty(fault.status!);
      throw fault.error ?? const SocketFailure('the connection was lost');
    }

    if (request.ifNoneMatch != null && request.ifNoneMatch == page.etag) {
      return FetchReply.empty(304, etag: page.etag);
    }

    final from = request.from ?? 0;
    if (from > page.bytes.length) return FetchReply.empty(416);
    final rest = page.bytes.sublist(from);
    // Armed for one reply only: the resumed request that follows is answered
    // in full, which is the sequence resuming has to survive.
    final stop = page.stopAfter;
    page.stopAfter = null;
    return FetchReply(
      status: from == 0 ? 200 : 206,
      body: stop != null
          ? _stopsShort(rest, stop)
          : page.cut <= 0
          ? Stream.value(rest)
          : Stream.fromIterable([
              for (var at = 0; at < rest.length; at += page.cut)
                rest.sublist(at, (at + page.cut).clamp(0, rest.length)),
            ]),
      etag: page.etag,
      length: page.bytes.length,
      from: from == 0 ? null : from,
    );
  }

  @override
  void close() => _closed = true;

  /// Hands over part of [bytes] and then loses the connection, once.
  ///
  /// The failure that resuming exists for, and the one that is invisible in a
  /// test that only ever fails before the body: a reply that started, sent
  /// most of a large file and stopped.
  static Stream<List<int>> _stopsShort(List<int> bytes, int after) async* {
    yield bytes.sublist(0, after.clamp(0, bytes.length));
    throw const SocketFailure('the connection dropped partway');
  }
}

/// One thing a [MapTransport] serves.
class TransportPage {
  TransportPage(
    this.bytes, {
    this.etag,
    this.cut = 0,
    this.stopAfter,
    List<Fault>? faults,
  }) : faults = faults ?? [];

  final List<int> bytes;
  final String? etag;

  /// How many bytes per chunk, or nought for one chunk.
  final int cut;

  /// Sends this many bytes and then drops the connection.
  ///
  /// Cleared once it has happened, so the retry that follows gets a whole
  /// reply — which is the sequence resuming has to survive.
  int? stopAfter;

  /// Failures to hand out before answering properly, one per request.
  final List<Fault> faults;

  Fault? take() => faults.isEmpty ? null : faults.removeAt(0);
}

/// A failure a [MapTransport] hands out once.
class Fault {
  /// A status code rather than a body.
  const Fault.status(int this.status) : error = null;

  /// A thrown failure: no answer at all.
  const Fault.thrown([this.error = const SocketFailure('no route to host')])
    : status = null;

  final int? status;
  final Object? error;
}

/// A connection that failed, named by this package so that code catching it
/// does not have to import `dart:io` or know which client is underneath.
class SocketFailure implements Exception {
  const SocketFailure(this.reason);

  final String reason;

  @override
  String toString() => 'The connection failed: $reason.';
}
