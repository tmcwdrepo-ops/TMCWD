/// Matches TMCWD.Model.ReadingStatus; do not renumber.
enum ReadingSheetStatus {
  created(1),
  inProgress(2),
  completed(3),
  deleted(4);

  const ReadingSheetStatus(this.value);
  final int value;
}

enum SyncStatus { pending, syncing, synced, failed }
