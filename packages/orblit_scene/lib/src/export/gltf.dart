import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../components/drawing.dart';
import '../components/staging.dart';
import '../components/transform.dart';
import '../document.dart';
import '../entity.dart';
import '../material.dart';
import 'graft.dart';

/// A scene as a glTF document and the bytes it refers to.
///
/// The two are kept apart because the same document is written two ways: a
/// `.gltf` puts the bytes in a file beside it, a `.glb` puts them in a chunk
/// after it. Deciding that here would mean writing the scene twice.
class GltfScene {
  const GltfScene({
    required this.json,
    required this.binary,
    this.problems = const [],
  });

  final Map<String, Object?> json;

  final Uint8List binary;

  /// What could not be written exactly, in the order it was met.
  ///
  /// Reported rather than thrown: a scene that lost one texture is still a
  /// scene worth having, and the caller is the one who knows whether to show
  /// this to somebody or fail the build over it.
  final List<String> problems;
}

/// Writes [scene] as glTF.
///
/// [materials] resolves the `.omat` paths entities name, and without it a
/// material is just the colour its mesh was drawn in. [files] supplies the
/// bytes of anything the scene points at — imported models, textures — keyed
/// by the same project path the scene names. Passing bytes rather than a way
/// to read them keeps this package free of a filesystem, which is what lets
/// the same code run in the editor, in a cook step and on the web.
GltfScene sceneToGltf(
  SceneDocument scene, {
  MaterialLibrary? materials,
  Map<String, Uint8List> files = const {},
}) => _Writer(scene, materials, files).write();

class _Writer {
  _Writer(this.scene, this.materials, this.files);

  final SceneDocument scene;
  final MaterialLibrary? materials;
  final Map<String, Uint8List> files;

  final GltfBuffer buffer = GltfBuffer();
  final List<String> problems = [];
  final Set<String> used = {};

  final Map<String, Object?> json = {
    'asset': {'version': '2.0', 'generator': 'Orblit'},
    'nodes': <Object?>[],
    'meshes': <Object?>[],
    'materials': <Object?>[],
    'cameras': <Object?>[],
    'textures': <Object?>[],
    'images': <Object?>[],
    'samplers': <Object?>[],
  };

  /// Node index by entity id, filled before anything is written so that a
  /// parent can name a child it has not reached yet.
  final Map<String, int> node = {};

  /// Models already copied in, by the path the scene names them.
  final Map<String, Grafted> _graftedAt = {};

  final Map<String, int> _materialAt = {};
  final Set<String> _unresolved = {};
  final Map<String, int> _imageAt = {};
  final Map<String, int> _samplerAt = {};
  final Map<String, int> _textureAt = {};

  /// Look names, in the order the scene first mentions them. Their positions
  /// are the variant indices every primitive's mappings count into.
  final List<String> looks = [];

  List<Object?> list(String name) => json[name]! as List<Object?>;

  GltfScene write() {
    for (final entity in scene.entities) {
      node[entity.id] = list('nodes').length;
      list('nodes').add(<String, Object?>{});
    }

    final children = <String, List<int>>{};
    for (final entity in scene.entities) {
      final parent = entity.parent;
      if (parent != null && node.containsKey(parent)) {
        (children[parent] ??= []).add(node[entity.id]!);
      }
    }

    for (final entity in scene.entities) {
      _fill(entity, children[entity.id] ?? const []);
    }

    final roots = [
      for (final entity in scene.entities)
        if (entity.parent == null || !node.containsKey(entity.parent))
          node[entity.id],
    ];
    json['scene'] = 0;
    json['scenes'] = [
      {
        'name': scene.name,
        // An empty list is not an empty scene in this format, it is a
        // mistake: every array glTF has must have something in it.
        if (roots.isNotEmpty) 'nodes': roots,
      },
    ];

    if (looks.isNotEmpty) {
      _document('KHR_materials_variants')['variants'] = [
        for (final look in looks) {'name': look},
      ];
      used.add('KHR_materials_variants');
    }

    // The settings are not a node and have nowhere in the format to go, so
    // they ride on the scene's extras — which is where a re-import looks for
    // them, and which every other loader ignores exactly as it should. The
    // version rides with them, so a re-import knows what the components on
    // each node meant when they were written.
    ((json['scenes']! as List).first as Map<String, Object?>)['extras'] = {
      'orblit': {
        'formatVersion': SceneDocument.formatVersion,
        'settings': scene.settings.toJson(),
      },
    };

    final binary = buffer.bytes;
    if (buffer.views.isNotEmpty) json['bufferViews'] = buffer.views;
    if (buffer.accessors.isNotEmpty) json['accessors'] = buffer.accessors;
    if (binary.isNotEmpty) {
      json['buffers'] = [
        {'byteLength': binary.length},
      ];
    }

    for (final name in const [
      'nodes',
      'meshes',
      'materials',
      'cameras',
      'textures',
      'images',
      'samplers',
    ]) {
      if (list(name).isEmpty) json.remove(name);
    }

    if (used.isNotEmpty) json['extensionsUsed'] = _union(used.toList()..sort());

    return GltfScene(json: json, binary: binary, problems: problems);
  }

