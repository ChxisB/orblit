# Changelog

## 0.1.0

- Port of `orblit_quest` 0.1.0.
- `Quest` is `QuestDefinition`, because a type named like its
  namespace is flagged by CA1724.
- `questExtension` and `questStageName` are `QuestNames.Extension` and
  `QuestNames.StageName`.
- A stage index is a `long`, so `StageOf` and
  `Advance` take and give `long`.
- A rule a quest breaks throws an `ArgumentException` whose message reads
  `name rule (value).`. `Parse`
  passes that text on in a `QuestFormatException`.
- A quest key is checked with `\z`, not `$`, so a key ending in a new line is
  refused.
