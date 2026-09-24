part of 'scene.dart';

// How a scene becomes the flat arrays the channel carries, one topic at a
// time. `OrblitScene.toMessage` assembles what these return.

extension _Message on OrblitScene {
  /// What the renderer needs to know about the objects themselves,
  /// and about the meshes and materials they point at.
  Map<String, Object> _objectMessage() {
    final count = objects.length;
    final keys = makeKeyList(count);
    final transforms = Float32List(count * 16);
    final colours = Float32List(count * 3);
    final meshes = Int32List(count);
    final flags = Int32List(count);
    final objectMaterials = Int32List(count);

    // Materials are referred to by their position in this frame's list, so
    // the renderer never has to search. Keys are what survive between frames;
    // indices are what travel in one.
    final materialAt = <int, int>{};
    for (var i = 0; i < materials.length; i++) {
      materialAt[materials[i].key] = i;
    }

    // Morph weights, packed end to end with a count each rather than a fixed
    // width per object. A face rig has dozens and a crate has none, and a
    // width that suits both is a width that is wrong for both.
    final morphCounts = Int32List(count);
    final allWeights = <double>[];

    // Paths are sent once and referred to by index, because the same mesh is
    // usually on many objects and the message goes over the channel on every
    // frame of a drag.
    final paths = <String>[];
    final indices = <String, int>{};

    for (var i = 0; i < count; i++) {
      final object = objects[i];
      final mesh = object.mesh;
      keys[i] = object.key;
      meshes[i] = mesh == null
          ? -1
          : indices.putIfAbsent(mesh, () {
              paths.add(mesh);
              return paths.length - 1;
            });
      flags[i] = object._flags;
      final material = object.material;
      objectMaterials[i] = material == null ? -1 : (materialAt[material] ?? -1);
      // Matrix4's storage is already column-major, which is what Filament's
      // mat4f expects, so this copies rather than transposes.
      transforms.setRange(i * 16, i * 16 + 16, object.transform.storage);
      colours[i * 3] = object.colour.x;
      colours[i * 3 + 1] = object.colour.y;
      colours[i * 3 + 2] = object.colour.z;

      final weights = object.morphWeights;
      if (weights != null && weights.isNotEmpty) {
        morphCounts[i] = weights.length;
        allWeights.addAll(weights);
      }
    }

    // Never empty, because the far side takes a pointer and an empty typed
    // list has none to give.
    final morphWeights = Float32List.fromList(
      allWeights.isEmpty ? const [0.0] : allWeights,
    );

    return {
      'objectKeys': keys,
      'transforms': transforms,
      'colours': colours,
      'meshes': meshes,
      'objectFlags': flags,
      'objectMorphCounts': morphCounts,
      'objectMorphWeights': morphWeights,
      'meshPaths': paths,
      'objectMaterials': objectMaterials,
    };
  }

  /// The materials, and the textures they draw from. Both travel by
  /// index: the same image is usually on several materials.
  Map<String, Object> _materialMessage() {
    final materialCount = materials.length;
    final materialKeys = makeKeyList(materialCount);
    final materialFlags = Int32List(materialCount);
    final materialParams = Float32List(materialCount * OrblitMaterial.stride);
    final materialMaps = Int32List(materialCount * OrblitMaterial.mapCount);
    final materialVideos = Int32List(materialCount);

    final videoAt = <int, int>{};
    for (var i = 0; i < videos.length; i++) {
      videoAt[videos[i].key] = i;
    }

    // The same trick as mesh paths: an image is usually on several materials
    // and always on several frames, so it travels once and is pointed at.
    final texturePaths = <String>[];
    final textureSrgb = <int>[];
    final textureAt = <OrblitTexture, int>{};

    for (var i = 0; i < materialCount; i++) {
      final material = materials[i];
      materialKeys[i] = material.key;
      materialFlags[i] = material.flags;
      material.pack(materialParams, i * OrblitMaterial.stride);
      final video = material.video;
      materialVideos[i] = video == null ? -1 : (videoAt[video] ?? -1);
      final maps = material.maps;
      for (var m = 0; m < OrblitMaterial.mapCount; m++) {
        final map = maps[m];
        materialMaps[i * OrblitMaterial.mapCount + m] = map == null
            ? -1
            : textureAt.putIfAbsent(map, () {
                texturePaths.add(map.path);
                textureSrgb.add(map.srgb ? 1 : 0);
                return texturePaths.length - 1;
              });
      }
    }

    return {
      'materialKeys': materialKeys,
      'materialFlags': materialFlags,
      'materialParams': materialParams,
      'materialMaps': materialMaps,
      'materialVideos': materialVideos,
      'texturePaths': texturePaths,
      'textureSrgb': Int32List.fromList(textureSrgb),
    };
  }

