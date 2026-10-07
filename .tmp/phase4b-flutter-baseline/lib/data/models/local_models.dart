// SQLite row models only; API mapping belongs to Phase 3.
// Decimal values are canonical decimal strings, never binary floating point.
// Dates are ISO 8601 strings; billing dates retain their office calendar date.
// No offline billing or due-date calculation is introduced in Phase 1.

class ReadingSheet {
  const ReadingSheet({
    required this.id,
    required this.serverId,
    required this.name,
    required this.zoneBookId,
    required this.zone,
    required this.book,
    required this.assignedTo,
    this.billingDate,
    this.dueDate,
    this.billingPeriodStart,
    this.seqFrom,
    this.seqTo,
    this.disconnectionDate,
    this.dateCreated,
    this.dateUpload,
    required this.status,
    required this.downloadedAt,
  });

  final int id;
  final int serverId;
  final String name;
  final int zoneBookId;
  final String zone;
  final String book;
  final int assignedTo;
  final String? billingDate;
  final String? dueDate;
  final String? billingPeriodStart;
  final int? seqFrom;
  final int? seqTo;
  final String? disconnectionDate, dateCreated, dateUpload;
  final int status;
  final String downloadedAt;

  Map<String, Object?> toMap() => {
    'id': id,
    'server_id': serverId,
    'name': name,
    'zone_book_id': zoneBookId,
    'zone': zone,
    'book': book,
    'assigned_to': assignedTo,
    'billing_date': billingDate,
    'due_date': dueDate,
    'billing_period_start': billingPeriodStart,
    'seq_from': seqFrom,
    'seq_to': seqTo,
    'disconnection_date': disconnectionDate,
    'date_created': dateCreated,
    'date_upload': dateUpload,
    'status': status,
    'downloaded_at': downloadedAt,
  };

  factory ReadingSheet.fromMap(Map<String, Object?> map) => ReadingSheet(
    id: map['id'] as int,
    serverId: map['server_id'] as int,
    name: map['name'] as String,
    zoneBookId: map['zone_book_id'] as int,
    zone: map['zone'] as String,
    book: map['book'] as String,
    assignedTo: map['assigned_to'] as int,
    billingDate: map['billing_date'] as String?,
    dueDate: map['due_date'] as String?,
    billingPeriodStart: map['billing_period_start'] as String?,
    seqFrom: map['seq_from'] as int?,
    seqTo: map['seq_to'] as int?,
    disconnectionDate: map['disconnection_date'] as String?,
    dateCreated: map['date_created'] as String?,
    dateUpload: map['date_upload'] as String?,
    status: map['status'] as int,
    downloadedAt: map['downloaded_at'] as String,
  );
}

class Account {
  const Account({
    required this.id,
    required this.serverAccountId,
    required this.readingSheetId,
    required this.accountNumber,
    required this.customerName,
    required this.address,
    required this.meterNumber,
    required this.classification,
    required this.meterSize,
    required this.previousReading,
    required this.previousBalance,
    this.previousReadingDate,
    this.inRoster = true,
    required this.sequenceNumber,
  });

  final int id;
  final int serverAccountId;
  final int readingSheetId;
  final String accountNumber;
  final String customerName;
  final String address;
  final String meterNumber;
  final int classification;
  final String meterSize;
  final String previousReading;
  final String previousBalance;
  final String? previousReadingDate;
  final bool inRoster;
  final int sequenceNumber;

  Map<String, Object?> toMap() => {
    'id': id,
    'server_account_id': serverAccountId,
    'reading_sheet_id': readingSheetId,
    'account_number': accountNumber,
    'customer_name': customerName,
    'address': address,
    'meter_number': meterNumber,
    'classification': classification,
    'meter_size': meterSize,
    'previous_reading': previousReading,
    'previous_balance': previousBalance,
    'previous_reading_date': previousReadingDate,
    'in_roster': inRoster ? 1 : 0,
    'sequence_number': sequenceNumber,
  };

