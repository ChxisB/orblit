import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';

/// A project's assets in, one target's bundle out.
///
/// Usage:
///   `dart run orblit_asset:cook --target <name> [options]`
///
/// Options:
///   `--target <name>`  What to cook for: ${CookTargets.names}. Repeatable;
///                      each one gets its own folder under `--out`.
///   `--assets <dir>`   Where the project's assets are (default: assets).
///   `--out <dir>`      Where bundles are written (default: build/assets).
///   `--cache <dir>`    Where cooked bytes are kept between runs (default:
///                      `$ORBLIT_CACHE`, else `~/.cache/orblit-cook`).
///   `--cache-limit <n>` Megabytes the cache may hold before it evicts the
///                      least recently used entries, or 0 for no limit
///                      (default: 2048).
///   `--jobs <n>`       How many importers may run at once (default: 4).
///   `--quiet`          Print only the summary and anything that went wrong.
///   `--verbose`        Print every asset, including the ones already cached.
///
/// Exit codes: 0 when everything cooked, 1 when an asset failed, 2 when the
/// arguments did not make sense. A build reads the code; the text is for a
/// person.
Future<void> main(List<String> arguments) async {
  final options = _Options.parse(arguments);
  if (options == null) exit(2);
  if (options.help) {
    stdout.write(_usage);
    exit(0);
  }

  var failures = 0;
  for (final target in options.targets) {
    final project = CookProject(
      assetsPath: options.assetsPath,
      outPath: options.outPath,
      target: target,
      cachePath: options.cachePath,
      limitBytes: options.cacheLimitBytes,
      concurrency: options.jobs,
    );

    final CookReport report;
    try {
      report = await project.run(
        onResult: (result) => _report(result, options),
      );
    } on ArgumentError catch (error) {
      stderr.writeln('cook: ${error.message} (${error.invalidValue})');
      exit(2);
    }

    failures += report.count(CookStatus.failed);
    if (!options.quiet) {
      stdout.writeln(
        '${target.name}: ${report.summary} -> ${project.bundlePath}',
      );
    }
  }

  // A failure is worth repeating at the end. On a project of any size the
  // line that said what broke has scrolled away by the time the cook
  // finishes, and a build log is read from the bottom.
  if (failures > 0) {
    stderr.writeln(
      '\ncook: $failures asset${failures == 1 ? '' : 's'} failed. The bundle '
      'was written without ${failures == 1 ? 'it' : 'them'}.',
    );
    exit(1);
  }
}

void _report(CookResult result, _Options options) {
  switch (result.status) {
    case CookStatus.failed:
      stderr.writeln('  failed  ${result.id}: ${result.error}');
    case CookStatus.cooked:
      if (!options.quiet) stdout.writeln('  cooked  ${result.id}');
      for (final note in result.notes) {
        if (!options.quiet) stdout.writeln('          $note');
      }
    case CookStatus.cached:
      if (options.verbose) stdout.writeln('  cached  ${result.id}');
    case CookStatus.skipped:
      if (options.verbose) stdout.writeln('  skipped ${result.id}');
  }
}

String get _usage =>
    '''
Usage: dart run orblit_asset:cook --target <name> [options]

  --target <name>     What to cook for. Repeatable.
                      One of: ${CookTargets.names.join(', ')}
  --assets <dir>      Where the project's assets are (default: assets)
  --out <dir>         Where bundles are written (default: build/assets)
  --cache <dir>       Where cooked bytes are kept between runs
  --cache-limit <n>   Megabytes the cache may hold, or 0 for no limit
                      (default: 2048)
  --jobs <n>          How many importers may run at once (default: 4)
  --quiet             Print only the summary and anything that went wrong
  --verbose           Print every asset, including cached ones
  --help

Each target is written to <out>/<target>/, with a manifest.json naming what
every file is. A build copies one of those folders and nothing else, which is
how an iOS build ends up carrying ASTC and no BC.
''';

class _Options {
  _Options({
    required this.targets,
    required this.assetsPath,
    required this.outPath,
    required this.cachePath,
    required this.cacheLimitBytes,
    required this.jobs,
    required this.quiet,
    required this.verbose,
    required this.help,
  });

  final List<CookTarget> targets;
  final String assetsPath;
  final String outPath;
  final String? cachePath;
  final int? cacheLimitBytes;
  final int jobs;
  final bool quiet;
  final bool verbose;
  final bool help;

  /// Reads the arguments, or complains and returns null.
  ///
  /// Hand-rolled rather than pulled from a package, because `args` would be
  /// this package's second dependency and it is used in exactly one file.
  static _Options? parse(List<String> arguments) {
    final targets = <CookTarget>[];
    var assetsPath = 'assets';
    var outPath = 'build${Platform.pathSeparator}assets';
    String? cachePath;
    var cacheLimitMegabytes = 2048;
    var jobs = 4;
    var quiet = false;
    var verbose = false;
    var help = false;

    String? next(int index, String flag) {
      if (index + 1 >= arguments.length) {
        stderr.writeln('cook: $flag needs a value');
        return null;
      }
      return arguments[index + 1];
    }

    for (var i = 0; i < arguments.length; i++) {
      final argument = arguments[i];
      switch (argument) {
        case '--help' || '-h':
          help = true;
        case '--quiet':
          quiet = true;
        case '--verbose':
          verbose = true;
        case '--target':
          final value = next(i++, argument);
          if (value == null) return null;
          final target = CookTargets.find(value);
          if (target == null) {
            stderr.writeln(
              'cook: "$value" is not a target. The targets are '
              '${CookTargets.names.join(', ')}.',
            );
            return null;
          }
          targets.add(target);
        case '--assets':
          final value = next(i++, argument);
          if (value == null) return null;
          assetsPath = value;
        case '--out':
          final value = next(i++, argument);
          if (value == null) return null;
          outPath = value;
        case '--cache':
          final value = next(i++, argument);
          if (value == null) return null;
          cachePath = value;
        case '--cache-limit':
          final value = next(i++, argument);
          if (value == null) return null;
          final megabytes = int.tryParse(value);
          if (megabytes == null || megabytes < 0) {
            stderr.writeln(
              'cook: --cache-limit is megabytes, so "$value" is not one. '
              'Pass 0 for no limit.',
            );
            return null;
          }
          cacheLimitMegabytes = megabytes;
        case '--jobs':
          final value = next(i++, argument);
          if (value == null) return null;
          final count = int.tryParse(value);
          if (count == null || count < 1) {
            stderr.writeln(
              'cook: --jobs is how many at once, so it is at '
              'least 1, not "$value".',
            );
            return null;
          }
          jobs = count;
        default:
          stderr.writeln('cook: unknown option "$argument"');
          stderr.write(_usage);
          return null;
      }
    }

    if (!help && targets.isEmpty) {
      stderr.writeln(
        'cook: nothing to cook for. Pass --target, one of '
        '${CookTargets.names.join(', ')}.',
      );
      return null;
    }
    if (quiet && verbose) {
      stderr.writeln('cook: --quiet and --verbose ask for opposite things.');
      return null;
    }

    return _Options(
      targets: targets,
      assetsPath: assetsPath,
      outPath: outPath,
      cachePath: cachePath,
      // Zero means no limit, which the cache spells as null — it refuses a
      // limit of zero bytes, since a cache that may hold nothing is not one.
      cacheLimitBytes: cacheLimitMegabytes == 0
          ? null
          : cacheLimitMegabytes * 1024 * 1024,
      jobs: jobs,
      quiet: quiet,
      verbose: verbose,
      help: help,
    );
  }
}
