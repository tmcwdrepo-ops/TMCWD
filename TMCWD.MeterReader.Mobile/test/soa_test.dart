import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/official_soa.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/account_list_screen.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_entry.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/soa_screen.dart';
import 'reading_sheet_test.dart' show GetClient, session, settleIO;
import 'download_persistence_test.dart' show payload, download;

Map<String, dynamic> official({int paymentStatus = 1}) => {
  'readingSheetId': 15,
  'sheetName': 'InProgress sheet',
  'readingId': 200,
  'billingId': 300,
  'billingReferenceId': 'OFFICIAL-300',
  'accountId': 20,
  'accountNumber': 'A20',
  'name': 'Synthetic Customer',
  'address': 'Test Street',
  'meterNumber': 'M20',
  'classification': 1,
  'meterSize': 0.5,
  'previousReading': 10,
  'currentReading': 11,
  'usage': 99,
  'readingDate': '2026-01-31T12:00:00',
  'billingPeriod': '2026-02-01T00:00:00',
  'billingDate': '2026-02-01T00:00:00',
  'dueDate': '2026-02-15T00:00:00',
  'disconnectionDate': null,
  'billingAdjustment': -2.5,
  'otherCharges': 4,
  'totalBillAmount': 9876.54,
  'remainingAmount': 1234.56,
  'paymentStatus': paymentStatus,
};

