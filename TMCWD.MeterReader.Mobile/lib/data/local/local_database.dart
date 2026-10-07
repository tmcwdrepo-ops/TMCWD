import 'dart:math';
import 'dart:convert';
import '../models/official_soa.dart';
import '../../core/reading_validation.dart';
import '../../core/statuses.dart';
import 'package:flutter/services.dart';
import '../models/reading_sheet_download.dart';
import 'package:sqflite/sqflite.dart';

/// Device-local cache only. Never opens a connection to the server database.
class LocalDatabase {
  LocalDatabase._(this.database);

  final Database database;
  static const schemaVersion = 3;

  static Future<LocalDatabase> open({
    DatabaseFactory? factory,
    String? path,
    String fileName = 'tmcwd_meter_reader.db',
  }) async {
    final selectedFactory = factory ?? databaseFactory;
    final location =
        path ?? '${await selectedFactory.getDatabasesPath()}/$fileName';
    final schema = await rootBundle.loadString('lib/data/local/schema.sql');
    final database = await selectedFactory.openDatabase(
      location,
      options: OpenDatabaseOptions(
        version: schemaVersion,
        onConfigure: (db) => db.execute('PRAGMA foreign_keys = ON'),
        onOpen: (db) async {
          // Retry uploads interrupted by a previous process; the API is idempotent.
          await db.update('readings', {
            'sync_status': 'failed',
          }, where: "sync_status = 'syncing'");
        },
        onCreate: (db, version) async {
          for (final statement in schema.split('-- next')) {
            if (statement.trim().isNotEmpty) {
              await db.execute(statement);
            }
          }
        },
        onUpgrade: (db, oldVersion, newVersion) async {
          if (oldVersion < 2) {
            for (final column in [
              'disconnection_date',
              'date_created',
              'date_upload',
            ]) {
              await db.execute(
                'ALTER TABLE reading_sheets ADD COLUMN $column TEXT',
              );
            }
            await db.execute(
              'ALTER TABLE accounts ADD COLUMN previous_reading_date TEXT',
            );
            await db.execute(
              'ALTER TABLE accounts ADD COLUMN in_roster INTEGER NOT NULL DEFAULT 1 CHECK(in_roster IN (0,1))',
            );
          }
          if (oldVersion < 3) {
            await db.execute(
              schema
                  .split('-- next')
                  .singleWhere(
                    (statement) =>
                        statement.contains('CREATE TABLE official_soa_cache'),
                  ),
            );
          }
        },
        onDowngrade: (db, oldVersion, newVersion) async {
          throw StateError('Database downgrade is unsupported.');
        },
      ),
    );
    return LocalDatabase._(database);
  }

  Future<void> cacheSoa(OfficialSoa soa, String payloadJson) async {
    final values = <String, Object?>{
      'reading_sheet_id': soa.readingSheetId,
      'account_id': soa.accountId,
      'service_invoice_number': soa.serviceInvoiceNumber,
      'billing_reference_id': soa.billingReferenceId,
      'payload_json': payloadJson,
      'cached_at': DateTime.now().toUtc().toIso8601String(),
    };
    await database.transaction((tx) async {
      final updated = await tx.update(
        'official_soa_cache',
        values,
        where: 'reading_sheet_id = ? AND account_id = ?',
        whereArgs: [soa.readingSheetId, soa.accountId],
      );
      if (updated == 0) await tx.insert('official_soa_cache', values);
    });
  }

  Future<SoaResult?> cachedSoa(int sheetId, int accountId) async {
    final rows = await database.query(
      'official_soa_cache',
      where: 'reading_sheet_id = ? AND account_id = ?',
      whereArgs: [sheetId, accountId],
    );
    if (rows.isEmpty) return null;
    final row = rows.single;
    final soa = OfficialSoa.fromJson(
      jsonDecode(row['payload_json'] as String) as Map<String, dynamic>,
    );
    if (soa.readingSheetId != sheetId || soa.accountId != accountId)
      throw const FormatException('Invalid cached SOA identity.');
    return SoaResult(
      SoaState.available,
      soa: soa,
      cachedAt: DateTime.parse(row['cached_at'] as String),
    );
  }

