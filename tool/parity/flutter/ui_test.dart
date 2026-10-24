// Writes dotnet/fixtures/orblit_ui/style.json and node.json: what the class
// list, CSS, theme and breakpoint readers give for a spread of inputs, and what
// the node tree reads, edits and places. It needs the Flutter package it reads,
// so tool/parity/flutter/regen.sh copies it into that package's test folder,
// runs it there and removes the copy.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_ui/orblit_ui.dart';

// ignore: avoid_relative_lib_imports, the helper is not in any package
import '../../../tool/parity/lib/fixture.dart';

Object? _field(Object? value) => switch (value) {
  final Color c => c.toARGB32(),
  final double d => number(d),
  final int i => big(i),
  _ => value,
};

Map<String, Object?> _style(UiStyle s) {
  final fields = <String, Object?>{
    'direction': s.direction,
    'mainAxis': s.mainAxis,
    'crossAxis': s.crossAxis,
    'gap': s.gap,
    'grow': s.grow,
    'paddingTop': s.paddingTop,
    'paddingRight': s.paddingRight,
    'paddingBottom': s.paddingBottom,
    'paddingLeft': s.paddingLeft,
    'marginTop': s.marginTop,
    'marginRight': s.marginRight,
    'marginBottom': s.marginBottom,
    'marginLeft': s.marginLeft,
    'width': s.width,
    'height': s.height,
    'minWidth': s.minWidth,
    'minHeight': s.minHeight,
    'maxWidth': s.maxWidth,
    'maxHeight': s.maxHeight,
    'background': s.background,
    'colour': s.colour,
    'borderColour': s.borderColour,
    'borderWidth': s.borderWidth,
    'radius': s.radius,
    'opacity': s.opacity,
    'fontSize': s.fontSize,
    'fontWeight': s.fontWeight,
    'italic': s.italic,
    'letterSpacing': s.letterSpacing,
    'lineHeight': s.lineHeight,
    'textAlign': s.textAlign,
    'uppercase': s.uppercase,
    'shadow': s.shadow,
    'clip': s.clip,
    'scroll': s.scroll,
    'top': s.top,
    'right': s.right,
    'bottom': s.bottom,
    'left': s.left,
  };
  return {
    for (final e in fields.entries)
      if (e.value != null) e.key: _field(e.value),
    'isPositioned': s.isPositioned,
    'hasPadding': s.hasPadding,
    'hasMargin': s.hasMargin,
    'hasBox': s.hasBox,
    'hasSize': s.hasSize,
  };
}

UiTheme _theme(double? scale, List<double>? bp) {
  var theme = const UiTheme();
  if (bp != null) {
    theme = UiTheme(
      breakpoints: UiBreakpoints(sm: bp[0], md: bp[1], lg: bp[2], xl: bp[3]),
    );
  }
  return scale == null ? theme : theme.scaled(scale);
}

Map<String, Object?> _colour(String text) => {
  'op': 'colour',
  'text': text,
  'expect': attempt(() => parseColour(text)?.toARGB32()),
};

Map<String, Object?> _classes(
  String classes, {
  double? width,
  double? scale,
  List<double>? bp,
}) {
  final utilities = UiUtilities(theme: _theme(scale, bp));
  return {
    'op': 'classes',
    'classes': classes,
    'width': width == null ? null : number(width),
    'scale': scale == null ? null : number(scale),
    'bp': bp == null ? null : [for (final b in bp) number(b)],
    'expect': attempt(
      () => {
        'style': _style(utilities.parse(classes, width: width)),
        'unknown': utilities.unknownIn(classes),
      },
    ),
  };
}

Map<String, Object?> _css(String source, {double? scale}) {
  final css = UiCss(theme: _theme(scale, null));
  return {
    'op': 'css',
    'css': source,
    'scale': scale == null ? null : number(scale),
    'expect': attempt(
      () => {'style': _style(css.parse(source)), 'unknown': css.unknownIn(source)},
    ),
  };
}

Map<String, Object?> _breakpoints(List<double> bp, double width) {
  final points = UiBreakpoints(sm: bp[0], md: bp[1], lg: bp[2], xl: bp[3]);
  return {
    'op': 'breakpoints',
    'bp': [for (final b in bp) number(b)],
    'width': number(width),
    'expect': attempt(
      () => {'active': points.activeAt(width), 'label': points.labelAt(width)},
    ),
  };
}

