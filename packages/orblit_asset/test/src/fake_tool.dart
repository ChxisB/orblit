// A stand-in for one of the native cook programs.
//
// It records the arguments it was given into a file and writes whatever
// outputs it was told to, so a test can assert which flags an importer chose
// without building the real encoder — and, more importantly, can assert them
// through an actual process, which is the only way the quoting, the working
// directory and the exit code are really exercised.
//
//   fake_tool <record-file> <spec> [arguments...]
//
// The spec says what to do, as `key=value` pairs separated by semicolons:
//
//   exit=N            leave with this code (default 0)
//   say=TEXT          print TEXT to stderr
//   write=PATH        write a file at PATH, its contents being its own name
//   writeInto=I:NAME  write NAME into the directory named by argument I
//   writeArg=I        write a file at whatever argument I says
//   writeAfter=FLAG:NAME  write NAME inside the path FLAG names, whether it
//                     was given as `FLAG path` or as `FLAG=path`, and do
//                     nothing when this run was not given FLAG — one spec
//                     covers every call an importer makes, and an importer
//                     that runs the tool twice passes different flags each
//                     time
import 'dart:io';

void main(List<String> arguments) {
  final record = File(arguments[0]);
  final spec = arguments[1];
  final rest = arguments.sublist(2);
  record.writeAsStringSync('${rest.join('\n')}\n\n', mode: FileMode.append);

  var code = 0;
  for (final instruction in spec.split(';')) {
    if (instruction.isEmpty) continue;
    final split = instruction.indexOf('=');
    final key = instruction.substring(0, split);
    final value = instruction.substring(split + 1);
    switch (key) {
      case 'exit':
        code = int.parse(value);
      case 'say':
        stderr.writeln(value);
      case 'write':
        _write(value);
      case 'writeArg':
        _write(rest[int.parse(value)]);
      case 'writeInto':
        final parts = value.split(':');
        final directory = rest[int.parse(parts[0])];
        _write(
          '$directory${directory.endsWith(Platform.pathSeparator) ? '' : Platform.pathSeparator}${parts[1]}',
        );
      case 'writeAfter':
        final parts = value.split(':');
        final joined = rest.firstWhere(
          (argument) => argument.startsWith('${parts[0]}='),
          orElse: () => '',
        );
        final String directory;
        if (joined.isNotEmpty) {
          directory = joined.substring(parts[0].length + 1);
        } else {
          final at = rest.indexOf(parts[0]);
          if (at < 0) break;
          if (at + 1 >= rest.length) {
            stderr.writeln('fake_tool: no argument after ${parts[0]}');
            exit(3);
          }
          directory = rest[at + 1];
        }
        _write('$directory${Platform.pathSeparator}${parts[1]}');
      default:
        stderr.writeln('fake_tool: unknown instruction "$key"');
        exit(3);
    }
  }
  exit(code);
}

void _write(String path) {
  final file = File(path);
  file.parent.createSync(recursive: true);
  file.writeAsStringSync('contents of ${file.uri.pathSegments.last}');
}
