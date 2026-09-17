import 'dart:math' as math;

/// The questions a renderer answers about its device, in the order the C ABI
/// numbers them — `orblit_capability` in `orblit_renderer.h`, which
/// `native_contract_test.dart` holds this to.
enum OrblitCapability {
  backend,
  featureLevel,
  maxTextureSize,
  maxArrayTextureLayers,
  compressedFormats,
  halfFloatTextures,
  workerThreads,
  systemMemoryMegabytes,
}

/// Which graphics API is drawing, numbered as `OrblitBackend` is.
enum OrblitGraphicsApi { platformDefault, metal, vulkan, openGl, webGpu }

/// A family of block-compressed texture formats, with the bit
/// `orblit_format_family` gives it.
///
/// Families rather than formats, because a cooked set of textures is chosen
/// by family: the question is whether this device can sample ASTC at all, not
/// whether it can sample one particular block size of it.
enum OrblitTextureFamily {
  /// ETC2 — required by OpenGL ES 3.0, so every Android GPU has it.
  etc2(1 << 0),

  /// ASTC — Apple's A8 onwards and most Android GPUs of the last decade.
  astc(1 << 1),

  /// S3TC, the DXT formats — desktops.
  bc1to3(1 << 2),

  /// RGTC, one and two channels — desktops; the right home for normal maps.
  bc4and5(1 << 3),

  /// BPTC half float — desktops; environments in high dynamic range.
  bc6h(1 << 4),

  /// BPTC — desktops, and the best-looking of the colour formats there.
  bc7(1 << 5);

  const OrblitTextureFamily(this.bit);

  final int bit;
}

/// How much a device can be asked to do, as one word.
///
/// Most quality choices hang off this rather than off the numbers under it,
/// because the numbers only mean anything together: sixteen cores and a
/// feature-level-one GPU is still a device that draws the slim surface.
enum OrblitDeviceTier { low, medium, high }

/// What the device under a viewport can do, as its renderer measured it.
///
/// Asked for with [OrblitView.profileOf]. Every answer is a fact about the
/// device and the driver, measured once when the renderer started, so it can
/// be kept for the life of the view.
class OrblitDeviceProfile {
  const OrblitDeviceProfile({
    this.api = OrblitGraphicsApi.platformDefault,
    this.featureLevel = 1,
    this.maxTextureSize = 2048,
    this.maxArrayTextureLayers = 256,
    this.textureFamilies = const {},
    this.halfFloatTextures = false,
    this.workerThreads = 1,
    this.systemMemoryMegabytes,
  });

  /// The answers in [OrblitCapability]'s order, as the plugin sends them.
  ///
  /// An answer that is missing or -1 — a renderer older than the question, or
  /// one that could not tell — is read as the least a device this engine runs
  /// on can have, so an unknown device is treated as a modest one rather than
  /// a capable one. Guessing low costs some quality; guessing high costs a
  /// crash on the phone that could not keep up.
  factory OrblitDeviceProfile.fromCapabilities(List<int> answers) {
    int? known(OrblitCapability question) {
      if (question.index >= answers.length) return null;
      final answer = answers[question.index];
      return answer < 0 ? null : answer;
    }

    final api = known(OrblitCapability.backend);
    final formats = known(OrblitCapability.compressedFormats) ?? 0;
    final memory = known(OrblitCapability.systemMemoryMegabytes);
    return OrblitDeviceProfile(
      api: api != null && api < OrblitGraphicsApi.values.length
          ? OrblitGraphicsApi.values[api]
          : OrblitGraphicsApi.platformDefault,
      featureLevel: known(OrblitCapability.featureLevel) ?? 1,
      // The least OpenGL ES 3.0 promises, which is the floor of everything
      // this renderer starts on.
      maxTextureSize: known(OrblitCapability.maxTextureSize) ?? 2048,
      maxArrayTextureLayers:
          known(OrblitCapability.maxArrayTextureLayers) ?? 256,
      textureFamilies: {
        for (final family in OrblitTextureFamily.values)
          if (formats & family.bit != 0) family,
      },
      halfFloatTextures: known(OrblitCapability.halfFloatTextures) == 1,
      workerThreads: math.max(1, known(OrblitCapability.workerThreads) ?? 1),
      // Nought is the platform declining to say, not a machine with none.
      systemMemoryMegabytes: memory == null || memory == 0 ? null : memory,
    );
  }

  final OrblitGraphicsApi api;

  /// Filament's feature level, 0 to 3. Below 3 the renderer draws the slim
  /// lit surface: nine samplers rather than twelve.
  final int featureLevel;

  /// Texels on a side of the largest 2D texture.
  final int maxTextureSize;

  final int maxArrayTextureLayers;

  /// The block-compressed families this device can sample.
  final Set<OrblitTextureFamily> textureFamilies;

  /// Whether a half-float texture can be sampled and mipmapped, which an
  /// environment prefiltered at run time needs.
  final bool halfFloatTextures;

  /// Threads work can spread across. One in a browser without threads.
  final int workerThreads;

  /// Physical memory, or null where the platform will not say — a browser.
  final int? systemMemoryMegabytes;

  /// Whether this device draws the slim lit surface.
  bool get slimSurface => featureLevel < 3;

