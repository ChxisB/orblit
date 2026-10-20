# Changelog

## 0.2.1

- The string quoting sits in its own file, `DartQuote.cs`, so Orblit.Codegen can
  compile the same source. Nothing written changes.

## 0.2.0

- `DartJson.Encode` writes a `double` the way `jsonEncode` does, so `1.0`
  stays `1.0` and `1e21` is `1e+21`. NaN and infinity are refused, as Dart
  refuses them.

## 0.1.0

- `DartJson.Decode` reads what Dart's `jsonDecode` reads and refuses what it
  refuses. A whole number that fits 64 bits is a `long`, so `-0` is `0`. Any
  other number is a `double`, so `1e999` is infinity. A repeated key keeps its
  first place and takes the last value.
- `DartJson.Encode` writes what `jsonEncode` and `JsonEncoder.withIndent`
  write, for null, bool, whole numbers, strings, lists and maps.
- Nesting deeper than 512 is refused. Dart has no such limit.
