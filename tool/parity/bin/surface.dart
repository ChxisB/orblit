import 'package:orblit_asset/src/surface/surface_diagnostics.dart';
import 'package:orblit_asset/src/surface/surface_error.dart';
import 'package:orblit_asset/src/surface/surface_lines.dart';
import 'package:orblit_asset/src/surface/surface_param.dart';
import 'package:orblit_asset/src/surface/surface_translator.dart';
import 'package:orblit_parity_tool/fixture.dart';

const _path = 'materials/pulse.oshader';

const _pulse = '''
shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded;

group_uniforms look;
uniform vec4 tint : source_color = vec4(1.0, 0.5, 0.2, 1.0);
uniform float speed : hint_range(0.0, 10.0, 0.1) = 2.0;
instance uniform float glow = 1.0;
uniform sampler2D albedo_map : source_color, filter_nearest;

void fragment() {
  float pulse = 0.5 + 0.5 * sin(TIME * speed);
  ALBEDO = tint.rgb * texture(albedo_map, UV).rgb;
  EMISSION = tint.rgb * pulse * glow;
  ALPHA = tint.a;
}
''';

// Characters a pattern and a trim disagree about are written by code, so the
// fixture reads the same in any editor.
final _nbsp = String.fromCharCode(0xa0);
final _nextLine = String.fromCharCode(0x85);
final _lineSeparator = String.fromCharCode(0x2028);
final _bom = String.fromCharCode(0xfeff);
final _e = String.fromCharCode(0xe9);
final _arabicDigit = String.fromCharCode(0x663);
const _emoji = '😀';

const _modes = [
  'blend_mix',
  'blend_add',
  'blend_mul',
  'blend_premul_alpha',
  'cull_back',
  'cull_front',
  'cull_disabled',
  'depth_draw_opaque',
  'depth_draw_always',
  'depth_draw_never',
  'depth_test_default',
  'depth_test_disabled',
  'unshaded',
];

final _modeLines = <String>[
  for (final mode in _modes) 'render_mode $mode;\nvoid fragment() {}',
  for (final a in _modes)
    for (final b in _modes) 'render_mode $a, $b;\nvoid fragment() {}',
  'render_mode blend_add,cull_front,depth_draw_never;\nvoid fragment() {}',
  'render_mode  blend_add ,  unshaded ;\nvoid fragment() {}',
  'render_mode\tunshaded;\nvoid fragment() {}',
  'render_mode blend_mix;\nrender_mode blend_add;\nvoid fragment() {}',
  'render_mode unshaded; // trailing\nvoid fragment() {}',
  'render_mode wireframe;\nvoid fragment() {}',
  'render_mode blend_mix, wireframe;\nvoid fragment() {}',
  'render_mode ;\nvoid fragment() {}',
  'render_mode blend_mix,;\nvoid fragment() {}',
  'render_mode;\nvoid fragment() {}',
  'render_mode Unshaded;\nvoid fragment() {}',
  'render_mode unshaded\nvoid fragment() {}',
  'void fragment() {}\nrender_mode wireframe;',
  'render_mode unshaded; void fragment() {}',
];

final _hintTexts = <String>[
  'source_color',
  'hint_range(0.0, 10.0, 0.1)',
  'hint_range(0, 1)',
  'hint_enum(a, b, c)',
  'hint_normal',
  'hint_default_white',
  'hint_default_black',
  'hint_default_transparent',
  'filter_nearest',
  'filter_linear',
  'filter_nearest_mipmap',
  'filter_linear_mipmap',
  'repeat_enable',
  'repeat_disable',
  'source_color, filter_nearest',
  'source_color, source_color',
  'hint_range(0, 1), hint_range(2, 3)',
  'hint_range(0, (1, 2)), source_color',
  'hint_range (0, 1)',
  'hint_range()',
  'hint_range( )',
  'hint_range(0,1)',
  '  source_color  ',
  'hint_range(',
  'hint_range)',
  'hint_range(0, 1)x',
  'hint_range(0, 1',
  'hint_wibble',
  'Source_Color',
  'source color',
  ', source_color',
  'source_color,',
  '',
  ',',
  'hint_range(a,\r\nb)',
  'source_color\r',
  'hint_range(0, 1$_lineSeparator)',
  'hint_range$_nbsp(0, 1)',
  'source_color$_e',
  '${_e}source_color',
];