  Future<void> saveDownload(SheetDownload download, int readerId) async {
    if (download.sheet.assignedTo != readerId)
      throw StateError('Unexpected reader.');
    await database.transaction((tx) async {
      final stored = await tx.query(
        'reading_sheets',
        where: 'server_id = ?',
        whereArgs: [download.sheet.serverId],
      );
      if (stored.isNotEmpty && stored.single['assigned_to'] != readerId)
        throw StateError('Unexpected local reader.');
      final sheetValues = download.sheet.toMap()..remove('id');
      final sheetId = stored.isEmpty
          ? await tx.insert('reading_sheets', sheetValues)
          : stored.single['id'] as int;
      if (stored.isNotEmpty)
        await tx.update(
          'reading_sheets',
          sheetValues,
          where: 'id = ?',
          whereArgs: [sheetId],
        );
      await tx.update(
        'accounts',
        {'in_roster': 0},
        where: 'reading_sheet_id = ?',
        whereArgs: [sheetId],
      );
      for (final account in download.accounts) {
        final existing = await tx.query(
          'accounts',
          where: 'reading_sheet_id = ? AND server_account_id = ?',
          whereArgs: [sheetId, account.accountId],
        );
        final values = <String, Object?>{
          'reading_sheet_id': sheetId,
          'server_account_id': account.accountId,
          'account_number': account.accountNumber,
          'customer_name': account.name,
          'meter_number': account.meterNumber,
          'address': account.address,
          'classification': account.classification,
          'meter_size': account.meterSize,
          'sequence_number': account.sequence,
          'previous_reading': account.previousReading.toString(),
          'previous_reading_date': account.previousReadingDate
              ?.toIso8601String(),
          'in_roster': account.inRoster ? 1 : 0,
        };
        // No balance is supplied by this API; keep any existing financial snapshot.
        final accountId = existing.isEmpty
            ? await tx.insert('accounts', {...values, 'previous_balance': '0'})
            : existing.single['id'] as int;
        if (existing.isNotEmpty)
          await tx.update(
            'accounts',
            values,
            where: 'id = ?',
            whereArgs: [accountId],
          );
        final reading = await tx.query(
          'readings',
          where: 'reading_sheet_id = ? AND account_id = ?',
          whereArgs: [sheetId, accountId],
        );
        // A network snapshot never owns pending, syncing or failed local work.
        if (reading.isNotEmpty && reading.single['sync_status'] != 'synced')
          continue;
        if (account.readingId == null) continue;
        final now = DateTime.now().toUtc().toIso8601String();
        final readingValues = <String, Object?>{
          'server_reading_id': account.readingId,
          'reading_sheet_id': sheetId,
          'account_id': accountId,
          'previous_reading': account.previousReading.toString(),
          'current_reading': account.currentReading?.toString(),
          'reading_date': account.readingDate!.toIso8601String(),
          'reading_status': account.readingStatus,
          'sync_status': 'synced',
          'synced_at': now,
          'remarks': account.remarks,
        };
        if (reading.isEmpty) {
          await tx.insert('readings', {
            ...readingValues,
            'local_uuid': _readingUuid(),
            'meter_condition': '',
            'created_at': now,
            'updated_at': now,
          });
        } else {
          await tx.update(
            'readings',
            {...readingValues, 'consumption': null},
            where: 'id = ?',
            whereArgs: [reading.single['id']],
          );
        }
      }
    });
  }

  Future<List<AssignedSheet>> downloadedSheets(int readerId) async {
    final rows = await database.query(
      'reading_sheets',
      where: 'assigned_to = ? AND status != 4',
      whereArgs: [readerId],
      orderBy: 'id',
    );
    return rows
        .map(
          (row) => AssignedSheet.fromJson({
            'id': row['server_id'],
            'name': row['name'],
            'status': row['status'],
            'billingDate': row['billing_date'],
            'zoneBookId': row['zone_book_id'],
            'downloadedAt': row['downloaded_at'],
          }),
        )
        .toList();
  }

