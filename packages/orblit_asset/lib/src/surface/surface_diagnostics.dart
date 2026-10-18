import 'surface_translator.dart';

final _matcError = RegExp(r'^ERROR: \d+:(\d+): (.*)$', multiLine: true);

/// What matc said, with each line number replaced by the file and line the
/// author wrote. Empty when matc's text names no line.
List<String> surfaceDiagnostics(String matcOutput, SurfaceTranslation from) => [
  for (final match in _matcError.allMatches(matcOutput))
    if (!match.group(2)!.startsWith("'' : compilation terminated"))
      '${from.originOf(int.parse(match.group(1)!))}: ${match.group(2)}',
];
