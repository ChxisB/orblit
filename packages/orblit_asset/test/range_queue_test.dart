import 'dart:async';
import 'dart:typed_data';

import 'package:orblit_asset/src/net/range_queue.dart';
import 'package:test/test.dart';

/// A file whose byte at offset n is n & 0xff, so a reply can be checked
/// against where it came from.
Uint8List bytesAt(int at, int bytes) =>
    Uint8List.fromList([for (var i = 0; i < bytes; i++) (at + i) & 0xff]);

/// Answers every range at once, or holds each until a test lets it go.
final class Host {
  Host({this.hold = false});

  final bool hold;
  final calls = <({int at, int bytes})>[];
  final _gates = <Completer<void>>[];

  Future<Uint8List> fetch(int at, int bytes) async {
    calls.add((at: at, bytes: bytes));
    if (hold) {
      final gate = Completer<void>();
      _gates.add(gate);
      await gate.future;
    }
    return bytesAt(at, bytes);
  }

  /// Lets the [n]th request (from nought) be answered.
  void release(int n) => _gates[n].complete();

  int get totalBytes => calls.fold(0, (sum, call) => sum + call.bytes);
}

/// Long enough for a zero delay to fire and for every reply to land.
Future<void> settle() => Future<void>.delayed(const Duration(milliseconds: 20));

RangeQueue queueOver(
  Host host, {
  int mergeGap = 16,
  int maxBatch = 1 << 20,
  int parallel = 4,
}) => RangeQueue(
  fetch: host.fetch,
  batching: RangeBatching(
    mergeGap: mergeGap,
    maxBatch: maxBatch,
    parallel: parallel,
    delay: Duration.zero,
  ),
);

