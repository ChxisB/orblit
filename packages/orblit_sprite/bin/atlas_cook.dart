import 'dart:io';

import 'package:orblit_sprite/orblit_sprite.dart';

import 'src/png_codec.dart';

/// A folder of PNGs in, atlas pages and their descriptors out.
///
/// Usage:
///   `dart run orblit_sprite:atlas_cook <folder> [options]`
///
/// Options:
///   `--out <dir>`          Where pages and descriptors are written (default:
///                          alongside the input folder).
///   `--prefix <name>`      Base name for pages: `<prefix>0.png`,
///                          `<prefix>0.json`, and so on (default: atlas).
///   `--max-page-size <n>`  The largest a page's width or height may be
///                          (default: 2048). Pass the target device's
///                          texture size budget for a real cook.
///   `--padding <n>`        Texels kept between neighbours (default: 2).
///   `--border <n>`         Texels kept from the page edge (default: 0).
///   `--extrude <n>`        Edge texels copied into padding (default: 1).
///   `--trim-threshold <n>` Alpha at or below this trims (default: 0).
///   `--heuristic <name>`   One of short, long, area, corner, contact; left
///                          unset, every heuristic is tried and the best kept.
///   --no-rotate            Never turn a sprite a quarter turn.
///   --no-trim              Never cut transparent borders.
///   --no-merge             Never share a rectangle between identical sprites.
///   --square               Force every page's width to equal its height.
///   --no-power-of-two      Do not round a page's size up to a power of two.
///
/// This is a starting point for Phase 6's `dart run orblit_asset:cook`, not
/// a replacement for it: it packs one folder into one atlas and stops, with
/// none of the content-addressed caching or import-settings files that
/// belong to the asset pipeline as a whole.
Future<void> main(List<String> arguments) async {
  if (arguments.isEmpty || arguments.first == '--help') {
    stdout.writeln('Usage: dart run orblit_sprite:atlas_cook <folder> [options]');
    exit(arguments.isEmpty ? 2 : 0);
  }

  final input = Directory(arguments.first);
  if (!input.existsSync()) {
    stderr.writeln('No such folder: ${input.path}');
    exit(2);
  }

  String? outDir;
  var prefix = 'atlas';
  var maxPageSize = 2048;
  var padding = 2;
  var border = 0;
  var extrude = 1;
  var trimThreshold = 0;
  var allowRotation = true;
  var trim = true;
  var mergeDuplicates = true;
  var square = false;
  var powerOfTwo = true;
  MaxRectsHeuristic? heuristic;

  const heuristicsByName = {
    'short': MaxRectsHeuristic.bestShortSideFit,
    'long': MaxRectsHeuristic.bestLongSideFit,
    'area': MaxRectsHeuristic.bestAreaFit,
    'corner': MaxRectsHeuristic.bottomLeft,
    'contact': MaxRectsHeuristic.contactPoint,
  };

  for (var i = 1; i < arguments.length; i++) {
    final arg = arguments[i];
    String next() => arguments[++i];
    switch (arg) {
      case '--out':
        outDir = next();
      case '--prefix':
        prefix = next();
      case '--max-page-size':
        maxPageSize = int.parse(next());
      case '--padding':
        padding = int.parse(next());
      case '--border':
        border = int.parse(next());
      case '--extrude':
        extrude = int.parse(next());
      case '--trim-threshold':
        trimThreshold = int.parse(next());
      case '--heuristic':
        final name = next();
        heuristic = heuristicsByName[name];
        if (heuristic == null) {
          stderr.writeln('Unknown heuristic "$name"; want one of ${heuristicsByName.keys.join(', ')}.');
          exit(2);
        }
      case '--no-rotate':
        allowRotation = false;
      case '--no-trim':
        trim = false;
      case '--no-merge':
        mergeDuplicates = false;
      case '--square':
        square = true;
      case '--no-power-of-two':
        powerOfTwo = false;
      default:
        stderr.writeln('Unknown option "$arg".');
        exit(2);
    }
  }

  final files = input
      .listSync()
      .whereType<File>()
      .where((f) => f.path.toLowerCase().endsWith('.png'))
      .toList()
    ..sort((a, b) => a.path.compareTo(b.path));

  if (files.isEmpty) {
    stderr.writeln('No .png files in ${input.path}.');
    exit(1);
  }

  final sprites = <AtlasSprite>[];
  for (final file in files) {
    final name = file.uri.pathSegments.last.replaceAll(RegExp(r'\.png$', caseSensitive: false), '');
    try {
      final decoded = decodePng(file.readAsBytesSync());
      sprites.add(AtlasSprite(name: name, width: decoded.width, height: decoded.height, pixels: decoded.pixels));
    } on PngFormatException catch (error) {
      stderr.writeln('Skipped ${file.path}: $error');
    }
  }

  final options = AtlasPackOptions(
    maxPageSize: maxPageSize,
    padding: padding,
    border: border,
    extrude: extrude,
    trimAlphaThreshold: trimThreshold,
    allowRotation: allowRotation,
    trim: trim,
    mergeDuplicates: mergeDuplicates,
    square: square,
    powerOfTwo: powerOfTwo,
    heuristic: heuristic,
  );

  final stopwatch = Stopwatch()..start();
  final result = await packAtlasInBackground(sprites, options);
  stopwatch.stop();

  final destination = Directory(outDir ?? input.path)..createSync(recursive: true);
  for (var i = 0; i < result.pages.length; i++) {
    final page = result.pages[i];
    final imageName = '$prefix$i.png';
    File('${destination.path}/$imageName').writeAsBytesSync(encodePng(page.width, page.height, page.pixels));
    File(
      '${destination.path}/$prefix$i.json',
    ).writeAsStringSync(writeAtlas(page.toAtlas(imageName)));
  }

  stdout.writeln(
    'orblit_sprite:atlas_cook: ${sprites.length} sprite(s) into '
    '${result.pages.length} page(s) with ${result.heuristic.name}, '
    '${stopwatch.elapsedMilliseconds} ms',
  );
  for (var i = 0; i < result.pages.length; i++) {
    final page = result.pages[i];
    stdout.writeln(
      '  $prefix$i.png  ${page.width}x${page.height}  '
      '${(page.fillRatio * 100).toStringAsFixed(1)}% full  '
      '${page.regions.length} region(s)',
    );
  }
  if (result.problems.isNotEmpty) {
    stderr.writeln('${result.problems.length} sprite(s) could not be placed:');
    for (final problem in result.problems) {
      stderr.writeln('  $problem');
    }
    exit(1);
  }
}
