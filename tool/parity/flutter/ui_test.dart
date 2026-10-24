// Writes dotnet/fixtures/orblit_ui/style.json, node.json, canvas.json,
// document.json and strings.json: what the class list, CSS, theme and
// breakpoint readers give for a spread of inputs, what the node tree reads,
// edits and places, how a canvas lays out a grid, how a document reads and
// writes, and which plural form and text a locale table gives. It needs the
// Flutter package it reads, so tool/parity/flutter/regen.sh copies it into that
// package's test folder, runs it there and removes the copy.
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

Map<String, Object?> _spec({
  double width = 1920,
  double height = 1080,
  String fit = 'contain',
  double safeArea = 0.05,
  int columns = 12,
  double gutter = 24,
  double minScale = 0.8,
  double maxScale = 1.5,
}) => {
  'width': number(width),
  'height': number(height),
  'fit': fit,
  'safeArea': number(safeArea),
  'columns': columns,
  'gutter': number(gutter),
  'minScale': number(minScale),
  'maxScale': number(maxScale),
};

double _d(Object? value) =>
    value is String ? double.parse(value) : (value! as num).toDouble();

UiCanvas _canvasOf(Map<String, Object?> s) => UiCanvas(
  width: _d(s['width']),
  height: _d(s['height']),
  fit: CanvasFit.named(s['fit']),
  safeArea: _d(s['safeArea']),
  columns: s['columns']! as int,
  gutter: _d(s['gutter']),
  minScale: _d(s['minScale']),
  maxScale: _d(s['maxScale']),
);

Map<String, Object?> _canvasFields(UiCanvas c) => {
  'width': number(c.width),
  'height': number(c.height),
  'fit': c.fit.name,
  'label': c.fit.label,
  'safeArea': number(c.safeArea),
  'columns': c.columns,
  'gutter': number(c.gutter),
  'minScale': number(c.minScale),
  'maxScale': number(c.maxScale),
  'json': tree(c.toJson()),
};

final _specs = <Map<String, Object?>>[
  _spec(),
  _spec(width: 1000, columns: 4, gutter: 20),
  _spec(fit: 'responsive'),
  _spec(width: 1280, height: 720, fit: 'width'),
  _spec(width: 1280, height: 720, fit: 'height'),
  _spec(width: 1280, height: 720, fit: 'none'),
  _spec(width: 1280, height: 720, fit: 'responsive', columns: 6, gutter: 32),
  _spec(fit: 'responsive', minScale: 0.5, maxScale: 2),
  _spec(fit: 'responsive', minScale: 2, maxScale: 1),
  _spec(fit: 'responsive', minScale: double.nan),
  _spec(fit: 'responsive', maxScale: double.nan),
  _spec(fit: 'responsive', minScale: 1, maxScale: 1),
  _spec(width: 0, height: 0),
  _spec(width: 0, height: 1080, fit: 'responsive'),
  _spec(width: 1920, height: 0, fit: 'width'),
  _spec(width: -100, height: 50, fit: 'responsive'),
  _spec(width: double.nan, fit: 'responsive'),
  _spec(width: double.infinity, fit: 'responsive'),
  _spec(columns: 0),
  _spec(columns: -3),
  _spec(columns: 1, gutter: 0),
  _spec(columns: 5),
  _spec(columns: 7, gutter: 10),
  _spec(columns: 8),
  _spec(columns: 24, gutter: 8),
  _spec(gutter: 0),
  _spec(gutter: -5),
  _spec(gutter: 400),
  _spec(gutter: double.nan),
  _spec(gutter: double.infinity),
  _spec(safeArea: 0),
  _spec(safeArea: 0.45),
  _spec(safeArea: 0.5),
  _spec(safeArea: 1),
  _spec(safeArea: -0.1),
  _spec(safeArea: double.nan),
];

const _canvasWidths = [
  0.0, -1.0, 1.0, 39.0, 40.0, 41.0, 80.0, 100.0, 390.0, 480.0, 640.0, 900.0, //
  1000.0, 1280.0, 1920.0, 2880.0, 3840.0, 7680.0, 1e9, 1e308, double.nan,
  double.infinity, double.negativeInfinity, -0.0,
];