Map<String, Object?> _starts(String name) {
  const points = UiBreakpoints();
  return {
    'op': 'starts',
    'name': name,
    'expect': attempt(
      () => {
        'start': points.startOf(name) == null
            ? null
            : number(points.startOf(name)!),
        'knows': points.knows(name),
      },
    ),
  };
}

Map<String, Object?> _scaled(double factor) {
  const theme = UiTheme();
  return {
    'op': 'scaled',
    'factor': number(factor),
    'expect': attempt(() {
      final scaled = theme.scaled(factor);
      return {
        'same': identical(scaled, theme),
        'step': number(scaled.step),
        'text': tree(scaled.text),
        'radius': tree(scaled.radius),
        'palette': scaled.palette.length,
      };
    }),
  };
}

const _words = [
  'row', 'col', 'column', 'stack', 'grow', 'clip', 'scroll-y', 'scroll-x', //
  'italic', 'uppercase', 'border', 'rounded', 'shadow', 'shadow-lg',
  'w-full', 'h-full', 'full', 'ROW', 'rows', 'row-', 'grow-', '',
];

const _measured = [
  'p', 'px', 'py', 'pt', 'pr', 'pb', 'pl', 'm', 'mx', 'my', 'mt', 'mr', 'mb', //
  'ml', 'w', 'h', 'min-w', 'min-h', 'max-w', 'max-h', 'gap', 'inset', 'top',
  'right', 'bottom', 'left',
];

const _measures = [
  '0', '1', '4', '0.5', '-2', '.5', '1e2', 'px', 'full', 'auto', '[13]', //
  '[ 13 ]', '[1e2]', '[0x10]', '[x]', '[]', '[full]', '', 'x', 'NaN',
  'Infinity', '1e400', '0x10',
];

const _colourWords = [
  'slate-50', 'slate-500', 'ember-500', 'steel-900', 'moss-400', 'rose-100', //
  'amber-300', 'white', 'black', 'transparent', 'current', 'slate',
  'slate-1000', 'nonesuch-500', 'Slate-500', '', '[#fff]', '[#123456]',
  '[#12345678]', '[rgb(1,2,3)]', '[rgba(1,2,3,.5)]', '[red]', '[]', '[ #fff ]',
  '#fff',
];

const _suffixes = {
  'items': [
    'start', 'center', 'end', 'stretch', 'baseline', 'between', '', 'foo', //
  ],
  'justify': [
    'start', 'center', 'end', 'between', 'around', 'evenly', 'stretch', '', //
    'foo',
  ],
  'text': [
    'xs', 'sm', 'base', 'lg', 'xl', '2xl', '3xl', '4xl', '5xl', 'left', //
    'center', 'right', 'justify', 'start', '',
  ],
  'font': [
    'thin', 'light', 'normal', 'medium', 'semibold', 'bold', 'black', //
    'extrabold', '', '400',
  ],
  'border': ['0', '1', '2', '0.5', 'x', '-1', 'NaN'],
  'rounded': [
    'none', 'sm', 'md', 'lg', 'xl', '2xl', 'full', '3xl', '4', '0.5', 'x', //
    '', '-2', 'NaN',
  ],
  'opacity': [
    '0', '5', '50', '100', '150', '-20', '0.5', 'NaN', 'Infinity', //
    '-Infinity', '1e400', 'x', '', '-0',
  ],
  'flex': ['1', '2', '0', '0.5', '-1', 'auto', 'none', 'x', '', 'NaN', '1e400'],
  'leading': ['1', '1.5', '0', '-1', 'x', 'NaN', ''],
  'tracking': ['0.5', '-1', '0', 'x', ''],
};

const _prefixed = [
  'sm:p-2 md:p-4 lg:p-8', 'lg:p-8 md:p-4 sm:p-2', 'md:text-xl text-base', //
  'lg:row', 'xl:full', 'hover:p-2', 'md:hover:p-2', 'md:', ':p-2',
  'md:nonesuch', 'MD:p-2', 'sm:sm:p-2', 'p-1 sm:p-2 p-3',
  'md:text-xl lg:text-3xl', 'lg:text-3xl md:text-xl',
];

const _spaced = [
  ' p-4  gap-2 ', 'p-4\tgap-2', 'p-4\ngap-2', 'p-4\u00a0gap-2', //
  'p-4\u0085gap-2', 'p-4\ufeffgap-2', 'p-4\u2028gap-2', 'p-4\u200bgap-2',
  '\u0085p-4', 'p-4\u0085', '   ', 'p-4 p-4 p-4', 'unknown p-4 unknown-2',
];

