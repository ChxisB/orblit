import 'history.dart';
import 'record.dart';
import 'store.dart';

/// A store that also keeps each write in a [SaveHistory]. Reads go to the
/// store, so a history that is lost loses no current save.
final class HistoryStore implements SaveStore {
  HistoryStore(this.store, this.history);

  /// Saves in [directory] and their history in `directory/history`.
  HistoryStore.files(String directory)
    : this(FileSaveStore(directory), SaveHistory('$directory/history'));

  final SaveStore store;
  final SaveHistory history;

  @override
  Future<SaveRecord?> read(String key) => store.read(key);

  /// Writes the save, then its version. A failure of either fails the write,
  /// and writing again is safe, so a queue keeps the save and retries.
  @override
  Future<void> write(String key, SaveRecord record) async {
    await store.write(key, record);
    await history.record(key, record);
  }

  @override
  Future<bool> exists(String key) => store.exists(key);

  @override
  Future<List<String>> keys() => store.keys();
}