Map<String, Object?> _canvasOp(
  String op,
  Map<String, Object?> spec,
  Map<String, Object?> input,
  Object? Function(UiCanvas) run,
) => {
  'op': op,
  'canvas': spec,
  ...input,
  'expect': attempt(() => run(_canvasOf(spec))),
};

List<Object> _edges(({double left, double right}) span) => [
  number(span.left),
  number(span.right),
];

List<Map<String, Object?>> _canvasCases() => [
  for (final spec in _specs)
    for (final w in _canvasWidths) ...[
      _canvasOp(
        'canvas-fluid',
        spec,
        {'width': number(w)},
        (c) => number(c.fluidScale(w)),
      ),
      _canvasOp(
        'canvas-columns',
        spec,
        {'width': number(w)},
        (c) => {
          'span': _edges(c.gridSpanAcross(w)),
          'at': c.columnsAt(w),
          'across': [
            for (final column in c.columnsAcross(w))
              [number(column.left), number(column.right)],
          ],
        },
      ),
    ],
  for (final spec in _specs)
    for (final (w, h) in const <(double, double)>[
      (960, 1080), (1920, 540), (3840, 540), (100, 100), (0, 0), (-5, 10), //
      (double.nan, 1), (1, double.nan), (double.infinity, double.infinity),
      (1920, 1080), (390, 844), (1280, 0), (0, 720),
    ])
      _canvasOp(
        'canvas-scale',
        spec,
        {'width': number(w), 'height': number(h)},
        (c) => {'scale': number(c.scaleFor(w, h)), 'aspect': number(c.aspect)},
      ),
  for (final spec in [_specs[0], _specs[1], _specs[6], _specs[22], _specs[18]])
    for (final w in const [1000.0, 1920.0, 390.0, 0.0, double.nan])
      for (final x in const [
        0.0, 49.0, 50.0, 53.0, 150.0, 257.0, 260.0, 280.0, 950.0, 1000.0, //
        96.0, 100.0, -3.0, double.nan, double.infinity,
      ])
        for (final within in const [8.0, 0.0, 100.0, -1.0, double.nan])
          _canvasOp(
            'canvas-snap',
            spec,
            {'x': number(x), 'width': number(w), 'within': number(within)},
            (c) {
              final edge = c.snapAcross(x, w, within: within);
              return edge == null ? null : number(edge);
            },
          ),
];

const _fitNames = [
  'responsive', 'width', 'height', 'contain', 'none', 'sideways', //
  'Responsive', '', ' width', 'null', '3',
];

final _canvasJson = [
  '{}', '[]', '"x"', 'null', '3', '1.5', 'true', '{"width":1280}', //
  '{"width":1280,"height":720}', '{"width":0}', '{"width":-5}', '{"width":1e999}',
  '{"width":"x"}', '{"width":null}', '{"width":1.5}', '{"width":true}',
  '{"width":12345678901234567890}', '{"height":720.5}',
  '{"safeArea":9.0}', '{"safeArea":-1.0}', '{"safeArea":0}', '{"safeArea":0.45}',
  '{"safeArea":0.46}', '{"safeArea":-0.0}', '{"safeArea":1e999}',
  '{"safeArea":-1e999}', '{"safeArea":"x"}', '{"safeArea":0.3}',
  '{"columns":0}', '{"columns":1}', '{"columns":6}', '{"columns":12}',
  '{"columns":24}', '{"columns":25}', '{"columns":99}', '{"columns":-3}',
  '{"columns":2.5}', '{"columns":3.5}', '{"columns":-2.5}', '{"columns":0.5}',
  '{"columns":1e999}', '{"columns":-1e999}', '{"columns":1e300}',
  '{"columns":"x"}', '{"columns":null}', '{"columns":4.4999}',
  '{"gutter":0}', '{"gutter":400}', '{"gutter":401}', '{"gutter":-1}',
  '{"gutter":12.5}', '{"gutter":1e999}', '{"gutter":"x"}',
  '{"minScale":0.05}', '{"minScale":0.1}', '{"minScale":0.5}', '{"minScale":1}',
  '{"minScale":1.5}', '{"minScale":-1}', '{"maxScale":0.5}', '{"maxScale":1}',
  '{"maxScale":2}', '{"maxScale":8}', '{"maxScale":9}', '{"maxScale":1e999}',
  '{"fit":"responsive","columns":6,"gutter":32,"minScale":0.5,"maxScale":2}',
  '{"width":1280,"height":720}',
  for (final fit in _fitNames) '{"fit":"$fit"}',
  '{"fit":3}', '{"fit":null}', '{"fit":["width"]}',
];