final _defaultTexts = <String>[
  '1.0',
  '1',
  '0',
  '-3',
  '+2',
  '- 3',
  '2.5',
  '.5',
  '5.',
  '1e3',
  '1E-2',
  '1.5e+2f',
  '1.0f',
  '1.5F',
  '1ff',
  'f',
  '0x10',
  '0X1f',
  '-0x10',
  '0xffffffffffffffff',
  '0x10000000000000000',
  '9223372036854775807',
  '-9223372036854775808',
  '9223372036854775808',
  '99999999999999999999',
  '1e400',
  '-1e400',
  'Infinity',
  '-Infinity',
  '+Infinity',
  'NaN',
  '-NaN',
  'nan',
  'infinity',
  '1_0',
  '1,5',
  '1 2',
  ' 1.0 ',
  '$_nbsp 1.0 $_nbsp',
  '$_nextLine 1.0',
  '$_bom 1.0',
  _arabicDigit,
  '',
  ' ',
  'true',
  'false',
  'True',
  'TRUE',
  'yes',
  '1.0;',
  'vec2(1, 2)',
  'vec2(1.0,2.0)',
  'vec2( 1 , 2 )',
  'vec2(1)',
  'vec2(1, 2, 3)',
  'vec2()',
  'vec2(1.0',
  'vec2 (1.0, 2.0)',
  'vec2(1.0, 2.0) ',
  'vec2(1.0, 2.0)x',
  'vec2(vec2(1, 2))',
  'vec2(1.0,,2.0)',
  'vec3(0.5)',
  'vec3(1, 2)',
  'vec3(1, 2, 3)',
  'vec3(1.0, 0, 0.5f)',
  'vec3(1.0f)',
  'vec3(Infinity, NaN, -Infinity)',
  'vec3(0x10, 1e1, 5)',
  'vec3(1, 2, 3, 4)',
  'vec3 = 1.0',
  'vec4(1.0, 0.5, 0.2, 1.0)',
  'vec4(1, 2, 3)',
  'vec4(1)',
  'vec4(1, (2), 3, 4)',
  'sampler2D(1)',
  'sampler2D(1, 2)',
  'sampler2D()',
  'sampler2D',
  'float(1.0)',
  'int(1)',
  'bool(true)',
  'vec2(1.0, 2.0)\r',
];

