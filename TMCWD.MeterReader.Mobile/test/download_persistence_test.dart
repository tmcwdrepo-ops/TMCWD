import 'dart:io';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/auth_session.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'reading_sheet_test.dart' show GetClient, session;

Map<String, dynamic> payload() => {
  'readingSheet': {
    'id': 15,
    'name': 'Offline sheet',
    'assignedTo': 10,
    'status': 2,
    'zoneBookId': 1,
    'billingDate': '2026-02-01T00:00:00',
    'dueDate': '2026-02-15T00:00:00',
    'billingPeriodStart': '2026-01-31T00:00:00',
    'seqFrom': 1,
    'seqTo': 41,
    'disconnectionDate': '2026-02-20T00:00:00',
    'dateCreated': '2026-01-01T00:00:00',
    'dateUpload': null,
  },
  'accounts': [
    {
      'accountId': 20,
      'accountNumber': 'A20',
      'name': 'Test account',
      'meterNumber': 'M20',
      'address': 'Test Street',
      'zone': 2,
      'book': 3,
      'sequence': 1,
      'classification': 1,
      'meterSize': 0.5,
      'previousReading': 40,
      'previousReadingDate': '2026-01-01T00:00:00',
      'readingId': 200,
      'currentReading': 50,
      'readingDate': '2026-01-31T00:00:00',
      'readingStatus': 2,
    },
    {
      'accountId': 21,
      'accountNumber': 'A21',
      'name': 'New account',
      'meterNumber': 'M21',
      'address': 'Test Street',
      'zone': 2,
      'book': 3,
      'sequence': 2,
      'classification': 1,
      'meterSize': 0.5,
      'previousReading': 7,
      'previousReadingDate': null,
      'readingId': null,
      'currentReading': null,
      'readingDate': null,
      'readingStatus': null,
    },
  ],
};
SheetDownload download(Map<String, dynamic> json) =>
    SheetDownload.fromJson(json, 15, 10);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  sqfliteFfiInit();
  late LocalDatabase local;
  setUp(() async {
    local = await LocalDatabase.open(
      factory: databaseFactoryFfi,
      path: inMemoryDatabasePath,
    );
  });
  tearDown(() async {
    await local.close();
  });

  test(
    'download persists sheet/accounts/saved reading and repeated download is idempotent',
    () async {
      final client = GetClient()..download = payload();
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(
        auth,
        session,
        database: Future.value(local),
      );
      await service.download(15);
      final first = (await local.database.query('readings')).single;
      await service.download(15);
      expect((await local.downloadedSheets(10)).single.isDownloaded, true);
      expect(await local.downloadedSheets(11), isEmpty);
      final accounts = await local.openDownload(15, 10);
      expect(accounts.map((a) => a.accountId), [20, 21]);
      expect(accounts.first.currentReading, 50);
      expect(accounts.first.previousReadingDate, DateTime(2026, 1, 1));
      expect(accounts.last.currentReading, isNull);
      expect(accounts.last.previousReadingDate, isNull);
      expect((await local.database.query('reading_sheets')).length, 1);
      expect((await local.database.query('accounts')).length, 2);
      final second = (await local.database.query('readings')).single;
      expect(second['id'], first['id']);
      expect(second['local_uuid'], first['local_uuid']);
      expect(second['sync_status'], 'synced');
      expect(
        (await local.database.query(
          'reading_sheets',
        )).single['disconnection_date'],
        '2026-02-20T00:00:00',
      );
    },
  );

  test(
    'partial SQL failure rolls back all changes and failed HTTP leaves offline work intact',
    () async {
      await local.saveDownload(download(payload()), 10);
      final beforeSheet = await local.database.query('reading_sheets');
      final beforeAccounts = await local.database.query('accounts');
      final beforeReadings = await local.database.query('readings');
      await local.database.execute(
        "CREATE TRIGGER fail_roster BEFORE UPDATE ON accounts WHEN NEW.account_number = 'FAIL' BEGIN SELECT RAISE(ABORT, 'Synthetic failure'); END",
      );
      final incoming = payload();
      (incoming['readingSheet'] as Map)['name'] = 'Should roll back';
      (incoming['accounts'] as List)[0]['name'] = 'Should also roll back';
      (incoming['accounts'] as List)[1]['accountNumber'] = 'FAIL';
      await expectLater(
        local.saveDownload(download(incoming), 10),
        throwsA(isA<DatabaseException>()),
      );
      expect(await local.database.query('reading_sheets'), beforeSheet);
      expect(await local.database.query('accounts'), beforeAccounts);
      expect(await local.database.query('readings'), beforeReadings);
      final client = GetClient()..error = const SocketException('offline');
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(
        auth,
        session,
        database: Future.value(local),
      );
      await expectLater(service.download(15), throwsA(isA<AuthFailure>()));
      final cached = await service.loadAssigned();
      expect(cached.sheets.single.id, 15);
      expect(cached.notice, contains('Showing downloaded sheets'));
      final opened = await service.openDownloaded(15);
      expect(opened.length, 2);
      expect(opened.first.currentReading, 50);
      expect(client.paths.length, 2); // Opening SQLite makes no request.
      expect(await local.database.query('readings'), beforeReadings);
    },
  );

  test(
    'refresh preserves every unsynced field and retains omitted local work',
    () async {
      await local.saveDownload(download(payload()), 10);
      final id = (await local.database.query('readings')).single['id'];
      await local.database.update(
        'readings',
        {
          'current_reading': '61',
          'remarks': 'Local draft',
          'sync_status': 'pending',
          'updated_at': '2026-02-01T01:00:00Z',
        },
        where: 'id = ?',
        whereArgs: [id],
      );
      await local.database.insert('sync_queue', {
        'reading_id': id,
        'created_at': '2026-02-01T01:00:00Z',
      });
      for (final state in ['pending', 'syncing', 'failed']) {
        await local.database.update(
          'readings',
          {'sync_status': state},
          where: 'id = ?',
          whereArgs: [id],
        );
        final before = await local.database.query('readings');
        final queue = await local.database.query('sync_queue');
        final incoming = payload();
        (incoming['accounts'] as List)[0].addAll({
          'name': 'Updated server name',
          'previousReading': 45,
          'currentReading': 999,
        });
        await local.saveDownload(download(incoming), 10);
        expect(await local.database.query('readings'), before);
        expect(await local.database.query('sync_queue'), queue);
        final account = (await local.database.query(
          'accounts',
          where: 'server_account_id = 20',
        )).single;
        expect(account['customer_name'], 'Updated server name');
        expect(account['previous_reading'], '45');
        final opened = await local.openDownload(15, 10);
        expect(opened.first.currentReading, 61);
        expect(opened.first.remarks, 'Local draft');
      }
      final removed = payload();
      (removed['accounts'] as List).removeAt(0);
      await local.saveDownload(download(removed), 10);
      expect((await local.openDownload(15, 10)).map((a) => a.accountId), [
        20,
        21,
      ]);
      expect(
        (await local.database.query(
          'accounts',
          where: 'server_account_id = 20',
        )).single['in_roster'],
        0,
      );
    },
  );

  test('reader partitions survive switching readers and reopening', () async {
    final directory = await Directory.systemTemp.createTemp('tmcwd_partition_');
    final firstPath = '${directory.path}/${session.databaseFileName}';
    final other = AuthSession(
      token: 'other',
      userId: 11,
      name: 'Other',
      email: 'other@example.invalid',
      expiresAt: session.expiresAt,
      apiBaseUrl: session.apiBaseUrl,
    );
    try {
      final first = await LocalDatabase.open(
        factory: databaseFactoryFfi,
        path: firstPath,
      );
      await first.saveDownload(download(payload()), 10);
      await first.close();
      final second = await LocalDatabase.open(
        factory: databaseFactoryFfi,
        path: '${directory.path}/${other.databaseFileName}',
      );
      expect(await second.downloadedSheets(11), isEmpty);
      await second.close();
      final reopened = await LocalDatabase.open(
        factory: databaseFactoryFfi,
        path: firstPath,
      );
      expect((await reopened.openDownload(15, 10)).length, 2);
      await reopened.close();
    } finally {
      await directory.delete(recursive: true);
    }
  });

  test(
    'version 1 migrates forward without losing readings, remarks or queued work',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'tmcwd_migration_',
      );
      final path = '${directory.path}/v1.db';
      try {
        final schema = (await rootBundle.loadString('lib/data/local/schema.sql'))
            .split('-- next')
            .where((sql) => !sql.contains('CREATE TABLE official_soa_cache'))
            .join('-- next')
            .split('\n')
            .where(
              (line) => !RegExp(
                r'^\s*(disconnection_date|date_created|date_upload|previous_reading_date|in_roster)\s',
              ).hasMatch(line),
            )
            .join('\n');
        final old = await databaseFactoryFfi.openDatabase(
          path,
          options: OpenDatabaseOptions(
            version: 1,
            onCreate: (db, _) async {
              for (final sql in schema.split('-- next')) {
                if (sql.trim().isNotEmpty) await db.execute(sql);
              }
            },
          ),
        );
        await old.insert('reading_sheets', {
          'id': 1,
          'server_id': 15,
          'name': 'Existing',
          'zone_book_id': 1,
          'zone': '2',
          'book': '3',
          'assigned_to': 10,
          'downloaded_at': '2026-01-01T00:00:00Z',
        });
        await old.insert('accounts', {
          'id': 1,
          'server_account_id': 20,
          'reading_sheet_id': 1,
          'account_number': 'A20',
          'customer_name': 'Existing',
          'address': '',
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
          'current_reading': '61',
          'reading_date': '2026-01-31',
          'meter_condition': '',
          'remarks': 'Retain me',
          'created_at': '2026-01-31',
          'updated_at': '2026-01-31',
        });
        await old.insert('sync_queue', {
          'reading_id': 1,
          'created_at': '2026-01-31',
        });
        final before = await old.query('readings');
        await old.close();
        final migrated = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        expect(
          await migrated.database.getVersion(),
          LocalDatabase.schemaVersion,
        );
        expect(await migrated.database.query('readings'), before);
        expect((await migrated.database.query('sync_queue')).length, 1);
        expect((await migrated.openDownload(15, 10)).single.currentReading, 61);
        await migrated.saveDownload(download(payload()), 10);
        expect(
          await migrated.database.query('readings', where: 'id = 1'),
          before,
        );
        expect(
          (await migrated.database.query('reading_sheets')).single['id'],
          1,
        );
        await migrated.close();
      } finally {
        await directory.delete(recursive: true);
      }
    },
  );
}