void main() {
  group('a range queue', () {
    test('sends forty adjacent blobs as one request', () async {
      final host = Host();
      final queue = queueOver(host);

      final replies = await Future.wait([
        for (var i = 0; i < 40; i++) queue.want(100 + i * 50, 50),
      ]);

      expect(host.calls, [(at: 100, bytes: 2000)]);
      for (var i = 0; i < 40; i++) {
        expect(replies[i], bytesAt(100 + i * 50, 50), reason: 'blob $i');
      }
    });

    test('joins ranges closer than the gap and fetches the gap too', () async {
      final host = Host();
      final queue = queueOver(host, mergeGap: 16);

      final replies = await Future.wait([
        queue.want(0, 100),
        queue.want(116, 100),
      ]);

      expect(host.calls, [(at: 0, bytes: 216)]);
      expect(host.totalBytes, 100 + 100 + 16);
      expect(replies[0], bytesAt(0, 100));
      expect(replies[1], bytesAt(116, 100));
    });

    test('keeps ranges further apart than the gap separate', () async {
      final host = Host();
      final queue = queueOver(host, mergeGap: 16);

      await Future.wait([queue.want(0, 100), queue.want(117, 100)]);

      expect(host.calls, [(at: 0, bytes: 100), (at: 117, bytes: 100)]);
    });

    test('sends overlapping ranges as one request', () async {
      final host = Host();
      final queue = queueOver(host, mergeGap: 0);

      final replies = await Future.wait([
        queue.want(0, 100),
        queue.want(50, 100),
        queue.want(60, 10),
      ]);

      expect(host.calls, [(at: 0, bytes: 150)]);
      expect(replies[0], bytesAt(0, 100));
      expect(replies[1], bytesAt(50, 100));
      expect(replies[2], bytesAt(60, 10));
    });

    test('measures the gap from the furthest end so far', () async {
      final host = Host();
      final queue = queueOver(host, mergeGap: 5);

      await Future.wait([
        queue.want(0, 100),
        queue.want(60, 10),
        queue.want(104, 10),
      ]);

      expect(host.calls, [(at: 0, bytes: 114)]);
    });

    test(
      'keeps two wants from the same place apart when they differ',
      () async {
        final host = Host();
        final queue = queueOver(host);

        final replies = await Future.wait([
          queue.want(0, 10),
          queue.want(0, 20),
        ]);

        expect(replies[0], bytesAt(0, 10));
        expect(replies[1], bytesAt(0, 20));
      },
    );

    test('never sends a request over the batch size', () async {
      final host = Host();
      final queue = queueOver(host, maxBatch: 1000);

      final replies = await Future.wait([
        for (var i = 0; i < 40; i++) queue.want(i * 100, 100),
      ]);

      expect(host.calls, hasLength(4));
      for (final call in host.calls) {
        expect(call.bytes, lessThanOrEqualTo(1000));
      }
      expect(host.totalBytes, 4000);
      for (var i = 0; i < 40; i++) {
        expect(replies[i], bytesAt(i * 100, 100), reason: 'blob $i');
      }
    });

    test('sends one blob over the batch size whole', () async {
      final host = Host();
      final queue = queueOver(host, maxBatch: 1000);

      final reply = await queue.want(0, 5000);

      expect(host.calls, [(at: 0, bytes: 5000)]);
      expect(reply, bytesAt(0, 5000));
    });

    test(
      'starts a new request for a want that comes after the window',
      () async {
        final host = Host();
        final queue = queueOver(host);

        await queue.want(0, 10);
        await queue.want(10, 10);

        expect(host.calls, [(at: 0, bytes: 10), (at: 10, bytes: 10)]);
      },
    );

    test('hands each asker its own copy of the bytes', () async {
      final host = Host();
      final queue = queueOver(host, mergeGap: 0);
      final replies = await Future.wait([
        queue.want(0, 100),
        queue.want(50, 100),
      ]);

      replies[0][60] = 0xAA;

      expect(replies[1][10], 60);
    });

    test('hands two askers for the same bytes separate copies', () async {
      final host = Host();
      final queue = queueOver(host);

      final replies = await Future.wait([queue.want(0, 10), queue.want(0, 10)]);

      expect(host.calls, hasLength(1));
      replies[0][0] = 0xAA;
      expect(replies[1][0], 0);
    });

    test('keeps one request free for an urgent want', () async {
      final host = Host(hold: true);
      final queue = queueOver(host, mergeGap: 0, parallel: 2);
      final lower = [
        queue.want(0, 10, priority: RangePriority.later),
        queue.want(1000, 10, priority: RangePriority.later),
      ];
      await settle();
      expect(host.calls, [(at: 0, bytes: 10)]);

      final urgent = queue.want(5000, 10);
      await settle();
      expect(host.calls.last, (at: 5000, bytes: 10));

      host.release(0);
      host.release(1);
      await settle();
      host.release(2);
      await Future.wait([...lower, urgent]);
      expect(host.calls, hasLength(3));
    });

    test('lets a want that can wait use the only request there is', () async {
      final host = Host();
      final queue = queueOver(host, parallel: 1);

      final reply = await queue.want(0, 10, priority: RangePriority.later);

      expect(reply, bytesAt(0, 10));
    });

    test('runs no more requests at once than parallel allows', () async {
      final host = Host(hold: true);
      final queue = queueOver(host, mergeGap: 0, parallel: 2);
      final replies = [for (var i = 0; i < 4; i++) queue.want(i * 1000, 10)];

      await settle();
      expect(host.calls, hasLength(2));

      host.release(0);
      await settle();
      expect(host.calls, hasLength(3));

      host.release(1);
      host.release(2);
      await settle();
      expect(host.calls, hasLength(4));
      host.release(3);
      expect(await Future.wait(replies), hasLength(4));
    });

    test('answers an urgent want before the ones that can wait', () async {
      final host = Host(hold: true);
      final queue = queueOver(host, mergeGap: 0, parallel: 1);
      final first = queue.want(0, 10);
      await settle();
      final replies = [
        queue.want(1000, 10, priority: RangePriority.later),
        queue.want(2000, 10, priority: RangePriority.later),
        queue.want(5000, 10),
      ];
      await settle();
      expect(host.calls, [(at: 0, bytes: 10)]);

      host.release(0);
      await settle();
      expect(host.calls.last, (at: 5000, bytes: 10));
      host.release(1);
      await settle();
      host.release(2);
      await settle();
      host.release(3);

      await Future.wait([first, ...replies]);
      expect(host.calls.map((call) => call.at), [0, 5000, 1000, 2000]);
    });

    test('runs a want that can wait when nothing urgent does', () async {
      final host = Host();
      final queue = queueOver(host);

      final reply = await queue.want(40, 10, priority: RangePriority.later);

      expect(reply, bytesAt(40, 10));
      expect(host.calls, [(at: 40, bytes: 10)]);
    });

    test('moves a want up when someone then needs the same bytes', () async {
      final host = Host(hold: true);
      final queue = queueOver(host, mergeGap: 0, parallel: 1);
      final first = queue.want(0, 10);
      await settle();
      final lower = queue.want(1000, 10, priority: RangePriority.later);
      final later = queue.want(2000, 10, priority: RangePriority.later);
      final promoted = queue.want(2000, 10);
      await settle();

      host.release(0);
      await settle();
      expect(host.calls.last, (at: 2000, bytes: 10));
      host.release(1);
      await settle();
      host.release(2);

      expect(await promoted, bytesAt(2000, 10));
      expect(await later, bytesAt(2000, 10));
      await Future.wait([first, lower]);
      expect(host.calls, hasLength(3));
    });

    test('keeps a second ask at low priority from jumping the queue', () async {
      final host = Host(hold: true);
      final queue = queueOver(host, mergeGap: 0, parallel: 1);
      final first = queue.want(0, 10);
      await settle();
      final urgent = queue.want(3000, 10);
      final lower = queue.want(1000, 10, priority: RangePriority.later);
      final again = queue.want(1000, 10, priority: RangePriority.later);
      await settle();

      host.release(0);
      await settle();
      host.release(1);
      await settle();
      host.release(2);

      await Future.wait([first, urgent, lower, again]);
      expect(host.calls.map((call) => call.at), [0, 3000, 1000]);
    });

    test(
      'fails every want in a request that failed, and then recovers',
      () async {
        var fail = true;
        final calls = <int>[];
        final queue = RangeQueue(
          fetch: (at, bytes) async {
            calls.add(at);
            if (fail) throw const FormatException('the connection was lost');
            return bytesAt(at, bytes);
          },
          batching: const RangeBatching(delay: Duration.zero, parallel: 1),
        );

        final failed = [queue.want(0, 10), queue.want(10, 10)];
        for (final want in failed) {
          await expectLater(want, throwsFormatException);
        }
        expect(calls, [0]);

        fail = false;
        expect(await queue.want(20, 10), bytesAt(20, 10));
      },
    );

    test('fails a want when the reply is short, rather than hanging', () async {
      final queue = RangeQueue(
        fetch: (at, bytes) async => Uint8List(bytes - 1),
        batching: const RangeBatching(delay: Duration.zero),
      );

      await expectLater(queue.want(0, 10), throwsRangeError);
    });

    test('refuses settings that cannot work', () {
      RangeQueue make(RangeBatching batching) => RangeQueue(
        fetch: (at, bytes) async => Uint8List(bytes),
        batching: batching,
      );

      expect(
        () => make(const RangeBatching(mergeGap: -1)),
        throwsArgumentError,
      );
      expect(() => make(const RangeBatching(maxBatch: 0)), throwsArgumentError);
      expect(() => make(const RangeBatching(parallel: 0)), throwsArgumentError);
      expect(
        () => make(const RangeBatching(delay: Duration(milliseconds: -1))),
        throwsArgumentError,
      );
      expect(make(const RangeBatching(mergeGap: 0)), isA<RangeQueue>());
      expect(make(const RangeBatching(maxBatch: 1)), isA<RangeQueue>());
    });

    test('fails a range that cannot exist', () async {
      final queue = queueOver(Host());

      await expectLater(queue.want(-1, 10), throwsRangeError);
      await expectLater(queue.want(0, 0), throwsRangeError);
    });
  });
}
