import 'dart:async';
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
  testWidgets(
    'sync button shows results, refreshes SQLite and supports offline retry',
    (tester) async {
      sqfliteFfiInit();
      await tester.runAsync(() async {
        final local = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: inMemoryDatabasePath,
        );
        final client = UploadClient()..gate = Completer<void>();
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
          await service.saveReading(15, 20, '55', 'Keep remark');
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
                  accounts: await service.openDownloaded(15),
                  onClose: () {},
                ),
              ),
            ),
          );
          expect(find.text('Pending: 1  Synced: 0  Failed: 0'), findsOneWidget);
          await tester.tap(find.text('Sync Readings'));
          await client.started.future;
          await tester.pump();
          expect(find.text('Syncing...'), findsOneWidget);
          expect(
            tester.widget<FilledButton>(find.byType(FilledButton)).onPressed,
            isNull,
          );
          client.gate!.complete();
          await settleIO(tester);
          expect(find.text('Sync complete'), findsOneWidget);
          expect(
            find.text('Attempted: 1\nSynced: 1\nFailed: 0\nSkipped: 0'),
            findsOneWidget,
          );
          await tester.tap(find.text('OK'));
          await tester.pumpAndSettle();
          expect(find.text('Pending: 0  Synced: 1  Failed: 0'), findsOneWidget);
          await tester.tap(find.text('Sync Readings'));
          await settleIO(tester);
          expect(
            find.text('Attempted: 0\nSynced: 0\nFailed: 0\nSkipped: 1'),
            findsOneWidget,
          );
          expect(client.requests.length, 1);
          await tester.tap(find.text('OK'));
          await tester.pumpAndSettle();
          await service.saveReading(15, 20, '56', 'Offline remark');
          client.offline = true;
          await tester.tap(find.text('Sync Readings'));
          await settleIO(tester);
          expect(
            find.textContaining('Readings remain saved on this device.'),
            findsOneWidget,
          );
          expect(
            (await service.openDownloaded(15)).first.currentReadingText,
            '56',
          );
          expect(
            (await service.openDownloaded(15)).first.remarks,
            'Offline remark',
          );
          await tester.tap(find.text('OK'));
          await tester.pumpAndSettle();
          expect(find.text('Pending: 0  Synced: 0  Failed: 1'), findsOneWidget);
          client.offline = false;
          await tester.tap(find.text('Sync Readings'));
          await settleIO(tester);
          expect(
            find.text('Attempted: 1\nSynced: 1\nFailed: 0\nSkipped: 0'),
            findsOneWidget,
          );
          expect(client.server.length, 1);
          await tester.pumpWidget(const SizedBox());
        } finally {
          auth.close();
          await local.close();
        }
      });
    },
  );
}
