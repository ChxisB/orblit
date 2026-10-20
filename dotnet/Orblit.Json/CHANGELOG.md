# Changelog

## 0.1.0

- `DartJson.Decode` reads what Dart's `jsonDecode` reads and refuses what it
  refuses. A whole number that fits 64 bits is a `long`, so `-0` is `0`. Any
  other number is a `double`, so `1e999` is infinity. A repeated key keeps its
  first place and takes the last value.
- `DartJson.Encode` writes what `jsonEncode` and `JsonEncoder.withIndent`
  write, for null, bool, whole numbers, strings, lists and maps. A double is
  refused until a package needs to write one.
- Nesting deeper than 512 is refused. Dart has no such limit.
