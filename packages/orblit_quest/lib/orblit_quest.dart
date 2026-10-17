/// Quests as data, kept in a progress table.
///
/// A [Quest] is an ordered list of [Stage]s, read from a `.oquest` file. A
/// [QuestBook] checks the quests against a game's progress schema and moves a
/// player through them. The player's place is one integer in a
/// `ProgressTable`, so saving it and sending it to the owner are the table's
/// job.
library;

export 'src/book.dart' show Moved, QuestBook, QuestMove, QuestRefusal, Refused;
export 'src/quest.dart'
    show Quest, QuestFormatException, Stage, questExtension, questStageName;