const _widths = [
  null, 0.0, 639.0, 640.0, 899.5, 900.0, 1279.0, 1280.0, 1680.0, 4000.0, //
  -5.0, double.nan, double.infinity,
];

const _themes = <({double? scale, List<double>? bp})>[
  (scale: null, bp: [900, 640, 1280, 1680]),
  (scale: null, bp: [700, 700, 1280, 1680]),
  (scale: null, bp: [double.nan, 640, 900, 1280]),
  (scale: null, bp: [640, 900, 1280, double.infinity]),
  (scale: 2, bp: null),
  (scale: 0.5, bp: null),
  (scale: 1, bp: null),
  (scale: 0, bp: null),
  (scale: double.nan, bp: null),
  (scale: double.infinity, bp: null),
];

List<Map<String, Object?>> _colourCases() => [
  for (final text in const [
    '#fff', '#FFF', '#123456', '#12345678', '#1234', '#12345', '#1234567', //
    '#', '#ggg', '#12345g', ' #abc ', '#+1234', '#123456+1', '#123456-1',
    '#123456 1', '#1234567 ', '#-12345', '#+12345', '#abc\u0085', '#\uff41bc',
    '#12345\u00a0', '#1 345678', '#12345678 ', '#00000000', '#ffffffff',
    '#123456 7', '#12345 78', '#123456\u00857', '#12345\u008578', '#123456\ufeff7',
    '#12345 7', '#0x12ab', '#12ab340x', '#12ab34\u00007', '#-1234567',
    '#1234567-', '#+1234567', '#1234567+', '#123456-1', '#ABCDEF12',
    '#abcdef12', '#AbCdEf', '#aBc', '#000', '#00000', '##fff', '# fff',
    '#fff ', '\n#fff\n', '\u0085#fff', '#fff\u0085', '\ufeff#fff', '#FF', '#1',
    'rgb(1,2,3)', 'rgb(1 2 3)', 'rgba(10, 20, 30, 0.5)',
    'rgb(10 20 30 / 50%)', 'rgb(100%, 50%, 0%)', 'rgb(300, -5, 20)',
    'rgb(1,2)', 'rgb()', 'rgb(a,b,c)', 'RGB(1,2,3)', 'rgb(1,2,3',
    'rgb(1,2,3)x', 'rgba(1,2,3,x)', 'rgba(1,2,3,2)', 'rgba(1,2,3,-1)',
    'rgb(NaN,1,2)', 'rgb(1,Infinity,2)', 'rgb(Infinity%,1,2)',
    'rgb(1e300%,1,2)', 'rgb(1e300,1,2)', 'rgba(1,2,3,1e300)',
    'rgba(1,2,3,NaN)', 'rgb(0.5,1.5,2.5)', 'rgb(1,2,3)\n', 'rgb(1,\n2,\n3)',
    'rgb(1\u00a02\u00a03)', 'rgb(1\u00852\u00853)', 'rgb(1\ufeff2\ufeff3)',
    'rgb(1,,2,,3)', 'rgb(,1,2,3)', 'rgb(1/2/3)', 'rgb(1%, 2%, 3%)',
    'rgb(-1%,0,0)', 'rgb(1, 2, 3, 4, 5)', 'rgb(1,2,3,50%)', 'rgb( 1 , 2 , 3 )',
    'rgba(255,255,255,0.004)', 'rgba(255,255,255,0.002)',
    'rgb(1e2,1.5e1,5)', 'rgb(0x10,0,0)', 'red', '', '  ', 'transparent',
    'slate-500',
  ])
    _colour(text),
];

List<Map<String, Object?>> _classCases() => [
  for (final word in _words) _classes(word),
  for (final prefix in _measured)
    for (final measure in _measures) _classes('$prefix-$measure'),
  for (final prefix in const ['bg', 'text', 'border'])
    for (final colour in _colourWords) _classes('$prefix-$colour'),
  for (final entry in _suffixes.entries)
    for (final suffix in entry.value) _classes('${entry.key}-$suffix'),
  for (final list in _spaced) _classes(list),
  for (final list in _prefixed)
    for (final width in _widths) _classes(list, width: width),
  for (final theme in _themes)
    for (final list in const [
      'md:text-xl lg:text-3xl',
      'sm:p-2 md:p-4 lg:p-8',
      'p-4 text-lg rounded-lg gap-2 text-3xl rounded-full',
    ])
      for (final width in const [null, 500.0, 700.0, 1000.0, 2000.0])
        _classes(list, width: width, scale: theme.scale, bp: theme.bp),
  _classes('p-4 row items-center justify-between gap-3'),
  _classes('p-2 p-8'),
  _classes('p-2 pt-8'),
  _classes('bg-slate-800 text-ember-500 bg-[#123456]'),
  _classes('p-4 rounded-lg wobbly bg-slate-800'),
  _classes('p-4 wobbly nonsense-9'),
];

