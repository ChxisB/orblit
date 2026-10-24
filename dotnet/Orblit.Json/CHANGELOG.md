# Changelog

## 0.3.0

- `PlainText` holds the text rules the asset, mesh and interface readers share:
  `IsSpace`, `IsPatternSpace`, `Trim`, `TrimEnd`, `SplitSpace`, `ToLower`,
  `ParseDouble` and `ParseInt`. Orblit.Asset and Orblit.Mesh read them from
  here. Nothing read or written changes.

## 0.2.1

- The string quoting sits in its own file, `JsonQuote.cs`, so Orblit.Codegen can
  compile the same source. Nothing written changes.

## 0.2.0

- `JsonText.Encode` writes a `double` so that `1.0` stays `1.0` and `1e21` is
  `1e+21`. NaN and infinity are refused.

## 0.1.0

- `JsonText.Decode` reads standard JSON. A whole number that fits 64 bits is a `long`, so `-0` is `0`. Any
  other number is a `double`, so `1e999` is infinity. A repeated key keeps its
  first place and takes the last value.
- `JsonText.Encode` writes, compact or indented, null, bool, whole numbers, strings, lists and maps.
- Nesting deeper than 512 is refused.
