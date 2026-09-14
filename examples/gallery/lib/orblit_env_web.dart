// The ORBLIT_* switches in a browser, where there is no process environment.
//
// The query string is the web's spelling of a shell prefix:
//
//   ORBLIT_EXAMPLE=Lights ORBLIT_LIGHT=spot flutter run     (everywhere else)
//   ?ORBLIT_EXAMPLE=Lights&ORBLIT_LIGHT=spot                (here)
//
// Chosen over `--dart-define` deliberately: a define is baked in at compile
// time, and the whole point of these switches is sweeping a setting across
// runs — a screenshot per example, a frame per detail tier — which on the web
// means changing a URL rather than rebuilding. `String.fromEnvironment` is
// still honoured underneath for anything a build did define, so
// `--dart-define=ORBLIT_EXAMPLE=Lights` also works; the query string wins
// where both name the same switch.
library;

import 'package:flutter/foundation.dart';

/// Every `ORBLIT_*` switch this page was opened with.
Map<String, String> readOrblitEnvironment() => {
  // A handful of defines are worth honouring without a query string, so a
  // built page can carry a default. Only those named here can be read at all:
  // String.fromEnvironment needs a literal, so there is no way to enumerate
  // what a build defined.
  for (final name in _defined)
    if (const bool.hasEnvironment('') || true)
      if (_define(name) case final value? when value.isNotEmpty) name: value,
  ...Uri.base.queryParameters,
};

/// Where a gallery's diagnostics go in a browser: the console, which is what
/// a headless capture records alongside its screenshot.
void warn(String message) => debugPrint(message);

/// The switches a `--dart-define` may set. The query string can set any.
const List<String> _defined = [
  'ORBLIT_EXAMPLE',
  'ORBLIT_SECONDS',
  'ORBLIT_BACKEND',
];

String? _define(String name) => switch (name) {
  'ORBLIT_EXAMPLE' => const String.fromEnvironment('ORBLIT_EXAMPLE'),
  'ORBLIT_SECONDS' => const String.fromEnvironment('ORBLIT_SECONDS'),
  'ORBLIT_BACKEND' => const String.fromEnvironment('ORBLIT_BACKEND'),
  _ => null,
};
