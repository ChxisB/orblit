import 'dart:typed_data';

import 'asset_id.dart';

/// Somewhere assets can be read from, by id.
///
/// Two questions and nothing more, so that the places assets actually live —
/// a directory while developing, a Flutter bundle in a shipped app, a map in a
/// test — can all stand behind the same code without any of them having to
/// pretend to be a file system. There is no listing: a Flutter bundle cannot
/// list itself cheaply, and anything that needs to know which assets exist
/// should be reading a manifest rather than looking around.
abstract interface class AssetSource {
  /// The bytes of [id], or an [AssetNotFound] when this source does not have
  /// it.
  ///
  /// Thrown rather than returned as null, because an asset asked for by name
  /// and missing is nearly always a mistake — a typo, a file never copied —
  /// and a null passed quietly along turns up as a blank texture three calls
  /// later, far from the name that was wrong.
  Future<Uint8List> read(AssetId id);

  /// Whether [id] can be read from here.
  ///
  /// A hint rather than a promise: an asset can go away between the asking
  /// and the reading, so code that goes on to read still has to be ready for
  /// [AssetNotFound].
  Future<bool> exists(AssetId id);
}

/// An asset that was asked for and is not there.
class AssetNotFound implements Exception {
  const AssetNotFound(this.id, {this.reason});

  final AssetId id;

  /// Why, when the source knows something more useful than that it is not
  /// there — that the whole directory is missing, say.
  final String? reason;

  String get message => reason == null
      ? 'There is no asset "$id".'
      : 'There is no asset "$id": $reason.';

  @override
  String toString() => message;
}

/// Assets held in memory, for tests and for tools that build them on the fly.
///
/// The bytes are copied on the way in and on the way out. Every other source
/// hands back fresh bytes on each read, so code is free to decode in place;
/// this one doing the same means a test cannot pass only because nothing
/// happened to write into a shared buffer.
class MemoryAssetSource implements AssetSource {
  MemoryAssetSource([Map<AssetId, List<int>> assets = const {}])
    : _assets = {
        for (final MapEntry(:key, :value) in assets.entries)
          key: Uint8List.fromList(value),
      };

  final Map<AssetId, Uint8List> _assets;

  @override
  Future<Uint8List> read(AssetId id) async {
    final bytes = _assets[id];
    if (bytes == null) throw AssetNotFound(id);
    return Uint8List.fromList(bytes);
  }

  @override
  Future<bool> exists(AssetId id) async => _assets.containsKey(id);
}

/// Assets read through a function, with null meaning there is no such asset.
///
/// How something this package cannot depend on gets in. A Flutter app passes
/// a function over its `rootBundle`, translating the error that throws for a
/// missing key into null, and everything downstream sees an ordinary source
/// without this package ever importing Flutter. Anything else the function
/// throws is passed on untouched: a failure to read is not the same thing as
/// the asset not being there, and turning one into the other hides it.
class CallbackAssetSource implements AssetSource {
  const CallbackAssetSource(Future<Uint8List?> Function(AssetId id) load)
    : _load = load;

  final Future<Uint8List?> Function(AssetId id) _load;

  @override
  Future<Uint8List> read(AssetId id) async {
    final bytes = await _load(id);
    if (bytes == null) throw AssetNotFound(id);
    return bytes;
  }

  /// Whether the function gives anything for [id].
  ///
  /// This loads the asset to find out, since a bare function has no cheaper
  /// way to be asked — nor does a Flutter bundle behind one. Where the bytes
  /// are wanted anyway, reading and catching [AssetNotFound] does the work
  /// once rather than twice.
  @override
  Future<bool> exists(AssetId id) async => await _load(id) != null;
}

/// Several sources searched in order, the first one that has an asset winning.
///
/// For one set of assets laid over another: a project's own files over the
/// engine's defaults, a mod over the game it modifies, a directory being
/// edited over the bundle that shipped. Whatever is in an earlier layer hides
/// the same id further down.
class LayeredAssetSource implements AssetSource {
  LayeredAssetSource(List<AssetSource> layers)
    : layers = List.unmodifiable(layers);

  /// The sources, searched from the first.
  final List<AssetSource> layers;

  /// The bytes from the first layer that has [id].
  ///
  /// Each layer is read rather than asked whether it has the asset first,
  /// because for some sources asking is reading, and doing it twice doubles
  /// the cost of every hit. Only [AssetNotFound] moves on to the next layer:
  /// any other failure is a layer that has the asset and could not read it,
  /// and quietly serving an older copy from underneath would hide that.
  @override
  Future<Uint8List> read(AssetId id) async {
    for (final layer in layers) {
      try {
        return await layer.read(id);
      } on AssetNotFound {
        // Not in this layer; a later one may have it.
      }
    }
    throw AssetNotFound(
      id,
      reason: layers.isEmpty
          ? 'there are no sources to look in'
          : 'none of the ${layers.length} sources has it',
    );
  }

  @override
  Future<bool> exists(AssetId id) async {
    for (final layer in layers) {
      if (await layer.exists(id)) return true;
    }
    return false;
  }
}
