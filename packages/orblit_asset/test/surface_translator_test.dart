import 'package:orblit_asset/src/surface/surface_error.dart';
import 'package:orblit_asset/src/surface/surface_lines.dart';
import 'package:orblit_asset/src/surface/surface_param.dart';
import 'package:orblit_asset/src/surface/surface_translator.dart';
import 'package:test/test.dart';

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

SurfaceTranslation _translate(String source, {SurfaceReader? read}) =>
    translateSurface(
      name: 'pulse',
      path: 'materials/pulse.oshader',
      source: source,
      read: read,
    );

SurfaceError _refused(String source, {SurfaceReader? read}) {
  try {
    _translate(source, read: read);
  } on SurfaceError catch (error) {
    return error;
  }
  fail('expected the source to be refused');
}

void main() {
  group('a surface', () {
    test('keeps the author\'s lines and declares the parameters', () {
      final out = _translate(_pulse);
      final lines = out.material.split('\n');

      expect(lines, contains('#define tint materialParams.tint'));
      expect(lines, contains('#define albedo_map materialParams_albedo_map'));
      expect(out.material, contains('{ type : float4, name : "tint" }'));
      expect(
        out.material,
        contains('{ type : sampler2d, name : "albedo_map" }'),
      );
      expect(out.material, contains('shadingModel : unlit'));
      expect(out.material, contains('blending : transparent'));
      expect(out.material, contains('doubleSided : true'));
      expect(
        out.material,
        contains('material.baseColor = vec4(ALBEDO * ALPHA'),
      );
    });

    test('quotes names and removes its aliases after the author\'s text', () {
      final out = _translate('uniform float n;\nvoid fragment() {}');
      final lines = out.material.split('\n');

      expect(lines, contains('        { type : float, name : "n" }'));
      expect(
        lines.indexOf('#undef n'),
        greaterThan(lines.indexOf('#define n materialParams.n')),
      );
    });

    test(
      'a uniform named like a material field does not rewrite the entry',
      () {
        final lines = _translate(
          'uniform vec3 emissive;\nvoid fragment() { EMISSION = emissive; }',
        ).material.split('\n');

        expect(
          lines.indexOf('#undef emissive'),
          lessThan(lines.indexWhere((l) => l.contains('material.emissive'))),
        );
      },
    );

    test('maps a material line back to the line the author wrote', () {
      final out = _translate(_pulse);
      final lines = out.material.split('\n');
      final pulseAt = lines.indexWhere((l) => l.contains('float pulse')) + 1;
      final authorAt =
          _pulse.split('\n').indexWhere((l) => l.contains('float pulse')) + 1;

      expect(
        out.originOf(pulseAt).toString(),
        'materials/pulse.oshader:$authorAt',
      );
      expect(out.originOf(lines.length).file, 'materials/pulse.oshader');
    });

    test('reads types, hints, defaults, groups and instance', () {
      final params = _translate(_pulse).params;

      expect(params.map((p) => p.name), [
        'tint',
        'speed',
        'glow',
        'albedo_map',
      ]);
      expect(params[0].isColor, isTrue);
      expect(params[0].defaultValue, [1.0, 0.5, 0.2, 1.0]);
      expect(params[1].hints['hint_range'], ['0.0', '10.0', '0.1']);
      expect(params[1].defaultValue, 2.0);
      expect(params[2].instance, isTrue);
      expect(params[2].group, 'look');
      expect(params[3].type, SurfaceType.texture);
      expect(
        params[3].hints.keys,
        containsAll(['source_color', 'filter_nearest']),
      );
      expect(params[0].toJson()['type'], 'vec4');
    });

    test('broadcasts one number to a whole vector', () {
      final out = _translate('uniform vec3 c = vec3(0.5);\nvoid fragment() {}');
      expect(out.params.single.defaultValue, [0.5, 0.5, 0.5]);
    });

    test('writes the normal only when the author sets it', () {
      expect(_translate(_pulse).material, isNot(contains('material.normal')));
      final lit = _translate(
        'void fragment() { NORMAL_MAP = vec3(0.0, 0.0, 1.0); }',
      );
      expect(lit.material, contains('material.normal = NORMAL_MAP;'));
      expect(lit.material, contains('shadingModel : lit'));
    });

    test('maps every render mode it has', () {
      String head(String modes) =>
          _translate('render_mode $modes;\nvoid fragment() {}').material;

      expect(head('blend_add'), contains('blending : add'));
      expect(head('blend_mul'), contains('blending : multiply'));
      expect(head('blend_premul_alpha'), isNot(contains('ALBEDO * ALPHA')));
      expect(head('cull_front'), contains('culling : front'));
      expect(head('depth_draw_never'), contains('depthWrite : false'));
      expect(head('depth_test_disabled'), contains('depthCulling : false'));
    });
  });

  group('includes', () {
    const include = 'float twice(float x) {\n  return x * 2.0;\n}\n';

    test('are expanded in place with their own lines in the map', () {
      final out = _translate(
        '#include "lib/math.oshaderinc"\nvoid fragment() { ROUGHNESS = twice(0.1); }',
        read: (path) =>
            path == 'materials/lib/math.oshaderinc' ? include : null,
      );
      final at =
          out.material.split('\n').indexWhere((l) => l.contains('return x')) +
          1;

      expect(out.originOf(at).toString(), 'materials/lib/math.oshaderinc:2');
      expect(out.includes, {'materials/lib/math.oshaderinc'});
    });

    test('name both files in a cycle', () {
      final files = {
        'materials/a.oshaderinc': '#include "b.oshaderinc"',
        'materials/b.oshaderinc': '#include "a.oshaderinc"',
      };
      final error = _refused(
        '#include "a.oshaderinc"\nvoid fragment() {}',
        read: (path) => files[path],
      );
      expect(
        error.message,
        contains('a.oshaderinc includes materials/b.oshaderinc'),
      );
    });

    test('must be .oshaderinc files', () {
      final error = _refused('#include "a.glsl"\nvoid fragment() {}');
      expect(error.origin.line, 1);
      expect(error.message, contains('.oshaderinc'));
    });
  });

  group('what it refuses', () {
    test('says which line and why', () {
      final cases = {
        'void fragment() {}\nrender_mode wireframe;': (2, 'wireframe'),
        'shader_type particles;\nvoid fragment() {}': (1, 'particles'),
        'void fragment() {}\nvoid vertex() {}': (2, 'vertex()'),
        'varying vec3 pos;\nvoid fragment() {}': (1, 'varying'),
        'uniform vec3 c : hint_wibble;\nvoid fragment() {}': (1, 'hint_wibble'),
        'uniform float f = vec2(1.0);\nvoid fragment() {}': (1, 'vec2(1.0)'),
        'instance uniform sampler2D t;\nvoid fragment() {}': (1, 'texture'),
        'uniform mat4 m;\nvoid fragment() {}': (1, 'mat4'),
      };
      for (final entry in cases.entries) {
        final error = _refused(entry.key);
        expect(error.origin.line, entry.value.$1, reason: entry.key);
        expect(error.message, contains(entry.value.$2), reason: entry.key);
      }
    });

    test('wants a fragment function', () {
      expect(_refused('uniform float f;').message, contains('fragment()'));
    });
  });
}