  /// The video tokens, one stride of floats each.
  Map<String, Object> _videoMessage() {
    final videoCount = videos.length;
    final videoKeys = makeKeyList(videoCount);
    final videoFlags = Int32List(videoCount);
    final videoParams = Float32List(videoCount * OrblitVideo.stride);
    final videoPaths = <String>[];
    for (var i = 0; i < videoCount; i++) {
      final video = videos[i];
      videoKeys[i] = video.key;
      videoFlags[i] = video.flags;
      videoPaths.add(video.path);
      final at = i * OrblitVideo.stride;
      videoParams[at] = video.rate;
      videoParams[at + 1] = video.volume;
      // Negative for "no seek asked for", so a token that has moved with no
      // target is a no-op rather than a jump to the start.
      videoParams[at + 2] = video.seekTo ?? -1;
      videoParams[at + 3] = video.seekToken.toDouble();
    }

    return {
      'videoKeys': videoKeys,
      'videoFlags': videoFlags,
      'videoParams': videoParams,
      'videoPaths': videoPaths,
    };
  }

  /// The decals, with their pictures sent once and pointed at.
  Map<String, Object> _decalMessage() {
    // Decals, with their pictures sent once and pointed at, the same trick
    // as mesh paths and material maps.
    final decalParams = Float32List(decals.length * OrblitDecal.stride);
    final decalImages = Int32List(decals.length);
    final decalPaths = <String>[];
    final decalPathAt = <String, int>{};
    for (var i = 0; i < decals.length; i++) {
      final decal = decals[i];
      decal.pack(decalParams, i * OrblitDecal.stride);
      final texture = decal.texture;
      decalImages[i] = texture == null
          ? -1
          : decalPathAt.putIfAbsent(texture.path, () {
              decalPaths.add(texture.path);
              return decalPaths.length - 1;
            });
    }

    return {
      'decalParams': decalParams,
      'decalImages': decalImages,
      'decalPaths': decalPaths,
    };
  }

  /// The reflection probes.
  Map<String, Object> _probeMessage() {
    // The probes, packed the same way as everything else: keys in one array
    // and a fixed stride of floats in another, so the renderer walks them
    // without matching a single string.
    final probeKeys = makeKeyList(probes.length);
    final probeParams = Float32List(probes.length * OrblitProbe.stride);
    for (var i = 0; i < probes.length; i++) {
      probeKeys[i] = probes[i].key;
      probes[i].pack(probeParams, i * OrblitProbe.stride);
    }

    return {'probeKeys': probeKeys, 'probeParams': probeParams};
  }

  /// The lights, packed the way everything else is: keys in one
  /// array and a fixed stride of floats in another.
  Map<String, Object> _lightMessage() {
    final lightCount = lights.length;
    final lightKeys = makeKeyList(lightCount);
    final lightKinds = Int32List(lightCount);
    final lightFlags = Int32List(lightCount);
    final lightParams = Float32List(lightCount * OrblitLight.stride);

    for (var i = 0; i < lightCount; i++) {
      final light = lights[i];
      lightKeys[i] = light.key;
      lightKinds[i] = light.kind.index;
      lightFlags[i] = light.castShadows ? 1 : 0;
      light._pack(lightParams, i * OrblitLight.stride);
    }

    return {
      'lightKeys': lightKeys,
      'lightKinds': lightKinds,
      'lightFlags': lightFlags,
      'lightParams': lightParams,
    };
  }

  /// Where the eye is and how it is set.
  Map<String, Object> _cameraMessage() => {
    'cameraPosition': _vector(camera.position),
    'cameraTarget': _vector(camera.target),
    'fieldOfView': camera.fieldOfView,
    'orthographic': camera.orthographic,
    'viewHeight': camera.viewHeight,
    'aperture': camera.aperture,
    'shutterSpeed': camera.shutterSpeed,
    'sensitivity': camera.sensitivity,
  };

  /// The sky and the weather in front of it.
  Map<String, Object> _skyMessage() => {
    'skyColour': _vector(sky.colour),
    'ambient': sky.ambient,
    'showBody': sky.showBody,
    'skyParams': sky.packed,
    'fogEnabled': fog.isVisible,
    'fogParams': fog._packed,
    'precipitationEnabled': precipitation.isVisible,
    'precipitationParams': precipitation._packed,
    'skyEnabled': sky.drawn,
  };