void main() {
  test(
    'official SOA parses backend fields, server usage and persisted amounts with bearer GET',
    () async {
      final client = GetClient()..download = official();
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(auth, session);
      final result = await service.getSoa(15, 20);
      expect(result.state, SoaState.available);
      final soa = result.soa!;
      expect(soa.readingSheetId, 15);
      expect(soa.readingId, 200);
      expect(soa.billingId, 300);
      expect(soa.billingReferenceId, 'OFFICIAL-300');
      expect(soa.sheetName, 'InProgress sheet');
      expect(soa.name, 'Synthetic Customer');
      expect(soa.accountNumber, 'A20');
      expect(soa.meterNumber, 'M20');
      expect(soa.classification, 1);
      expect(soa.meterSize, '0.5');
      expect(soa.previousReading, '10');
      expect(soa.currentReading, '11');
      expect(soa.usage, '99');
      expect(soa.totalBillAmount, '9876.54');
      expect(soa.remainingAmount, '1234.56');
      expect(soa.billingAdjustment, '-2.5');
      expect(soa.otherCharges, '4');
      expect(soa.dueDate, DateTime(2026, 2, 15));
      expect(soa.disconnectionDate, isNull);
      expect(client.paths, ['/api/reading-sheet/mobile/15/accounts/20/soa']);
      expect(
        client.headers.values[HttpHeaders.authorizationHeader],
        'Bearer ${session.token}',
      );
      expect(soaPeso('9876.54'), '₱9,876.54');
      expect(soaPeso('-2.5'), '₱-2.50');
      expect(soaPeso('0'), '₱0.00');
    },
  );

  test(
    'SOA distinguishes not posted, inaccessible, offline, malformed and other conflicts',
    () async {
      final client = GetClient()
        ..status = 409
        ..download = {'code': 'not_posted'};
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(auth, session);
      expect((await service.getSoa(15, 20)).state, SoaState.notPosted);
      client.download = {'message': 'Duplicate billing'};
      expect((await service.getSoa(15, 20)).state, SoaState.failed);
      client.status = 404;
      expect((await service.getSoa(15, 20)).state, SoaState.unavailable);
      client.status = 200;
      client.download = {};
      expect((await service.getSoa(15, 20)).state, SoaState.failed);
      client.error = const SocketException('Offline');
      final offline = await service.getSoa(15, 20);
      expect(offline.state, SoaState.offline);
      expect(offline.soa, isNull);
      for (final status in [401, 403]) {
        client.error = null;
        client.status = status;
        expect(
          () => service.getSoa(15, 20),
          throwsA(
            isA<AuthFailure>().having(
              (e) => e.statusCode,
              'HTTP status',
              status,
            ),
          ),
        );
      }
    },
  );

  for (final status in [1, 2]) {
    testWidgets(
      'SOA displays official amounts and ${status == 1 ? 'Unpaid' : 'Paid'} without omitted charges',
      (tester) async {
        tester.view.physicalSize = const Size(800, 2000);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final auth = AuthService(
          apiBaseUrl: session.apiBaseUrl,
          client: GetClient()..download = official(paymentStatus: status),
        );
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
        expect(find.text('Statement of Account'), findsOneWidget);
        expect(find.text('₱9,876.54'), findsOneWidget);
        expect(find.text('₱1,234.56'), findsOneWidget);
        expect(find.text(status == 1 ? 'Unpaid' : 'Paid'), findsOneWidget);
        expect(find.text('99'), findsOneWidget);
        expect(find.text('OFFICIAL-300'), findsOneWidget);
        expect(find.textContaining('Penalty'), findsNothing);
        expect(find.textContaining('Arrears'), findsNothing);
        expect(find.byType(TextField), findsNothing);
      },
    );
  }

  for (final state in ['not_posted', 'offline', 'auth']) {
    testWidgets(
      'SOA shows recoverable $state state and forwards auth failure',
      (tester) async {
        final client = GetClient()
          ..status = state == 'auth' ? 401 : 409
          ..download = {'code': 'not_posted'};
        if (state == 'offline') client.error = const SocketException('Offline');
        final auth = AuthService(
          apiBaseUrl: session.apiBaseUrl,
          client: client,
        );
        addTearDown(auth.close);
        AuthFailure? forwarded;
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SoaScreen(
                service: ReadingSheetService(auth, session),
                sheetId: 15,
                accountId: 20,
                onClose: () {},
                onAuthFailure: (error) => forwarded = error,
              ),
            ),
          ),
        );
        await tester.runAsync(() => settleIO(tester));
        expect(find.text('Total Bill Amount'), findsNothing);
        expect(find.text('Back to accounts'), findsOneWidget);
        if (state == 'auth') {
          expect(forwarded?.statusCode, 401);
          expect(find.text('Session expired. Sign in again.'), findsOneWidget);
        } else if (state == 'offline') {
          expect(
            find.textContaining('SOA cannot be loaded while offline'),
            findsOneWidget,
          );
        } else {
          expect(find.text('SOA not available yet'), findsOneWidget);
          expect(find.text('Awaiting billing post.'), findsOneWidget);
          client.status = 200;
          client.download = official();
          await tester.tap(find.text('Retry'));
          await tester.runAsync(() => settleIO(tester));
          expect(find.text('A20'), findsOneWidget);
        }
      },
    );
  }

  testWidgets(
    'InProgress account menu views SOA, preserves local readings and still opens entry',
    (tester) async {
      sqfliteFfiInit();
      await tester.runAsync(() async {
        final local = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: inMemoryDatabasePath,
        );
        final client = GetClient()..download = official();
        final auth = AuthService(
          apiBaseUrl: session.apiBaseUrl,
          client: client,
        );
        try {
          await local.saveDownload(download(payload()), 10);
          final service = ReadingSheetService(
            auth,
            session,
            database: Future.value(local),
          );
          await service.saveReading(15, 20, '55', 'Keep local draft');
          final before = await local.database.query('readings');
          final accounts = await service.openDownloaded(15);
          await tester.pumpWidget(
            MaterialApp(
              home: Scaffold(
                body: AccountListScreen(
                  service: service,
                  sheet: AssignedSheet.fromJson({
                    'id': 15,
                    'status': 2,
                    'zoneBookId': 1,
                  }),
                  accounts: accounts,
                  onClose: () {},
                ),
              ),
            ),
          );
          await tester.tap(find.byTooltip('Account actions for A20'));
          await tester.pumpAndSettle();
          await tester.tap(find.text('View SOA'));
          await settleIO(tester);
          expect(find.byType(SoaScreen), findsOneWidget);
          expect(find.text('OFFICIAL-300'), findsOneWidget);
          await tester.tap(find.text('Back to accounts'));
          await tester.pumpAndSettle();
          expect(await local.database.query('readings'), before);
          await tester.tap(find.text('A20'));
          await tester.pumpAndSettle();
          expect(find.byType(ReadingEntry), findsOneWidget);
          expect(
            tester.widget<TextField>(find.byType(TextField)).controller!.text,
            '55',
          );
          await tester.pumpWidget(const SizedBox());
        } finally {
          auth.close();
          await local.close();
        }
      });
    },
  );

  penaltyParsingTests();
}

