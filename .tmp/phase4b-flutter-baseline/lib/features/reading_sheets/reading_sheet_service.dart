import '../../data/local/local_database.dart';
import '../../data/models/reading_sheet_download.dart';
export '../../data/models/reading_sheet_download.dart';
import '../../data/models/auth_session.dart';
import '../auth/auth_service.dart';

class ReadingSheetService {
  const ReadingSheetService(this.auth, this.session, {this.database});
  final AuthService auth;
  final AuthSession session;
  final Future<LocalDatabase>? database;
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