  /// [names] plus whatever a grafted model already declared, without repeats.
  List<Object?> _union(List<String> names) {
    final ours = (json['extensionsUsed'] as List?) ?? const [];
    return [
      ...ours,
      for (final name in names)
        if (!ours.contains(name)) name,
    ];
  }

  Map<String, Object?> _document(String extension) {
    final extensions =
        json.putIfAbsent('extensions', () => <String, Object?>{})
            as Map<String, Object?>;
    return extensions.putIfAbsent(extension, () => <String, Object?>{})
        as Map<String, Object?>;
  }

  void _fill(SceneEntity entity, List<int> children) {
    final out = list('nodes')[node[entity.id]!]! as Map<String, Object?>;
    out['name'] = entity.name;

    final transform = entity[SceneComponents.transform];
    if (transform is TransformComponent) _place(out, transform);

    final mesh = entity[SceneComponents.mesh];
    final material = entity[SceneComponents.material];
    final extra = <int>[];
    if (mesh is MeshComponent) {
      _draw(
        out,
        entity,
        mesh,
        material is MaterialComponent ? material : null,
      ).forEach(extra.add);
    }

    final light = entity[SceneComponents.light];
    if (light is LightComponent) _shine(out, light);

    final camera = entity[SceneComponents.camera];
    if (camera is CameraComponent) _frame(out, camera);

    if (children.isNotEmpty || extra.isNotEmpty) {
      out['children'] = [...children, ...extra];
    }

    // Everything this package knows about an entity that glTF has no word
    // for — which components it has, what each one holds, whether it is
    // hidden — written back exactly as the scene file states it. That is what
    // makes exporting and importing again give the same scene rather than a
    // flattened photograph of one.
    out['extras'] = {
      'orblit': {
        'id': entity.id,
        if (!entity.visible) 'visible': false,
        'components': {
          for (final entry in entity.components.entries)
            entry.key: entry.value.toJson(),
        },
      },
    };
  }

  void _place(Map<String, Object?> out, TransformComponent transform) {
    final position = transform.position;
    if (position.x != 0 || position.y != 0 || position.z != 0) {
      out['translation'] = [position.x, position.y, position.z];
    }

    final scale = transform.scale;
    if (scale.x != 1 || scale.y != 1 || scale.z != 1) {
      out['scale'] = [scale.x, scale.y, scale.z];
    }

    final euler = transform.rotation;
    if (euler.x != 0 || euler.y != 0 || euler.z != 0) {
      // The same order the scene is drawn in — Z, then Y, then X — turned
      // into the quaternion glTF stores. Composing it here rather than
      // writing three numbers means a loader that knows nothing about
      // Orblit's convention still puts the object the way round it was.
      final rotation = Matrix4.rotationZ(radians(euler.z))
        ..multiply(Matrix4.rotationY(radians(euler.y)))
        ..multiply(Matrix4.rotationX(radians(euler.x)));
      final q = Quaternion.fromRotation(rotation.getRotation())..normalize();
      out['rotation'] = [q.x, q.y, q.z, q.w];
    }
  }