// ── Phase 6D penalty-field parsing tests ────────────────────────────────────

Map<String, dynamic> _officialWithPenalty() => {
  ...official(),
  'totalDue': '120.00',
  'currentPenaltyAmount': null,
  'penaltyAfterDueAmount': '12.00',
  'totalAfterDue': '132.00',
};

Map<String, dynamic> _officialWithCurrentPenalty() => {
  ...official(),
  'totalDue': '120.00',
  'currentPenaltyAmount': '12.00',
  'penaltyAfterDueAmount': '12.00',
  'totalAfterDue': '132.00',
};

void penaltyParsingTests() {
  test(
    'OfficialSoa parses informational penalty fields before DueDate (no current penalty)',
    () {
      final soa = OfficialSoa.fromJson(_officialWithPenalty());
      expect(soa.totalDue, '120.00');
      expect(soa.currentPenaltyAmount, isNull);
      expect(soa.penaltyAfterDueAmount, '12.00');
      expect(soa.totalAfterDue, '132.00');
    },
  );

  test(
    'OfficialSoa parses current penalty amount when penalty rows exist after DueDate',
    () {
      final soa = OfficialSoa.fromJson(_officialWithCurrentPenalty());
      expect(soa.currentPenaltyAmount, '12.00');
      expect(soa.penaltyAfterDueAmount, '12.00');
      expect(soa.totalAfterDue, '132.00');
    },
  );

  test(
    'OfficialSoa.fromJson treats missing penalty keys as null (historical SOA)',
    () {
      final soa = OfficialSoa.fromJson(official());
      expect(soa.currentPenaltyAmount, isNull);
      expect(soa.penaltyAfterDueAmount, isNull);
      expect(soa.totalAfterDue, isNull);
    },
  );

  testWidgets(
    'ServiceInvoicePreview shows issued Penalty and Total After Due rows when present',
    (tester) async {
      tester.view.physicalSize = const Size(800, 2400);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final soa = OfficialSoa.fromJson(_officialWithPenalty());
      await tester.pumpWidget(
        MaterialApp(
          home: ServiceInvoicePreview(
            result: SoaResult(SoaState.available, soa: soa),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('TOTAL DUE'), findsOneWidget);
      expect(find.text('Penalty'), findsOneWidget);
      expect(find.text('TOTAL AFTER DUE'), findsOneWidget);
      // currentPenaltyAmount is null so 'Penalty (Applied)' must NOT appear.
      expect(find.text('Penalty (Applied)'), findsNothing);
    },
  );

  testWidgets(
    'ServiceInvoicePreview prints one issued penalty when current penalty is applied',
    (tester) async {
      tester.view.physicalSize = const Size(800, 2400);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final soa = OfficialSoa.fromJson(_officialWithCurrentPenalty());
      await tester.pumpWidget(
        MaterialApp(
          home: ServiceInvoicePreview(
            result: SoaResult(SoaState.available, soa: soa),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Penalty'), findsOneWidget);
      expect(find.text('Penalty (Applied)'), findsNothing);
      expect(find.text('Penalty After Due'), findsNothing);
      expect(find.text('TOTAL AFTER DUE'), findsOneWidget);
    },
  );

  testWidgets('ServiceInvoicePreview omits all penalty rows when not present', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 2400);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final soa = OfficialSoa.fromJson(official());
    await tester.pumpWidget(
      MaterialApp(
        home: ServiceInvoicePreview(
          result: SoaResult(SoaState.available, soa: soa),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('Penalty'), findsNothing);
    expect(find.text('TOTAL AFTER DUE'), findsNothing);
  });
}

// Register penalty parsing tests inside the existing main().
