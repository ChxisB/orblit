# Changelog

## 0.1.0

- `ProgressSchema` with `Whole` and `Slice` names, `ProgressTable` and
  `Changes`. A save from a schema with fewer names loads under one with more.
- `Condition` compares one named value with a number, with `Compare` for the
  six operators. A list holds when every condition does. Quests and dialogue
  read them from JSON as `{"name": ..., "op": ..., "value": ...}`.
- `ProgressSchema.fits(name, value)` says whether a value fits a name's bits.