Map<String, Object?> _canvasJsonCase(String text) => {
  'op': 'canvas-json',
  'json': text,
  'expect': attempt(() => _canvasFields(UiCanvas.fromJson(jsonDecode(text)))),
};

List<Map<String, Object?>> _canvasJsonCases() => [
  for (final text in _canvasJson) _canvasJsonCase(text),
  for (final fit in _fitNames)
    {
      'op': 'fit-named',
      'name': fit,
      'expect': attempt(() {
        final found = CanvasFit.named(fit);
        return {'name': found.name, 'label': found.label};
      }),
    },
];

const _rootTexts = [
  '{"type":"box"}',
  '{"type":"column","class":"p-4","children":[{"type":"text","text":"Title"}]}',
  '{"type":"stack","props":{"a":1,"b":1.5,"c":[1,2.0],"d":{"e":null}}}',
  '"just words"',
];

const _names = [
  'Interface', 'Main menu', '', ' ', 'a"b', 'tab\t', 'nl\n', '\u0000', //
  'é', '\u{1F600}', ' ', 'back\\slash', '</script>', '\u007f',
];

String _documentText(
  Map<String, Object?> spec,
  String rootText,
  String name,
) => UiDocument(
  name: name,
  canvas: _canvasOf(spec),
  root: UiNode.decode(rootText),
).toText();

const _documentTexts = [
  'nonsense', '', ' ', '{}', '[]', 'null', '"orblit.ui"', '3', //
  '{"kind":"orblit.data"}', '{"kind":"orblit.ui"}', '{"kind":"orblit.ui","name":3}',
  '{"kind":"orblit.ui","name":"Main menu"}', '{"kind":"orblit.ui","name":null}',
  '{"kind":"orblit.ui","canvas":[],"root":"hi"}',
  '{"kind":"orblit.ui","canvas":{"width":1280,"height":720,"fit":"width"}}',
  '{"kind":"orblit.ui","canvas":{"columns":1e999}}',
  '{"kind":"orblit.ui","canvas":{"width":1e999}}',
  '{"kind":"orblit.ui","canvas":{"safeArea":9}}',
  '{"kind":"orblit.ui","root":{"type":"row"}}',
  '{"kind":"orblit.ui","root":{"type":"row","children":["a",1,null]}}',
  '{"kind":"orblit.ui","root":3}', '{"kind":"orblit.ui","root":null}',
  '{"kind":"orblit.ui","formatVersion":99,"extra":true}',
  '{"kind":"orblit.ui","name":"\\ud83d"}', '{"kind":"orblit.ui","name":"\\u00e9\\ud83d\\ude00"}',
  '﻿{"kind":"orblit.ui"}', ' {"kind":"orblit.ui"} ', '{"kind":"orblit.ui"} x',
  '{"kind":"orblit.ui"', '{"kind":"orblit.ui",}', '{"kind":"Orblit.UI"}',
  '{"kind":["orblit.ui"]}', '{"kind":"orblit.ui","kind":"x"}',
];

Object? _read(UiDocument? document) => document == null
    ? null
    : _units({
        'name': document.name,
        'canvas': _canvasFields(document.canvas),
        'root': tree(document.root.toJson()),
      });

