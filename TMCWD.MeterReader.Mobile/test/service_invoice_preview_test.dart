import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/official_soa.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/soa_screen.dart';
import 'reading_sheet_test.dart' show GetClient, session, settleIO;
import 'soa_test.dart' show official;

Map<String, dynamic> invoice() => {
  ...official(),
  'serviceInvoiceNumber': 'TEST-00077',
  'qrPayload': 'TMCWD|SI|TEST-00077',
  'meterReaderName': 'Synthetic Reader',
  'readingRemarks': 'Meter checked',
  'periodFrom': '2026-01-01',
  'periodTo': '2026-01-31',
  'billAmount': '9876.54',
  'meterMaintenance': '20.00',
  'powerCostAdjustment': '1.25',
  'arrearsAmount': '100.00',
  'totalDue': '9997.79',
  'currentPenaltyAmount': null,
  'penaltyAfterDueAmount': '12.34',
  'totalAfterDue': '10010.13',
  'chargeLines': [
    {
      'description': 'Minimum (0-10 cu.m)',
      'quantity': null,
      'unitCost': '9876.54',
      'amount': '9876.54',
      'sortOrder': 0,
    },
  ],
  'disconnectionDate': '2026-02-25T00:00:00',
  'birConfig': {
    'ptuNumber': 'SYNTHETIC-PTU',
    'seriesFrom': 1,
    'seriesTo': 9999999,
    'invoicePrefix': 'TEST-',
    'seriesWidth': 7,
    'permitDateIssued': '2026-01-01',
    'effectiveFrom': '2026-01-01',
    'effectiveTo': '2026-12-31',
  },
};

