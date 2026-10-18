import 'dart:convert';
import 'dart:io';
import 'dart:math' as math;

import 'package:matcher/matcher.dart';
import 'package:orblit_scene/orblit_scene.dart';

const _encoder = JsonEncoder.withIndent('  ');

/// What changed from [before] to [after], as text. A scene that did not change
/// is `[]`.
///
/// The text is the diff the editor and the renderer apply, so a golden file of
/// it is a scene's frame without pixels.
String describeChange(SceneDocument before, SceneDocument after) =>
    '${_encoder.convert(SceneDiff.between(before, after).toJson())}\n';

/// A String equal to what the file at [path] holds. A relative [path] is read
/// from the directory the tests run in, which is the package root.
///
/// With [update] true, or with `ORBLIT_UPDATE_GOLDENS=1` in the environment
/// when it is left out, the file is written and the match passes. A file that
/// changes is named on stderr.
Matcher matchesTextFile(String path, {bool? update}) => _TextFile(
  path,
  update ?? Platform.environment['ORBLIT_UPDATE_GOLDENS'] == '1',
);

final class _TextFile extends Matcher {
  _TextFile(this._path, this._update);

  final String _path;
  final bool _update;

  @override
  bool matches(Object? item, Map<Object?, Object?> matchState) {
    if (item is! String) return false;
    final file = File(_path);
    final same = file.existsSync() && _read(file) == item;
    if (!_update) return same;
    // A matcher that writes: a second command to rewrite goldens would drift.
    if (!same) {
      file.parent.createSync(recursive: true);
      file.writeAsStringSync(item);
      stderr.writeln('wrote $_path');
    }
    return true;
  }

  // A checkout may turn the file's line endings into CRLF.
  static String _read(File file) =>
      file.readAsStringSync().replaceAll('\r\n', '\n');

  @override
  Description describe(Description description) =>
      description.add('the text in $_path');

  @override
  Description describeMismatch(
    Object? item,
    Description mismatch,
    Map<Object?, Object?> matchState,
    bool verbose,
  ) {
    if (item is! String) return mismatch.add('is not a String');
    final file = File(_path);
    if (!file.existsSync()) {
      return mismatch.add(
        'there is no such file. Run with ORBLIT_UPDATE_GOLDENS=1 to write it',
      );
    }
    final want = const LineSplitter().convert(_read(file));
    final got = const LineSplitter().convert(item);
    final line = Iterable<int>.generate(math.max(want.length, got.length))
        .firstWhere(
          (i) => i >= want.length || i >= got.length || want[i] != got[i],
          orElse: () => -1,
        );
    if (line < 0) return mismatch.add('differs only in its line endings');
    return mismatch.add(
      'differs at line ${line + 1}: the file has '
      '${line < want.length ? '"${want[line]}"' : 'nothing'} and this has '
      '${line < got.length ? '"${got[line]}"' : 'nothing'}',
    );
  }
}
