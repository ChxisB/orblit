import 'surface_error.dart';

/// One line of a surface source, with where it was written.
final class SurfaceLine {
  const SurfaceLine(this.origin, this.text);

  final SurfaceOrigin origin;
  final String text;

  SurfaceLine withText(String replacement) => SurfaceLine(origin, replacement);
}

/// Returns the text of a project path, or null when there is no such file.
typedef SurfaceReader = String? Function(String path);

/// The longest chain of includes. Deep enough for any real layout and short
/// enough that a cycle the check below missed would still stop.
const maxIncludeDepth = 25;

final _includeLine = RegExp(r'^\s*#\s*include\s+"([^"]*)"\s*$');

/// The source as lines with comments blanked and every `#include` replaced by
/// the lines it names, in order.
///
/// A comment becomes spaces rather than disappearing so that the columns and
/// the line count of the author's text stay what the author sees.
List<SurfaceLine> expandSurface(
  String path,
  String text,
  SurfaceReader read, {
  List<String> includes = const [],
  Set<String>? seen,
}) {
  final out = <SurfaceLine>[];
  final lines = _blankComments(text).split('\n');
  for (var i = 0; i < lines.length; i++) {
    final at = SurfaceOrigin(path, i + 1);
    final match = _includeLine.firstMatch(lines[i]);
    if (match == null) {
      out.add(SurfaceLine(at, lines[i]));
      continue;
    }
    final target = _resolve(path, match.group(1)!, at);
    final chain = [...includes, path];
    _checkChain(chain, target, at);
    seen?.add(target);
    final body = read(target);
    if (body == null) throw SurfaceError(at, 'no file `$target` to include');
    out.addAll(expandSurface(target, body, read, includes: chain, seen: seen));
  }
  return out;
}

void _checkChain(List<String> chain, String target, SurfaceOrigin at) {
  if (chain.contains(target)) {
    throw SurfaceError(
      at,
      'include cycle: ${[...chain, target].join(' includes ')}',
    );
  }
  if (chain.length >= maxIncludeDepth) {
    throw SurfaceError(at, 'includes nest more than $maxIncludeDepth deep');
  }
}

/// A leading slash is the project root. Anything else is beside the file that
/// includes it.
String _resolve(String from, String named, SurfaceOrigin at) {
  if (!named.endsWith('.oshaderinc')) {
    throw SurfaceError(at, 'only .oshaderinc files can be included');
  }
  final parts = named.startsWith('/')
      ? <String>[]
      : (from.split('/')..removeLast());
  for (final part in named.split('/')) {
    if (part == '..') {
      if (parts.isEmpty) throw SurfaceError(at, '`$named` leaves the project');
      parts.removeLast();
    } else if (part.isNotEmpty && part != '.') {
      parts.add(part);
    }
  }
  return parts.join('/');
}

String _blankComments(String text) {
  final out = StringBuffer();
  var block = false;
  for (var i = 0; i < text.length; i++) {
    final c = text[i];
    final next = i + 1 < text.length ? text[i + 1] : '';
    if (block && c == '*' && next == '/') {
      block = false;
      out.write('  ');
      i++;
    } else if (block) {
      out.write(c == '\n' ? c : ' ');
    } else if (c == '/' && next == '*') {
      block = true;
      out.write('  ');
      i++;
    } else if (c == '/' && next == '/') {
      i = _skipLine(text, i, out);
    } else {
      out.write(c);
    }
  }
  return out.toString();
}

/// Blanks to the end of the line and returns the index of its last character.
int _skipLine(String text, int from, StringBuffer out) {
  var i = from;
  while (i < text.length && text[i] != '\n') {
    out.write(' ');
    i++;
  }
  if (i < text.length) out.write('\n');
  return i;
}