void main() {
  testWidgets(
    'consumption period displays exact server dates in fresh and cached previews',
    (tester) async {
      final soa = OfficialSoa.fromJson({
        ...invoice(),
        'periodFrom': '2026-09-05',
        'periodTo': '2026-10-05',
        'readingDate': '2026-10-05',
        'billingDate': '2026-10-06',
        'dueDate': '2026-10-21',
      });
      expect(soa.periodFrom, DateTime(2026, 9, 5));
      expect(soa.periodTo, DateTime(2026, 10, 5));
      for (final offline in [false, true]) {
        await tester.pumpWidget(
          MaterialApp(
            home: ServiceInvoicePreview(
              result: SoaResult(
                SoaState.available,
                soa: soa,
                cachedAt: offline ? DateTime(2026, 10, 6) : null,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        final context = tester.element(find.byType(ServiceInvoicePreview));
        final labels = MaterialLocalizations.of(context);
        expect(
          find.text(labels.formatMediumDate(DateTime(2026, 9, 5))),
          findsOneWidget,
        );
        expect(
          find.text(labels.formatMediumDate(DateTime(2026, 10, 5))),
          findsOneWidget,
        );
        expect(find.text('Reading Date'), findsNothing);
        expect(
          find.text('Offline copy'),
          offline ? findsOneWidget : findsNothing,
        );
      }
    },
  );
  test(
    'classification follows all shared enum codes and meter size is never defaulted',
    () {
      const labels = [
        'Residential',
        'Government',
        'Industrial',
        'Commercial',
        'Commercial A',
        'Commercial B',
        'Commercial C',
        'Wholesale',
        'Bulk',
      ];
      for (var i = 0; i < labels.length; i++) {
        expect(
          OfficialSoa.fromJson({
            ...invoice(),
            'classification': i + 1,
          }).classificationLabel,
          labels[i],
        );
      }
      expect(
        OfficialSoa.fromJson({
          ...invoice(),
          'classification': 99,
        }).classificationLabel,
        'Unknown (99)',
      );
      for (final size in ['0', '0.0', '0.00', '-0.5']) {
        expect(
          OfficialSoa.fromJson({
            ...invoice(),
            'meterSize': size,
          }).availableMeterSize,
          isNull,
        );
      }
      expect(
        OfficialSoa.fromJson({
          ...invoice(),
          'meterSize': '0.75',
        }).availableMeterSize,
        '0.75',
      );
    },
  );

  testWidgets(
    'fresh SOA opens narrow scrollable read-only invoice with authoritative fields',
    (tester) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final client = GetClient()..download = invoice();
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SoaScreen(
              service: ReadingSheetService(auth, session),
              sheetId: 15,
              accountId: 20,
              onClose: () {},
            ),
          ),
        ),
      );
      await tester.runAsync(() => settleIO(tester));
      expect(find.text('Residential'), findsOneWidget);
      await tester.tap(find.text('Preview Service Invoice'));
      await tester.pumpAndSettle();
      final preview = find.byType(ServiceInvoicePreview);
      Finder text(String value) =>
          find.descendant(of: preview, matching: find.text(value));
      for (final value in [
        'TRECE MARTIRES CITY WATER DISTRICT',
        'Brgy. San Agustin, Trece Martires City, Cavite',
        '(046) 419 2664 / 09178050391',
        'Non-VAT Reg. TIN 005-644-154-00000',
        'SERVICE INVOICE',
        'Nature of Service',
        'WATER UTILITY',
        'Service Invoice No.',
        'TEST-00077',
        'A20',
        'Synthetic Customer',
        'Test Street',
        'Residential',
        'M20',
        '0.5',
        'Bill Month',
        'February 2026',
        'Present',
        '11',
        'Previous',
        '10',
        'Usage',
        '99',
        'Billing Adjustment',
        '₱-2.50',
        'Other Charges',
        '₱4.00',
        'Bill Amount',
        'Meter Maintenance',
        'Power Cost Adjustment',
        'Arrears',
        'TOTAL DUE',
        'Penalty After Due',
        'TOTAL AFTER DUE',
        'Period From',
        'Period To',
        'Minimum (0-10 cu.m)',
        'Qty',
        'Unit Cost',
        'Amount',
        '₱9,997.79',
        '₱10,010.13',
        '₱9,876.54',
        'Remaining Amount',
        '₱1,234.56',
        'Billing Date',
        'Due Date',
        'Disconnection Date',
        'Synthetic Reader',
        'Meter checked',
        'SYNTHETIC-PTU',
        'PTU No.',
        'Date Issued',
        'Series',
        '0000001 - 9999999',
        'THIS DOCUMENT IS NOT VALID\nFOR CLAIM OF INPUT TAX',
        'TMCWD|SI|TEST-00077',
      ]) {
        expect(text(value), findsWidgets, reason: value);
      }
      for (final omitted in [
        'OFFICIAL-300',
        'PCA',
        'Water Bill',
        'Not available',
        'Offline copy',
        'Series Prefix',
        'Series Width',
        'Effective From',
        'Effective To',
        'SeqFrom',
        'SeqTo',
      ])
        expect(text(omitted), findsNothing);
      expect(
        find.descendant(of: preview, matching: find.byType(TextField)),
        findsNothing,
      );
      final logo = tester.widget<Image>(
        find.descendant(of: preview, matching: find.byType(Image)),
      );
      expect(
        (logo.image as AssetImage).assetName,
        'assets/icons/tmcwd_logo.png',
      );
      expect(logo.semanticLabel, 'TMCWD logo');
      expect(
        find.descendant(of: preview, matching: find.byType(QrImageView)),
        findsOneWidget,
        reason: 'QrImageView must be present for the QR payload',
      );
      // The QR payload text is verified above via text('TMCWD|SI|TEST-00077').
      expect(
        find.descendant(
          of: preview,
          matching: find.byType(SingleChildScrollView),
        ),
        findsOneWidget,
      );
      expect(client.paths, hasLength(1));
      await tester.drag(
        find.descendant(
          of: preview,
          matching: find.byType(SingleChildScrollView),
        ),
        const Offset(0, -2000),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.pageBack();
      await tester.pumpAndSettle();
      expect(find.byType(ServiceInvoicePreview), findsNothing);
      expect(find.text('Preview Service Invoice'), findsOneWidget);
    },
  );

  testWidgets(
    'historical invoice never substitutes internal key, zero meter size or absent footer values',
    (tester) async {
      final soa = OfficialSoa.fromJson({...official(), 'meterSize': 0.0});
      await tester.pumpWidget(
        MaterialApp(
          home: ServiceInvoicePreview(
            result: SoaResult(SoaState.available, soa: soa),
          ),
        ),
      );
      expect(
        find.text(
          'Historical billing — official service invoice number unavailable',
        ),
        findsOneWidget,
      );
      for (final absent in [
        'Service Invoice No.',
        'OFFICIAL-300',
        'Meter Size',
        '0.0',
        'PTU',
        'BIR',
        'QR placeholder',
        'Period From',
        'Period To',
        'Meter Reader',
        'Reading Date',
        'Remarks',
      ])
        expect(find.text(absent), findsNothing);
      expect(find.byType(TextField), findsNothing);
    },
  );

  testWidgets(
    'cached SOA uses same preview and keeps retrieval/staleness notices without fetching again',
    (tester) async {
      sqfliteFfiInit();
      await tester.runAsync(() async {
        final local = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: inMemoryDatabasePath,
        );
        final client = GetClient()..download = invoice();
        final auth = AuthService(
          apiBaseUrl: session.apiBaseUrl,
          client: client,
        );
        try {
          final service = ReadingSheetService(
            auth,
            session,
            database: Future.value(local),
          );
          await service.getSoa(15, 20);
          client.error = const SocketException('Offline');
          final cached = (await local.cachedSoa(15, 20))!;
          final before = await local.database.query('official_soa_cache');
          await tester.pumpWidget(
            MaterialApp(
              home: Scaffold(
                body: SoaScreen(
                  service: service,
                  sheetId: 15,
                  accountId: 20,
                  onClose: () {},
                ),
              ),
            ),
          );
          await settleIO(tester);
          await tester.tap(find.text('Preview Service Invoice'));
          await tester.pumpAndSettle();
          final preview = find.byType(ServiceInvoicePreview);
          final context = tester.element(preview);
          final labels = MaterialLocalizations.of(context);
          final date = cached.cachedAt!.toLocal();
          for (final value in [
            'Offline copy',
            'Last retrieved: ${labels.formatMediumDate(date)} ${labels.formatTimeOfDay(TimeOfDay.fromDateTime(date))}',
            'TEST-00077',
            'TMCWD|SI|TEST-00077',
          ]) {
            expect(
              find.descendant(of: preview, matching: find.text(value)),
              findsOneWidget,
            );
          }
          expect(
            find.descendant(
              of: preview,
              matching: find.textContaining('may have changed'),
            ),
            findsOneWidget,
          );
          expect(client.paths, hasLength(2));
          expect(await local.database.query('official_soa_cache'), before);
          await tester.pumpWidget(const SizedBox());
        } finally {
          auth.close();
          await local.close();
        }
      });
    },
  );
}
