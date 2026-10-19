/// 32-bit wrapping multiply that stays exact in a double, so the same numbers
/// come out on the web as on the VM.
int _times(int a, int b) {
  final x = a & 0xFFFFFFFF;
  return ((x & 0xFFFF) * b + (((x >> 16) * b) & 0xFFFF) * 0x10000) & 0xFFFFFFFF;
}

/// A number from 0 up to, and not including, 1 that depends on its three
/// arguments and nothing else.
///
/// Written out rather than taken from `Random` because it must give the same
/// answer on every machine and for ever: a particle is found again by asking
/// for it, not by remembering it.
double unitAt(int seed, int index, int channel) {
  var h =
      _times(index, 0x9E3779B1) ^
      _times(channel + 1, 0x85EBCA6B) ^
      _times(seed, 0xC2B2AE35);
  h ^= h >> 16;
  h = _times(h, 0x7FEB352D);
  h ^= h >> 15;
  h = _times(h, 0x846CA68B);
  h ^= h >> 16;
  return h / 0x100000000;
}
