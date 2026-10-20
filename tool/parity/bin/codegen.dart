// Usage: `dart run bin/codegen.dart <repository root>`
import 'package:orblit_codegen/orblit_codegen.dart';
import 'package:orblit_parity_tool/fixture.dart';

/// Text as UTF-16 code units, so a lone surrogate survives the fixture.
List<int> _units(String text) => text.codeUnits;

const _kinds = <String>[
  'float32',
  'float64',
  'int32',
  'uint32',
  'int64',
  'uint8',
];

/// Characters a name or a path may meet: JSON escapes, control characters,
/// non-ASCII, a pair and a lone surrogate.
const _awkward = <int>[
  0x22,
  0x27,
  0x5C,
  0x2F,
  0x08,
  0x09,
  0x0A,
  0x0C,
  0x0D,
  0x00,
  0x1F,
  0x7F,
  0xE9,
  0x2028,
  0xD83D,
  0xDE00,
  0xD800,
  0xDC00,
  0xFFFF,
];

ComponentDeclaration _declaration({
  String name = 'Position',
  String? className,
  String kind = 'float32',
  List<String> fields = const ['x', 'y', 'z'],
  bool replicated = false,
  bool ownerWritable = false,
  String source = 'lib/position.dart',
}) => ComponentDeclaration(
  name: name,
  className: className ?? name,
  kind: kind,
  fields: fields,
  replicated: replicated,
  ownerWritable: ownerWritable,
  source: source,
);

Map<String, Object?> _case(
  List<ComponentDeclaration> components, {
  String package = 'my_game',
}) => {
  'op': 'manifest',
  'package': _units(package),
  'components': [
    for (final c in components)
      {
        'name': _units(c.name),
        'class': _units(c.className),
        'kind': _units(c.kind),
        'fields': [for (final f in c.fields) _units(f)],
        'replicated': c.replicated,
        'ownerWritable': c.ownerWritable,
        'source': _units(c.source),
      },
  ],
  'expect': attempt(
    () => const ComponentEmitter().manifest(
      package,
      ScanResult(components, const []).components,
    ),
  ),
};

List<Map<String, Object?>> _flagCases() => [
  _case(const []),
  for (final replicated in [false, true])
    for (final ownerWritable in [false, true])
      _case([
        _declaration(replicated: replicated, ownerWritable: ownerWritable),
      ]),
  _case([
    for (final kind in _kinds)
      _declaration(name: 'C_$kind', kind: kind, fields: ['a', 'b']),
  ]),
  _case([_declaration(fields: const [])]),
  _case([
    _declaration(fields: [for (var i = 0; i < 50; i++) 'f$i']),
  ]),
];

/// Each awkward character in each place a string is written.
List<Map<String, Object?>> _textCases() {
  final cases = <Map<String, Object?>>[];
  for (final unit in _awkward) {
    final text = 'a${String.fromCharCode(unit)}b';
    cases
      ..add(_case([_declaration(name: text, className: 'Plain')]))
      ..add(_case([_declaration(className: text)]))
      ..add(_case([_declaration(kind: text)]))
      ..add(
        _case([
          _declaration(fields: [text, 'y']),
        ]),
      )
      ..add(_case([_declaration(source: text)]))
      ..add(_case([_declaration()], package: text));
  }
  return cases;
}

/// Names in an order that is not sorted, and the ones that sort differently by
/// UTF-16 unit than by code point.
List<Map<String, Object?>> _sortCases() {
  String name(int i) => 'Component${(i * 7) % 41}';
  return [
    _case([for (var i = 0; i < 40; i++) _declaration(name: name(i))]),
    _case([
      for (final n in ['Zeta', 'alpha', 'Alpha', 'zeta', '', '_x', '1', '~'])
        _declaration(name: n),
    ]),
    // U+1F600 is a pair starting 0xD83D, so by unit it sorts before U+FF5E.
    _case([
      for (final n in ['\u{FF5E}', '\u{1F600}', '\u{FFFF}', '\u{E000}'])
        _declaration(name: n),
    ]),
    // Three components with one name keep the order they came in.
    _case([
      for (final c in ['Third', 'First', 'Second'])
        _declaration(name: 'Same', className: c),
      _declaration(name: 'Before'),
    ]),
    _case([
      for (var i = 100; i > 0; i--)
        _declaration(name: 'N${i.toString().padLeft(3, '0')}'),
    ]),
  ];
}

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_codegen',
    name: 'codegen',
    cases: [..._flagCases(), ..._textCases(), ..._sortCases()],
  );
}