  /// What the renderer needs to know about the splat clouds.
  ///
  /// Absent altogether when there are none, so every scene that never uses
  /// them sends exactly what it sent before. The records of an in-memory
  /// cloud travel only when its revision is not the one the renderer holds —
  /// [sent] — for the same reason a population's transforms do.
  Map<String, Object>? _splatMessage(Map<int, int>? sent) {
    if (splats.isEmpty) return null;

    final count = splats.length;
    final keys = Int32List(count);
    final flags = Int32List(count);
    final revisions = Int32List(count);
    final params = Float32List(count * OrblitSplats.stride);
    final paths = <String>[];
    final changed = <OrblitSplats>[];
    var bytes = 0;

    for (var i = 0; i < count; i++) {
      final cloud = splats[i];
      keys[i] = cloud.key;
      flags[i] = cloud.flags;
      revisions[i] = cloud.revision;
      cloud.packParams(params, i * OrblitSplats.stride);
      paths.add(cloud.path ?? '');
      final data = cloud.data;
      if (data != null && (sent == null || sent[cloud.key] != cloud.revision)) {
        changed.add(cloud);
        bytes += data.length;
      }
    }

    final data = Uint8List(bytes);
    final changedKeys = Int32List(changed.length);
    final changedCounts = Int32List(changed.length);
    var at = 0;
    for (var i = 0; i < changed.length; i++) {
      final records = changed[i].data!;
      changedKeys[i] = changed[i].key;
      changedCounts[i] = changed[i].count;
      data.setRange(at, at + records.length, records);
      at += records.length;
    }

    return {
      'splatKeys': keys,
      'splatFlags': flags,
      'splatRevisions': revisions,
      'splatParams': params,
      'splatPaths': paths,
      'splatChanged': changedKeys,
      'splatChangedCounts': changedCounts,
      'splatData': data,
    };
  }

  /// What the renderer needs to know about the sprite layers.
  ///
  /// Absent when there are none. Every layer's settings travel every time —
  /// they are twenty floats, and moving a whole layer by its transform is how
  /// a backdrop scrolls without sending a sprite — but its sprites travel only
  /// when its revision is not the one the renderer holds, [sent].
  Map<String, Object>? _spriteMessage(Map<int, int>? sent) {
    if (sprites.isEmpty) return null;

    final count = sprites.length;
    final keys = Int32List(count);
    final flags = Int32List(count);
    final orders = Int32List(count);
    final revisions = Int32List(count);
    final params = Float32List(count * OrblitSprites.layerStride);
    final paths = <String>[];
    final changed = <OrblitSprites>[];
    var floats = 0;

    for (var i = 0; i < count; i++) {
      final layer = sprites[i];
      keys[i] = layer.key;
      flags[i] = layer.flags;
      orders[i] = layer.order;
      revisions[i] = layer.revision;
      layer.packParams(params, i * OrblitSprites.layerStride);
      paths.add(layer.image?.path ?? '');
      if (sent == null || sent[layer.key] != layer.revision) {
        changed.add(layer);
        floats += layer.sprites.length;
      }
    }

    final data = Float32List(floats);
    final changedKeys = Int32List(changed.length);
    final changedCounts = Int32List(changed.length);
    var at = 0;
    for (var i = 0; i < changed.length; i++) {
      final records = changed[i].sprites;
      changedKeys[i] = changed[i].key;
      changedCounts[i] = changed[i].count;
      data.setRange(at, at + records.length, records);
      at += records.length;
    }

    return {
      'spriteKeys': keys,
      'spriteFlags': flags,
      'spriteOrders': orders,
      'spriteRevisions': revisions,
      'spriteParams': params,
      'spritePaths': paths,
      'spriteChanged': changedKeys,
      'spriteChangedCounts': changedCounts,
      'spriteData': data,
    };
  }

