import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/core/reading_validation.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_entry.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'download_persistence_test.dart' show payload;
import 'reading_sheet_test.dart' show GetClient, session, settleIO;

Map<String, dynamic> unreadPayload({int count = 2}) {
  final data = payload();
  final template = (data['accounts'] as List).first as Map<String, dynamic>;
  data['accounts'] = List.generate(
    count,
    (i) => {
      ...template,
      'accountId': 20 + i,
      'accountNumber': 'A-${20 + i}',
      'sequence': i + 1,
      'previousReading': 0,
      'currentReading': null,
      'readingId': null,
      'readingStatus': null,
      'readingDate': null,
    },
  );
  return data;
}

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
    'exact usage follows backend negative/decreasing decimal validation',
    () {
      expect(readingUsage(null, '40'), isNull);
      expect(readingUsage('', '40'), isNull);
      expect(readingUsage('0', '0'), '0');
      expect(readingUsage('0.3', '0.1'), '0.2');
      expect(
        readingUsage('12345678901234567890.01', '12345678901234567890'),
        '0.01',
      );
      for (final current in ['-1', '39', 'NaN', 'Infinity', 'invalid']) {
        expect(() => readingUsage(current, '40'), throwsFormatException);
      }
    },
  );

  test(
    'local save works without HTTP, marks pending and preserves zero versus null',
    () async {
      await local.saveDownload(
        SheetDownload.fromJson(unreadPayload(), 15, 10),
        10,
      );
      final client = GetClient()..error = const SocketException('Offline');
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(
        auth,
        session,
        database: Future.value(local),
      );
      final accounts = await service.saveReading(15, 20, '0', 'Actual zero');
      expect(accounts.first.currentReadingText, '0');
      expect(accounts.first.remarks, 'Actual zero');
      expect(accounts.last.currentReadingText, isNull);
      expect(client.paths, isEmpty);
      final first = (await local.database.query('readings')).single;
      expect(first['current_reading'], '0');
      expect(first['consumption'], '0');
      expect(first['sync_status'], 'pending');
      expect(first['reading_status'], 2);
      expect(
        first['local_uuid'],
        matches(
          RegExp(
            r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
          ),
        ),
      );
      await service.saveReading(15, 20, '0.3', 'Updated locally');
      final updated = (await local.database.query('readings')).single;
      expect(updated['id'], first['id']);
      expect(updated['local_uuid'], first['local_uuid']);
      expect(updated['created_at'], first['created_at']);
      expect(updated['consumption'], '0.3');
      await service.saveReading(15, 20, '', 'No reading entered');
      expect(
        (await local.database.query('readings')).single['current_reading'],
        isNull,
      );
      expect(
        (await local.openDownload(15, 10)).first.currentReadingText,
        isNull,
      );
    },
  );

  test(
    'validation rejects decreasing readings, wrong reader, and completed work without changes',
    () async {
      await local.saveDownload(SheetDownload.fromJson(payload(), 15, 10), 10);
      final before = await local.database.query('readings');
      await expectLater(
        local.saveReading(15, 20, 10, '39', ''),
        throwsFormatException,
      );
      await expectLater(
        local.saveReading(15, 20, 11, '55', ''),
        throwsFormatException,
      );
      expect(await local.database.query('readings'), before);
      await local.database.update('readings', {'reading_status': 3});
      await expectLater(
        local.saveReading(15, 20, 10, '55', ''),
        throwsFormatException,
      );
      await local.database.update('reading_sheets', {'status': 3});
      await expectLater(
        local.saveReading(15, 21, 10, '8', ''),
        throwsFormatException,
      );
    },
  );

  test(
    're-download preserves locally entered value, remarks, UUID and frozen previous baseline',
    () async {
      await local.saveDownload(SheetDownload.fromJson(payload(), 15, 10), 10);
      await local.saveReading(15, 20, 10, '55.25', 'Unsynced meter remark');
      final before = await local.database.query('readings');
      final refresh = payload();
      (refresh['accounts'] as List).first.addAll({
        'previousReading': 99,
        'currentReading': 999,
      });
      await local.saveDownload(SheetDownload.fromJson(refresh, 15, 10), 10);
      expect(await local.database.query('readings'), before);
      final restored = (await local.openDownload(15, 10)).first;
      expect(restored.currentReadingText, '55.25');
      expect(restored.previousReadingText, '40');
      expect(restored.remarks, 'Unsynced meter remark');
      await local.saveReading(15, 20, 10, '56.25', 'Updated draft');
      expect(
        (await local.database.query('readings')).single['consumption'],
        '16.25',
      );
    },
  );

  test(
    'local progress counts four entered readings out of 41, including zero',
    () async {
      await local.saveDownload(
        SheetDownload.fromJson(unreadPayload(count: 41), 15, 10),
        10,
      );
      await local.saveReading(15, 24, 10, '', '');
      expect(await local.database.query('readings'), isEmpty);
      for (var i = 0; i < 4; i++) {
        await local.saveReading(15, 20 + i, 10, i.toString(), '');
      }
      final accounts = await local.openDownload(15, 10);
      expect(accounts.length, 41);
      expect(accounts.where((a) => a.currentReadingText != null).length, 4);
      expect(accounts.where((a) => a.currentReadingText == null).length, 37);
    },
  );

  test(
    'locally entered readings and remarks survive database close/reopen',
    () async {
      final directory = await Directory.systemTemp.createTemp('tmcwd_entry_');
      final path = '${directory.path}/reader.db';
      try {
        final first = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        await first.saveDownload(
          SheetDownload.fromJson(unreadPayload(), 15, 10),
          10,
        );
        await first.saveReading(15, 20, 10, '12.5', 'Persist after restart');
        final before = await first.database.query('readings');
        await first.close();
        final reopened = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        expect(await reopened.database.query('readings'), before);
        final row = (await reopened.openDownload(15, 10)).first;
        expect(row.currentReadingText, '12.5');
        expect(row.remarks, 'Persist after restart');
        await reopened.close();
      } finally {
        await directory.delete(recursive: true);
      }
    },
  );

  testWidgets('Save & Next and Previous restore fields and local progress', (
    tester,
  ) async {
    await tester.runAsync(() async {
      await local.saveDownload(
        SheetDownload.fromJson(unreadPayload(), 15, 10),
        10,
      );
      final auth = AuthService(
        apiBaseUrl: session.apiBaseUrl,
        client: GetClient()..error = const SocketException('Offline'),
      );
      addTearDown(auth.close);
      final service = ReadingSheetService(
        auth,
        session,
        database: Future.value(local),
      );
      final accounts = await local.openDownload(15, 10);
      var closed = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: ReadingEntry(
              service: service,
              sheetId: 15,
              accounts: accounts,
              initialIndex: 0,
              onClose: () {
                closed = true;
              },
              onBusyChanged: (_) {},
            ),
          ),
        ),
      );
      expect(find.text('Local readings: 0/2'), findsOneWidget);
      await tester.enterText(find.byType(TextField).first, '0');
      await tester.enterText(find.byType(TextField).last, 'Actual zero');
      await tester.ensureVisible(find.text('Save & Next'));
      await tester.tap(find.text('Save & Next'));
      await settleIO(tester);
      // Give the async DB write and setState rebuild time to complete.
      for (var i = 0; i < 20; i++) {
        await Future<void>.delayed(const Duration(milliseconds: 20));
        await tester.pump();
      }
      await tester.pumpAndSettle();
      expect(find.text('Account 2 of 2', skipOffstage: false), findsOneWidget);
      expect(find.text('Local readings: 1/2'), findsOneWidget);
      expect(
        tester.widget<TextField>(find.byType(TextField).first).controller!.text,
        '',
      );
      await tester.tap(find.text('Previous'));
      await settleIO(tester);
      expect(
        tester.widget<TextField>(find.byType(TextField).first).controller!.text,
        '0',
      );
      expect(
        tester.widget<TextField>(find.byType(TextField).last).controller!.text,
        'Actual zero',
      );
      await tester.enterText(find.byType(TextField).first, '-1');
      await tester.tap(find.text('Next'));
      await settleIO(tester);
      expect(find.text('Present reading cannot be negative.'), findsOneWidget);
      expect(find.text('Account 1 of 2'), findsOneWidget);
      await tester.enterText(find.byType(TextField).first, '0');
      await tester.tap(find.text('Back to accounts'));
      await settleIO(tester);
      expect(closed, true);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
