class OfficialSoa {
  OfficialSoa.fromJson(Map<String, dynamic> json)
    : readingSheetId = json['readingSheetId'] as int,
      sheetName = json['sheetName'] as String?,
      readingId = json['readingId'] as int,
      billingId = json['billingId'] as int,
      billingReferenceId = json['billingReferenceId'] as String,
      accountId = json['accountId'] as int,
      accountNumber = json['accountNumber'] as String,
      name = json['name'] as String,
      address = json['address'] as String,
      meterNumber = json['meterNumber'] as String,
      classification = json['classification'] as int,
      meterSize = _decimal(json['meterSize']),
      previousReading = _decimal(json['previousReading']),
      currentReading = _decimal(json['currentReading']),
      usage = _decimal(json['usage']),
      readingDate = DateTime.parse(json['readingDate'] as String),
      billingPeriod = DateTime.parse(json['billingPeriod'] as String),
      billingDate = _date(json['billingDate']),
      dueDate = _date(json['dueDate']),
      disconnectionDate = _date(json['disconnectionDate']),
      billingAdjustment = _decimal(json['billingAdjustment']),
      otherCharges = _decimal(json['otherCharges']),
      totalBillAmount = _decimal(json['totalBillAmount']),
      remainingAmount = _decimal(json['remainingAmount']),
      paymentStatus = json['paymentStatus'] as int,
      serviceInvoiceNumber = json['serviceInvoiceNumber'] as String?,
      qrPayload = json['qrPayload'] as String?,
      birConfig = json['birConfig'] as Map<String, dynamic>?,
      meterReaderName = json['meterReaderName'] as String?,
      readingRemarks = json['readingRemarks'] as String?,
      periodFrom = _date(json['periodFrom']),
      periodTo = _date(json['periodTo']),
      billAmount = _optionalDecimal(json['billAmount']),
      meterMaintenance = _optionalDecimal(json['meterMaintenance']),
      powerCostAdjustment = _optionalDecimal(json['powerCostAdjustment']),
      arrearsAmount = _optionalDecimal(json['arrearsAmount']),
      totalDue = _optionalDecimal(json['totalDue']),
      currentPenaltyAmount = _optionalDecimal(json['currentPenaltyAmount']),
      penaltyAfterDueAmount = _optionalDecimal(json['penaltyAfterDueAmount']),
      totalAfterDue = _optionalDecimal(json['totalAfterDue']),
      chargeLines = (json['chargeLines'] as List? ?? [])
          .map(
            (line) => InvoiceChargeLine.fromJson(line as Map<String, dynamic>),
          )
          .toList();

  final int readingSheetId,
      readingId,
      billingId,
      accountId,
      classification,
      paymentStatus;
  final String? sheetName;
  final String? serviceInvoiceNumber,
      qrPayload,
      meterReaderName,
      readingRemarks;
  final Map<String, dynamic>? birConfig;
  final DateTime? periodFrom, periodTo;
  final String? billAmount,
      meterMaintenance,
      powerCostAdjustment,
      arrearsAmount,
      totalDue,
      currentPenaltyAmount,
      penaltyAfterDueAmount,
      totalAfterDue;
  final List<InvoiceChargeLine> chargeLines;
  final String billingReferenceId, accountNumber, name, address, meterNumber;
  final String meterSize, previousReading, currentReading, usage;
  final String billingAdjustment,
      otherCharges,
      totalBillAmount,
      remainingAmount;
  final DateTime readingDate, billingPeriod;
  final DateTime? billingDate, dueDate, disconnectionDate;

  // TMCWD.Model.AccountClassification; keep the shared enum's codes.
  String get classificationLabel => switch (classification) {
    1 => 'Residential',
    2 => 'Government',
    3 => 'Industrial',
    4 => 'Commercial',
    5 => 'Commercial A',
    6 => 'Commercial B',
    7 => 'Commercial C',
    8 => 'Wholesale',
    9 => 'Bulk',
    _ => 'Unknown ($classification)',
  };

  // Zero is not a usable meter dimension; never substitute a default size.
  String? get availableMeterSize =>
      !meterSize.startsWith('-') && RegExp(r'[1-9]').hasMatch(meterSize)
      ? meterSize
      : null;

  String get paymentStatusLabel => switch (paymentStatus) {
    0 => 'Draft',
    1 => 'Unpaid',
    2 => 'Paid',
    3 => 'Overdue',
    4 => 'Waived',
    _ => 'Unknown',
  };
  static DateTime? _date(Object? value) =>
      value == null ? null : DateTime.parse(value as String);
  static String? _optionalDecimal(Object? value) =>
      value == null ? null : _decimal(value);
  static String _decimal(Object? value) {
    if (value is! num && value is! String)
      throw const FormatException('Missing numeric SOA value.');
    final text = value.toString();
    if (!RegExp(r'^-?\d+(\.\d+)?$').hasMatch(text))
      throw const FormatException('Invalid numeric SOA value.');
    return text;
  }
}

class InvoiceChargeLine {
  InvoiceChargeLine.fromJson(Map<String, dynamic> json)
    : description = json['description'] as String,
      quantity = OfficialSoa._optionalDecimal(json['quantity']),
      unitCost = OfficialSoa._decimal(json['unitCost']),
      amount = OfficialSoa._decimal(json['amount']),
      sortOrder = json['sortOrder'] as int;
  final String description;
  final String? quantity;
  final String unitCost, amount;
  final int sortOrder;
}

enum SoaState { available, notPosted, unavailable, offline, failed }

class SoaResult {
  const SoaResult(this.state, {this.soa, this.message, this.cachedAt});
  final SoaState state;
  final OfficialSoa? soa;
  final String? message;
  final DateTime? cachedAt;
}