  /// What the renderer needs to know about the terrains: three arrays read in
  /// step, laid out as orblit_renderer_apply_terrain says.
  ///
  /// A region's maps and a terrain's pictures are left out when [sent] says
  /// the renderer already holds them. A region 256 texels across is three
  /// quarters of a megabyte, and a brush stroke changes one or two.
  Map<String, Object>? _terrainMessage(Map<int, OrblitTerrainHeld>? sent) {
    if (terrain.isEmpty) return null;

    // The renderer refuses a key named twice, and with it the whole scene, so
    // the first terrain with a key is the one drawn.
    final keys = <int>{};
    final drawn = [
      for (final ground in terrain)
        if (keys.add(ground.key)) ground,
    ];
    assert(drawn.length == terrain.length, 'terrain keys are unique');

    var intCount = 1;
    var floatCount = 0;
    var dataLength = 0;
    final pictures = <bool>[];
    final arrived = <List<bool>>[];
    for (final ground in drawn) {
      final held = sent?[ground.key];
      final send = held == null || !held.holdsPictures(ground);
      final maps = [
        for (final region in ground.regions)
          held == null || !held.holdsRegion(ground, region),
      ];
      pictures.add(send);
      arrived.add(maps);
      intCount +=
          OrblitTerrain.headerInts +
          ground.regions.length * OrblitTerrain.regionInts;
      floatCount +=
          OrblitTerrain.stride + ground.sets.length * OrblitTerrainSet.stride;
      if (send) dataLength += ground.picturesLength;
      dataLength += maps.where((map) => map).length * ground.regionLength;
    }

    final ints = Int32List(intCount)..[0] = drawn.length;
    final floats = Float32List(floatCount);
    final data = Uint8List(dataLength);
    var intAt = 1;
    var floatAt = 0;
    var dataAt = 0;
    for (var t = 0; t < drawn.length; t++) {
      final ground = drawn[t];
      ground.writeInts(
        ints,
        intAt,
        picturesArrive: pictures[t],
        arrived: arrived[t],
      );
      intAt +=
          OrblitTerrain.headerInts +
          ground.regions.length * OrblitTerrain.regionInts;
      ground.writeFloats(floats, floatAt);
      floatAt +=
          OrblitTerrain.stride + ground.sets.length * OrblitTerrainSet.stride;
      if (pictures[t]) {
        ground.writePictures(data, dataAt);
        dataAt += ground.picturesLength;
      }
      for (var r = 0; r < ground.regions.length; r++) {
        if (!arrived[t][r]) continue;
        OrblitTerrain.writeRegion(data, dataAt, ground.regions[r]);
        dataAt += ground.regionLength;
      }
    }

    return {'terrainInts': ints, 'terrainFloats': floats, 'terrainData': data};
  }

  /// What the renderer needs to know about the populations.
  ///
  /// The transforms and colours are left out for any population whose
  /// revision the renderer already has. That is the entire point: six
  /// megabytes of transforms is not something to send sixty times a second in
  /// order to say that nothing moved.
  Map<String, Object>? _populationMessage(Map<int, int>? sent) {
    if (populations.isEmpty) return null;

    final keys = Int32List(populations.length);
    final counts = Int32List(populations.length);
    final meshes = Int32List(populations.length);
    final flags = Int32List(populations.length);
    final revisions = Int32List(populations.length);
    final ranges = Float32List(populations.length);
    final bounds = Float32List(populations.length * 6);
    final paths = <String>[];

    // Only the ones that have changed, packed end to end. The renderer takes
    // them in the order the changed keys appear.
    final changed = <OrblitPopulation>[];
    var members = 0;

    for (var i = 0; i < populations.length; i++) {
      final population = populations[i];
      keys[i] = population.key;
      counts[i] = population.count;
      flags[i] = population.flags;
      revisions[i] = population.revision;
      ranges[i] = population.range;

      meshes[i] = -1;
      if (population.mesh != null) {
        meshes[i] = paths.indexOf(population.mesh!);
        if (meshes[i] < 0) {
          meshes[i] = paths.length;
          paths.add(population.mesh!);
        }
      }

      bounds[i * 6 + 0] = population.minimum.x;
      bounds[i * 6 + 1] = population.minimum.y;
      bounds[i * 6 + 2] = population.minimum.z;
      bounds[i * 6 + 3] = population.maximum.x;
      bounds[i * 6 + 4] = population.maximum.y;
      bounds[i * 6 + 5] = population.maximum.z;

      if (sent == null || sent[population.key] != population.revision) {
        changed.add(population);
        members += population.count;
      }
    }

    final transforms = Float32List(members * 16);
    final colours = Float32List(members * 3);
    final changedKeys = Int32List(changed.length);
    var atTransform = 0;
    var atColour = 0;

    for (var i = 0; i < changed.length; i++) {
      changedKeys[i] = changed[i].key;
      transforms.setRange(
        atTransform,
        atTransform + changed[i].transforms.length,
        changed[i].transforms,
      );
      colours.setRange(
        atColour,
        atColour + changed[i].colours.length,
        changed[i].colours,
      );
      atTransform += changed[i].transforms.length;
      atColour += changed[i].colours.length;
    }

    return {
      'populationKeys': keys,
      'populationCounts': counts,
      'populationMeshes': meshes,
      'populationFlags': flags,
      'populationRevisions': revisions,
      'populationRanges': ranges,
      'populationBounds': bounds,
      'populationPaths': paths,
      'populationChanged': changedKeys,
      'populationTransforms': transforms,
      'populationColours': colours,
    };
  }
}

Float32List _vector(Vector3 value) =>
    Float32List.fromList([value.x, value.y, value.z]);
