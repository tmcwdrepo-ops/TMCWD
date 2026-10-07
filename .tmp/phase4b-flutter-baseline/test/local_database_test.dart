import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/local_models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  sqfliteFfiInit();

  test(
    'database opens, models round-trip and records survive reopening',
    () async {
      final directory = await Directory.systemTemp.createTemp('tmcwd_test_');
      final path = '${directory.path}/local.db';
      LocalDatabase? local;
      try {
        local = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        final sheet = ReadingSheet(
          id: 1,
          serverId: 15,
          name: 'Synthetic sheet',
          zoneBookId: 2,
          zone: '3',
          book: '2',
          assignedTo: 7,
          status: 1,
          billingDate: '2026-10-05',
          dueDate: '2026-10-20',
          downloadedAt: '2026-10-05T00:00:00Z',
        );
        await local.database.insert('reading_sheets', sheet.toMap());
        await local.close();
        local = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        final restored = ReadingSheet.fromMap(
          (await local.database.query('reading_sheets')).single,
        );
        expect(restored.toMap(), sheet.toMap());
        expect(await local.database.getVersion(), LocalDatabase.schemaVersion);
        expect(
          (await local.database.rawQuery('PRAGMA foreign_keys'))
              .single
              .values
              .single,
          1,
        );
      } finally {
        await local?.close();
        await directory.delete(recursive: true);
      }
    },
  );
}