const _lengthProperties = [
  'gap', 'padding-top', 'padding-right', 'padding-bottom', 'padding-left', //
  'margin-top', 'margin-right', 'margin-bottom', 'margin-left', 'width',
  'height', 'min-width', 'min-height', 'max-width', 'max-height', 'top',
  'right', 'bottom', 'left', 'border-width', 'border-radius', 'font-size',
  'letter-spacing',
];

const _lengths = [
  '0', '8px', '-8px', '+8px', '1.5px', '.5px', '5.px', '1e2px', '8PX', //
  ' 8px ', '8 px', '1rem', '1.5em', '12pt', '-3pt', '100%', '50%', 'auto',
  'none', '8', 'x', 'px', 'NaNpx', 'Infinitypx', '1e400px', '0x10px', '1,5px',
];

const _sides = [
  '8px', '8px 12px', '1px 2px 3px', '1px 2px 3px 4px', '1px 2px 3px 4px 5px', //
  'auto', '0 auto', '8px auto', '0', '0 0', 'x', 'x y', '1px x', '100%',
  '100% 0', '1rem 2rem', '  1px   2px ', '1px\t2px', '1px\n2px',
  '1px\u00a02px', '1px\u00852px', '1px\ufeff2px', '1px  x  3px',
];

const _colourValues = [
  '#fff', 'red', 'slate-500', 'ember-500', ' white ', 'rgb(1,2,3)', //
  'rgba(1,2,3,.5)', 'transparent', 'current', 'currentColor', 'nonesuch', '#12',
  '[#fff]', 'Slate-500', 'rgb(NaN,1,2)', 'rgb(1, 2, 3)',
];

const _keywords = {
  'display': ['flex', 'block', 'inline', 'none', 'grid', 'FLEX'],
  'flex-direction': ['row', 'column', 'row-reverse', 'column-reverse'],
  'position': ['absolute', 'relative', 'fixed'],
  'align-items': [
    'flex-start', 'start', 'center', 'flex-end', 'end', 'stretch', //
    'baseline', 'foo',
  ],
  'justify-content': [
    'flex-start', 'start', 'center', 'flex-end', 'end', 'space-between', //
    'space-around', 'space-evenly', 'stretch', 'foo',
  ],
  'text-align': ['left', 'center', 'right', 'justify', 'start', 'end'],
  'font-style': ['italic', 'normal', 'oblique'],
  'text-transform': ['uppercase', 'lowercase', 'capitalize', 'none'],
  'overflow': ['hidden', 'auto', 'scroll', 'visible'],
  'overflow-y': ['hidden', 'auto', 'scroll', 'visible'],
  'overflow-x': ['hidden', 'auto', 'scroll', 'visible'],
  'font-weight': [
    'normal', 'bold', '700', '100', '900', 'bolder', '0x2BC', '1e3', //
    '12345678901234', '9223372036854775807', '9223372036854775808', '-5',
    '+5', ' 7 ', '', '0x', '0x7FFFFFFFFFFFFFFF', '0xFFFFFFFFFFFFFFFF',
  ],
  'flex': [
    '1', '1 1 0%', '2 3', '0', '-1', 'x', 'auto', 'NaN', '1e400', ' 1', //
    '1\u00a02', '1\u00852', '.5',
  ],
  'flex-grow': ['1', '2.5', '0', 'x', ''],
  'line-height': ['1.5', '2', '1.5px', 'normal', 'NaN', '-1', '1e400', '.5'],
  'opacity': [
    '0.5', '1', '2', '-1', '0', 'NaN', 'x', '50%', 'Infinity', '-Infinity', //
    '1e400', '-0', '-0.0', '0.0', '1.0000001',
  ],
  'border': [
    '1px solid #333', '2px solid #334455', '2px', 'solid', '#333', 'none', //
    '0', 'thin', '1px dashed slate-500', '3px rgb(1,2,3)', '1px  #fff',
    'x', '4px 5px', '1px solid rgba(1, 2, 3, 0.5)', '1rem solid white',
  ],
  'box-shadow': [
    '0 4px 8px rgba(0,0,0,.5)', 'none', '0 4px 8px #000', '8px', '0 0', //
    '1px 2px 3px 4px', 'x', '0 4px 8px rgba(0, 0, 0, .5)', 'inset 0 1px #fff',
  ],
};

