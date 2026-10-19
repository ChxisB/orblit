import 'dart:io';

import 'package:orblit_save/orblit_save.dart';
import 'package:orblit_save/src/history_cli.dart';
import 'package:test/test.dart';

import 'support.dart';

void main() {
  late Directory dir;
  late HistoryStore store;
  late StringBuffer out;
  late StringBuffer err;

  setUp(() async {
    dir = Directory.systemTemp.createTempSync('orblit_cli_test');
    store = HistoryStore.files(dir.path);
    out = StringBuffer();
    err = StringBuffer();
    await store.write('ana', recordOf([1], at: 1000));
    await store.write('ana', recordOf([2], reason: const Leave(), at: 3000));
  });
  tearDown(() => dir.deleteSync(recursive: true));

  Future<int> run(List<String> args) =>
      runHistory([dir.path, ...args], out: out, err: err, clock: fixedClock);

  const early = '1970-01-01T00:00:02.000Z';
  const first = '1970-01-01T00:00:01.000Z';

  test('list with no key prints the keys', () async {
    expect(await run(['list']), 0);
    expect(out.toString(), 'ana\n');
  });

  test('list with a key prints each version, newest first', () async {
    expect(await run(['list', 'ana']), 0);

    final lines = out.toString().trim().split('\n');
    expect(lines, hasLength(2));
    expect(lines[0], startsWith('1970-01-01T00:00:03.000Z  leave  '));
    expect(lines[1], startsWith('$first  autosave  '));
  });

  test('restore writes the version when told the server is stopped', () async {
    expect(await run(['restore', 'ana', first, '--server-stopped']), 0);

    expect((await store.read('ana'))!.bytes, [1]);
    expect(out.toString(), 'Restored ana.\n');
  });

  test(
    'restore without --server-stopped prints usage and writes nothing',
    () async {
      expect(await run(['restore', 'ana', first]), 64);

      expect((await store.read('ana'))!.bytes, [2]);
      expect(err.toString(), contains('--server-stopped'));
    },
  );

  test('restore of a time with no version fails with its message', () async {
    expect(await run(['restore', 'ana', early, '--server-stopped']), 1);

    expect(err.toString(), contains('has no version'));
    expect((await store.read('ana'))!.bytes, [2]);
  });

  test('a time that is not a time fails', () async {
    expect(await run(['restore', 'ana', 'soon', '--server-stopped']), 1);
    expect(err.toString(), isNotEmpty);
  });

  test('restore-all lists what it would change and writes nothing', () async {
    expect(await run(['restore-all', early]), 0);

    expect(out.toString(), contains('ana  $first  autosave'));
    expect(out.toString(), contains('1 to restore. Add --apply to write.'));
    expect((await store.read('ana'))!.bytes, [2]);
  });

  test('restore-all --apply writes', () async {
    expect(await run(['restore-all', early, '--apply', '--server-stopped']), 0);

    expect((await store.read('ana'))!.bytes, [1]);
    expect(out.toString(), contains('Restored 1.'));
  });

  test('restore-all --apply without --server-stopped is refused', () async {
    expect(await run(['restore-all', early, '--apply']), 64);
    expect((await store.read('ana'))!.bytes, [2]);
  });

  test('wrong arguments print usage', () async {
    expect(await run([]), 64);
    expect(await run(['dance']), 64);
    expect(await run(['list', 'a', 'b']), 64);
    expect(await run(['restore', 'ana', '--server-stopped']), 64);
    expect(await run(['list', '--force']), 64);
    expect(err.toString(), contains('dart run orblit_save:history'));
  });
}