// A uniform line for each default, each type, so the translator's own reading
// of the declaration is checked beside the direct calls.
final _uniformLines = <String>[
  for (final type in SurfaceType.values)
    for (final text in _defaultTexts.take(40))
      'uniform ${type.word} u = $text;\nvoid fragment() {}',
  for (final text in _defaultTexts.skip(40))
    for (final word in ['float', 'vec2', 'vec3', 'vec4', 'bool', 'int'])
      'uniform $word u = $text;\nvoid fragment() {}',
  for (final text in _hintTexts)
    'uniform float u : $text;\nvoid fragment() {}',
  for (final text in _hintTexts)
    'uniform vec3 u : $text = vec3(1.0);\nvoid fragment() {}',
  'uniform float n;\nvoid fragment() {}',
  'uniform vec3 emissive;\nvoid fragment() { EMISSION = emissive; }',
  'uniform vec3 c = vec3(0.5);\nvoid fragment() {}',
  'uniform mat4 m;\nvoid fragment() {}',
  'uniform float;\nvoid fragment() {}',
  'uniform float u\nvoid fragment() {}',
  'uniform float u',
  'uniform float u : source_color',
  'uniform float u = 1.0',
  'uniform float u = 1.0; void fragment() {}',
  'uniform float a; uniform float b;\nvoid fragment() {}',
  'uniform float a = 1.0; // trailing\nvoid fragment() {}',
  'uniform float a = 1.0 /* c */;\nvoid fragment() {}',
  'uniform float a = /* c */ 1.0;\nvoid fragment() {}',
  'uniform float /* c */ a;\nvoid fragment() {}',
  'uniform   float\ta  =  2.0  ;  \nvoid fragment() {}',
  'uniform float a = 1.0;\r\nuniform float b = 2.0;\r\nvoid fragment() {}\r\n',
  'uniform vec3 color\n    : source_color\n    = vec3(1.0, 0.0, 0.0);\nvoid fragment() {}',
  'uniform vec3 color\n    : source_color\n\n\n    = vec3(1.0);\nvoid fragment() {}',
  'uniform float a,\n b;\nvoid fragment() {}',
  'instance uniform float g = 1.0;\nvoid fragment() {}',
  'instance  uniform float g;\nvoid fragment() {}',
  'instance uniform sampler2D t;\nvoid fragment() {}',
  'instance uniform vec4 t : source_color = vec4(1);\nvoid fragment() {}',
  'instanceuniform float g;\nvoid fragment() {}',
  'instance float g;\nvoid fragment() {}',
  'const uniform float g;\nvoid fragment() {}',
  'uniforms float x;\nvoid fragment() {}',
  'uniform_x float x;\nvoid fragment() {}',
  'uniform$_e float x;\nvoid fragment() {}',
  'uniform${_nbsp}float x;\nvoid fragment() {}',
  'uniform${_nextLine}float x;\nvoid fragment() {}',
  'uniform$_bom float x;\nvoid fragment() {}',
  '$_bom${_nbsp}uniform float x;\nvoid fragment() {}',
  '${_e}uniform float x;\nvoid fragment() {}',
  'uniform float x = 1.0$_lineSeparator;\nvoid fragment() {}',
  'uniform float x$_lineSeparator= 1.0;\nvoid fragment() {}',
  'uniform float x = 1.0;$_lineSeparator\nvoid fragment() {}',
  'uniform float x = 1.0;$_nbsp\nvoid fragment() {}',
  'uniform float x = 1.0;$_nextLine\nvoid fragment() {}',
  'uniform float ${_e}x = 1.0;\nvoid fragment() {}',
  'uniform float x$_emoji;\nvoid fragment() {}',
  'uniform float u_1 = 1.0;\nvoid fragment() {}',
  'uniform float 1u = 1.0;\nvoid fragment() {}',
  'uniform float x;\nuniform float x;\nvoid fragment() {}',
  'void fragment() {\n  uniform float x;\n}',
  'struct S {\n  uniform float x;\n};\nvoid fragment() {}',
  '}\nuniform float x;\nvoid fragment() {}',
  'void fragment() {}\nuniform float x;',
  'void fragment() { }\nuniform float x;',
  'void fragment() {\n}\nuniform float x;',
  'void fragment() {{}}\nuniform float x;',
  'void fragment() {\n  {\n  }\n}\nuniform float x;',
  'group_uniforms look;\nuniform float a;\ngroup_uniforms;\nuniform float b;\nvoid fragment() {}',
  'group_uniforms a.b;\nuniform float a;\nvoid fragment() {}',
  'group_uniforms a.b.c ;\nuniform float a;\nvoid fragment() {}',
  'group_uniforms a b;\nuniform float a;\nvoid fragment() {}',
  'group_uniforms look\nuniform float a;\nvoid fragment() {}',
  'group_uniforms;\nuniform float a;\nvoid fragment() {}',
  'group_uniforms $_e;\nuniform float a;\nvoid fragment() {}',
  'group_uniforms look; group_uniforms other;\nuniform float a;\nvoid fragment() {}',
];

