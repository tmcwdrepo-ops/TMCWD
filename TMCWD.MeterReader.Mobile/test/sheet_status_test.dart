import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/account_list_screen.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'download_persistence_test.dart' show payload, download;
import 'reading_sheet_test.dart' show session, settleIO;
import 'reading_sync_test.dart' show UploadClient;

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  sqfliteFfiInit();
  late LocalDatabase local;
  late UploadClient client;
  late ReadingSheetService service;
  setUp(() async {
    local = await LocalDatabase.open(
      factory: databaseFactoryFfi,
      path: inMemoryDatabasePath,
    );
    await local.saveDownload(download(payload()), 10);
    client = UploadClient();
    final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
    service = ReadingSheetService(auth, session, database: Future.value(local));
    addTearDown(auth.close);
  });
  tearDown(() => local.close());

  test(
    'classification and readiness distinguish unread, sync states and posted readings, including zero',
    () {
      expect(DownloadAccount.readyForPosting([]), false);
      for (final state in [
        'Unread',
        'Pending',
        'Syncing',
        'Synced',
        'Failed',
        'Completed',
      ]) {
        final row = Map<String, dynamic>.from(payload()['accounts'][1] as Map);
        row['previousReading'] = 0;
        row['currentReading'] = state == 'Unread' ? null : 0;
        row['readingId'] = ['Synced', 'Completed'].contains(state) ? 300 : null;
        row['syncStatus'] = state == 'Completed'
            ? 'synced'
            : state.toLowerCase();
        row['readingStatus'] = state == 'Completed' ? 3 : 2;
        final account = DownloadAccount.fromJson(row);
        expect(account.statusLabel, state);
        expect(
          DownloadAccount.readyForPosting([account]),
          ['Synced', 'Completed'].contains(state),
        );
      }
      final row = Map<String, dynamic>.from(payload()['accounts'][0] as Map)
        ..['syncStatus'] = 'synced';
      row['currentReading'] = 39;
      expect(
        DownloadAccount.readyForPosting([DownloadAccount.fromJson(row)]),
        false,
      );
      row['currentReading'] = 50;
      row['readingId'] = null;
      expect(
        DownloadAccount.readyForPosting([DownloadAccount.fromJson(row)]),
        false,
      );
      row['readingId'] = 200;
      row['inRoster'] = false;
      expect(
        DownloadAccount.readyForPosting([DownloadAccount.fromJson(row)]),
        false,
      );
    },
  );

  test(
    'sync derives Ready for Posting without changing sheet/reading status or DateUpload',
    () async {
      expect(
        DownloadAccount.readyForPosting(await service.openDownloaded(15)),
        false,
      );
      await service.saveReading(15, 21, '7', 'Second');
      expect(
        DownloadAccount.readyForPosting(await service.openDownloaded(15)),
        false,
      );
      expect((await service.sync(15)).synced, 1);
      final accounts = await service.openDownloaded(15);
      expect(DownloadAccount.readyForPosting(accounts), true);
      expect(accounts.every((a) => a.readingStatus == 2 && a.canEdit), true);
      final sheet = (await local.database.query('reading_sheets')).single;
      expect(sheet['status'], 2);
      expect(sheet['date_upload'], isNull);
      expect(await local.database.query('bills'), isEmpty);
      expect(client.requests, hasLength(1));
      await service.saveReading(15, 21, '8', 'Still editable');
      expect(
        DownloadAccount.readyForPosting(await service.openDownloaded(15)),
        false,
      );
    },
  );

  test(
    'failed sync preserves local work and only retry restores readiness',
    () async {
      await service.saveReading(15, 21, '7', 'Retain');
      client.offline = true;
      expect((await service.sync(15)).failed, 1);
      final failed = await service.openDownloaded(15);
      expect(failed.last.statusLabel, 'Failed');
      expect(failed.last.currentReadingText, '7');
      expect(failed.last.remarks, 'Retain');
      expect(DownloadAccount.readyForPosting(failed), false);
      client.offline = false;
      await service.sync(15);
      expect(
        DownloadAccount.readyForPosting(await service.openDownloaded(15)),
        true,
      );
      expect((await local.downloadedSheets(10)).single.status.value, 2);
    },
  );

  test(
    'historical accounts remain viewable but cannot form a non-empty required roster',
    () async {
      final json = jsonDecode(jsonEncode(payload())) as Map<String, dynamic>;
      (json['accounts'] as List).removeLast();
      json['accounts'][0]['inRoster'] = false;
      await local.saveDownload(download(json), 10);
      final historical = await service.openDownloaded(15);
      expect(historical, hasLength(1));
      expect(historical.single.inRoster, false);
      expect(DownloadAccount.readyForPosting(historical), false);
    },
  );

  test(
    'synced InProgress sheet persists after reopen and remains editable offline',
    () async {
      final directory = await Directory.systemTemp.createTemp('tmcwd_posting_');
      final path = '${directory.path}/reader.db';
      var file = await LocalDatabase.open(
        factory: databaseFactoryFfi,
        path: path,
      );
      try {
        await file.saveDownload(download(payload()), 10);
        final repository = ReadingSheetService(
          service.auth,
          session,
          database: Future.value(file),
        );
        await repository.saveReading(15, 21, '7', 'Keep after reopen');
        await repository.sync(15);
        await file.close();
        file = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        client.offline = true;
        expect((await file.downloadedSheets(10)).single.status.value, 2);
        final reopened = await file.openDownload(15, 10);
        expect(DownloadAccount.readyForPosting(reopened), true);
        expect(reopened.every((a) => a.canEdit), true);
        expect(reopened.last.remarks, 'Keep after reopen');
        await file.saveReading(15, 21, 10, '8', 'Offline edit');
        expect(
          DownloadAccount.readyForPosting(await file.openDownload(15, 10)),
          false,
        );
      } finally {
        await file.close();
        await directory.delete(recursive: true);
      }
    },
  );

  test(
    'only downloaded web-completed status locks the locally viewable sheet',
    () async {
      final posted = jsonDecode(jsonEncode(payload())) as Map<String, dynamic>;
      posted['readingSheet']['status'] = 3;
      posted['readingSheet']['dateUpload'] = '2026-02-01T00:00:00';
      posted['accounts'][0]['readingStatus'] = 3;
      await local.saveDownload(download(posted), 10);
      client.offline = true;
      expect((await local.downloadedSheets(10)).single.status.value, 3);
      final accounts = await service.openDownloaded(15);
      expect(accounts, hasLength(2));
      expect(accounts.every((a) => !a.canEdit), true);
      expect(accounts.first.statusLabel, 'Completed');
      await expectLater(
        service.saveReading(15, 20, '51', 'Changed'),
        throwsA(isA<AuthFailure>()),
      );
    },
  );

  testWidgets(
    'summary and filters refresh after sync, show Ready for Posting, and keep entry editable',
    (tester) async {
      await tester.runAsync(() async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: AccountListScreen(
                service: service,
                sheet: (await local.downloadedSheets(10)).single,
                accounts: await service.openDownloaded(15),
                onClose: () {},
              ),
            ),
          ),
        );
        expect(find.text('Complete Reading Sheet'), findsNothing);
        expect(find.text('Ready for Posting'), findsNothing);
        expect(find.textContaining('2 accounts · Unread: 1'), findsOneWidget);
        await tester.tap(find.text('All'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Unread').last);
        await tester.pumpAndSettle();
        expect(find.text('A21'), findsOneWidget);
        expect(find.text('A20'), findsNothing);
        await tester.tap(find.text('Unread').first);
        await tester.pumpAndSettle();
        await tester.tap(find.text('All').last);
        await tester.pumpAndSettle();
        await service.saveReading(15, 21, '7', 'Second');
        await tester.tap(find.text('Sync Readings'));
        await settleIO(tester);
        await tester.tap(find.text('OK'));
        await tester.pumpAndSettle();
        expect(find.text('Pending: 0  Synced: 2  Failed: 0'), findsOneWidget);
        expect(find.text('Ready for Posting'), findsOneWidget);
        expect((await local.downloadedSheets(10)).single.status.value, 2);
        await tester.tap(find.text('A20'));
        await tester.pumpAndSettle();
        expect(
          tester.widget<TextField>(find.byType(TextField).first).enabled,
          true,
        );
        await tester.pumpWidget(const SizedBox());
      });
    },
  );
}