  /// Gives [out] something to draw, returning any extra nodes it needs.
  ///
  /// A model imported from a file is the awkward case: glTF cannot reference
  /// another document, so the whole of it is copied in and its roots become
  /// this node's children.
  List<int> _draw(
    Map<String, Object?> out,
    SceneEntity entity,
    MeshComponent mesh,
    MaterialComponent? wearing,
  ) {
    final asset = mesh.asset;
    if (asset != null && mesh.geometry == null) {
      final already = _graftedAt[asset];
      if (already != null && already.shareable) {
        return regraft(into: json, first: already);
      }
      final bytes = files[asset];
      if (bytes == null) {
        problems.add(
          '${entity.name} draws $asset and its bytes were not supplied, so '
          'the export places an empty node where the model goes.',
        );
        return const [];
      }
      final chunks = glbChunks(bytes);
      if (chunks == null) {
        problems.add(
          '${entity.name} draws $asset, which is not a GLB. Only a model that '
          'carries its own bytes can be copied into an export.',
        );
        return const [];
      }
      try {
        final grafted = graft(
          into: json,
          buffer: buffer,
          model: chunks.json,
          bytes: chunks.binary,
          where: asset,
        );
        _graftedAt[asset] = grafted;
        return grafted.roots;
      } on GraftFailure catch (failure) {
        problems.add('${entity.name}: ${failure.message}');
        return const [];
      }
    }

    final geometry = mesh.geometry ?? mesh.shape?.build();
    if (geometry == null) return const [];

    final triangles = geometry.triangulate();
    if (triangles.indices.isEmpty || triangles.positions.isEmpty) {
      return const [];
    }

    // One view over every face, and an accessor per run into a stretch of
    // it. The builder narrows the width to what the numbers need and says
    // which it chose, so the offsets below are right whichever it picked.
    final indices = buffer.addIndexView(triangles.indices);

    final position = buffer.addPositions(triangles.positions);
    final normal = triangles.normals.isEmpty
        ? null
        : buffer.addFloats(
            triangles.normals,
            'VEC3',
            target: GltfTarget.arrayBuffer,
          );
    final uv = triangles.uvs.isEmpty
        ? null
        : buffer.addFloats(
            triangles.uvs,
            'VEC2',
            target: GltfTarget.arrayBuffer,
          );

    final slot = _material(wearing?.asset, mesh.colour);
    final mappings = _mappings(wearing, mesh.colour);

    // A normal map is read in the texture's own frame, and a mesh that does
    // not say which way that frame runs leaves every renderer to guess. They
    // guess differently, so the bumps lean one way here and the other way in
    // the next tool along. Worked out only when something actually wears a
    // normal map: it is four more floats a vertex, and most things do not.
    final tangent = uv != null && normal != null && _needsTangents(wearing)
        ? buffer.addFloats(
            tangentsOf(triangles),
            'VEC4',
            target: GltfTarget.arrayBuffer,
          )
        : null;

    list('meshes').add(<String, Object?>{
      'name': entity.name,
      'primitives': [
        for (final run in triangles.runs)
          <String, Object?>{
            'attributes': {
              'POSITION': position,
              if (normal != null) 'NORMAL': normal,
              if (uv != null) 'TEXCOORD_0': uv,
              if (tangent != null) 'TANGENT': tangent,
            },
            'indices': buffer.addAccessor(
              view: indices.view,
              byteOffset: run.start * indices.width,
              componentType: indices.componentType,
              count: run.count,
              type: 'SCALAR',
            ),
            'mode': 4,
            if (slot != null) 'material': slot,
            if (mappings != null)
              'extensions': {
                'KHR_materials_variants': {'mappings': mappings},
              },
            // Which of the mesh's painted surfaces this stretch of faces
            // wore. Not turned into a material here — what a surface means is
            // the material system's to say, and this package keeps it as the
            // file wrote it — but kept, so importing again can.
            if (triangles.groups.isNotEmpty)
              'extras': {
                'orblit': {'surface': run.material},
              },
          },
      ],
    });
    out['mesh'] = list('meshes').length - 1;
    return const [];
  }

  /// Whether anything this entity wears — now or under a look — is read in
  /// the texture's own frame and so needs one written down.
  bool _needsTangents(MaterialComponent? wearing) {
    if (wearing == null || materials == null) return false;
    for (final path in [
      if (wearing.asset != null) wearing.asset!,
      ...wearing.looks.values,
    ]) {
      if (materials!.resolve(path).maps['normal'] != null) return true;
    }
    return false;
  }

  /// The per-primitive variant mappings, or null when this entity has none.
  List<Object?>? _mappings(MaterialComponent? wearing, Tint colour) {
    if (wearing == null || wearing.looks.isEmpty) return null;
    return [
      for (final entry in wearing.looks.entries)
        {
          'material': _material(entry.value, colour) ?? 0,
          'variants': [_look(entry.key)],
        },
    ];
  }

  int _look(String name) {
    final at = looks.indexOf(name);
    if (at >= 0) return at;
    looks.add(name);
    return looks.length - 1;
  }

