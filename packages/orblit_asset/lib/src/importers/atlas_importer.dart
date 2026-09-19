import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_sprite/orblit_sprite.dart';

import '../asset_id.dart';
import '../import_settings.dart';
import '../cook.dart';
import '../importer.dart';

/// Packs a folder of sprites into as few pages as it can.
///
/// An atlas is not a file somebody has; it is a file somebody wants made out
/// of a folder they have. So the asset this claims is a small document naming
/// the sprites — `ui.atlas.json` — rather than any one of the pictures, and
/// the pictures it names are its dependencies. That is what makes it fit the
/// cook at all: adding a sprite to the folder edits the document, editing one
/// sprite changes a dependency, and either one repacks while nothing else in
/// the project moves.
///
/// ```json
/// {
///   "sprites": ["ui/play.png", "ui/pause.png", "ui/stop.png"],
///   "maxPageSize": 2048,
///   "padding": 2
/// }
/// ```
///
/// The sprites are listed rather than globbed. A glob would make the cook's
/// answer depend on what happened to be in a folder, which is the one thing a
/// content-addressed cache cannot key on — and it would quietly pick up the
/// `.png` somebody left in there while exporting.
///
/// The outputs are `page0.png`, `page0.json`, `page1.png` and so on: pages a
/// texture importer can then cook, and a descriptor per page saying which
/// rectangle each sprite landed in. Pages come out as PNG rather than as a
/// compressed texture because packing and encoding are separate jobs — the
/// pages want cooking per target the same as any other picture, and doing it
/// here would mean doing it again.
class AtlasImporter extends Importer {
  const AtlasImporter();

  @override
  String get name => 'atlas';

  @override
  Set<String> get extensions => const {'json'};

  @override
  int get version => 1;

  /// Only a document that says it is an atlas.
  ///
  /// `.json` is far too common an extension to claim outright — a scene's
  /// settings, a localisation table and somebody's notes all end in it. The
  /// double extension is the claim, the same way `.import.json` is.
  @override
  bool handles(AssetId id) => id.name.toLowerCase().endsWith('.atlas.json');

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => {
    'maxPageSize': _size(settings, 'maxPageSize'),
    'padding': _count(settings, 'padding'),
    'border': _count(settings, 'border'),
    'extrude': _count(settings, 'extrude'),
    'trimAlphaThreshold': _count(settings, 'trimAlphaThreshold'),
    'trim': settings.values['trim'],
    'mergeDuplicates': settings.values['mergeDuplicates'],
    'allowRotation': settings.values['allowRotation'],
    'square': settings.values['square'],
    'powerOfTwo': settings.values['powerOfTwo'],
    'heuristic': settings.values['heuristic'],
  };

  @override
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async => _spritesIn(id, bytes).toSet();

  @override
  Future<ImportResult> import(ImportRequest request) async {
    final document = _documentIn(request.id, request.bytes);
    final sprites = _spritesIn(request.id, request.bytes);
    if (sprites.isEmpty) {
      throw ImportFailure(
        request.id,
        'an atlas has to name at least one sprite, and this one names none',
      );
    }

    // The document's own settings sit under the sprites; anything in a
    // `.import.json` or given at the call wins, so that a project can cook the
    // same atlas differently for two targets without editing it.
    //
    // Nulls are dropped before the overlay rather than after it. Resolved
    // settings carry a key for every option whether or not anybody set one,
    // so laying them over the document whole would have every unset option
    // erase what the document said — which is the same as the document
    // having no settings at all.
    final overrides = {...request.settings}
      ..removeWhere((_, value) => value == null);
    final settled = {...document, ...overrides};

    final maxPageSize =
        settled['maxPageSize'] as int? ?? request.target.maxTextureSize ?? 2048;

    final packed = <AtlasSprite>[];
    for (final sprite in sprites) {
      final bytes = await request.source.read(sprite);
      final DecodedPng picture;
      try {
        picture = decodePng(bytes);
      } on PngFormatException catch (error) {
        throw ImportFailure(
          request.id,
          'the sprite $sprite could not be read: ${error.message}',
          cause: error,
        );
      }
      packed.add(
        AtlasSprite(
          // Named by id, so two folders can each have a `play.png` and the
          // descriptor still says which one a region is.
          name: '$sprite',
          width: picture.width,
          height: picture.height,
          pixels: picture.pixels,
        ),
      );
    }

    final result = packAtlas(
      packed,
      AtlasPackOptions(
        maxPageSize: maxPageSize,
        padding: settled['padding'] as int? ?? 2,
        border: settled['border'] as int? ?? 0,
        extrude: settled['extrude'] as int? ?? 1,
        trimAlphaThreshold: settled['trimAlphaThreshold'] as int? ?? 0,
        trim: settled['trim'] as bool? ?? true,
        mergeDuplicates: settled['mergeDuplicates'] as bool? ?? true,
        allowRotation: settled['allowRotation'] as bool? ?? false,
        square: settled['square'] as bool? ?? false,
        powerOfTwo: settled['powerOfTwo'] as bool? ?? true,
        heuristic: _heuristicNamed(request.id, settled['heuristic']),
      ),
    );

    // A sprite too large for a page at all is a failure rather than a note.
    // The alternative is an atlas that packed nine of ten sprites and a game
    // that draws nothing where the tenth was, found at run time.
    if (result.problems.isNotEmpty) {
      throw ImportFailure(
        request.id,
        'these sprites do not fit on a ${maxPageSize}x$maxPageSize page: '
        '${result.problems.join('; ')}',
      );
    }

    final outputs = <String, List<int>>{};
    for (var page = 0; page < result.pages.length; page++) {
      final it = result.pages[page];
      outputs['page$page.png'] = encodePng(it.width, it.height, it.pixels);
      outputs['page$page.json'] = utf8.encode(
        writeAtlas(it.toAtlas('page$page.png')),
      );
    }

    return ImportResult(
      outputs: outputs,
      notes: [
        'packed ${sprites.length} sprites onto ${result.pages.length} '
            'page${result.pages.length == 1 ? '' : 's'} '
            'with ${result.heuristic.name}',
        for (var page = 0; page < result.pages.length; page++)
          'page$page is ${result.pages[page].width}x'
              '${result.pages[page].height}, '
              '${(result.pages[page].fillRatio * 100).round()}% full',
      ],
    );
  }

