import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/official_soa.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/auth_session.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/soa_screen.dart';
import 'reading_sheet_test.dart' show GetClient, session, settleIO;
import 'soa_test.dart' show official;
import 'download_persistence_test.dart' show payload, download;

Map<String, dynamic> snapshot() => {
  ...official(),
  'remainingAmount': '1000.001234567890123456789',
  'serviceInvoiceNumber': 'TEST-00077',
  'qrPayload': 'TMCWD|SI|TEST-00077',
  'birConfig': {
    'ptuNumber': 'SYNTHETIC',
    'invoicePrefix': 'TEST-',
    'seriesWidth': 5,
  },
  'meterReaderName': 'Synthetic Reader',
  'readingRemarks': 'Normal Reading',
  'periodFrom': null,
  'periodTo': '2026-01-31',
  'billAmount': '900.001234567890123456789',
  'meterMaintenance': '20.00',
  'powerCostAdjustment': null,
  'arrearsAmount': null,
  'totalDue': null,
  'currentPenaltyAmount': null,
  'penaltyAfterDueAmount': null,
  'totalAfterDue': null,
  'chargeLines': [
    {
      'description': '11-20 cu.m',
      'quantity': '5',
      'unitCost': '10.00',
      'amount': '50.00',
      'sortOrder': 1,
    },
  ],
};

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  sqfliteFfiInit();
  late LocalDatabase local;
  late GetClient client;
  late AuthService auth;
  late ReadingSheetService service;
  setUp(() async {
    local = await LocalDatabase.open(
      factory: databaseFactoryFfi,
      path: inMemoryDatabasePath,
    );
    client = GetClient()..download = snapshot();
    auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
    service = ReadingSheetService(auth, session, database: Future.value(local));
  });
  tearDown(() async {
    auth.close();
    await local.close();
  });

  test(
    '200 caches exact snapshot; repeated fresh responses update one row and override stale payment',
    () async {
      final fresh = await service.getSoa(15, 20);
      expect(fresh.cachedAt, isNull);
      var rows = await local.database.query('official_soa_cache');
      expect(jsonDecode(rows.single['payload_json'] as String), snapshot());
      final originalId = rows.single['id'];
      final cachedAt = rows.single['cached_at'];
      await Future<void>.delayed(const Duration(milliseconds: 2));
      client.download = {
        ...snapshot(),
        'remainingAmount': '0.00',
        'paymentStatus': 2,
        'periodFrom': '2026-09-05',
        'periodTo': '2026-10-05',
        'billAmount': '170.00',
        'totalDue': '190.00',
        'penaltyAfterDueAmount': '17.00',
        'totalAfterDue': '207.00',
      };
      final paid = await service.getSoa(15, 20);
      expect(paid.soa!.remainingAmount, '0.00');
      expect(paid.soa!.paymentStatusLabel, 'Paid');
      expect(paid.cachedAt, isNull);
      rows = await local.database.query('official_soa_cache');
      expect(rows, hasLength(1));
      expect(rows.single['id'], originalId);
      expect(rows.single['cached_at'], isNot(cachedAt));
      client.error = const SocketException('Offline');
      final offline = await service.getSoa(15, 20);
      expect(offline.soa!.remainingAmount, '0.00');
      expect(offline.soa!.paymentStatusLabel, 'Paid');
      expect(offline.cachedAt, isNotNull);
      expect(offline.soa!.periodFrom, DateTime(2026, 9, 5));
      expect(offline.soa!.periodTo, DateTime(2026, 10, 5));
      expect(offline.soa!.billAmount, '170.00');
      expect(offline.soa!.penaltyAfterDueAmount, '17.00');
      expect(offline.soa!.totalAfterDue, '207.00');
    },
  );

  test(
    'offline cache preserves Phase 6D fields, decimal text and all local work',
    () async {
      await local.saveDownload(download(payload()), 10);
      await local.saveReading(15, 20, 10, '55', 'Keep draft');
      final before = {
        for (final table in [
          'reading_sheets',
          'accounts',
          'readings',
          'sync_queue',
        ])
          table: await local.database.query(table),
      };
      await service.getSoa(15, 20);
      client.error = const SocketException('Offline');
      final cached = await service.getSoa(15, 20);
      expect(cached.state, SoaState.available);
      expect(cached.soa!.remainingAmount, snapshot()['remainingAmount']);
      expect(cached.soa!.totalBillAmount, '9876.54');
      expect(
        cached.soa!.serviceInvoiceNumber,
        snapshot()['serviceInvoiceNumber'],
      );
      expect(cached.soa!.qrPayload, snapshot()['qrPayload']);
      expect(cached.soa!.birConfig, snapshot()['birConfig']);
      expect(cached.soa!.meterReaderName, snapshot()['meterReaderName']);
      expect(cached.soa!.readingRemarks, snapshot()['readingRemarks']);
      expect(cached.soa!.billAmount, snapshot()['billAmount']);
      expect(cached.soa!.meterMaintenance, '20.00');
      expect(cached.soa!.periodFrom, isNull);
      expect(cached.soa!.periodTo, DateTime(2026, 1, 31));
      expect(cached.soa!.powerCostAdjustment, isNull);
      expect(cached.soa!.arrearsAmount, isNull);
      expect(cached.soa!.totalAfterDue, isNull);
      expect(cached.soa!.currentPenaltyAmount, isNull);
      expect(cached.soa!.penaltyAfterDueAmount, isNull);
      expect(cached.soa!.chargeLines.single.unitCost, '10.00');
      expect(cached.soa!.chargeLines.single.quantity, '5');
      expect(cached.soa!.chargeLines.single.amount, '50.00');
      expect(cached.soa!.chargeLines.single.sortOrder, 1);
      for (final table in before.keys)
        expect(await local.database.query(table), before[table]);
      expect((await service.getSoa(15, 999)).state, SoaState.offline);
    },
  );

  for (final status in [401, 403, 404, 409, 500]) {
    test('HTTP $status does not fall back to cached SOA', () async {
      await service.getSoa(15, 20);
      final before = await local.database.query('official_soa_cache');
      client.status = status;
      client.download = {'code': 'not_posted'};
      if (status == 401 || status == 403) {
        await expectLater(
          service.getSoa(15, 20),
          throwsA(
            isA<AuthFailure>().having((e) => e.statusCode, 'status', status),
          ),
        );
      } else {
        final result = await service.getSoa(15, 20);
        expect(result.soa, isNull);
        expect(
          result.state,
          status == 409
              ? SoaState.notPosted
              : status == 404
              ? SoaState.unavailable
              : SoaState.failed,
        );
      }
      expect(await local.database.query('official_soa_cache'), before);
    });
  }

  test(
    'malformed or wrong-identity 200 does not replace cache or use it',
    () async {
      await service.getSoa(15, 20);
      final before = await local.database.query('official_soa_cache');
      for (final bad in [
        {},
        {...snapshot(), 'accountId': 999},
      ]) {
        client.download = bad;
        final result = await service.getSoa(15, 20);
        expect(result.state, SoaState.failed);
        expect(result.soa, isNull);
        expect(await local.database.query('official_soa_cache'), before);
      }
    },
  );

  testWidgets('cached screen labels offline freshness and stale balance', (
    tester,
  ) async {
    await tester.runAsync(() => service.getSoa(15, 20));
    client.error = const SocketException('Offline');
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
    await tester.runAsync(() => settleIO(tester));
    expect(find.text('Official SOA'), findsOneWidget);
    expect(find.text('Offline copy'), findsOneWidget);
    final cached = (await tester.runAsync(() => local.cachedSoa(15, 20)))!;
    final context = tester.element(find.byType(SoaScreen));
    final date = cached.cachedAt!.toLocal();
    final labels = MaterialLocalizations.of(context);
    expect(
      find.text(
        'Last retrieved: ${labels.formatMediumDate(date)} ${labels.formatTimeOfDay(TimeOfDay.fromDateTime(date))}',
      ),
      findsOneWidget,
    );
    expect(find.textContaining('may have changed'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  test(
    'v2 migration preserves every old table and cache survives reopen with reader isolation',
    () async {
      final directory = await Directory.systemTemp.createTemp('soa_v2_');
      final path = '${directory.path}/${session.databaseFileName}';
      LocalDatabase? migrated;
      LocalDatabase? other;
      try {
        final oldSchema =
            (await rootBundle.loadString('lib/data/local/schema.sql'))
                .split('-- next')
                .where(
                  (sql) => !sql.contains('CREATE TABLE official_soa_cache'),
                );
        final old = await databaseFactoryFfi.openDatabase(
          path,
          options: OpenDatabaseOptions(
            version: 2,
            onCreate: (db, _) async {
              for (final sql in oldSchema)
                if (sql.trim().isNotEmpty) await db.execute(sql);
            },
          ),
        );
        await old.insert('reading_sheets', {
          'id': 1,
          'server_id': 15,
          'name': 'Saved',
          'zone_book_id': 1,
          'zone': '2',
          'book': '3',
          'assigned_to': 10,
          'downloaded_at': 'saved',
        });
        await old.insert('accounts', {
          'id': 1,
          'server_account_id': 20,
          'reading_sheet_id': 1,
          'account_number': 'A20',
          'customer_name': 'Saved',
          'address': 'Street',
          'meter_number': 'M20',
          'classification': 1,
          'meter_size': '0.5',
          'previous_reading': '40',
          'previous_balance': '0',
          'sequence_number': 1,
        });
        await old.insert('readings', {
          'id': 1,
          'local_uuid': '00000000-0000-4000-8000-000000000001',
          'reading_sheet_id': 1,
          'account_id': 1,
          'previous_reading': '40',
          'current_reading': '55',
          'reading_date': '2026-01-31',
          'meter_condition': '',
          'remarks': 'Keep',
          'sync_status': 'pending',
          'created_at': 'saved',
          'updated_at': 'saved',
        });
        await old.insert('sync_queue', {
          'reading_id': 1,
          'created_at': 'saved',
          'retry_count': 2,
          'last_error': 'Offline',
        });
        await old.insert('printer_settings', {
          'id': 1,
          'device_address': 'TEST',
          'device_name': 'Test',
        });
        await old.insert('billing_rates', {
          'id': 1,
          'server_id': 1,
          'classification': 1,
          'meter_size': '0.5',
          'minimum_charge': '100.10',
          'rate11_to20': '12.3456',
          'rate21_to30': '13',
          'rate31_to40': '14',
          'rate41_up': '15',
          'effective_date': '2026-01-01',
          'is_active': 1,
        });
        await old.insert('bills', {
          'id': 1,
          'reading_id': 1,
          'billing_period': '2026-01',
          'billing_date': '2026-01-01',
          'due_date': '2026-01-15',
          'water_charge': '150.10',
          'previous_balance': '123.45',
          'penalties': '0',
          'other_charges': '0',
          'total_amount_due': '273.55',
          'created_at': 'saved',
        });
        final tables = [
          'reading_sheets',
          'accounts',
          'readings',
          'sync_queue',
          'printer_settings',
          'billing_rates',
          'bills',
        ];
        final before = {
          for (final table in tables) table: await old.query(table),
        };
        await old.close();
        migrated = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        expect(await migrated.database.getVersion(), 3);
        for (final table in tables)
          expect(await migrated.database.query(table), before[table]);
        await migrated.cacheSoa(
          OfficialSoa.fromJson(snapshot()),
          jsonEncode(snapshot()),
        );
        await migrated.close();
        migrated = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        expect(
          (await migrated.cachedSoa(15, 20))!.soa!.remainingAmount,
          snapshot()['remainingAmount'],
        );
        final readerB = AuthSession(
          token: 'synthetic',
          userId: 11,
          name: 'Other',
          email: 'other@example.invalid',
          expiresAt: session.expiresAt,
          apiBaseUrl: session.apiBaseUrl,
        );
        other = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: '${directory.path}/${readerB.databaseFileName}',
        );
        expect(await other.cachedSoa(15, 20), isNull);
        expect(
          (await migrated.cachedSoa(15, 20))!.soa!.qrPayload,
          snapshot()['qrPayload'],
        );
      } finally {
        await migrated?.close();
        await other?.close();
        await directory.delete(recursive: true);
      }
    },
  );
}