  factory Account.fromMap(Map<String, Object?> map) => Account(
    id: map['id'] as int,
    serverAccountId: map['server_account_id'] as int,
    readingSheetId: map['reading_sheet_id'] as int,
    accountNumber: map['account_number'] as String,
    customerName: map['customer_name'] as String,
    address: map['address'] as String,
    meterNumber: map['meter_number'] as String,
    classification: map['classification'] as int,
    meterSize: map['meter_size'] as String,
    previousReading: map['previous_reading'] as String,
    previousBalance: map['previous_balance'] as String,
    previousReadingDate: map['previous_reading_date'] as String?,
    inRoster: map['in_roster'] != 0,
    sequenceNumber: map['sequence_number'] as int,
  );
}

class Reading {
  const Reading({
    this.id,
    required this.localUuid,
    this.serverReadingId,
    required this.readingSheetId,
    required this.accountId,
    required this.previousReading,
    this.currentReading,
    this.consumption,
    required this.readingDate,
    required this.readingStatus,
    required this.meterCondition,
    required this.remarks,
    required this.syncStatus,
    this.syncedAt,
    required this.createdAt,
    required this.updatedAt,
  });

  final int? id;
  final String localUuid;
  final int? serverReadingId;
  final int readingSheetId;
  final int accountId;
  final String previousReading;
  final String? currentReading;
  final String? consumption;
  final String readingDate;
  final int readingStatus;
  final String meterCondition;
  final String remarks;
  final String syncStatus;
  final String? syncedAt;
  final String createdAt;
  final String updatedAt;

  Map<String, Object?> toMap() => {
    if (id != null) 'id': id,
    'local_uuid': localUuid,
    'server_reading_id': serverReadingId,
    'reading_sheet_id': readingSheetId,
    'account_id': accountId,
    'previous_reading': previousReading,
    'current_reading': currentReading,
    'consumption': consumption,
    'reading_date': readingDate,
    'reading_status': readingStatus,
    'meter_condition': meterCondition,
    'remarks': remarks,
    'sync_status': syncStatus,
    'synced_at': syncedAt,
    'created_at': createdAt,
    'updated_at': updatedAt,
  };

  factory Reading.fromMap(Map<String, Object?> map) => Reading(
    id: map['id'] as int?,
    localUuid: map['local_uuid'] as String,
    serverReadingId: map['server_reading_id'] as int?,
    readingSheetId: map['reading_sheet_id'] as int,
    accountId: map['account_id'] as int,
    previousReading: map['previous_reading'] as String,
    currentReading: map['current_reading'] as String?,
    consumption: map['consumption'] as String?,
    readingDate: map['reading_date'] as String,
    readingStatus: map['reading_status'] as int,
    meterCondition: map['meter_condition'] as String,
    remarks: map['remarks'] as String,
    syncStatus: map['sync_status'] as String,
    syncedAt: map['synced_at'] as String?,
    createdAt: map['created_at'] as String,
    updatedAt: map['updated_at'] as String,
  );
}

class BillingRate {
  const BillingRate({
    required this.id,
    required this.serverId,
    required this.classification,
    required this.meterSize,
    required this.minimumCharge,
    required this.rate11To20,
    required this.rate21To30,
    required this.rate31To40,
    required this.rate41Up,
    required this.effectiveDate,
    required this.isActive,
  });

  final int id;
  final int serverId;
  final int classification;
  final String meterSize;
  final String minimumCharge;
  final String rate11To20;
  final String rate21To30;
  final String rate31To40;
  final String rate41Up;
  final String effectiveDate;
  final bool isActive;

  Map<String, Object?> toMap() => {
    'id': id,
    'server_id': serverId,
    'classification': classification,
    'meter_size': meterSize,
    'minimum_charge': minimumCharge,
    'rate11_to20': rate11To20,
    'rate21_to30': rate21To30,
    'rate31_to40': rate31To40,
    'rate41_up': rate41Up,
    'effective_date': effectiveDate,
    'is_active': isActive ? 1 : 0,
  };

  factory BillingRate.fromMap(Map<String, Object?> map) => BillingRate(
    id: map['id'] as int,
    serverId: map['server_id'] as int,
    classification: map['classification'] as int,
    meterSize: map['meter_size'] as String,
    minimumCharge: map['minimum_charge'] as String,
    rate11To20: map['rate11_to20'] as String,
    rate21To30: map['rate21_to30'] as String,
    rate31To40: map['rate31_to40'] as String,
    rate41Up: map['rate41_up'] as String,
    effectiveDate: map['effective_date'] as String,
    isActive: map['is_active'] == 1,
  );
}

