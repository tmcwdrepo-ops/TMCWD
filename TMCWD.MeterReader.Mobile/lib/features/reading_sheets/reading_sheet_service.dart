import '../../data/local/local_database.dart';
import '../../data/models/reading_sheet_download.dart';
export '../../data/models/reading_sheet_download.dart';
import '../../data/models/auth_session.dart';
import '../auth/auth_service.dart';
import '../../core/reading_validation.dart';
import '../../data/models/official_soa.dart';

class ReadingSheetService {
  ReadingSheetService(this.auth, this.session, {this.database});
  final AuthService auth;
  final AuthSession session;
  final Future<LocalDatabase>? database;
  final _sync = <int, Future<SyncResult>>{};

  Future<SoaResult> getSoa(int sheetId, int accountId) async {
    try {
      late String payloadJson;
      final json = await auth.getJson(
        session,
        'api/reading-sheet/mobile/$sheetId/accounts/$accountId/soa',
        onResponse: (text) => payloadJson = text,
      );
      final soa = OfficialSoa.fromJson(json as Map<String, dynamic>);
      if (soa.readingSheetId != sheetId || soa.accountId != accountId)
        throw const FormatException();
      if (database != null) {
        try {
          await (await database!).cacheSoa(soa, payloadJson);
        } catch (_) {
          return SoaResult(
            SoaState.available,
            soa: soa,
            message:
                'Fresh SOA loaded, but the offline copy could not be saved.',
          );
        }
      }
      return SoaResult(SoaState.available, soa: soa);
    } on AuthFailure catch (error) {
      if (error.statusCode == 401 || error.statusCode == 403) rethrow;
      if (error.statusCode == 409 && error.code == 'not_posted') {
        return const SoaResult(
          SoaState.notPosted,
          message: 'Awaiting billing post.',
        );
      }
      if (error.statusCode == 404) {
        return const SoaResult(
          SoaState.unavailable,
          message:
              'This account SOA is no longer available in the accessible roster.',
        );
      }
      if (error.code == 'network_unavailable' &&
          error.statusCode == null &&
          database != null) {
        try {
          final cached = await (await database!).cachedSoa(sheetId, accountId);
          if (cached != null) return cached;
        } catch (_) {
          return const SoaResult(
            SoaState.offline,
            message:
                'SOA cannot be loaded while offline. The saved copy could not be opened.',
          );
        }
      }
      return SoaResult(
        error.code == 'network_unavailable'
            ? SoaState.offline
            : SoaState.failed,
        message: error.code == 'network_unavailable'
            ? 'SOA cannot be loaded while offline or the server cannot be reached.'
            : 'SOA could not be loaded. Please retry.',
      );
    } on FormatException {
      return const SoaResult(
        SoaState.failed,
        message: 'TMCWD returned an invalid SOA response.',
      );
    } on TypeError {
      return const SoaResult(
        SoaState.failed,
        message: 'TMCWD returned an invalid SOA response.',
      );
    }
  }

  Future<SyncResult> sync(int sheetId) {
    return _sync.putIfAbsent(
      sheetId,
      () => _upload(sheetId).whenComplete(() {
        _sync.remove(sheetId);
      }),
    );
  }

  Future<SyncResult> _upload(int sheetId) async {
    if (database == null)
      throw const AuthFailure('Local storage is unavailable.');
    final local = await database!;
    final List<Map<String, Object?>> rows;
    try {
      rows = await local.syncReadings(session.userId, sheetId);
    } on FormatException catch (error) {
      throw AuthFailure(error.message);
    }
    var attempted = 0, synced = 0, failed = 0, skipped = 0;
    final errors = <int, String>{};
    for (final row in rows) {
      if (row['sync_status'] == 'synced' ||
          row['current_reading'] == null ||
          !await local.beginSync(row)) {
        skipped++;
        continue;
      }
      attempted++;
      try {
        final response = await auth.postJson(
          session,
          'api/reading-sheet/mobile/$sheetId/readings',
          {
            'accountId': row['server_account_id'],
            // Preserve .NET decimal precision; never round-trip through a Dart double.
            'currentReading': row['current_reading'],
            'remarks': row['remarks'],
            'readingDate': row['reading_date'],
          },
        );
        if (response is! Map<String, dynamic> ||
            response['readingId'] is! int ||
            (response['readingId'] as int) <= 0 ||
            response['readingSheetId'] != sheetId ||
            response['accountId'] != row['server_account_id'] ||
            response['remarks'] != row['remarks'] ||
            response['currentReading'] is! String ||
            readingUsage(response['currentReading'] as String, '0') !=
                readingUsage(row['current_reading'] as String, '0')) {
          throw const AuthFailure(
            'Invalid upload confirmation. Local work is preserved. Retry.',
          );
        }
        await local.finishSync(row, serverId: response['readingId'] as int);
        synced++;
      } on AuthFailure catch (error) {
        await local.finishSync(row, error: error.message);
        failed++;
        errors[row['server_account_id'] as int] = error.message;
        if (error.statusCode == 401 || error.statusCode == 403) rethrow;
      } catch (_) {
        const message =
            'Upload could not be confirmed. Local work is preserved. Retry.';
        await local.finishSync(row, error: message);
        failed++;
        errors[row['server_account_id'] as int] = message;
      }
    }
    return SyncResult(attempted, synced, failed, skipped, errors);
  }

