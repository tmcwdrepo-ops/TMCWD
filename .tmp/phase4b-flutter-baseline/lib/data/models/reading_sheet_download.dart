import '../../core/statuses.dart';
import 'local_models.dart' as local;

class AssignedSheet {
  AssignedSheet.fromJson(Map<String, dynamic> json)
    : id = json['id'] as int,
      name = json['name'] as String?,
      status = ReadingSheetStatus.values.firstWhere(
        (s) => s.value == json['status'],
      ),
      billingDate = json['billingDate'] == null
          ? null
          : DateTime.parse(json['billingDate'] as String),
      zoneBookId = json['zoneBookId'] as int,
      downloadedAt = json['downloadedAt'] as String?;

  final int id;
  final String? name;
  final ReadingSheetStatus status;
  final DateTime? billingDate;
  final int zoneBookId;
  final String? downloadedAt;
  bool get isDownloaded => downloadedAt != null;
  String get statusLabel => switch (status) {
    ReadingSheetStatus.created => 'Created',
    ReadingSheetStatus.inProgress => 'In progress',
    ReadingSheetStatus.completed => 'Completed',
    ReadingSheetStatus.deleted => 'Deleted',
  };
}

class DownloadAccount {
  DownloadAccount.fromJson(Map<String, dynamic> json)
    : accountId = json['accountId'] as int,
      accountNumber = json['accountNumber'] as String,
      name = json['name'] as String,
      meterNumber = json['meterNumber'] as String,
      address = json['address'] as String,
      zone = json['zone'] as int,
      book = json['book'] as int,
      sequence = json['sequence'] as int,
      previousReading = json['previousReading'] as num,
      previousReadingDate = json['previousReadingDate'] == null
          ? null
          : DateTime.parse(json['previousReadingDate'] as String),
      currentReading = json['currentReading'] as num?,
      classification = json['classification'] as int,
      meterSize = (json['meterSize'] as num).toString(),
      readingId = json['readingId'] as int?,
      readingStatus = json['readingStatus'] as int?,
      readingDate = json['readingDate'] == null
          ? null
          : DateTime.parse(json['readingDate'] as String),
      remarks = json['remarks'] as String? ?? '',
      syncStatus = json['syncStatus'] as String?,
      previousReadingText =
          json['previousReadingText'] as String? ??
          (json['previousReading'] as num).toString(),
      currentReadingText =
          json['currentReadingText'] as String? ??
          (json['currentReading'] as num?)?.toString();

  final int accountId, zone, book, sequence;
  final String accountNumber, name, meterNumber, address;
  final num previousReading;
  final DateTime? previousReadingDate;
  final num? currentReading;
  final int classification;
  final String meterSize;
  final int? readingId, readingStatus;
  final DateTime? readingDate;
  final String remarks;
  final String? syncStatus;
  final String previousReadingText;
  final String? currentReadingText;
  bool get canEdit =>
      readingStatus != ReadingSheetStatus.completed.value &&
      readingStatus != ReadingSheetStatus.deleted.value;
}

class SheetDownload {
  SheetDownload.fromJson(Map<String, dynamic> data, int id, int readerId)
    : accounts = (data['accounts'] as List)
          .map((row) => DownloadAccount.fromJson(row as Map<String, dynamic>))
          .toList() {
    final json = data['readingSheet'] as Map<String, dynamic>;
    final assigned = AssignedSheet.fromJson(json);
    if (assigned.id != id ||
        json['assignedTo'] != readerId ||
        assigned.status == ReadingSheetStatus.deleted ||
        accounts.any((a) => a.accountId <= 0) ||
        accounts.map((a) => a.accountId).toSet().length != accounts.length) {
      throw const FormatException('Invalid download identity or roster.');
    }
    sheet = local.ReadingSheet(
      id: id,
      serverId: id,
      name: assigned.name ?? '',
      zoneBookId: assigned.zoneBookId,
      zone: accounts.isEmpty ? '' : accounts.first.zone.toString(),
      book: accounts.isEmpty ? '' : accounts.first.book.toString(),
      assignedTo: readerId,
      billingDate: json['billingDate'] as String?,
      dueDate: json['dueDate'] as String?,
      billingPeriodStart: json['billingPeriodStart'] as String?,
      seqFrom: json['seqFrom'] as int?,
      seqTo: json['seqTo'] as int?,
      disconnectionDate: json['disconnectionDate'] as String?,
      dateCreated: json['dateCreated'] as String?,
      dateUpload: json['dateUpload'] as String?,
      status: assigned.status.value,
      downloadedAt: DateTime.now().toUtc().toIso8601String(),
    );
    if (accounts.any(
      (a) =>
          a.readingId != null &&
          (a.currentReading == null ||
              a.readingDate == null ||
              a.readingStatus == null),
    )) {
      throw const FormatException('Incomplete saved reading.');
    }
  }
  late final local.ReadingSheet sheet;
  final List<DownloadAccount> accounts;
}
