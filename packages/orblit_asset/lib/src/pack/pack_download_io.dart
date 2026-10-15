// The download behind PackFolder.download, which needs dart:io.
import 'dart:async';
import 'dart:io';
import 'dart:math';

import '../net/fetcher.dart' show FetchCorrupt, FetchFailed, FetchProgress;
import '../net/policy.dart';
import '../net/transport.dart';
import 'asset_pack.dart';
import 'pack_file_io.dart';
import 'pack_offer.dart';

/// One pack being fetched into a `.part` file and promoted when it is whole.
///
/// This is not `AssetFetcher`, and not by oversight. That keeps what it
/// fetches in memory and files it under its hash in a content store, which
/// is right for a texture and wrong for a pack of hundreds of megabytes. A
/// pack is written to disk as it arrives, so a download that dies with the
/// process can carry on from the file, and it is checked as a pack and not as
/// bytes.
final class PackDownload {
  PackDownload({
    required this.offer,
    required this.transport,
    required this.origin,
    required this.part,
    required this.attempts,
    required this.backoff,
    required this.onProgress,
    Random? jitter,
  }) : assert(attempts > 0),
       _jitter = jitter ?? Random();

  final PackOffer offer;
  final AssetTransport transport;
  final AssetOrigin origin;

  /// Where the bytes go until they are known to be the pack. The finished
  /// pack is this file's sibling, named by the hash.
  final File part;

  final int attempts;
  final Duration backoff;
  final void Function(FetchProgress progress)? onProgress;
  final Random _jitter;

  /// Fetches, retries and checks. Answers the pack, filed under its name.
  Future<AssetPack> run() async {
    origin.requireAllowed(offer.url);
    Object? last;
    for (var attempt = 1; attempt <= attempts; attempt++) {
      if (attempt > 1) {
        await Future<void>.delayed(_waitBefore(attempt - 1));
      }
      try {
        return await _attempt(attempt);
      } catch (error) {
        if (!_mayWorkLater(error)) rethrow;
        last = error is _TryAgain ? error.cause : error;
      }
    }
    throw FetchFailed(offer.url, attempts, last ?? 'no answer');
  }

  /// One request, from wherever the part file ends.
  Future<AssetPack> _attempt(int attempt) async {
    final have = await _length();
    final reply = await transport.send(
      FetchRequest(offer.url, from: have > 0 ? have : null),
    );
    if (reply.status == 416 && have > 0) {
      // The server has no byte there. Either the part is the whole file and
      // the rename never happened, or the file on the server is shorter than
      // the part and the part is of no use.
      return _verify(resumed: true);
    }
    if (reply.status != 200 && reply.status != 206) {
      await reply.body.listen(null).cancel();
      throw reply.mayWorkLater
          ? _TryAgain(reply.status)
          : FetchFailed(offer.url, attempt, reply.status);
    }
    final start = await _startOf(reply, have);
    await _write(reply, start);
    return _verify(resumed: start > 0);
  }

  /// Where in the file this reply's bytes go.
  ///
  /// Nought when the server sent the whole file, which it does to a resumed
  /// request when it cannot or will not honour a range. A server that resumes
  /// from some other byte is not trusted with the part, and the part goes.
  Future<int> _startOf(FetchReply reply, int have) async {
    if (reply.status == 200) return 0;
    if (reply.from == have) return have;
    await reply.body.listen(null).cancel();
    await _discard();
    throw _TryAgain('the server resumed at ${reply.from}, not at $have');
  }

  /// Writes the reply's bytes to the part file and checks it came to the
  /// length the server gave.
  ///
  /// The part is kept when the reply ended short, since that is what the next
  /// attempt resumes from. It is dropped when the reply was too big, which
  /// no resume can mend.
  Future<void> _write(FetchReply reply, int start) async {
    final limit = origin.policy.maxBytes;
    final total = reply.length;
    if (total != null && total > limit) {
      await reply.body.listen(null).cancel();
      throw _tooBig(total, limit);
    }
    final written = await _receive(reply, start, limit);
    if (written > limit) {
      await _discard();
      throw _tooBig(written, limit);
    }
    if (total != null && written != total) {
      if (written > total) await _discard();
      throw _TryAgain('the reply ended at $written of $total bytes');
    }
  }