const _misc = [
  'padding: 8px', 'padding: 8px 12px', 'padding: 1px 2px 3px 4px', //
  'color: #fff', 'color: #1a2b3c', 'color: #11223380', 'color: rgb(255, 0, 0)',
  'color: rgba(0, 0, 0, 0.5)', 'color: slate-500',
  'display: flex; align-items: center; justify-content: space-between;'
      ' gap: 12px; flex: 1',
  'border: 2px solid #334455', '.card { padding: 6px; border-radius: 4px }',
  'float: left; padding: 4px', '{padding: 4px}', 'a{}', '{', '}', 'x { y } ',
  '}{', '{ a: b } trailing', '{ padding: 4px; }', '{padding: 4px', 'padding: 4px}',
  'x {padding: 4px} y {margin: 2px}', 'PADDING: 4px', 'Padding-Top : 4px',
  ' padding : 4px ', 'padding:4px;;;color:#fff', 'padding', ': 4px',
  'padding: ', 'a: b: c', 'padding: 4px: x', 'color: #fff; color: red',
  'padding: 4px; padding-top: 8px', 'padding-top: 8px; padding: 4px',
  'foo: bar', 'display: grid', 'position: relative', '', ' ', ';', ';;;',
  'padding: 4px;\n  margin: 2px;\n  color: #fff;\n', 'left: 40px; top: 12px',
  'overflow: auto; overflow-x: hidden', 'overflow-x: scroll; overflow-y: auto',
  'width: 100%; height: 100%', 'width: 50%', 'margin: 0 auto',
  'padding: 1rem; gap: 0.5rem', 'font-size: 1.25rem; line-height: 1.5',
];

List<Map<String, Object?>> _cssCases() => [
  for (final property in _lengthProperties)
    for (final length in _lengths) _css('$property: $length'),
  for (final property in const ['padding', 'margin'])
    for (final sides in _sides) _css('$property: $sides'),
  for (final property in const [
    'background', 'background-color', 'color', 'border-color', //
  ])
    for (final colour in _colourValues) _css('$property: $colour'),
  for (final entry in _keywords.entries)
    for (final value in entry.value) _css('${entry.key}: $value'),
  for (final source in _misc) _css(source),
  for (final scale in const [2.0, 0.5, 1.0, 0.0])
    for (final source in const [
      'padding: 1rem', 'gap: 2em', 'width: 10px', 'font-size: 12pt', //
      'border: 1rem solid white',
    ])
      _css(source, scale: scale),
];

List<Map<String, Object?>> _breakpointCases() => [
  for (final bp in const <List<double>>[
    [640, 900, 1280, 1680], //
    [900, 640, 1280, 1680],
    [700, 700, 1280, 1680],
    [double.nan, 640, 900, 1280],
    [640, 900, 1280, double.infinity],
    [0, -0.0, 5, 5],
    [1680, 1280, 900, 640],
  ])
    for (final width in const [
      -1.0, 0.0, -0.0, 5.0, 639.0, 640.0, 700.0, 899.0, 900.0, 1000.0, //
      1279.0, 1280.0, 1680.0, 5000.0, double.nan, double.infinity,
      -double.infinity,
    ])
      _breakpoints(bp, width),
  for (final name in const [
    'sm', 'md', 'lg', 'xl', 'base', '2xl', '', 'SM', 'sm ', 'xs', //
  ])
    _starts(name),
];

List<Map<String, Object?>> _themeCases() => [
  for (final factor in const [
    1.0, 2.0, 0.5, 1.5, 0.0, -1.0, double.nan, double.infinity, //
    -double.infinity, 1e308, 3.0, 0.1,
  ])
    _scaled(factor),
  {
    'op': 'palette',
    'expect': attempt(
      () => {
        'names': UiTheme.defaultPalette.keys.toList(),
        'colours': [
          for (final c in UiTheme.defaultPalette.values) c.toARGB32(),
        ],
      },
    ),
  },
  for (final name in const [
    'slate-500', 'white', 'current', 'transparent', 'Slate-500', ' white', //
    '', 'slate', 'amber-900', 'amber-950',
  ])
    {
      'op': 'palette-colour',
      'name': name,
      'expect': attempt(() => const UiTheme().colour(name)?.toARGB32()),
    },
];

