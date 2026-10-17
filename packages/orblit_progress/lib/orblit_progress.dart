/// Named small integers for a game's progress.
///
/// A [ProgressSchema] names the numbers. A [ProgressTable] holds them and
/// never changes: writing one gives a new table and the [Changes] that were
/// made. A game sends [Changes] to the one player they belong to, and saves
/// the whole table. A [Condition] compares one named value with a number.
library;

export 'src/condition.dart' show Compare, Condition;
export 'src/schema.dart' show Place, ProgressName, ProgressSchema, Slice, Whole;
export 'src/table.dart' show Changes, ProgressTable;