List<Map<String, Object?>> _documentCases() => [
  for (final text in _documentTexts)
    {
      'op': 'read',
      'text': text,
      'expect': attempt(() => _read(UiDocument.read(text))),
    },
  for (final text in _documentTexts)
    {
      'op': 'read-text',
      'text': text,
      'expect': attempt(() => UiDocument.read(text)?.toText()),
    },
  for (final name in _names)
    {
      'op': 'blank',
      'name': name,
      'expect': attempt(() {
        final made = UiDocument.blank(name);
        return {
          'name': made.name,
          'canvas': _canvasFields(made.canvas),
          'root': tree(made.root.toJson()),
          'text': made.toText(),
        };
      }),
    },
  for (final spec in _specs)
    for (final root in _rootTexts)
      {
        'op': 'write',
        'canvas': spec,
        'root': root,
        'name': 'Main menu',
        'expect': attempt(() => _documentText(spec, root, 'Main menu')),
      },
  for (final name in _names)
    {
      'op': 'write',
      'canvas': _specs[0],
      'root': _rootTexts[1],
      'name': name,
      'expect': attempt(() => _documentText(_specs[0], _rootTexts[1], name)),
    },
];

const _pluralLocales = [
  'en', 'fr', 'ja', 'zh', 'ko', 'vi', 'th', 'id', 'ru', 'uk', 'pl', 'cs', //
  'sk', 'ar', 'pt', 'pt-PT', 'pt-BR', 'pt_pt', 'PT-pt', 'zh-Hant-TW', 'hi',
  'de', 'xx', ' en ', 'RU', 'ru-RU', 'ar-EG', 'pl-PL', 'fr-CA', '', ' ',
  '-', 'en--us',
];

const _pluralCounts = [
  0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 19, 20, 21, 22, 24, //
  25, 26, 99, 100, 101, 102, 103, 104, 105, 110, 111, 112, 113, 114, 115,
  121, 122, 1000, 1001, 1002, 1011, 1012, 100000, -1, -2, -5, -11, -12, -21,
  -22, -100, -111, 9223372036854775807, -9223372036854775807,
  -9223372036854775808, 9223372036854775806,
];

const _tags = [
  'en', 'EN', 'fr_CA', 'fr-CA', ' fr-ca ', 'zh_Hant_TW', 'a__b', 'a--b', //
  'İ', 'ǅ', 'SS', 'ß', 'x ', ' x', '\u0085x', 'x﻿',
  '\t', '\n', '', ' ', '_', '-', 'en_', '_en', 'K', 'Σ',
];

const _chains = [
  ('zh-Hant-TW', 'en-GB'), ('en', 'en'), ('fr_CA', 'en'), ('FR-ca', 'FR-CA'), //
  ('de', 'zh-hant'), ('a-b-c-d', 'x-y'), ('en', ' '), (' ', 'en'), ('', ''),
  ('x', 'x-y'), ('x-y', 'x'),
];

List<Map<String, Object?>> _pluralCases() => [
  for (final locale in _pluralLocales)
    for (final count in _pluralCounts)
      {
        'op': 'plural',
        'locale': locale,
        'count': big(count),
        'expect': attempt(() => UiStrings.pluralForm(locale, count)),
      },
  for (final tag in _tags)
    {'op': 'tag', 'tag': tag, 'expect': attempt(() => UiStrings.tagOf(tag))},
  for (final (locale, fallback) in _chains)
    {
      'op': 'chain',
      'locale': locale,
      'fallback': fallback,
      'expect': attempt(
        () => UiStrings(fallback: fallback).chain(locale),
      ),
    },
];