const _nodeTexts = [
  '''
{
  "type": "column",
  "class": "p-4 gap-2",
  "children": [
    {"type": "text", "text": "Score", "class": "text-sm text-slate-400"},
    {"type": "button", "text": "Again", "props": {"onPressed": "restart"}}
  ]
}
''',
  '"just words"', '""', '{oh dear', '', '   ', 'null', '1', '1.5', 'true', //
  '[]', '[1,2]', '{}', '{"type": 3}', '{"type": null}', '{"type": "row"}',
  '{"class": 3, "className": "x"}', '{"className": "x"}',
  '{"class": "a", "className": "b"}', '{"class": "", "className": "b"}',
  '{"style": 5}', '{"style": "color: red"}', '{"text": 5}', '{"text": ""}',
  '{"text": "hi"}', '{"props": []}', '{"props": {}}',
  '{"props": {"a":1,"b":[1,2],"c":{"d":null},"e":1.5,"f":"s","g":true}}',
  '{"props": {"big": 12345678901234567890, "neg": -5, "exp": 1e2}}',
  '{"key": 3}', '{"key": "k"}', '{"key": ""}', '{"children": {}}',
  '{"children": []}',
  '{"children": [1, null, "x", {"type": 3}, [], {"children": ["deep"]}]}',
  '{"type": "a", "type": "b"}', ' {"type": "row"} ', '{"a":}', '[1,', '"abc',
  '{"a" 1}', 'tru', "{'a':1}", '[1,]', '01', '1.', '-', '"\\x"', '"\\u12"',
  '"a\tb"', '{"a": 1} x', '{"a": 1}{"b": 2}', '\ufeff{"type": "row"}',
  '{"type": "row"}\u0000', '{"children": [{"children": [{"children": []}]}]}',
  '{"type": "text", "text": "\u00dcn\u00efc\u00f6d\u00e9 \\u00e9 \\ud83d\\ude00"}', '{"text": null}',
  '{"props": null, "children": null, "key": null, "class": null}',
  '{"children": [{"type": "button", "props": {"textKey": "play"}}]}',
  '1e', '1e+', '1e5', '1E-5', '1.e5', '1.x', '-x', '-0', '-01', '--1', '+1', //
  '.5', '1e5.5', '0.5e', '[1', '[1 2]', '[1,,2]', '[,1]', '{"a"', '{"a":',
  '{"a":1', '{"a":1,', '{"a":1,}', '{1:2}', '{"a":1 "b":2}', '{,}', 'nul',
  'nulx', 'truex', 'fals', 'falsey', 'True', 'NaN', 'Infinity', '"\\u"',
  '"\\u12', '"\\u12g4"', '"\\', '"\\ud83d"', '"\\ude00"', '"\\/"',
  '"\\b\\f\\n\\r\\t"', '"a', '"a\nb"', '"\u0001"', '/* c */ 1',
  '// c\n1', '[1]x', ']', '}', '{]', '[}', '"a" "b"', '1 2', '\u000b1',
  '\u00a01', '1\u00a0', '\n\t\r 1 \n', '{"type":"row"} \u0000',
  '{"props":{"a":1e999}}', '{"props":{"a":-1e999}}', '{"props":{"a":1e-999}}',
  '{"props":{"a":-0}}', '{"props":{"a":-0.0}}', '{"props":{"a":9007199254740993}}',
  '{"props":{"a":-9223372036854775808}}', '{"props":{"a":9223372036854775807}}',
  '{"props":{"a":9223372036854775808}}', '{"props":{"a":1.0}}',
  '{"props":{"":1}}', '{"props":{"a":"\\u00e9\\ud83d\\ude00"}}',
  '{"props":{"a":"\\ud83d"}}', '{"props":{"a":"\\ude00"}}',  '-', '1.', '1ex', '1e+x', '1e-', '1e-x', '-.5', '-e5', '-+1', '0x', '0.', //
  '0.e', '0e', '00', '1.5.', '1.5e', '1.5e5e', '[1.]', '[1.x]', '[-]', '[-x]',
  '[1e]', '[1e5]', '{"a":1.}', '{"a":-}', '{"a":-x}', '[1.', '[-', '[1e',
  '[0.0e-0]', '[-0e0]', '[1E+2]', '[1e+]', '[1e+ ]', '[1. ]', '[- 1]',
  '{"a":1e}', '{"a":1e,"b":2}', '{"a":1.,"b":2}', '{"a":-,"b":2}',
  '"\\u123', '"\\u1234', '"\\u00', '"\\u00e', '"\\ud83d\\u', '"ab\\',
  '"\\q"', '"\\u+123"', '"\\u 123"', '"\\uD83D\\uDE00"', '"\\u00E9"',
  'nux', 'trux', 'tr', 'tx', 'fa x', 'n', 't', 'f', '[tr', '[nux]', '{', '[',
  '{ ', '[ ', '{"a":1, ', '{"a":1,\n', '[1, ', '{"a":[', '{"a":{',
];

