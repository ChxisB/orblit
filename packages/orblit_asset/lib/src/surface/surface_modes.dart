import 'surface_error.dart';

/// What the `render_mode` lines asked for, as the material file keys.
final class SurfaceModes {
  /// Filament's `blending` value.
  String blending = 'opaque';

  /// Whether the colour is multiplied by its alpha before it is written,
  /// which Filament's blending modes expect.
  bool premultiply = false;
  bool unshaded = false;
  String? culling;
  bool doubleSided = false;
  bool? depthWrite;
  bool? depthCulling;

  /// Adds one mode, or says it is not one this translator has.
  void add(String mode, SurfaceOrigin at) {
    final apply = _modes[mode];
    if (apply == null) {
      throw SurfaceError(at, 'render mode `$mode` is not supported');
    }
    apply(this);
  }
}

final _modes = <String, void Function(SurfaceModes)>{
  'blend_mix': (m) {
    m.blending = 'transparent';
    m.premultiply = true;
  },
  'blend_add': (m) {
    m.blending = 'add';
    m.premultiply = true;
  },
  'blend_mul': (m) => m.blending = 'multiply',
  'blend_premul_alpha': (m) => m.blending = 'transparent',
  'cull_back': (m) => m.culling = 'back',
  'cull_front': (m) => m.culling = 'front',
  'cull_disabled': (m) {
    m.culling = 'none';
    m.doubleSided = true;
  },
  'depth_draw_opaque': (m) {},
  'depth_draw_always': (m) => m.depthWrite = true,
  'depth_draw_never': (m) => m.depthWrite = false,
  'depth_test_default': (m) {},
  'depth_test_disabled': (m) => m.depthCulling = false,
  'unshaded': (m) => m.unshaded = true,
};