  /// The document, or a failure saying what is wrong with it.
  static Map<String, Object?> _documentIn(AssetId id, Uint8List bytes) {
    final Object? parsed;
    try {
      parsed = jsonDecode(utf8.decode(bytes));
    } on FormatException catch (error) {
      throw ImportFailure(
        id,
        'this is not JSON: ${error.message}',
        cause: error,
      );
    }
    if (parsed is! Map<String, Object?>) {
      throw ImportFailure(id, 'an atlas document has to be a JSON object');
    }
    return parsed;
  }

  /// Which sprites an atlas names, in the order it names them.
  ///
  /// The order is kept because the packer is deterministic for a given list,
  /// and a list that came back in a different order would repack a page that
  /// nothing about actually changed.
  static List<AssetId> _spritesIn(AssetId id, Uint8List bytes) {
    final named = _documentIn(id, bytes)['sprites'];
    if (named == null) {
      throw ImportFailure(id, 'an atlas document needs a "sprites" list');
    }
    if (named is! List) {
      throw ImportFailure(
        id,
        '"sprites" has to be a list of asset names, and this is a '
        '${named.runtimeType}',
      );
    }

    final sprites = <AssetId>[];
    for (final entry in named) {
      if (entry is! String) {
        throw ImportFailure(
          id,
          'every entry in "sprites" has to be an asset name, and one is a '
          '${entry.runtimeType}',
        );
      }
      // Relative to the project, like a scene's references and unlike a
      // glTF's, with `./` as the way to say "beside me" — the same rule
      // SceneImporter follows, because both are documents a person writes.
      final sprite = entry.startsWith('./') || entry.startsWith('../')
          ? id.resolve(entry)
          : AssetId.tryParse(entry);
      if (sprite == null) {
        throw ImportFailure(id, '"$entry" is not an asset name');
      }
      sprites.add(sprite);
    }
    return sprites;
  }

  static MaxRectsHeuristic? _heuristicNamed(AssetId id, Object? value) {
    if (value == null) return null;
    const byName = {
      'short': MaxRectsHeuristic.bestShortSideFit,
      'long': MaxRectsHeuristic.bestLongSideFit,
      'area': MaxRectsHeuristic.bestAreaFit,
      'corner': MaxRectsHeuristic.bottomLeft,
      'contact': MaxRectsHeuristic.contactPoint,
    };
    final found = byName[value];
    if (found == null) {
      throw ImportFailure(
        id,
        '"$value" is not a packing heuristic. Use one of '
        '${byName.keys.join(', ')}, or leave it out to have the best one '
        'chosen',
      );
    }
    return found;
  }

  static int? _size(ImportSettings settings, String key) {
    final value = settings.values[key];
    if (value == null) return null;
    if (value is! int || value <= 0) {
      throw ArgumentError.value(value, key, 'has to be a size in texels');
    }
    return value;
  }

  static int? _count(ImportSettings settings, String key) {
    final value = settings.values[key];
    if (value == null) return null;
    if (value is! int || value < 0) {
      throw ArgumentError.value(value, key, 'cannot be negative');
    }
    return value;
  }
}