  /// The glTF material for a `.omat` path worn over a colour.
  ///
  /// Both, because an entity with no material of its own still has a colour,
  /// and two entities wearing the same material in different colours are two
  /// materials in a file with nowhere else to put the difference.
  int? _material(String? path, Tint colour) {
    final resolved = path == null ? null : materials?.resolve(path);
    if (path != null && materials == null && _unresolved.add(path)) {
      problems.add(
        'the material $path was not resolved, so what wears it is written in '
        'the colour its mesh was drawn in.',
      );
    }

    // Kept apart by what they say, not by where they came from. Two entities
    // wearing the same material differ in a colour that material overrides,
    // and a file that wrote one entry each would be four hundred identical
    // materials — four hundred things to bind, for one appearance.
    final out = _describe(path, resolved, colour.linear);
    final key = jsonEncode(out);
    final cached = _materialAt[key];
    if (cached != null) return cached;

    list('materials').add(out);
    return _materialAt[key] = list('materials').length - 1;
  }

  Map<String, Object?> _describe(
    String? path,
    ResolvedMaterial? resolved,
    Vector3 colour,
  ) {
    final pbr = <String, Object?>{};
    final extensions = <String, Object?>{};
    final out = <String, Object?>{
      if (path != null) 'name': path.split('/').last.split('.').first,
    };

    final base = resolved?.numbers('baseColour');
    pbr['baseColorFactor'] = base != null && base.length == 4
        ? base
        : [colour.x, colour.y, colour.z, 1.0];

    final metallic = resolved?.number('metallic');
    if (metallic != null) pbr['metallicFactor'] = metallic;
    final roughness = resolved?.number('roughness');
    if (roughness != null) pbr['roughnessFactor'] = roughness;

    // A mesh with no metallic-roughness map and nothing to say about either
    // is a plain painted surface. glTF's defaults are fully metallic and
    // fully rough, which is not what anybody means by that, so they are
    // stated rather than left out.
    pbr.putIfAbsent('metallicFactor', () => 0.0);
    pbr.putIfAbsent('roughnessFactor', () => 0.8);

    final maps = resolved?.maps ?? const <String, String>{};
    final transform = _transform(resolved);
    final missed = <String, String>{};

    void wire(String slot, String key, Map<String, Object?> onto) {
      final source = maps[slot];
      if (source == null) return;
      // Asked for only once there is something to read with it, so a
      // material with no maps does not leave a sampler nothing uses.
      final texture = _texture(source, _sampler(resolved));
      if (texture == null) {
        missed[slot] = source;
        return;
      }
      onto[key] = {
        'index': texture,
        if (transform != null)
          'extensions': {'KHR_texture_transform': transform},
        if (key == 'normalTexture' && resolved?.number('normalScale') != null)
          'scale': resolved!.number('normalScale'),
        if (key == 'occlusionTexture' &&
            resolved?.number('ambientOcclusion') != null)
          'strength': resolved!.number('ambientOcclusion'),
      };
      if (transform != null) used.add('KHR_texture_transform');
    }

    wire('baseColour', 'baseColorTexture', pbr);
    wire('metallicRoughness', 'metallicRoughnessTexture', pbr);
    wire('normal', 'normalTexture', out);
    wire('occlusion', 'occlusionTexture', out);
    wire('emissive', 'emissiveTexture', out);
    for (final slot in const ['blendBaseColour', 'blendMask']) {
      final source = maps[slot];
      if (source != null) missed[slot] = source;
    }

    final emissive = resolved?.numbers('emissive');
    if (emissive != null && emissive.length == 3) {
      final strength = resolved?.number('emissiveIntensity') ?? 1;
      final peak = emissive.reduce(math.max);
      // glTF caps the factor at one and puts anything brighter in an
      // extension, so a glow authored at eight watts stays eight times as
      // bright rather than being quietly clipped to white.
      final over = peak > 1 ? peak : 1.0;
      out['emissiveFactor'] = [for (final c in emissive) c / over];
      final total = strength * over;
      if ((total - 1).abs() > 1e-6) {
        extensions['KHR_materials_emissive_strength'] = {
          'emissiveStrength': total,
        };
        used.add('KHR_materials_emissive_strength');
      }
    }

    final blend = resolved?.choice('blend');
    switch (blend) {
      case 'masked':
        out['alphaMode'] = 'MASK';
        final threshold = resolved?.number('maskThreshold');
        if (threshold != null) out['alphaCutoff'] = threshold;
      case 'transparent' || 'fade' || 'add':
        out['alphaMode'] = 'BLEND';
        if (blend == 'add') {
          missed['blend'] = 'add, which glTF has no word for';
        }
      case _:
        break;
    }

    if ((resolved?.flag('doubleSided') ?? false) ||
        resolved?.choice('culling') == 'none') {
      out['doubleSided'] = true;
    }

    if (resolved?.choice('shading') == 'unlit') {
      extensions['KHR_materials_unlit'] = <String, Object?>{};
      used.add('KHR_materials_unlit');
    }

    final clearCoat = resolved?.number('clearCoat');
    if (clearCoat != null && clearCoat > 0) {
      extensions['KHR_materials_clearcoat'] = {
        'clearcoatFactor': clearCoat,
        if (resolved?.number('clearCoatRoughness') != null)
          'clearcoatRoughnessFactor': resolved!.number('clearCoatRoughness'),
      };
      used.add('KHR_materials_clearcoat');
    }

    final sheen = resolved?.numbers('sheenColour');
    if (sheen != null && sheen.length == 3) {
      extensions['KHR_materials_sheen'] = {
        'sheenColorFactor': sheen,
        if (resolved?.number('sheenRoughness') != null)
          'sheenRoughnessFactor': resolved!.number('sheenRoughness'),
      };
      used.add('KHR_materials_sheen');
    }

    final anisotropy = resolved?.number('anisotropy');
    if (anisotropy != null && anisotropy != 0) {
      // Orblit signs anisotropy to say which way the grain runs; glTF states
      // the strength and the angle apart, so a negative turns into a quarter
      // turn of the same strength.
      extensions['KHR_materials_anisotropy'] = {
        'anisotropyStrength': anisotropy.abs().clamp(0.0, 1.0),
        if (anisotropy < 0) 'anisotropyRotation': math.pi / 2,
      };
      used.add('KHR_materials_anisotropy');
    }

    final reflectance = resolved?.number('reflectance');
    if (reflectance != null && reflectance != 0.5) {
      // Both describe how much a dielectric reflects head-on, and they agree
      // at the default: Orblit's f0 is 0.16r², glTF scales a 0.04 f0, so the
      // scale is 4r² and r of a half is a scale of one.
      final specular = (4 * reflectance * reflectance).clamp(0.0, 1.0);
      extensions['KHR_materials_specular'] = {
        'specularColorFactor': [specular, specular, specular],
      };
      used.add('KHR_materials_specular');
    }

    out['pbrMetallicRoughness'] = pbr;
    if (extensions.isNotEmpty) out['extensions'] = extensions;
    if (path != null || missed.isNotEmpty) {
      out['extras'] = {
        'orblit': {
          if (path != null) 'asset': path,
          if (missed.isNotEmpty) 'unwritten': missed,
        },
      };
    }
    return out;
  }