  bool supports(OrblitTextureFamily family) => textureFamilies.contains(family);

  /// Low, medium or high.
  ///
  /// Low is anything that cannot draw the standard surface, cannot hold a
  /// 4096 texture, has two threads or fewer, or has less than three gigabytes
  /// — the iOS simulator, iPhones before the 11, most of the Android phones in
  /// use and WebGL 2 on a small device all land here. High needs all of the
  /// standard surface, 8192 textures, eight threads and eight gigabytes it
  /// can *prove*: a device that will not say how much memory it has is not
  /// given the benefit of the doubt. Everything between is medium.
  ///
  /// Starting points, to be moved by measurement on real hardware rather
  /// than argued about.
  OrblitDeviceTier get tier {
    final memory = systemMemoryMegabytes;
    if (featureLevel < 3 ||
        maxTextureSize < 4096 ||
        workerThreads <= 2 ||
        (memory != null && memory < 3072)) {
      return OrblitDeviceTier.low;
    }
    if (maxTextureSize >= 8192 &&
        workerThreads >= 8 &&
        memory != null &&
        memory >= 8192) {
      return OrblitDeviceTier.high;
    }
    return OrblitDeviceTier.medium;
  }

  /// The largest side a texture should be loaded at on this device, before
  /// any the asset itself asks for.
  int get textureSizeBudget => math.min(maxTextureSize, switch (tier) {
    OrblitDeviceTier.low => 1024,
    OrblitDeviceTier.medium => 2048,
    OrblitDeviceTier.high => 4096,
  });

  /// Where a frame's texture upload budget starts, in kilobytes —
  /// [OrblitTextureLimits.uploadKilobytes] when an application does not say.
  ///
  /// Only where it starts. While textures arrive the renderer measures what
  /// a frame costs without uploading and with, and moves the budget to what
  /// fits a frame's slack — the larger of what is left of a sixtieth of a
  /// second and the scene's own cost — between bounds the tier sets. The
  /// same numbers as OrblitTextures.h, which says more.
  int get textureUploadKilobytes => switch (tier) {
    OrblitDeviceTier.low => 4096,
    OrblitDeviceTier.medium => 16384,
    OrblitDeviceTier.high => 32768,
  };

  /// The names to fetch for a cooked texture, best first, for a host that
  /// hands the renderer bytes rather than a file system.
  ///
  /// A cooked texture `x.ktx2` may have GPU-ready siblings beside it —
  /// `x.astc.ktx2`, `x.bc.ktx2` and `x.etc2.ktx2` — and the renderer, asked
  /// for `x.ktx2`, loads the first of them it finds holding a format this
  /// device samples, then `x.ktx2` itself. On disk it looks for them; a
  /// browser or a network cache has to provide them first, and this is which
  /// ones are worth fetching: the siblings of the families this device has,
  /// in the renderer's order, and the universal file last. The renderer still
  /// checks each file's actual format, so a candidate is only a candidate.
  ///
  /// Anything that is not a cooked set — a PNG, or a name that is already one
  /// sibling — is the only candidate for itself.
  List<String> textureCandidates(String path) {
    final lower = path.toLowerCase();
    if (!lower.endsWith('.ktx2')) return [path];
    final stem = path.substring(0, path.length - 5);
    final lowerStem = lower.substring(0, lower.length - 5);
    if (lowerStem.endsWith('.astc') ||
        lowerStem.endsWith('.bc') ||
        lowerStem.endsWith('.etc2')) {
      return [path];
    }
    final extension = path.substring(path.length - 5);
    return [
      if (supports(OrblitTextureFamily.astc)) '$stem.astc$extension',
      // BC7 for colour, BC5 for normals and BC4 for one channel, which is
      // what a cooked BC sibling holds.
      if (supports(OrblitTextureFamily.bc7) ||
          supports(OrblitTextureFamily.bc4and5))
        '$stem.bc$extension',
      if (supports(OrblitTextureFamily.etc2)) '$stem.etc2$extension',
      path,
    ];
  }

  /// How many Gaussian splats to draw at most.
  int get splatBudget => switch (tier) {
    OrblitDeviceTier.low => 250000,
    OrblitDeviceTier.medium => 1000000,
    OrblitDeviceTier.high => 3000000,
  };

  /// How much of a splat capture's view-dependent colour to read: the
  /// spherical-harmonic degree, 0 to 3. Each degree costs sixteen bytes a
  /// splat of GPU memory, which is the budget a small device runs out of.
  int get harmonicDegree => switch (tier) {
    OrblitDeviceTier.low => 0,
    OrblitDeviceTier.medium => 2,
    OrblitDeviceTier.high => 3,
  };

  /// Whether to sort splats on sixteen bits of depth rather than thirty-two —
  /// [OrblitSplats.coarseOrder]. On a low-tier device, where a full sort takes
  /// long enough that the order lags visibly behind a turning camera.
  bool get coarseSplatOrder => tier == OrblitDeviceTier.low;

  @override
  String toString() =>
      'OrblitDeviceProfile(${tier.name}: ${api.name}, feature level '
      '$featureLevel, textures to $maxTextureSize, '
      '${textureFamilies.map((f) => f.name).join('/')}, '
      '$workerThreads threads, ${systemMemoryMegabytes ?? '?'} MB)';
}