final _directiveLines = <String>[
  'shader_type spatial;\nvoid fragment() {}',
  'shader_type  spatial ;\nvoid fragment() {}',
  'shader_type spatial\nvoid fragment() {}',
  'shader_type;\nvoid fragment() {}',
  'shader_type canvas_item;\nvoid fragment() {}',
  'shader_type particles;\nvoid fragment() {}',
  'shader_type sky;\nvoid fragment() {}',
  'void fragment() {}\nvoid vertex() {}',
  'void fragment() {}\nvoid light() {}',
  'void fragment() {}\n  void  vertex ( ) {}',
  'void vertex() {}\nvoid fragment() {}',
  'varying vec3 pos;\nvoid fragment() {}',
  '  varying float x;\nvoid fragment() {}',
  'varying\nvoid fragment() {}',
  'varyings vec3 pos;\nvoid fragment() {}',
  'varying$_e vec3 pos;\nvoid fragment() {}',
  '${_e}varying vec3 pos;\nvoid fragment() {}',
  'varying$_nbsp vec3 pos;\nvoid fragment() {}',
  'void vertex2() {}\nvoid fragment() {}',
  'avoid vertex() {}\nvoid fragment() {}',
  'float varying;\nvoid fragment() {}',
  'void fragment() {\n  varying vec3 pos;\n}',
  'void fragment() {\n  void vertex() {}\n}',
];

final _fragmentLines = <String>[
  'void fragment() {}',
  'void fragment(){}',
  'void  fragment ( ) { }',
  'void\tfragment()\t{}',
  'void fragment() {}\r\n',
  'void fragment(float x) {}',
  'void fragment( float x ) {}',
  'void fragment',
  'void\nfragment() {}',
  'void fragment\n() {}',
  'avoid fragment() {}',
  'void fragmentx() {}',
  'void fragment_() {}',
  'void fragment() {} void fragment() {}',
  'void fragment() {}\nvoid fragment() {}',
  '// void fragment() {}',
  '/* void fragment() {} */',
  '/* void fragment() {} */\nvoid fragment() {}',
  '${_e}void fragment() {}',
  'void${_e}fragment() {}',
  'void fragment()$_e{}',
  'void${_nbsp}fragment() {}',
  'void${_nextLine}fragment() {}',
  'void$_bom fragment() {}',
  'void fragment$_bom() {}',
  'void fragment($_nbsp) {}',
  'float f() { return 1.0; }',
  'uniform float x;',
  '',
  '\n',
  '\n\n\n',
  '   ',
  'void fragment() { NORMAL_MAP = vec3(0.0, 0.0, 1.0); }',
  'render_mode unshaded;\nvoid fragment() { NORMAL_MAP = vec3(0.0, 0.0, 1.0); }',
  'void fragment() {\n  NORMAL_MAP = vec3(0.0, 0.0, 1.0);\n  ALBEDO = vec3(1.0);\n}\n',
  'void fragment() {\n  NORMAL_MAP = vec3(0.0, 0.0, 1.0);\n}\n',
  'void fragment() { // NORMAL_MAP\n}',
  'void fragment() { /* NORMAL_MAP */ }',
  'void fragment() { normal_map = 1; }',
  'void fragment() {\n  ALBEDO = vec3(1.0);\n  ROUGHNESS = 0.2;\n}\n',
  'float helper(float x) {\n  return x * 2.0;\n}\n\nvoid fragment() {\n  ROUGHNESS = helper(0.1);\n}\n',
  'void fragment() {\n  ALBEDO = vec3(1.0);\n}\n\n\n\n',
  'int a = 1; // one\nvoid fragment() { /* two */ }',
  'a /* x */ b\nvoid fragment() {}',
  'a /* x\ny */ b\nvoid fragment() {}',
  'a /* x\n\n\ny */ b\nvoid fragment() {}\n/* never closed\n',
  '/*',
  '/*/\nvoid fragment() {}',
  '/**/ void fragment() {}',
  '/* // */ void fragment() {}',
  '// /* \nvoid fragment() {}',
  'void fragment() {} // end',
  'void fragment() {} /* end */',
  '*/ void fragment() {}',
  'a //',
  'a /',
  'a / b\nvoid fragment() {}',
  '${_bom}void fragment() {}',
  'void fragment() {}$_lineSeparator',
  'a$_lineSeparator b\nvoid fragment() {}',
  'a // c$_lineSeparator b\nvoid fragment() {}',
  'void fragment() {\r\n  ALBEDO = vec3(1.0);\r\n}\r\n',
  '// comment\r\nvoid fragment() {}',
  '/* a\r\nb */ void fragment() {}',
];

