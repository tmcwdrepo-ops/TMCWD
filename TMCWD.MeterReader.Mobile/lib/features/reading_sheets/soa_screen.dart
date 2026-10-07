import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';
import '../../data/models/official_soa.dart';
import '../auth/auth_service.dart';
import 'reading_sheet_service.dart';

// Format supplied decimal text without deriving or recalculating an amount.
String soaPeso(String value) {
  final parts = value.split('.');
  final whole = parts.first.replaceAllMapped(
    RegExp(r'\B(?=(\d{3})+(?!\d))'),
    (_) => ',',
  );
  return '₱$whole.${(parts.length == 1 ? '' : parts[1]).padRight(2, '0')}';
}

List<Widget> _soaNotices(BuildContext context, SoaResult result) => [
  if (result.cachedAt != null) ...[
    const Text('Offline copy'),
    Text(
      'Last retrieved: ${MaterialLocalizations.of(context).formatMediumDate(result.cachedAt!.toLocal())} ${MaterialLocalizations.of(context).formatTimeOfDay(TimeOfDay.fromDateTime(result.cachedAt!.toLocal()))}',
    ),
    const Text(
      'Payment status and balance reflect this saved copy and may have changed.',
    ),
  ],
  if (result.message != null) Text(result.message!),
];

class SoaScreen extends StatefulWidget {
  const SoaScreen({
    super.key,
    required this.service,
    required this.sheetId,
    required this.accountId,
    required this.onClose,
    this.onAuthFailure,
  });
  final ReadingSheetService service;
  final int sheetId, accountId;
  final VoidCallback onClose;
  final ValueChanged<AuthFailure>? onAuthFailure;
  @override
  State<SoaScreen> createState() => _SoaScreenState();
}

class _SoaScreenState extends State<SoaScreen> {
  SoaResult? _result;
  String? _authError;
  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _result = null;
      _authError = null;
    });
    try {
      final result = await widget.service.getSoa(
        widget.sheetId,
        widget.accountId,
      );
      if (mounted) setState(() => _result = result);
    } on AuthFailure catch (error) {
      if (!mounted) return;
      setState(() => _authError = error.message);
      widget.onAuthFailure?.call(error);
    }
  }

  String _date(DateTime? value) =>
      value?.toIso8601String().split('T').first ?? 'Not available';
  Widget _group(String title, Map<String, String> fields) => Padding(
    padding: const EdgeInsets.only(bottom: 20),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium),
        const Divider(),
        for (final field in fields.entries)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(child: Text(field.key)),
                const SizedBox(width: 12),
                Expanded(child: Text(field.value, textAlign: TextAlign.end)),
              ],
            ),
          ),
      ],
    ),
  );
  @override
  Widget build(BuildContext context) {
    final soa = _result?.soa;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) widget.onClose();
      },
      child: SafeArea(
        child: Column(
          children: [
            TextButton.icon(
              onPressed: widget.onClose,
              icon: const Icon(Icons.arrow_back),
              label: const Text('Back to accounts'),
            ),
            const Text('TMCWD'),
            Text(
              'Statement of Account',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            Expanded(
              child: _result == null && _authError == null
                  ? const Center(child: CircularProgressIndicator())
                  : soa == null
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            if (_result?.state == SoaState.notPosted)
                              const Text('SOA not available yet'),
                            Text(
                              _authError ?? _result!.message!,
                              textAlign: TextAlign.center,
                            ),
                            if (_authError == null)
                              TextButton(
                                onPressed: _load,
                                child: const Text('Retry'),
                              ),
                          ],
                        ),
                      ),
                    )
                  : ListView(
                      padding: const EdgeInsets.all(16),
                      children: [
                        const Text('Official SOA'),
                        ..._soaNotices(context, _result!),
                        OutlinedButton(
                          onPressed: () => Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder: (_) =>
                                  ServiceInvoicePreview(result: _result!),
                            ),
                          ),
                          child: const Text('Preview Service Invoice'),
                        ),
                        _group('Account Information', {
                          'Account Number': soa.accountNumber,
                          'Customer/Account Name': soa.name,
                          'Service Address': soa.address,
                          'Meter Number': soa.meterNumber,
                          'Classification': soa.classificationLabel,
                          if (soa.availableMeterSize != null)
                            'Meter Size': soa.availableMeterSize!,
                        }),
                        _group('Meter Reading', {
                          'Previous Reading': soa.previousReading,
                          'Current Reading': soa.currentReading,
                          'Usage': soa.usage,
                          'Reading Date': _date(soa.readingDate),
                        }),
                        _group('Billing', {
                          'Sheet': soa.sheetName ?? '${soa.readingSheetId}',
                          'Billing Reference': soa.billingReferenceId,
                          'Billing Period': _date(soa.billingPeriod),
                          'Billing Date': _date(soa.billingDate),
                          'Due Date': _date(soa.dueDate),
                          'Disconnection Date': _date(soa.disconnectionDate),
                        }),
                        _group('Charges', {
                          'Billing Adjustment': soaPeso(soa.billingAdjustment),
                          'Other Charges': soaPeso(soa.otherCharges),
                          'Total Bill Amount': soaPeso(soa.totalBillAmount),
                          'Remaining Amount': soaPeso(soa.remainingAmount),
                        }),
                        _group('Payment', {
                          'Payment Status': soa.paymentStatusLabel,
                        }),
                      ],
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

