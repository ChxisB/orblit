import 'dart:convert';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_remote/orblit_remote.dart';

/// The generator the audio exporter uses, so a case list is the same on every
/// run.
final class _Rng {
  _Rng(this._seed);

  int _seed;

  int next(int below) {
    _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
    return (_seed >> 8) % below;
  }

  T pick<T>(List<T> from) => from[next(from.length)];

  bool chance(int outOf) => next(outOf) == 0;
}

const _keys = <String>['a', 'b', 'c', 'x', 'y', '10', '2', 'B', 'Position'];
const _words = <String>['', 'on', 'off', 'a/b', 'café'];

Object? _scalar(_Rng rng) => switch (rng.next(6)) {
  0 => rng.next(100) - 20,
  1 => (rng.next(40) - 10) / 4,
  2 => rng.next(5).toDouble(),
  3 => rng.pick(_words),
  4 => rng.chance(2),
  _ => null,
};

Object? _doc(_Rng rng, int depth) {
  final kind = rng.next(depth >= 3 ? 6 : 9);
  if (kind < 6) return _scalar(rng);
  if (kind < 8) {
    return <String, Object?>{
      for (var i = rng.next(4); i > 0; i--) rng.pick(_keys): _doc(rng, depth + 1),
    };
  }
  return <Object?>[for (var i = rng.next(4); i > 0; i--) _doc(rng, depth + 1)];
}

Object? _replace(_Rng rng, Object? node) => switch (node) {
  final int n => rng.pick<Object?>([n + 1, n.toDouble(), null, 'text', n * 2]),
  final double d => rng.pick<Object?>([
    d == d.truncateToDouble() ? d.toInt() : d + 0.5,
    d + 0.5,
    null,
    d,
  ]),
  final String s => rng.pick<Object?>(['$s!', 7, null, s]),
  final bool b => rng.pick<Object?>([!b, 0, b]),
  final Map<String, Object?> m => rng.pick<Object?>([
    {...m}..remove(m.keys.isEmpty ? '' : m.keys.first),
    <String, Object?>{...m, 'new': 1},
    <Object?>[],
    5,
  ]),
  final List<Object?> l => rng.pick<Object?>([
    [...l, rng.next(9)],
    l.isEmpty ? <Object?>[] : l.sublist(0, l.length - 1),
    <String, Object?>{'k': l.length},
  ]),
  _ => rng.pick<Object?>([1, <String, Object?>{'k': 1}, 'x', <Object?>[]]),
};

Object? _edit(_Rng rng, Object? node) {
  if (node is Map<String, Object?> && node.isNotEmpty && !rng.chance(3)) {
    final key = rng.pick(node.keys.toList());
    return <String, Object?>{...node, key: _edit(rng, node[key])};
  }
  if (node is List<Object?> && node.isNotEmpty && !rng.chance(3)) {
    final i = rng.next(node.length);
    return <Object?>[...node]..[i] = _edit(rng, node[i]);
  }
  return _replace(rng, node);
}

Object? _read(Object? doc) => jsonDecode(jsonEncode(doc));

Map<String, Object?> _case(Object? a, Object? b) => {
  'op': 'diff',
  'a': a,
  'b': b,
  'expect': [
    for (final d in diffJson(a, b))
      {'path': d.path, 'from': tree(d.from), 'to': tree(d.to)},
  ],
};

List<Map<String, Object?>> _diffCases() {
  final rng = _Rng(2024);
  return [
    _case(
      _read({'n': 1}),
      _read({'n': 1.0}),
    ),
    _case(
      _read({
        'entities': {
          '3': {
            'Position': {'x': 1, 'y': 2},
          },
        },
      }),
      _read({
        'entities': {
          '3': {
            'Position': {'x': 1, 'y': 5},
          },
        },
      }),
    ),
    _case(_read({'a': [1, 2, 3]}), _read({'a': [1, 9]})),
    _case(_read({'a': {'b': 1}}), _read({'a': 5})),
    for (var i = 0; i < 400; i++)
      () {
        final a = _read(_doc(rng, 0));
        return _case(a, _read(rng.chance(8) ? a : _edit(rng, a)));
      }(),
  ];
}

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_remote',
    name: 'diff',
    cases: _diffCases(),
  );
}