const _menu = <String, Map<String, String>>{
  'en': {
    'play': 'Play',
    'greet': 'Hello, {name}',
    'items.one': '{count} item',
    'items.other': '{count} items',
    'brace': '{} { } {a b} {a-b} {näme} {name}{name} {{name}} {name',
    'both': '{name} has {count} {name}',
    'only.other': 'only other',
    'zero.zero': 'never',
  },
  'fr': {'play': 'Jouer', 'greet': 'Bonjour, {name}', 'items.one': '{count} chose'},
  'fr-ca': {'play': 'Joue'},
  'ru': {
    'items.one': '{count} predmet',
    'items.few': '{count} predmeta',
    'items.many': '{count} predmetov',
    'items.other': '{count} predmeta?',
  },
  'ar': {
    'items.zero': 'zero {count}',
    'items.one': 'one {count}',
    'items.two': 'two {count}',
    'items.few': 'few {count}',
    'items.many': 'many {count}',
    'items.other': 'other {count}',
  },
  'pl': {'items.one': '1 rzecz', 'items.few': '{count} rzeczy'},
  'pt-pt': {'items.one': 'um', 'items.other': 'varios'},
  'pt': {'items.one': 'um (pt)', 'items.other': 'varios (pt)'},
};

const _keys = [
  'play', 'greet', 'items', 'items.other', 'items.one', 'quit', '', 'brace', //
  'both', 'only', 'only.other', 'zero', 'zero.zero', 'Play',
];

const _argTexts = [
  '{}', '{"name":"Ann"}', '{"name":null}', '{"name":5}', '{"name":1.0}', //
  '{"name":true}', '{"name":[1,"a",null,2.5]}', '{"name":{"a":1,"b":[]}}',
  '{"name":1e21}', '{"name":1e-7}', '{"name":-0.0}', '{"name":""}',
  '{"name":"\\u00e9"}', '{"count":0}', '{"count":1}', '{"count":2}',
  '{"count":3}', '{"count":5}', '{"count":11}', '{"count":21}',
  '{"count":22}', '{"count":100}', '{"count":-1}', '{"count":-21}',
  '{"count":1.0}', '{"count":3.0}', '{"count":1.5}', '{"count":-0.0}',
  '{"count":1e30}', '{"count":-1e30}', '{"count":1e999}',
  '{"count":12345678901234567890}', '{"count":"1"}', '{"count":null}',
  '{"count":true}', '{"count":[1]}', '{"count":9223372036854775807}',
  '{"count":-9223372036854775808}', '{"count":1,"name":"Ann"}',
  '{"count":2.0,"name":"Ann"}', '{"count":0.5,"name":"Ann"}',
];

Map<String, Object?> _script(
  List<Object?> steps, {
  String locale = 'en',
  String fallback = 'en',
}) {
  var heard = 0;
  final results = <Object?>[];
  UiStrings? made;
  final init = attempt(() {
    made = UiStrings(locale: locale, fallback: fallback)
      ..addListener(() => heard++);
    return null;
  });
  if (made != null) {
    for (final step in steps) {
      results.add(attempt(() => _run(made!, step! as List<Object?>)));
    }
  }
  return {
    'op': 'script',
    'locale': locale,
    'fallback': fallback,
    'steps': steps,
    'expect': {
      'init': init,
      'results': results,
      'heard': heard,
      'locale': made?.locale,
      'locales': made?.locales,
    },
  };
}

Map<String, String> _table(Object? text) =>
    (jsonDecode(text! as String) as Map).cast<String, String>();

Object? _at(List<Object?> step, int index) =>
    index < step.length ? step[index] : null;

Map<String, Object?> _args(Object? text) =>
    text == null ? const {} : (jsonDecode(text as String) as Map).cast();

Object? _run(UiStrings s, List<Object?> step) {
  final verb = step[0];
  return switch (verb) {
    'add' => () {
      s.add(step[1]! as String, _table(step[2]));
      return null;
    }(),
    'addJson' => () {
      s.addJson(step[1]! as String, step[2]! as String);
      return null;
    }(),
    'use' => () {
      s.use(step[1]! as String);
      return null;
    }(),
    'clear' => () {
      s.clear();
      return null;
    }(),
    'text' => s.text(step[1]! as String, _args(_at(step, 2))),
    'find' => s.find(step[1]! as String, _args(_at(step, 2))),
    'chain' => s.chain(step[1]! as String),
    'gaps' => s.gaps([for (final k in step[1]! as List) '$k']),
    'locale' => s.locale,
    'locales' => s.locales,
    _ => throw StateError('unknown step $verb'),
  };
}