  Map<String, Object?>? _transform(ResolvedMaterial? resolved) {
    final tiling = resolved?.numbers('tiling');
    final offset = resolved?.numbers('offset');
    if (tiling == null && offset == null) return null;
    return {
      if (offset != null && offset.length == 2) 'offset': offset,
      if (tiling != null && tiling.length == 2) 'scale': tiling,
    };
  }

  int _sampler(ResolvedMaterial? resolved) {
    final wrap = resolved?.choice('wrap') ?? 'repeat';
    final filter = resolved?.choice('filter') ?? 'smooth';
    final key = '$wrap|$filter';
    final cached = _samplerAt[key];
    if (cached != null) return cached;

    final mode = switch (wrap) {
      'clamp' => 33071,
      'mirror' => 33648,
      _ => 10497,
    };
    list('samplers').add(<String, Object?>{
      'magFilter': filter == 'sharp' ? 9728 : 9729,
      'minFilter': filter == 'sharp' ? 9984 : 9987,
      'wrapS': mode,
      'wrapT': mode,
    });
    return _samplerAt[key] = list('samplers').length - 1;
  }

  /// A texture over [path], or null when its bytes were not supplied.
  int? _texture(String path, int sampler) {
    final key = '$path|$sampler';
    final cached = _textureAt[key];
    if (cached != null) return cached;

    final image = _image(path);
    if (image == null) return null;

    final kind = _kindOf(files[path]!);
    list('textures').add(<String, Object?>{
      if (kind == 'image/ktx2' || kind == 'image/webp')
        'extensions': {
          if (kind == 'image/ktx2') 'KHR_texture_basisu': {'source': image},
          if (kind == 'image/webp') 'EXT_texture_webp': {'source': image},
        }
      else
        'source': image,
      'sampler': sampler,
    });
    if (kind == 'image/ktx2') used.add('KHR_texture_basisu');
    if (kind == 'image/webp') used.add('EXT_texture_webp');
    return _textureAt[key] = list('textures').length - 1;
  }