  /// Appends the body to the part file, stopping once it is past [limit],
  /// and answers how long the file is then.
  Future<int> _receive(FetchReply reply, int start, int limit) async {
    final file = await part.open(
      mode: start > 0 ? FileMode.append : FileMode.write,
    );
    var written = start;
    try {
      await for (final chunk in reply.body) {
        written += chunk.length;
        // Leaving the loop cancels the request, which is what stops a
        // server that sends more than it said.
        if (written > limit) break;
        await file.writeFrom(chunk);
        onProgress?.call(FetchProgress(received: written, total: reply.length));
      }
    } finally {
      await file.close();
    }
    return written;
  }

  FetchRefused _tooBig(int bytes, int limit) => FetchRefused(
    '${offer.url}',
    'it is $bytes bytes and the policy allows $limit',
  );

  /// Checks the part as a pack, and files it under its name when it is.
  ///
  /// A part that was resumed may be the start of a different file, as when
  /// the server's copy changed between two runs. It is thrown away and
  /// fetched again whole. A whole download that is wrong is the server's
  /// answer, and asking again would get the same one.
  Future<AssetPack> _verify({required bool resumed}) async {
    try {
      await _check();
    } on FormatException catch (error, stack) {
      await _reject(error, stack, resumed: resumed);
    } on FetchCorrupt catch (error, stack) {
      await _reject(error, stack, resumed: resumed);
    }
    final done = await part.rename(_finished());
    return _open(done);
  }

  /// Throws when the part is not the pack that was offered, whole.
  Future<void> _check() async {
    final bytes = FilePackBytes(part.path);
    try {
      final pack = await AssetPack.open(bytes);
      if (pack.hash != offer.hash) {
        throw FetchCorrupt(offer.url, wanted: offer.hash, got: pack.hash);
      }
      final damaged = await pack.findCorrupt();
      if (damaged.isNotEmpty) {
        throw FormatException(
          '${damaged.length} assets in the download from ${offer.url} do '
          'not match the hashes in its directory, starting with '
          '${damaged.first}',
        );
      }
    } finally {
      await bytes.close();
    }
  }

  Future<void> _reject(
    Object error,
    StackTrace stack, {
    required bool resumed,
  }) async {
    await _discard();
    Error.throwWithStackTrace(resumed ? _TryAgain(error) : error, stack);
  }

  Future<AssetPack> _open(File file) async {
    final bytes = FilePackBytes(file.path);
    try {
      return await AssetPack.open(bytes);
    } finally {
      await bytes.close();
    }
  }

  String _finished() {
    final separator = Platform.pathSeparator;
    final folder = part.path.substring(0, part.path.lastIndexOf(separator));
    return '$folder$separator${offer.hash.hex}.opack';
  }

  Future<int> _length() async => await part.exists() ? part.length() : 0;

  Future<void> _discard() async {
    if (await part.exists()) await part.delete();
  }

  /// Waits, longer each time, with a little noise.
  ///
  /// The noise matters more than the doubling. A hundred devices that lost
  /// the same server come back at the same moment without it.
  Duration _waitBefore(int attempt) {
    final grown = backoff * pow(2, attempt - 1).toDouble();
    final noise = 1 + (_jitter.nextDouble() - 0.5) * 0.4;
    return Duration(microseconds: (grown.inMicroseconds * noise).round());
  }

  /// False for what asking again cannot change: a refusal, a pack that is
  /// not the one offered, an answer such as 404, a full disk and a mistake in
  /// the program. A dropped connection and a busy server are the rest.
  static bool _mayWorkLater(Object error) =>
      error is! Error &&
      error is! FetchRefused &&
      error is! FetchCorrupt &&
      error is! FetchFailed &&
      error is! FormatException &&
      error is! FileSystemException;
}

/// A reason to try again, so that a retry reads as one in [PackDownload.run]
/// and nothing else does.
final class _TryAgain implements Exception {
  const _TryAgain(this.cause);

  final Object cause;

  @override
  String toString() => 'Try again: $cause';
}