/// A string with a lone surrogate cannot be compared as JSON text, so it
/// travels as its UTF-16 code units. UTF-8 turns a lone surrogate into U+FFFD,
/// which is how a string that holds one is told apart.
Object? _units(Object? value) => switch (value) {
  final String s when utf8.decode(utf8.encode(s)) != s => {'units': s.codeUnits},
  final Map<Object?, Object?> m => {
    for (final e in m.entries) e.key: _units(e.value),
  },
  final List<Object?> l => [for (final e in l) _units(e)],
  _ => value,
};

Map<String, Object?> _decode(String text) => {
  'op': 'decode',
  'text': text,
  'expect': attempt(() => _units(tree(UiNode.decode(text).toJson()))),
};

String _deep(int depth) => '${'[' * depth}${']' * depth}';

const _base = UiNode(
  type: 'column',
  classes: 'p-4',
  children: [
    UiNode(type: 'text', text: 'Title'),
    UiNode(
      type: 'row',
      children: [
        UiNode(type: 'button', text: 'One'),
        UiNode(type: 'button', text: 'Two'),
      ],
    ),
  ],
);

const _lone = UiNode(type: 'box');

const _paths = <List<int>>[
  [], [0], [1], [1, 0], [1, 1], [0, 0], [9], [-1], [1, 9], [1, -1], //
  [0, 0, 0], [1, 1, 0],
];

const _fresh = UiNode(type: 'button', text: 'X', key: 'fresh');

List<Map<String, Object?>> _editCases() {
  Map<String, Object?> edit(
    UiNode root,
    String action,
    List<int> path,
    Object? Function(UiNode) run, {
    int? index,
  }) => {
    'op': 'edit',
    'tree': root.toJson(),
    'action': action,
    'path': path,
    'index': index,
    'expect': attempt(() => run(root)),
  };

  Object? json(UiNode? node) => node == null ? null : tree(node.toJson());

  return [
    for (final root in const [_base, _lone])
      for (final path in _paths) ...[
        edit(root, 'at', path, (r) => json(r.at(path))),
        edit(root, 'replace', path, (r) => json(r.replaceAt(path, _fresh))),
        edit(root, 'remove', path, (r) => json(r.removeAt(path))),
        for (final index in const [0, 1, 2, 99, -1, -99])
          edit(
            root,
            'insert',
            path,
            (r) => json(r.insertAt(path, index, _fresh)),
            index: index,
          ),
        for (final index in const [0, 1, 2, 99, -1, -99])
          edit(
            root,
            'move',
            path,
            (r) => json(r.moveAt(path, index)),
            index: index,
          ),
      ],
  ];
}

Map<String, Object?> _walk(UiNode root) => {
  'op': 'walk',
  'tree': root.toJson(),
  'expect': attempt(
    () => {
      'seen': [
        for (final found in root.walk())
          {
            'path': found.path,
            'type': found.node.type,
            'text': found.node.text,
          },
      ],
      'textKeys': root.textKeys.toList()..sort(),
    },
  ),
};

const _keyedTree = UiNode(
  type: 'column',
  children: [
    UiNode(type: 'text', props: {'textKey': 'play'}),
    UiNode(type: 'text', text: 'Fixed'),
    UiNode(type: 'button', props: {'textKey': 'quit'}),
    UiNode(type: 'button', props: {'textKey': 'quit'}),
    UiNode(type: 'button', props: {'textKey': ''}),
    UiNode(
      type: 'row',
      children: [
        UiNode(type: 'text', props: {'textKey': 'deep'}),
      ],
    ),
  ],
);

List<Map<String, Object?>> _walkCases() => [
  _walk(_base),
  _walk(_lone),
  _walk(_keyedTree),
];

