/// Saves for a game server.
///
/// A game turns a player's state into bytes and gives them to a [SaveQueue]
/// with a [SaveReason]. The queue returns at once and a worker isolate writes
/// through a [SaveStore], so the tick never waits on a disk. [AutoSave]
/// picks which players to save on a tick. [HistoryStore] keeps older versions
/// and [SaveTools] restores or edits one.
///
/// This package uses `dart:io` and `dart:isolate`, so it is for servers and
/// tools, not the web.
library;

export 'src/autosave.dart' show AutoSave;
export 'src/history.dart'
    show HistoryEntry, SaveHistory, Spacing, defaultSpacing;
export 'src/history_store.dart' show HistoryStore;
export 'src/queue.dart' show SaveQueue;
export 'src/reason.dart'
    show Autosave, Custom, Disconnect, Failed, Leave, SaveReason, Shutdown;
export 'src/record.dart' show SaveRecord;
export 'src/store.dart' show FileSaveStore, MemorySaveStore, SaveStore;
export 'src/tools.dart' show RestorePlan, SaveTools;