  Future<List<DownloadAccount>> openDownload(int serverId, int readerId) async {
    final sheets = await database.query(
      'reading_sheets',
      where: 'server_id = ? AND assigned_to = ? AND status != 4',
      whereArgs: [serverId, readerId],
    );
    if (sheets.isEmpty) throw StateError('This sheet has not been downloaded.');
    final sheet = sheets.single;
    final rows = await database.rawQuery(
      '''
      SELECT a.*, r.previous_reading AS saved_previous_reading, r.server_reading_id, r.current_reading, r.reading_date, r.reading_status, r.remarks, r.sync_status
      FROM accounts a LEFT JOIN readings r ON r.account_id = a.id AND r.reading_sheet_id = a.reading_sheet_id AND r.reading_status != 4
      WHERE a.reading_sheet_id = ? AND (a.in_roster = 1 OR r.id IS NOT NULL)
      ORDER BY a.sequence_number, a.server_account_id
    ''',
      [sheet['id']],
    );
    return rows
        .map(
          (row) => DownloadAccount.fromJson({
            'accountId': row['server_account_id'],
            'accountNumber': row['account_number'],
            'name': row['customer_name'],
            'meterNumber': row['meter_number'],
            'address': row['address'],
            'zone': int.tryParse(sheet['zone'] as String) ?? 0,
            'book': int.tryParse(sheet['book'] as String) ?? 0,
            'sequence': row['sequence_number'],
            'classification': row['classification'],
            'meterSize': num.parse(row['meter_size'] as String),
            'previousReading': num.parse(
              (row['saved_previous_reading'] ?? row['previous_reading'])
                  as String,
            ),
            'previousReadingText':
                row['saved_previous_reading'] ?? row['previous_reading'],
            'currentReadingText': row['current_reading'],
            'previousReadingDate': row['previous_reading_date'],
            'currentReading': row['current_reading'] == null
                ? null
                : num.parse(row['current_reading'] as String),
            'readingId': row['server_reading_id'],
            'readingDate': row['reading_date'],
            'readingStatus': row['reading_status'],
            'remarks': row['remarks'],
            'syncStatus': row['sync_status'],
            'sheetStatus': sheet['status'],
            'inRoster': row['in_roster'] == 1,
          }),
        )
        .toList();
  }

  Future<void> close() => database.close();

  Future<List<Map<String, Object?>>> syncReadings(
    int readerId,
    int serverSheetId,
  ) async {
    final rows = await database.rawQuery(
      '''
    SELECT r.*, s.server_id AS server_sheet_id, s.billing_date, s.billing_period_start, a.server_account_id
    FROM readings r JOIN reading_sheets s ON s.id = r.reading_sheet_id
    JOIN accounts a ON a.id = r.account_id AND a.reading_sheet_id = s.id
    WHERE s.assigned_to = ? AND s.server_id = ? AND r.reading_status != 4
    ORDER BY r.id
  ''',
      [readerId, serverSheetId],
    );
    return rows
        .map(
          (row) => <String, Object?>{
            ...row,
            if (row['sync_status'] == 'pending' ||
                row['sync_status'] == 'failed')
              'reading_date': _intendedReadingDate(row),
          },
        )
        .toList();
  }

  static String _intendedReadingDate(Map<String, Object?> sheet) {
    final billing = DateTime.tryParse(sheet['billing_date'] as String? ?? '');
    if (billing == null)
      throw const FormatException(
        'The sheet has no valid billing date. Refresh the sheet before syncing.',
      );
    final expected = DateTime(billing.year, billing.month, billing.day - 1);
    final intended = DateTime.tryParse(
      sheet['billing_period_start'] as String? ?? '',
    );
    if (intended != null &&
        DateTime(intended.year, intended.month, intended.day) != expected)
      throw const FormatException(
        'The sheet reading date must be one day before billing date. Contact your administrator.',
      );
    return expected.toIso8601String();
  }

  Future<bool> beginSync(Map<String, Object?> row) => database.transaction((
    tx,
  ) async {
    final now = DateTime.now().toUtc().toIso8601String();
    final changed = await tx.update(
      'readings',
      {'sync_status': 'syncing', 'reading_date': row['reading_date']},
      where:
          "id = ? AND updated_at = ? AND sync_status IN ('pending','failed') AND current_reading IS NOT NULL",
      whereArgs: [row['id'], row['updated_at']],
    );
    if (changed == 0) return false;
    await tx.rawInsert(
      '''INSERT INTO sync_queue(reading_id, retry_count, last_attempt_at, last_error, created_at)
      VALUES (?, 1, ?, NULL, ?) ON CONFLICT(reading_id) DO UPDATE SET
      retry_count = retry_count + 1, last_attempt_at = excluded.last_attempt_at, last_error = NULL''',
      [row['id'], now, now],
    );
    return true;
  });