const _propsCases = [
  '{}', '{"textKey":"play"}', '{"textKey":""}', '{"textKey":3}', //
  '{"textArgs":{"name":"Ada","count":3}}', '{"textArgs":[1]}',
  '{"textArgs":{}}', '{"textArgs":null}', '{"textArgs":{"a":null,"b":1.5}}',
  '{"onPressed":"restart"}', '{"onPressed":""}', '{"onPressed":5}',
  '{"onPressed":null}', '{"onChanged":"edit","onPressed":"go"}',
  '{"textKey":"k","textArgs":{"count":1},"onPressed":"p"}',
];

Map<String, Object?> _props(String json) {
  final node = UiNode.decode('{"type":"button","props":$json}');
  return {
    'op': 'props',
    'props': json,
    'expect': attempt(
      () => {
        'textKey': node.textKey,
        'textArgs': tree(node.textArgs),
        'handlers': {
          for (final event in const ['onPressed', 'onChanged', 'onMissing'])
            event: node.handlerFor(event),
        },
      },
    ),
  };
}

const _placeCss = [
  '', 'padding: 4px', 'padding: 4px; left: 0px; top: 0px; color: #fff', //
  'left: 1px', 'LEFT : 5px; Top: 6px', 'right: 3px; bottom: 4px', ';;',
  'left', ' ; left: 4px ;', 'left: 4px; left: 5px', 'padding:4px;left:1px;top:2px',
  'color: #fff;', 'left: 9px; top: 9px; right: 1px; bottom: 2px',
];

const _drops = <(double, double, bool)>[
  (10.4, 20.6, true), (10.4, 20.6, false), (10, 20, false), //
  (0.5, -0.5, true), (-0.5, -0.5, true), (2.5, 3.5, true), (-2.5, 3.5, true),
  (0.005, 0.004, false), (0.006, 0.994, false), (1e21, 0, false),
  (0, 0, true), (12.345, 1.005, false), (-0.001, 0.001, false),
  (double.nan, 0, true), (double.nan, 0, false), (double.infinity, 0, false),
  (0, double.negativeInfinity, true), (1e15 + 0.5, 0, false),
];

const _placedCss = [
  'left: 40px; top: 12px', 'top: 8px', 'left: 5', 'left: 5PX', 'left: 5 px', //
  'left: abc', 'left: 1px: 2', 'left:', 'LEFT: 3px', 'left: -3.5px',
  'left: 1e2px', 'left: 5%', 'left: 5rem', 'padding: 4px', '', ';',
  'left: 5px; left: 6px', ' left : 7px ; top : 8px ', 'left: NaNpx',
  'left: Infinitypx', 'left: 0x10px', 'left: 5pxpx', 'right: 5px',
  'left: 4px\nleft: 5px', 'top: 1px; left',
];

List<Map<String, Object?>> _placeCases() => [
  for (final css in _placeCss)
    for (final (left, top, round) in _drops)
      {
        'op': 'place',
        'css': css,
        'left': number(left),
        'top': number(top),
        'round': round,
        'expect': attempt(() {
          final moved = UiNode(type: 'box', css: css).placeAt(
            left,
            top,
            round: round,
          );
          final placed = moved.placed;
          return {
            'css': moved.css,
            'placed': placed == null
                ? null
                : [number(placed.left), number(placed.top)],
          };
        }),
      },
  for (final css in [..._placeCss, ..._placedCss])
    {
      'op': 'placed',
      'css': css,
      'expect': attempt(() {
        final node = UiNode(type: 'box', css: css);
        final placed = node.placed;
        return {
          'placed': placed == null
              ? null
              : [number(placed.left), number(placed.top)],
          'unplaced': node.unplaced.css,
        };
      }),
    },
];

void main() {
  test('writes the ui fixtures', () {
    final root = Directory('../..').absolute.path;

    writeFixture(
      root: root,
      package: 'orblit_ui',
      name: 'style',
      cases: [
        ..._colourCases(),
        ..._classCases(),
        ..._cssCases(),
        ..._breakpointCases(),
        ..._themeCases(),
      ],
    );

    writeFixture(
      root: root,
      package: 'orblit_ui',
      name: 'node',
      cases: [
        for (final text in _nodeTexts) _decode(text),
        _decode(_deep(10)),
        _decode(_deep(511)),
        _decode(_deep(512)),
        ..._editCases(),
        ..._walkCases(),
        for (final json in _propsCases) _props(json),
        ..._placeCases(),
      ],
    );
  });
}