class ServiceInvoicePreview extends StatelessWidget {
  static String _seriesNumber(Object? value, Object? width) {
    final digits = width is int && width > 0 && width <= 30 ? width : 0;
    return '$value'.padLeft(digits, '0');
  }

  const ServiceInvoicePreview({super.key, required this.result});
  final SoaResult result;

  Widget _section(
    String? title,
    Map<String, String> fields, {
    double fontSize = 11,
  }) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      if (title != null) ...[
        const Divider(height: 6, color: Colors.black),
        Text(
          title,
          style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
        ),
      ],
      for (final field in fields.entries)
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 0.5),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(field.key, style: TextStyle(fontSize: fontSize)),
              ),
              const SizedBox(width: 3),
              Flexible(
                child: Text(
                  field.value,
                  textAlign: TextAlign.end,
                  style: TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize:
                        field.key == 'TOTAL DUE' ||
                            field.key == 'TOTAL AFTER DUE' ||
                            field.key == 'Total Bill Amount' ||
                            field.key == 'Remaining Amount'
                        ? 15
                        : fontSize,
                  ),
                ),
              ),
            ],
          ),
        ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    final soa = result.soa!;
    final bir = soa.birConfig;
    final labels = MaterialLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Service Invoice Preview')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(12),
        child: Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 320),
            child: Container(
              margin: const EdgeInsets.all(4),
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(2),
                border: Border.all(color: Colors.black12),
              ),
              child: DefaultTextStyle(
                style: const TextStyle(
                  color: Colors.black,
                  fontSize: 11,
                  height: 1.05,
                  fontFamily: 'monospace',
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.center,
                      children: [
                        Image.asset(
                          'assets/icons/tmcwd_logo.png',
                          width: 28,
                          height: 28,
                          fit: BoxFit.contain,
                          semanticLabel: 'TMCWD logo',
                        ),
                        const SizedBox(height: 2),
                        SizedBox(
                          width: double.infinity,
                          child: DefaultTextStyle.merge(
                            style: const TextStyle(fontSize: 10, height: 1.05),
                            child: const Column(
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: [
                                Text(
                                  'TRECE MARTIRES CITY WATER DISTRICT',
                                  textAlign: TextAlign.center,
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                    height: 1.05,
                                  ),
                                ),
                                Text(
                                  'Brgy. San Agustin, Trece Martires City, Cavite',
                                  textAlign: TextAlign.center,
                                ),
                                Text(
                                  '(046) 419 2664 / 09178050391',
                                  textAlign: TextAlign.center,
                                ),
                                Text(
                                  'Non-VAT Reg. TIN 005-644-154-00000',
                                  textAlign: TextAlign.center,
                                ),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),
                    const Divider(height: 8, color: Colors.black),
                    const Text(
                      'SERVICE INVOICE',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                    const Divider(height: 8, color: Colors.black),
                    ..._soaNotices(context, result),
                    _section(null, {
                      'Nature of Service': 'WATER UTILITY',
                      if (soa.serviceInvoiceNumber != null)
                        'Service Invoice No.': soa.serviceInvoiceNumber!,
                    }),
                    if (soa.serviceInvoiceNumber == null)
                      const Text(
                        'Historical billing — official service invoice number unavailable',
                      ),
                    const SizedBox(height: 2),
                    _section(null, {
                      'Account No.': soa.accountNumber,
                      'Customer Name': soa.name,
                      'Service Address': soa.address,
                      'Classification': soa.classificationLabel,
                      'Meter No.': soa.meterNumber,
                      if (soa.availableMeterSize != null)
                        'Meter Size': soa.availableMeterSize!,
                    }),
                    _section('Billing Details', {
                      'Bill Month': labels.formatMonthYear(soa.billingPeriod),
                      if (soa.periodFrom != null)
                        'Period From': labels.formatMediumDate(soa.periodFrom!),
                      if (soa.periodTo != null)
                        'Period To': labels.formatMediumDate(soa.periodTo!),
                      'Present': soa.currentReading,
                      'Previous': soa.previousReading,
                      'Usage': soa.usage,
                    }),
                    _section('Cost Breakdown', {}),
                    if (soa.chargeLines.isNotEmpty) ...[
                      const Row(
                        children: [
                          Expanded(flex: 3, child: Text('Description')),
                          Expanded(child: Text('Qty')),
                          Expanded(flex: 2, child: Text('Unit Cost')),
                          Expanded(flex: 2, child: Text('Amount')),
                        ],
                      ),
                      for (final line in soa.chargeLines)
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(flex: 3, child: Text(line.description)),
                            Expanded(child: Text(line.quantity ?? '')),
                            Expanded(
                              flex: 2,
                              child: Text(soaPeso(line.unitCost)),
                            ),
                            Expanded(
                              flex: 2,
                              child: Text(soaPeso(line.amount)),
                            ),
                          ],
                        ),
                    ],
                    _section(null, {
                      if (soa.billAmount != null)
                        'Bill Amount': soaPeso(soa.billAmount!),
                    }),
                    _section('Other Charges', {
                      if (soa.meterMaintenance != null)
                        'Meter Maintenance': soaPeso(soa.meterMaintenance!),
                      if (soa.powerCostAdjustment != null)
                        'Power Cost Adjustment': soaPeso(
                          soa.powerCostAdjustment!,
                        ),
                      if (soa.arrearsAmount != null)
                        'Arrears': soaPeso(soa.arrearsAmount!),
                      'Billing Adjustment': soaPeso(soa.billingAdjustment),
                      'Miscellaneous Charges': soaPeso(soa.otherCharges),
                      if (soa.billAmount == null)
                        'Total Bill Amount': soaPeso(soa.totalBillAmount),
                      if (soa.totalDue != null)
                        'TOTAL DUE': soaPeso(soa.totalDue!),
                      if (soa.currentPenaltyAmount != null)
                        'Penalty (Applied)': soaPeso(soa.currentPenaltyAmount!),
                      if (soa.penaltyAfterDueAmount != null)
                        'Penalty After Due': soaPeso(
                          soa.penaltyAfterDueAmount!,
                        ),
                      if (soa.totalAfterDue != null)
                        'TOTAL AFTER DUE': soaPeso(soa.totalAfterDue!),
                      'Remaining Amount': soaPeso(soa.remainingAmount),
                    }),
                    const Divider(height: 8, color: Colors.black),
                    _section(null, {
                      if (soa.billingDate != null)
                        'Billing Date': labels.formatMediumDate(
                          soa.billingDate!,
                        ),
                      if (soa.dueDate != null)
                        'Due Date': labels.formatMediumDate(soa.dueDate!),
                      if (soa.disconnectionDate != null)
                        'Disconnection Date': labels.formatMediumDate(
                          soa.disconnectionDate!,
                        ),
                    }),
                    if (soa.meterReaderName != null ||
                        soa.readingRemarks != null)
                      _section(null, {
                        if (soa.meterReaderName != null)
                          'Meter Reader': soa.meterReaderName!,
                        if (soa.readingRemarks != null)
                          'Remarks': soa.readingRemarks!,
                      }),
                    const Divider(height: 8, color: Colors.black),
                    const Text(
                      'THIS DOCUMENT IS NOT VALID\nFOR CLAIM OF INPUT TAX',
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 9.5),
                    ),
                    if (bir != null)
                      _section(null, {
                        if (bir['ptuNumber'] != null)
                          'PTU No.': '${bir['ptuNumber']}',
                        if (bir['permitDateIssued'] != null)
                          'Date Issued': '${bir['permitDateIssued']}',
                        if (bir['seriesFrom'] != null &&
                            bir['seriesTo'] != null)
                          'Series':
                              '${_seriesNumber(bir['seriesFrom'], bir['seriesWidth'])} - ${_seriesNumber(bir['seriesTo'], bir['seriesWidth'])}',
                      }, fontSize: 9.5),
                    if (soa.qrPayload != null) ...[
                      const Divider(height: 8, color: Colors.black),
                      Center(
                        child: QrImageView(
                          data: soa.qrPayload!,
                          size: 120,
                          backgroundColor: Colors.white,
                          semanticsLabel: 'Service Invoice lookup QR code',
                        ),
                      ),
                      Text(soa.qrPayload!, textAlign: TextAlign.center),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
