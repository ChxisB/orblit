# Changelog

## 0.1.0

- Port of `orblit_dialogue` 0.1.0.
- The Dart `Dialogue` is `DialogueDefinition`, because a type named like its
  namespace is flagged by CA1724.
- `dialogueExtension` is `DialogueNames.Extension`.
- `Before` is a delegate, `ProgressTable Before(ProgressTable table)`.
- A rule a dialogue breaks throws an `ArgumentException` whose message reads
  `name rule (value).`, the text Dart's `ArgumentError.value` held. `Parse`
  passes that text on in a `DialogueFormatException`. `Printed.Dart` prints a
  value as Dart does for that text.
- The format version is read as a `long`. A double such as `1.0` is no
  version.
- `BeforeNames` is an ordered list without repeats, where Dart gave a `Set`.
