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

/// Text equal to what the file at [path] holds.
///
/// With [update] true, or with `ORBLIT_UPDATE_GOLDENS=1` in the environment
/// when it is left out, the file is written and the match passes.
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
    final file = File(_path);
    if (_update) {
      // A matcher that writes, because a golden is rewritten by running the
      // test again and a second command to do it would drift from this one.
      file.parent.createSync(recursive: true);
      file.writeAsStringSync('$item');
      return true;
    }
    return file.existsSync() && file.readAsStringSync() == '$item';
  }

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
    final file = File(_path);
    if (!file.existsSync()) {
      return mismatch.add(
        'there is no such file. Run with ORBLIT_UPDATE_GOLDENS=1 to write it',
      );
    }
    final want = const LineSplitter().convert(file.readAsStringSync());
    final got = const LineSplitter().convert('$item');
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
