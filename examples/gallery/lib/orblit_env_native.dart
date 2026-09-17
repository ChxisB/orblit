// The process's real environment variables.
//
// Moved here from main.dart unchanged when the web build needed the reading
// to be conditional; the commentary below is the original, because the reason
// for the ffi path has not changed.
//
// `Platform.environment` already reads them correctly on every desktop
// platform this gallery runs on — that is how every `ORBLIT_*` switch has
// driven it from a shell for as long as they have existed. On the iOS
// simulator it comes back an empty map regardless of what the process was
// actually launched with: dumping it to a file mid-run showed
// `Platform.environment.length == 0` while `SIMCTL_CHILD_`-prefixed variables
// from that same launch were visible to `getenv` on the native side of the
// very same process. Nothing is wrong with the variables — `dart:io` is
// simply not reading them on this platform, which is also why every example
// looked the same however `ORBLIT_EXAMPLE` was set: the chooser always fell
// back to the first one.
//
// Read once through dart:ffi, straight from libc rather than through
// `dart:io`, and used as the base a real `Platform.environment` reading is
// then layered onto — so anywhere it already worked (every desktop platform),
// the result is exactly what it always was, and anywhere it did not (iOS),
// the native reading fills the gap.
library;

import 'dart:convert';
import 'dart:ffi' as ffi;
import 'dart:io';

/// Every `ORBLIT_*` switch this process was launched with.
Map<String, String> readOrblitEnvironment() {
  // Lowest of all, the few switches a build can carry as `--dart-define`s:
  // an Android app is launched by the system, with no environment anybody can
  // set, so a build that names its example is the only way to point an
  // emulator at one. Anything the environment says overrides these.
  final result = <String, String>{
    for (final entry in _defined.entries)
      if (entry.value.isNotEmpty) entry.key: entry.value,
  };
  try {
    result.addAll(_readNativeEnvironment());
  } catch (error) {
    // No native environment either — carries on with whatever dart:io
    // provides below, which on a platform without _NSGetEnviron is the same
    // answer this file always acted on.
    warn('[orblit] could not read the native environment: $error');
  }
  // Layered on top rather than checked first: wherever dart:io already has an
  // answer, that answer wins, so this changes nothing anywhere it did not
  // need to.
  result.addAll(Platform.environment);
  return result;
}

/// Where a gallery's diagnostics go: standard error, which is what a shell
/// sweep redirects and reads.
void warn(String message) => stderr.writeln(message);

/// `environ`, read straight through libc via `_NSGetEnviron` — Darwin's way
/// to reach the global from a position-independent image, present in
/// libSystem on macOS and iOS alike. Not behind a platform check: on macOS it
/// reads the same process environment `Platform.environment` already does, so
/// layering that on top afterwards leaves this platform untouched; on iOS it
/// is the one channel proven to still carry it.
Map<String, String> _readNativeEnvironment() {
  final nsGetEnviron = ffi.DynamicLibrary.process().lookupFunction<
      ffi.Pointer<ffi.Pointer<ffi.Pointer<ffi.Uint8>>> Function(),
      ffi.Pointer<ffi.Pointer<ffi.Pointer<ffi.Uint8>>> Function()>(
    '_NSGetEnviron',
  );
  final environ = nsGetEnviron().value; // char **, i.e. environ itself
  final result = <String, String>{};
  for (var i = 0; environ[i].address != 0; i++) {
    final entry = _readCString(environ[i]);
    final equals = entry.indexOf('=');
    // A name-less entry ('=' as the first character) has been seen on Apple
    // platforms; dart:io's own parser discards it the same way.
    if (equals > 0) {
      result[entry.substring(0, equals)] = entry.substring(equals + 1);
    }
  }
  return result;
}

String _readCString(ffi.Pointer<ffi.Uint8> chars) {
  final bytes = <int>[];
  for (var i = 0; chars[i] != 0; i++) {
    bytes.add(chars[i]);
  }
  return utf8.decode(bytes, allowMalformed: true);
}

/// The switches a `--dart-define` may set off the web. `String.fromEnvironment`
/// needs a literal, so they are named one by one.
const Map<String, String> _defined = {
  'ORBLIT_EXAMPLE': String.fromEnvironment('ORBLIT_EXAMPLE'),
  'ORBLIT_SECONDS': String.fromEnvironment('ORBLIT_SECONDS'),
  'ORBLIT_CIRCLING': String.fromEnvironment('ORBLIT_CIRCLING'),
  'ORBLIT_MODEL': String.fromEnvironment('ORBLIT_MODEL'),
  'ORBLIT_CLIP': String.fromEnvironment('ORBLIT_CLIP'),
  'ORBLIT_VARIANT': String.fromEnvironment('ORBLIT_VARIANT'),
  'ORBLIT_DAYLIGHT': String.fromEnvironment('ORBLIT_DAYLIGHT'),
  'ORBLIT_SAMPLES': String.fromEnvironment('ORBLIT_SAMPLES'),
  'ORBLIT_TEXTURES': String.fromEnvironment('ORBLIT_TEXTURES'),
  'ORBLIT_TEXTURE_FILES': String.fromEnvironment('ORBLIT_TEXTURE_FILES'),
  'ORBLIT_PICTURE': String.fromEnvironment('ORBLIT_PICTURE'),
  'ORBLIT_TEXTURE_SIZE': String.fromEnvironment('ORBLIT_TEXTURE_SIZE'),
};