  Future<({List<AssignedSheet> sheets, String? notice})> loadAssigned() async {
    var cached = <AssignedSheet>[];
    String? storageFailure;
    if (database != null) {
      try {
        cached = await (await database!).downloadedSheets(session.userId);
      } catch (_) {
        storageFailure =
            'Local storage unavailable. Downloads cannot be saved. Retry opening storage.';
      }
    }
    try {
      final remote = await assigned();
      final merged = {for (final sheet in cached) sheet.id: sheet};
      for (final sheet in remote) {
        merged[sheet.id] = AssignedSheet.fromJson({
          'id': sheet.id,
          'name': sheet.name,
          'status': sheet.status.value,
          'billingDate': sheet.billingDate?.toIso8601String(),
          'zoneBookId': sheet.zoneBookId,
          'downloadedAt': merged[sheet.id]?.downloadedAt,
        });
      }
      return (sheets: merged.values.toList(), notice: storageFailure);
    } on AuthFailure catch (error) {
      if (error.statusCode == 401 || error.statusCode == 403 || cached.isEmpty)
        rethrow;
      return (
        sheets: cached,
        notice: '${error.message} Showing downloaded sheets.',
      );
    }
  }

  Future<List<DownloadAccount>> openDownloaded(int id) async {
    if (database == null)
      throw const AuthFailure('Local storage is unavailable.');
    try {
      return await (await database!).openDownload(id, session.userId);
    } catch (_) {
      throw const AuthFailure(
        'Could not open the downloaded sheet. Please retry.',
      );
    }
  }

  Future<List<AssignedSheet>> assigned() async {
    final json = await auth.getJson(
      session,
      'api/reading-sheet/mobile/assigned',
    );
    return _parse(
      () => (json as List)
          .map((row) => AssignedSheet.fromJson(row as Map<String, dynamic>))
          .toList(),
    );
  }

  Future<List<DownloadAccount>> saveReading(
    int sheetId,
    int accountId,
    String current,
    String remarks,
  ) async {
    if (database == null)
      throw const AuthFailure('Local storage is unavailable.');
    try {
      final local = await database!;
      await local.saveReading(
        sheetId,
        accountId,
        session.userId,
        current,
        remarks,
      );
      return await local.openDownload(sheetId, session.userId);
    } on FormatException catch (error) {
      throw AuthFailure(error.message);
    } catch (_) {
      throw const AuthFailure(
        'Reading could not be saved locally. Please retry.',
      );
    }
  }

  Future<List<DownloadAccount>> download(int id) async {
    final json = await auth.getJson(
      session,
      'api/reading-sheet/mobile/$id/download',
    );
    final download = _parse(
      () => SheetDownload.fromJson(
        json as Map<String, dynamic>,
        id,
        session.userId,
      ),
    );
    if (database == null) return download.accounts;
    try {
      final local = await database!;
      await local.saveDownload(download, session.userId);
      return await local.openDownload(id, session.userId);
    } catch (_) {
      throw const AuthFailure(
        'Download could not be saved. Existing offline work is preserved. Please retry.',
      );
    }
  }

  T _parse<T>(T Function() parse) {
    try {
      return parse();
    } on Object {
      throw const AuthFailure(
        'TMCWD returned an invalid reading sheet response.',
      );
    }
  }
}

class SyncResult {
  const SyncResult(
    this.attempted,
    this.synced,
    this.failed,
    this.skipped,
    this.errors,
  );
  final int attempted, synced, failed, skipped;
  final Map<int, String> errors;
}