  int? _image(String path) {
    final cached = _imageAt[path];
    if (cached != null) return cached;

    final bytes = files[path];
    if (bytes == null) return null;
    final kind = _kindOf(bytes);
    if (kind == null) {
      problems.add(
        '$path is not a picture this can embed — glTF carries PNG, JPEG, '
        'WebP and KTX2, and this is none of them.',
      );
      return null;
    }

    list('images').add(<String, Object?>{
      'name': path.split('/').last,
      'mimeType': kind,
      'bufferView': buffer.addView(bytes),
    });
    return _imageAt[path] = list('images').length - 1;
  }

  void _shine(Map<String, Object?> out, LightComponent light) {
    final colour = light.colour.linear;
    final lights =
        _document(
              'KHR_lights_punctual',
            ).putIfAbsent('lights', () => <Object?>[])
            as List<Object?>;

    // A sun is stated per square metre and arrives as parallel rays, so its
    // strength is an illuminance; everything else is a source radiating into
    // the whole sphere, so its strength is an intensity. Both go through the
    // one conversion this engine has, which is the same simplification
    // Blender's exporter makes — so a scene comes back the brightness it left.
    final directional = light.kind == LightType.sun;
    final intensity = directional
        ? Photometry.irradianceToLux(light.power)
        : Photometry.lumensToCandela(Photometry.wattsToLumens(light.power));

    final outer = math.min(radians(light.spotSize) / 2, math.pi / 2);
    final entry = <String, Object?>{
      'type': switch (light.kind) {
        LightType.sun => 'directional',
        LightType.spot => 'spot',
        LightType.point || LightType.area => 'point',
      },
      'color': [colour.x, colour.y, colour.z],
      'intensity': intensity,
      if (light.kind == LightType.spot)
        'spot': {
          'innerConeAngle': outer * (1 - light.spotBlend.clamp(0.0, 1.0)),
          'outerConeAngle': outer,
        },
    };
    // An area light is a shape that emits, and a punctual light is a point.
    // Writing it as a point keeps the light; what is lost is the softness its
    // size gave the shadow, and the size is kept so importing can give it back.
    if (light.kind == LightType.area) {
      entry['extras'] = {
        'orblit': {'kind': 'area', 'sourceRadius': light.sourceRadius},
      };
    }

    lights.add(entry);
    (out.putIfAbsent('extensions', () => <String, Object?>{})
        as Map<String, Object?>)['KHR_lights_punctual'] = {
      'light': lights.length - 1,
    };
    used.add('KHR_lights_punctual');
  }

  void _frame(Map<String, Object?> out, CameraComponent camera) {
    // Orblit states the angle across whichever side of the frame is shorter,
    // which is the vertical one on every screen anybody ships. glTF states
    // the vertical angle outright, so on a landscape frame they are the same
    // number — and the original is kept either way.
    list('cameras').add(<String, Object?>{
      'type': 'perspective',
      'perspective': {
        'yfov': radians(camera.fieldOfView),
        'znear': camera.near,
        if (camera.far > camera.near) 'zfar': camera.far,
      },
      'extras': {
        'orblit': {'fieldOfView': camera.fieldOfView, 'across': 'shorter'},
      },
    });
    out['camera'] = list('cameras').length - 1;
  }
}

/// What a picture is, read from the first few bytes rather than the name.
///
/// A file called `.png` that is a JPEG is common enough; a `mimeType` that
/// disagrees with the bytes is a texture that does not load, in a file that
/// validates.
String? _kindOf(Uint8List bytes) {
  bool starts(List<int> magic) {
    if (bytes.length < magic.length) return false;
    for (var i = 0; i < magic.length; i++) {
      if (bytes[i] != magic[i]) return false;
    }
    return true;
  }

  if (starts(const [0x89, 0x50, 0x4E, 0x47])) return 'image/png';
  if (starts(const [0xFF, 0xD8, 0xFF])) return 'image/jpeg';
  if (starts(const [0xAB, 0x4B, 0x54, 0x58])) return 'image/ktx2';
  if (starts(const [0x52, 0x49, 0x46, 0x46]) &&
      bytes.length >= 12 &&
      starts(const [0x52]) &&
      bytes[8] == 0x57 &&
      bytes[9] == 0x45 &&
      bytes[10] == 0x42 &&
      bytes[11] == 0x50) {
    return 'image/webp';
  }
  return null;
}
