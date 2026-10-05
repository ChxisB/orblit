import 'clip.dart';

/// Maps a common cycle to evenly spaced named contacts in a clip.
///
/// The first marker is phase zero. Missing or repeated markers, or markers
/// in a different cyclic order, leave the clip on its ordinary lap clock.
double syncedLap(ClipDocument clip, double lap, List<String> names) {
  if (names.length < 2 || clip.duration <= 0) return lap;
  final anchors = <double>[];
  for (final name in names) {
    final found = clip.marks.where((mark) => mark.name == name).toList();
    if (found.length != 1) return lap;
    final phase = found.single.at / clip.duration;
    if (phase < 0 || phase >= 1) return lap;
    var at = phase;
    if (anchors.isNotEmpty && at <= anchors.last) at += 1;
    if (anchors.isNotEmpty && at <= anchors.last) return lap;
    anchors.add(at);
  }
  if (anchors.last >= anchors.first + 1) return lap;
  anchors.add(anchors.first + 1);
  final whole = lap.floorToDouble();
  final along = (lap - whole) * names.length;
  final segment = along.floor().clamp(0, names.length - 1);
  return whole +
      anchors[segment] +
      (anchors[segment + 1] - anchors[segment]) * (along - segment);
}