List<Object?> _loadMenu() => [
  for (final e in _menu.entries) ['add', e.key, jsonEncode(e.value)],
];

List<Map<String, Object?>> _scriptCases() => [
  for (final locale in const [
    'en', 'fr', 'fr_CA', 'fr-ca', 'ru', 'ar', 'pl', 'pt', 'pt-PT', 'de', //
    'zh-Hant-TW', 'FR',
  ])
    _script([
      ..._loadMenu(),
      ['use', locale],
      ['locale'],
      for (final key in _keys) ...[
        ['text', key],
        ['find', key],
        for (final args in _argTexts) ['text', key, args],
      ],
    ]),
  _script([
    ..._loadMenu(),
    ['gaps', <Object?>[]],
    ['gaps', <Object?>['quit']],
    ['gaps', <Object?>['quit', 'items.one', 'items.other', 'play.zero']],
    ['add', 'de', '{"items.other":"Dinge"}'],
    ['gaps', <Object?>['quit']],
    ['locales'],
    ['clear'],
    ['locales'],
    ['gaps', <Object?>[]],
    ['gaps', <Object?>['a', 'b.one']],
  ]),
  _script([
    ['gaps', <Object?>[]],
  ]),
  _script([
    ['add', 'en', '{"settings.other":"Other"}'],
    ['gaps', <Object?>['settings.other']],
    ['gaps', <Object?>['settings']],
    ['gaps', <Object?>['settings.few', 'x.zero']],
  ]),
  _script(
    [
      ['add', 'zh-hant', '{"play":"玩"}'],
      ['add', 'en', '{"quit":"Quit"}'],
      ['chain', 'zh-hant-tw'],
      ['text', 'play'],
      ['text', 'quit'],
      ['gaps', <Object?>[]],
    ],
    locale: 'zh-Hant-TW',
    fallback: 'en-GB',
  ),
  _script([
    ['addJson', 'de', '{"play": "Spielen"}'],
    ['use', 'de'],
    ['text', 'play'],
    for (final bad in const [
      '[]', '{"play": 3}', '{"play"', '', 'null', '"x"', '{"a":null}',
      '{"a":{"b":"c"}}', '{"a":["x"]}', '{}', '{"a":"b","a":"c"}', //
      '{"a":1.5}', '{"a":true}',
    ])
      ['addJson', 'de', bad],
    ['addJson', '', '{"a":"b"}'],
    ['addJson', ' ', '[]'],
    ['addJson', '', '{"a":'],
    ['locales'],
    ['text', 'a'],
  ]),
  _script([
    ['use', 'en'],
    ['use', 'EN'],
    ['use', 'fr'],
    ['use', 'fr_FR'],
    ['use', 'fr-fr'],
    ['use', ''],
    ['use', ' '],
    ['locale'],
    ['add', '', '{}'],
    ['add', 'x', '{}'],
    ['add', 'x', '{}'],
    ['clear'],
    ['clear'],
  ]),
  _script([], locale: ' '),
  _script([], fallback: ''),
  _script([['locale']], locale: 'EN_gb', fallback: 'FR_ca'),
  _script([
    ['add', 'en', '{"a":"1"}'],
    ['add', 'en', '{"a":"2","b":"3"}'],
    ['text', 'a'],
    ['text', 'b'],
    ['add', 'EN', '{"a":"4"}'],
    ['text', 'a'],
    ['locales'],
  ]),
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

    writeFixture(
      root: root,
      package: 'orblit_ui',
      name: 'canvas',
      cases: [..._canvasCases(), ..._canvasJsonCases()],
    );

    writeFixture(
      root: root,
      package: 'orblit_ui',
      name: 'document',
      cases: _documentCases(),
    );

    writeFixture(
      root: root,
      package: 'orblit_ui',
      name: 'strings',
      cases: [..._pluralCases(), ..._scriptCases()],
    );
  });
}
