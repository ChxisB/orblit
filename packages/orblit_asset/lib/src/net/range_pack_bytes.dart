import 'dart:typed_data';

import '../pack/pack_bytes.dart';
import '../pack/pack_format.dart';
import 'range_queue.dart';
import 'transport.dart';

/// The pack at [url] could not be read in parts.
///
/// Says what the server did, because the usual cause is a host that does not
/// answer a range and sends the whole file instead, and that is a setting to
/// change and not a bug to look for.
final class PackRangeFailed implements Exception {
  const PackRangeFailed(this.url, this.reason);

  final Uri url;
  final String reason;

  @override
  String toString() => 'Could not read part of the pack at $url: $reason.';
}

/// A pack on a server, read by asking for the bytes wanted.
///
/// An [AssetPack] needs a header, a directory and then one blob per asset
/// read. Over a connection that is a few kilobytes up front and the rest as
/// the game asks, where fetching the file whole is the whole pack before the
/// first frame. The first request takes the header, which also says how long
/// the file is, so [length] costs nothing a read would not.
///
/// The server has to answer a `Range` header with `206`. A host that ignores
/// it sends the whole file to every request, so this refuses that reply and
/// stops reading it, and says so in [PackRangeFailed]. Serve `.opack` as
/// `application/octet-stream` with no content encoding, since a range of an
/// encoded reply counts the encoded bytes.
///
/// Reads made close together are joined into fewer, larger requests, and an
/// urgent read goes before one that can wait. [batching] sets how. A read
/// with no [RangePriority] is urgent, which is what an [AssetPack] asks for.
///
/// A failure is not retried here. A caller that wants retries over a flaky
/// connection wraps the transport.
final class RangePackBytes implements PackBytes {
  RangePackBytes(
    this._transport,
    this.url, {
    RangeBatching batching = const RangeBatching(),
  }) {
    _queue = RangeQueue(fetch: _fetchBatch, batching: batching);
  }

  final AssetTransport _transport;

  /// Where the pack is.
  final Uri url;

  late final RangeQueue _queue;
  Future<_Head>? _head;

  @override
  Future<int> length() async => (await _headOf()).total;

  @override
  Future<Uint8List> read(
    int at,
    int bytes, {
    RangePriority priority = RangePriority.now,
  }) async {
    if (at < 0 || bytes < 0) {
      throw RangeError('Cannot read $bytes bytes at $at from a pack.');
    }
    final head = await _headOf();
    if (at + bytes > head.total) {
      throw RangeError(
        'Cannot read $bytes bytes at $at from a pack of ${head.total}.',
      );
    }
    if (bytes == 0) return Uint8List(0);
    if (at + bytes <= head.start.length) {
      return Uint8List.fromList(head.start.sublist(at, at + bytes));
    }
    return _queue.want(at, bytes, priority: priority);
  }

  Future<Uint8List> _fetchBatch(int at, int bytes) async {
    final head = await _headOf();
    return (await _fetch(at, bytes, known: head.total)).bytes;
  }

  /// The first request's answer, asked for once and shared.
  ///
  /// A failure is not remembered, so the next call asks again.
  Future<_Head> _headOf() async {
    try {
      return await (_head ??= _fetchHead());
    } catch (_) {
      _head = null;
      rethrow;
    }
  }

  Future<_Head> _fetchHead() async {
    final part = await _fetch(0, PackHeader.size, known: null);
    final total = part.total;
    if (total == null) {
      throw PackRangeFailed(
        url,
        'the server did not say how long it is. From another origin it has '
        'to list Content-Range in Access-Control-Expose-Headers',
      );
    }
    return _Head(total, part.bytes);
  }

  /// Asks for [bytes] bytes at [at] and checks the answer is that range.
  ///
  /// [known] is how long the pack was said to be by an earlier answer. A
  /// later answer that says otherwise is a pack that was replaced on the
  /// server part way through a read, and its blobs are not the directory's.
  Future<_Part> _fetch(int at, int bytes, {required int? known}) async {
    final reply = await _transport.send(
      FetchRequest(url, from: at, through: at + bytes - 1),
    );
    if (!_isTheRange(reply, at, bytes)) {
      await reply.body.listen(null).cancel();
      throw PackRangeFailed(url, _whyNot(reply, at));
    }
    final total = reply.length ?? known;
    if (known != null && total != known) {
      await reply.body.listen(null).cancel();
      throw PackRangeFailed(
        url,
        'the pack changed from $known to $total bytes while it was being read',
      );
    }
    final body = BytesBuilder(copy: false);
    await for (final chunk in reply.body) {
      body.add(chunk);
      if (body.length > bytes) {
        throw PackRangeFailed(url, 'the server sent more than was asked for');
      }
    }
    final got = body.takeBytes();
    final wanted = total == null ? null : (total - at).clamp(0, bytes);
    if (wanted != null && got.length != wanted) {
      throw PackRangeFailed(
        url,
        'the server sent ${got.length} bytes where $wanted were asked for',
      );
    }
    return _Part(got, total);
  }

  /// Whether [reply] is the bytes asked for, and not the file or an error.
  ///
  /// A `200` is the whole file, which is the range asked only when the range
  /// was the whole file. A server may answer that either way.
  static bool _isTheRange(FetchReply reply, int at, int bytes) {
    if (reply.status == 206) return reply.from == at;
    return reply.status == 200 && at == 0 && reply.length == bytes;
  }

  String _whyNot(FetchReply reply, int at) {
    if (reply.status == 200) {
      return 'the server ignored the range and sent the whole file';
    }
    if (reply.status == 206) {
      return 'the server sent the bytes from ${reply.from} and not from $at';
    }
    return 'the server answered ${reply.status}';
  }
}

final class _Head {
  const _Head(this.total, this.start);

  final int total;

  /// The bytes the first request returned, kept so that the header is not
  /// asked for twice.
  final Uint8List start;
}

final class _Part {
  const _Part(this.bytes, this.total);

  final Uint8List bytes;
  final int? total;
}