  Future<void> finishSync(
    Map<String, Object?> row, {
    int? serverId,
    String? error,
  }) => database.transaction((tx) async {
    final updated = await tx.update(
      'readings',
      {
        'sync_status': serverId == null ? 'failed' : 'synced',
        if (serverId != null) 'server_reading_id': serverId,
        'synced_at': serverId == null
            ? null
            : DateTime.now().toUtc().toIso8601String(),
      },
      where: "id = ? AND updated_at = ? AND sync_status = 'syncing'",
      whereArgs: [row['id'], row['updated_at']],
    );
    if (updated != 1)
      throw StateError('The local reading changed during synchronization.');
    if (serverId != null) {
      await tx.delete(
        'sync_queue',
        where: 'reading_id = ?',
        whereArgs: [row['id']],
      );
    } else {
      await tx.update(
        'sync_queue',
        {'last_error': error},
        where: 'reading_id = ?',
        whereArgs: [row['id']],
      );
    }
  });

  String _readingUuid() {
    final random = Random.secure();
    final bytes = List.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  Future<void> saveReading(
    int serverSheetId,
    int serverAccountId,
    int readerId,
    String current,
    String remarks,
  ) async {
    await database.transaction((tx) async {
      final sheets = await tx.query(
        'reading_sheets',
        where: 'server_id = ? AND assigned_to = ?',
        whereArgs: [serverSheetId, readerId],
      );
      if (sheets.isEmpty ||
          ![
            ReadingSheetStatus.created.value,
            ReadingSheetStatus.inProgress.value,
          ].contains(sheets.single['status'])) {
        throw const FormatException('The reading sheet is no longer active.');
      }
      final sheetId = sheets.single['id'];
      final accounts = await tx.query(
        'accounts',
        where: 'reading_sheet_id = ? AND server_account_id = ?',
        whereArgs: [sheetId, serverAccountId],
      );
      if (accounts.isEmpty)
        throw const FormatException(
          'The account does not belong to this reading sheet.',
        );
      final account = accounts.single;
      final readings = await tx.query(
        'readings',
        where: 'reading_sheet_id = ? AND account_id = ?',
        whereArgs: [sheetId, account['id']],
      );
      final stored = readings.isEmpty ? null : readings.single;
      if (stored != null &&
          [
            ReadingSheetStatus.completed.value,
            ReadingSheetStatus.deleted.value,
          ].contains(stored['reading_status'])) {
        throw const FormatException('A completed reading cannot be changed.');
      }
      if (stored?['sync_status'] == 'syncing')
        throw const FormatException(
          'This reading is currently syncing. Retry after sync finishes.',
        );
      final previous =
          (stored?['previous_reading'] ?? account['previous_reading'])
              as String;
      final value = readingUsage(current, '0');
      final usage = readingUsage(value, previous);
      if (stored == null && value == null && remarks.isEmpty) return;
      if (stored != null &&
          readingUsage(stored['current_reading'] as String?, '0') == value &&
          stored['remarks'] == remarks)
        return;
      final now = DateTime.now().toUtc().toIso8601String();
      final changes = <String, Object?>{
        'current_reading': value,
        'consumption': usage,
        'remarks': remarks,
        'sync_status': SyncStatus.pending.name,
        'synced_at': null,
        'updated_at': now,
      };
      if (stored == null) {
        await tx.insert('readings', {
          ...changes,
          'local_uuid': _readingUuid(),
          'reading_sheet_id': sheetId,
          'account_id': account['id'],
          'previous_reading': previous,
          'reading_date': _intendedReadingDate(sheets.single),
          'reading_status': ReadingSheetStatus.inProgress.value,
          'meter_condition': '',
          'created_at': now,
        });
      } else {
        await tx.update(
          'readings',
          changes,
          where: 'id = ?',
          whereArgs: [stored['id']],
        );
      }
    });
  }
}