final _includeFiles = <String, String>{
  'materials/lib/math.oshaderinc': 'float twice(float x) {\n  return x * 2.0;\n}\n',
  'materials/a.oshaderinc': '#include "b.oshaderinc"',
  'materials/b.oshaderinc': '#include "a.oshaderinc"',
  'materials/self.oshaderinc': '#include "self.oshaderinc"',
  'materials/up.oshaderinc': '#include "../outside.oshaderinc"',
  'outside.oshaderinc': 'float outside = 1.0;\n',
  'materials/top.oshaderinc': 'float top = 1.0;',
  'lib/shared.oshaderinc': 'uniform float shared = 0.5;\n',
  'materials/uniforms.oshaderinc':
      'group_uniforms inc;\nuniform float fromInclude : hint_range(0, 1) = 0.25;\nrender_mode cull_disabled;\n',
  'materials/frag.oshaderinc': 'void fragment() { ALBEDO = vec3(1.0); }\n',
  'materials/crlf.oshaderinc': 'float crlf = 1.0;\r\nfloat two = 2.0;\r\n',
  'materials/commented.oshaderinc': '/* open\nstill */ float c = 1.0;\n// tail',
  'materials/empty.oshaderinc': '',
  'materials/blank.oshaderinc': '\n\n',
  'materials/nest/inner.oshaderinc': '#include "../lib/math.oshaderinc"\nfloat inner = 1.0;\n',
  'materials/diamond_left.oshaderinc': '#include "top.oshaderinc"\n',
  'materials/diamond_right.oshaderinc': '#include "top.oshaderinc"\n',
  'materials/bad.oshaderinc': 'varying vec3 pos;\n',
  'materials/badtype.oshaderinc': '#include "x.glsl"\n',
  'materials/a b.oshaderinc': 'float spaced = 1.0;\n',
  'materials/$_e.oshaderinc': 'float accent = 1.0;\n',
};

Map<String, String> _chain(int count) => {
  for (var i = 0; i < count; i++)
    'materials/c$i.oshaderinc': i + 1 < count
        ? '#include "c${i + 1}.oshaderinc"\nfloat v$i = $i.0;\n'
        : 'float last = 1.0;\n',
};