class Bill {
  const Bill({
    this.id,
    this.serverBillId,
    required this.readingId,
    required this.billingPeriod,
    required this.billingDate,
    required this.dueDate,
    required this.waterCharge,
    required this.previousBalance,
    required this.penalties,
    required this.otherCharges,
    required this.totalAmountDue,
    this.serverTotalAmountDue,
    required this.reviewRequired,
    required this.printCount,
    required this.createdAt,
  });

  final int? id;
  final int? serverBillId;
  final int readingId;
  final String billingPeriod;
  final String billingDate;
  final String dueDate;
  final String waterCharge;
  final String previousBalance;
  final String penalties;
  final String otherCharges;
  final String totalAmountDue;
  final String? serverTotalAmountDue;
  final bool reviewRequired;
  final int printCount;
  final String createdAt;

  Map<String, Object?> toMap() => {
    if (id != null) 'id': id,
    'server_bill_id': serverBillId,
    'reading_id': readingId,
    'billing_period': billingPeriod,
    'billing_date': billingDate,
    'due_date': dueDate,
    'water_charge': waterCharge,
    'previous_balance': previousBalance,
    'penalties': penalties,
    'other_charges': otherCharges,
    'total_amount_due': totalAmountDue,
    'server_total_amount_due': serverTotalAmountDue,
    'review_required': reviewRequired ? 1 : 0,
    'print_count': printCount,
    'created_at': createdAt,
  };

  factory Bill.fromMap(Map<String, Object?> map) => Bill(
    id: map['id'] as int?,
    serverBillId: map['server_bill_id'] as int?,
    readingId: map['reading_id'] as int,
    billingPeriod: map['billing_period'] as String,
    billingDate: map['billing_date'] as String,
    dueDate: map['due_date'] as String,
    waterCharge: map['water_charge'] as String,
    previousBalance: map['previous_balance'] as String,
    penalties: map['penalties'] as String,
    otherCharges: map['other_charges'] as String,
    totalAmountDue: map['total_amount_due'] as String,
    serverTotalAmountDue: map['server_total_amount_due'] as String?,
    reviewRequired: map['review_required'] == 1,
    printCount: map['print_count'] as int,
    createdAt: map['created_at'] as String,
  );
}

class SyncQueueEntry {
  const SyncQueueEntry({
    this.id,
    required this.readingId,
    required this.retryCount,
    this.lastAttemptAt,
    this.lastError,
    required this.createdAt,
  });

  final int? id;
  final int readingId;
  final int retryCount;
  final String? lastAttemptAt;
  final String? lastError;
  final String createdAt;

  Map<String, Object?> toMap() => {
    if (id != null) 'id': id,
    'reading_id': readingId,
    'retry_count': retryCount,
    'last_attempt_at': lastAttemptAt,
    'last_error': lastError,
    'created_at': createdAt,
  };

  factory SyncQueueEntry.fromMap(Map<String, Object?> map) => SyncQueueEntry(
    id: map['id'] as int?,
    readingId: map['reading_id'] as int,
    retryCount: map['retry_count'] as int,
    lastAttemptAt: map['last_attempt_at'] as String?,
    lastError: map['last_error'] as String?,
    createdAt: map['created_at'] as String,
  );
}

class PrinterSettings {
  const PrinterSettings({
    required this.id,
    required this.deviceAddress,
    required this.deviceName,
    required this.printerType,
    required this.paperWidth,
  });

  final int id;
  final String deviceAddress;
  final String deviceName;
  final String printerType;
  final int paperWidth;

  Map<String, Object?> toMap() => {
    'id': id,
    'device_address': deviceAddress,
    'device_name': deviceName,
    'printer_type': printerType,
    'paper_width': paperWidth,
  };

  factory PrinterSettings.fromMap(Map<String, Object?> map) => PrinterSettings(
    id: map['id'] as int,
    deviceAddress: map['device_address'] as String,
    deviceName: map['device_name'] as String,
    printerType: map['printer_type'] as String,
    paperWidth: map['paper_width'] as int,
  );
}
