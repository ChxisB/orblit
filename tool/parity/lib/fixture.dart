import 'dart:convert';
import 'dart:io';

/// Writes `dotnet/fixtures/<package>/<name>.json` under [root].
///
/// The file is stamped with the commit that last changed the package, so a
/// fixture says which Dart it came from and only changes when that Dart does.
/// One case per line keeps a regeneration diff readable.
void writeFixture({
  required String root,
  required String package,
  required String name,
  required List<Map<String, Object?>> cases,
}) {
  final body = cases.map((c) => '    ${jsonEncode(c)}').join(',\n');
  final file = File('$root/dotnet/fixtures/$package/$name.json');
  file.parent.createSync(recursive: true);
  file.writeAsStringSync(
    '{\n'
    '  "package": ${jsonEncode(package)},\n'
    '  "dartCommit": ${jsonEncode(_commitOf(root, package))},\n'
    '  "cases": [\n$body\n  ]\n'
    '}\n',
  );
}

String _commitOf(String root, String package) {
  final path = 'packages/$package';
  String git(List<String> args) {
    final result = Process.runSync('git', args, workingDirectory: root);
    if (result.exitCode != 0) {
      throw StateError('git ${args.join(' ')} failed: ${result.stderr}');
    }
    return (result.stdout as String).trim();
  }

  final commit = git(['log', '-1', '--format=%H', '--', path]);
  final dirty = git(['status', '--porcelain', '--', path]).isNotEmpty;
  return dirty ? '$commit+dirty' : commit;
}