final _includeSources = <(String, Map<String, String>)>[
  (
    '#include "lib/math.oshaderinc"\nvoid fragment() { ROUGHNESS = twice(0.1); }',
    _includeFiles,
  ),
  ('#include "lib/math.oshaderinc"', _includeFiles),
  ('#include "a.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "self.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "up.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "../x.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "../../x.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "/outside.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "/lib/shared.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "/../x.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "./top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include ".//top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "lib/../top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "lib//math.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "nest/inner.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc"\n#include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "diamond_left.oshaderinc"\n#include "diamond_right.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "uniforms.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "frag.oshaderinc"', _includeFiles),
  ('#include "crlf.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "commented.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "empty.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "blank.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "bad.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "badtype.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "a b.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "$_e.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "missing.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "a.glsl"\nvoid fragment() {}', _includeFiles),
  ('#include "a.OSHADERINC"\nvoid fragment() {}', _includeFiles),
  ('#include ""\nvoid fragment() {}', _includeFiles),
  ('#include ".oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "/"\nvoid fragment() {}', _includeFiles),
  ('#include "/.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('# include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#\tinclude\t"top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('   #include "top.oshaderinc"   \nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc" // why\nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc" /* why */\nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc" extra\nvoid fragment() {}', _includeFiles),
  ('#include <top.oshaderinc>\nvoid fragment() {}', _includeFiles),
  ('#include top.oshaderinc\nvoid fragment() {}', _includeFiles),
  ('#include"top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#includes "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('x #include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('// #include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('/* #include "top.oshaderinc" */\nvoid fragment() {}', _includeFiles),
  ('/*\n#include "missing.oshaderinc"\n*/\nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc"\r\nvoid fragment() {}\r\n', _includeFiles),
  ('$_bom#include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('$_nbsp#include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('$_nextLine#include "top.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc"$_lineSeparator\nvoid fragment() {}', _includeFiles),
  ('#include "to${_lineSeparator}p.oshaderinc"\nvoid fragment() {}', _includeFiles),
  ('#include "top.oshaderinc"$_nbsp\nvoid fragment() {}', _includeFiles),
  ('#include "uniforms.oshaderinc"\nuniform float own = 1.0;\nvoid fragment() {}', _includeFiles),
  ('uniform float before = 1.0;\n#include "uniforms.oshaderinc"\nuniform float after = 1.0;\nvoid fragment() {}', _includeFiles),
  ('#include "c0.oshaderinc"\nvoid fragment() {}', _chain(2)),
  ('#include "c0.oshaderinc"\nvoid fragment() {}', _chain(24)),
  ('#include "c0.oshaderinc"\nvoid fragment() {}', _chain(25)),
  ('#include "c0.oshaderinc"\nvoid fragment() {}', _chain(26)),
  ('#include "c0.oshaderinc"\nvoid fragment() {}', _chain(30)),
];

final _names = <(String, String)>[
  ('pulse', _path),
  ('a-b c', 'materials/a-b c.oshader'),
  ('', 'materials/.oshader'),
  ('${_e}clair', 'materials/${_e}clair.oshader'),
  ('smile$_emoji', 'materials/smile$_emoji.oshader'),
  ('under_score9', 'materials/under_score9.oshader'),
  ('dot.name', 'materials/dot.name.oshader'),
  ('quote"name', 'materials/quote"name.oshader'),
  ('tab\tname', 'materials/tab\tname.oshader'),
  ('line\nname', 'materials/line\nname.oshader'),
  ('x', 'x.oshader'),
  ('deep', 'a/b/c/d/deep.oshader'),
];

final _matcOutputs = <String>[
  "ERROR: 0:12: 'foo' : undeclared identifier\nERROR: 0:12: 'foo' : undeclared identifier\n",
  "ERROR: 0:3: 'a' : bad\nERROR: 0:4: 'b' : worse\nERROR: 0:3: 'a' : bad\n",
  "ERROR: 0:3: 'x' : bad\nERROR: 0:4: '' : compilation terminated\n",
  "ERROR: 0:4: '' : compilation terminated\n",
  "ERROR: 0:4: '' : compilation terminated at the end\nERROR: 0:5: ok\n",
  "ERROR: 0:4: x '' : compilation terminated\n",
  'ERROR: 0:3: a\r\nERROR: 0:4: b\r\n',
  'ERROR: 0:3: a\rERROR: 0:4: b\r',
  'ERROR: 0:3: a${_lineSeparator}ERROR: 0:4: b',
  'ERROR: 0:3: a${_nextLine}ERROR: 0:4: b',
  'ERROR: 0:3: a\nERROR: 0:4: b',
  'warning: nothing here\n',
  '',
  '\n',
  'ERROR: 0:1: first line\nERROR: 0:2: second',
  'ERROR: 0:0: before the start\n',
  'ERROR: 0:1000: after the end\n',
  'ERROR: 12:5: \n',
  'ERROR: 12:5:\n',
  'ERROR: 12:5:  two spaces\n',
  'ERROR: 0:3:a\n',
  '  ERROR: 0:3: indented\n',
  'x ERROR: 0:3: not at the start\n',
  'ERROR:  0:3: extra space\n',
  'ERROR: 0:3: ERROR: 0:4: nested\n',
  'ERROR: 0:03: leading zero\n',
  'ERROR: a:3: letters\n',
  'ERROR: 0:3x: after digits\n',
  'ERROR: 1:99999999999999999999: huge\n',
  'ERROR: 1:9223372036854775807: biggest\n',
  'ERROR: 1:9223372036854775808: past biggest\n',
  'ERROR: $_arabicDigit:$_arabicDigit: arabic digits\n',
  'ERROR: 0:3: $_e\nERROR: 0:4: $_emoji\n',
  'WARNING: 0:3: warn\nERROR: 0:4: err\n',
  'error: 0:3: lower case\n',
  "ERROR: 0:3: 'x' : a\nsome other line\nERROR: 0:5: 'y' : b\n",
  'ERROR: 0:3: a\n\n\nERROR: 0:4: b\n',
  'ERROR: 0:3: a b c  \n',
  'ERROR: 0:3: a\tb\n',
  'ERROR: 0:6: in the body\nERROR: 0:7: past\n',
];

const _matcSources = <(String, Map<String, String>)>[
  (
    '#include "lib/math.oshaderinc"\nuniform float s = 1.0;\nvoid fragment() { ROUGHNESS = twice(s); }',
    {
      'materials/lib/math.oshaderinc':
          'float twice(float x) {\n  return x * 2.0;\n}\n',
    },
  ),
  (_pulse, {}),
  ('void fragment() {}', {}),
];

Map<String, Object?> _paramOut(SurfaceParam p) => {
  ...(tree(p.toJson())! as Map<String, Object?>),
  'line': p.line,
  'isColor': p.isColor,
  'kinds': _kinds(p.defaultValue),
};

// Which of the numbers were whole and which were fractions. A fixture holds
// 2 and 2.0 as the same number, and Dart tells them apart.
String _kinds(Object? value) => switch (value) {
  null => '',
  bool() => 'b',
  int() => 'i',
  double() => 'd',
  List<Object?>() => [for (final e in value) _kinds(e)].join(),
  _ => '?',
};

Map<String, Object?> _translationOut(SurfaceTranslation t) => {
  'material': t.material,
  'params': [for (final p in t.params) _paramOut(p)],
  'origins': [for (final o in t.origins) o.toString()],
  'includes': t.includes.toList(),
};

Map<String, Object?> _refusal(SurfaceError error) => {
  'refused': {
    'file': error.origin.file,
    'line': error.origin.line,
    'message': error.message,
    'text': error.toString(),
  },
};

// A refusal is part of the answer. Any other error is only that it threw.
Map<String, Object?> _try(Object? Function() body) => attempt(() {
  try {
    return body();
  } on SurfaceError catch (error) {
    return _refusal(error);
  }
});

Map<String, Object?> _translate(
  String name,
  String path,
  String source,
  Map<String, String> files,
) => {
  'op': 'translate',
  'in': [name, path, source, files],
  'expect': _try(
    () => _translationOut(
      translateSurface(
        name: name,
        path: path,
        source: source,
        read: (include) => files[include],
      ),
    ),
  ),
};

Map<String, Object?> _splitCase(String text) => {
  'op': 'split',
  'in': [text],
  'expect': attempt(() => splitTopLevel(text)),
};

Map<String, Object?> _hintsCase(String text) => {
  'op': 'hints',
  'in': [text],
  'expect': _try(() => tree(parseHints(text, const SurfaceOrigin('f', 3)))),
};

Map<String, Object?> _defaultCase(SurfaceType type, String text) => {
  'op': 'default',
  'in': [type.word, text],
  'expect': _try(() {
    final value = parseDefault(type, text, const SurfaceOrigin('f', 3));
    return {'value': tree(value), 'kinds': _kinds(value)};
  }),
};

Map<String, Object?> _includePathsCase(
  String path,
  String text,
) => {
  'op': 'include_paths',
  'in': [path, text],
  'expect': _try(() => includePaths(path, text)),
};

Map<String, Object?> _diagnosticsCase(
  String source,
  Map<String, String> files,
  String matc,
) => {
  'op': 'diagnostics',
  'in': [source, files, matc],
  'expect': _try(
    () => surfaceDiagnostics(
      matc,
      translateSurface(
        name: 'pulse',
        path: _path,
        source: source,
        read: (include) => files[include],
      ),
    ),
  ),
};

Map<String, Object?> _originCase(
  String source,
  Map<String, String> files,
  int line,
) => {
  'op': 'origin_of',
  'in': [source, files, line],
  'expect': _try(
    () => translateSurface(
      name: 'pulse',
      path: _path,
      source: source,
      read: (include) => files[include],
    ).originOf(line).toString(),
  ),
};

Map<String, Object?> _typeCase(String word) => {
  'op': 'type',
  'in': [word],
  'expect': attempt(() {
    final type = SurfaceType.ofWord(word);
    return type == null
        ? null
        : {
            'index': type.index,
            'word': type.word,
            'matType': type.matType,
            'width': type.width,
          };
  }),
};

Future<void> main(List<String> args) async {
  const empty = <String, String>{};
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'surface',
    cases: [
      for (final (name, path) in _names)
        _translate(name, path, _pulse, empty),
      for (final (name, path) in _names)
        _translate(name, path, 'void fragment() {}', empty),
      for (final source in [
        ..._modeLines,
        ..._uniformLines,
        ..._directiveLines,
        ..._fragmentLines,
      ])
        _translate('pulse', _path, source, empty),
      for (final (source, files) in _includeSources)
        _translate('pulse', _path, source, files),
      _translate('pulse', _path, _pulse.replaceAll('\n', '\r\n'), empty),
      for (final text in [
        '',
        'a',
        'a,b',
        ' a , b ',
        'f(a,b),c',
        '(a,b',
        'a),b',
        ')(,',
        ',',
        ',,',
        'a,(b,(c,d)),e',
        'a,\n b',
        '$_nbsp a$_nbsp,$_nextLine b$_bom',
        'a,$_e,b',
      ])
        _splitCase(text),
      for (final text in _hintTexts) _hintsCase(text),
      for (final type in SurfaceType.values)
        for (final text in _defaultTexts) _defaultCase(type, text),
      for (final (source, _) in _includeSources)
        _includePathsCase('materials/pulse.oshader', source),
      _includePathsCase('pulse.oshader', '#include "a.oshaderinc"'),
      _includePathsCase('a/b/c.oshader', '#include "../x.oshaderinc"'),
      _includePathsCase('a/b/c.oshader', '#include "../../x.oshaderinc"'),
      _includePathsCase('a/b/c.oshader', '#include "../../../x.oshaderinc"'),
      _includePathsCase('a//b.oshader', '#include "x.oshaderinc"'),
      _includePathsCase('', '#include "x.oshaderinc"'),
      for (final (source, files) in _matcSources)
        for (final matc in _matcOutputs)
          _diagnosticsCase(source, files, matc),
      for (final (source, files) in _matcSources)
        for (final line in [-5, 0, 1, 2, 3, 7, 9, 12, 14, 15, 16, 17, 40, 1000])
          _originCase(source, files, line),
      for (final word in [
        for (final type in SurfaceType.values) type.word,
        'mat4',
        'mat3',
        'Float',
        'FLOAT',
        'float ',
        ' float',
        'vec',
        'vec5',
        'sampler',
        'sampler2d',
        'sampler3D',
        'boolean',
        'integer',
        'scalar',
        'texture',
        '',
      ])
        _typeCase(word),
    ],
  );
}
