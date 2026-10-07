import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'auth_test.dart' show FakeRequest, FakeResponse;
import 'download_persistence_test.dart' show payload, download;
import 'reading_sheet_test.dart' show session, RecordingHeaders;

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
    final json = jsonDecode(jsonEncode(payload())) as Map<String, dynamic>;
    for (final row in json['accounts'] as List) {
      row['previousReading'] = 0;
      row['readingId'] = null;
      row['currentReading'] = null;
      row['readingStatus'] = null;
      row['readingDate'] = null;
    }
    await local.saveDownload(download(json), 10);
    client = UploadClient();
    final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
    addTearDown(auth.close);
    service = ReadingSheetService(auth, session, database: Future.value(local));
  });
  tearDown(() => local.close());

  test(
    'successful upload preserves zero, remarks, identity and skips synced readings',
    () async {
      await service.saveReading(15, 20, '0', 'Meter is zero');
      final before = (await local.database.query('readings')).single;
      final result = await service.sync(15);
      expect(
        (result.attempted, result.synced, result.failed, result.skipped),
        (1, 1, 0, 0),
      );
      final saved = (await local.database.query('readings')).single;
      expect(saved['sync_status'], 'synced');
      expect(saved['server_reading_id'], 300);
      expect(saved['current_reading'], '0');
      expect(saved['remarks'], 'Meter is zero');
      expect(saved['local_uuid'], before['local_uuid']);
      expect(saved['updated_at'], before['updated_at']);
      expect(await local.database.query('sync_queue'), isEmpty);
      expect(
        (client.requests.single.headers as RecordingHeaders)
            .values['authorization'],
        'Bearer ${session.token}',
      );
      expect(client.requests.single.followRedirects, false);
      expect((await service.sync(15)).skipped, 1);
      expect(client.requests.length, 1);
    },
  );

  test(
    'lost response and network failure retain work; retry creates no duplicate',
    () async {
      await service.saveReading(15, 20, '12.5', 'Keep this remark');
      client.loseResponse = true;
      final failed = await service.sync(15);
      expect(failed.failed, 1);
      expect(failed.errors[20], contains('timed out'));
      var saved = (await local.database.query('readings')).single;
      expect(saved['sync_status'], 'failed');
      expect(saved['current_reading'], '12.5');
      expect(saved['remarks'], 'Keep this remark');
      expect(client.server.length, 1);
      expect(
        (await local.database.query('sync_queue')).single['retry_count'],
        1,
      );
      client.offline = true;
      expect((await service.sync(15)).failed, 1);
      expect(
        (await service.openDownloaded(15)).first.remarks,
        'Keep this remark',
      );
      client.offline = false;
      client.loseResponse = false;
      expect((await service.sync(15)).synced, 1);
      saved = (await local.database.query('readings')).single;
      expect(saved['sync_status'], 'synced');
      expect(client.server.length, 1);
      expect(saved['remarks'], 'Keep this remark');
    },
  );

  test(
    'new save uses intended reading date and retry repairs old failed timestamp without changing local work',
    () async {
      await service.saveReading(15, 20, '1', 'Normal Reading');
      var row = (await local.database.query('readings')).single;
      expect(row['reading_date'], '2026-01-31T00:00:00.000');
      final before = {...row};
      await local.database.update('readings', {
        'reading_date': '2026-10-07T00:05:07.694573Z',
        'sync_status': 'failed',
      });
      expect((await service.sync(15)).synced, 1);
      final sent =
          jsonDecode(client.requests.single.body.toString())
              as Map<String, dynamic>;
      expect(sent['readingDate'], '2026-01-31T00:00:00.000');
      expect(sent['currentReading'], '1');
      expect(sent['remarks'], 'Normal Reading');
      row = (await local.database.query('readings')).single;
      for (final field in [
        'current_reading',
        'previous_reading',
        'remarks',
        'updated_at',
        'created_at',
        'local_uuid',
      ])
        expect(row[field], before[field]);
      expect(row['reading_date'], sent['readingDate']);
    },
  );

  test(
    '400/409 preserve and expose the backend validation reason without clearing the reading',
    () async {
      await service.saveReading(15, 20, '1', 'Normal Reading');
      for (final reason in {
        400: 'Reading date must be one day before billing date.',
        409:
            'A posted reading cannot be changed or moved to another account or sheet.',
      }.entries) {
        client.status = reason.key;
        client.message = reason.value;
        final result = await service.sync(15);
        expect(result.errors[20], reason.value);
        final row = (await local.database.query('readings')).single;
        expect(row['current_reading'], '1');
        expect(row['remarks'], 'Normal Reading');
        expect(row['sync_status'], 'failed');
      }
    },
  );

  test(
    'multiple readings return summary, blank remains null and failures retry',
    () async {
      await service.saveReading(15, 20, '1', 'First');
      await service.saveReading(15, 21, '', 'Blank draft');
      expect((await service.sync(15)).skipped, 1);
      await service.saveReading(15, 20, '2', 'Edited');
      await service.saveReading(15, 21, '3', 'Second');
      client.rejectAccount = 21;
      final result = await service.sync(15);
      expect(
        (result.attempted, result.synced, result.failed, result.skipped),
        (2, 1, 1, 0),
      );
      expect(result.errors[21], contains('locked'));
      client.rejectAccount = null;
      final retry = await service.sync(15);
      expect(
        (retry.attempted, retry.synced, retry.failed, retry.skipped),
        (1, 1, 0, 1),
      );
      expect(await local.database.query('readings'), hasLength(2));
      expect(client.server.length, 2);
    },
  );

  test(
    '401/403 propagate AuthFailure after retaining the local failed reading',
    () async {
      await service.saveReading(15, 20, '0', 'Retain');
      for (final status in [401, 403]) {
        client.status = status;
        await expectLater(
          service.sync(15),
          throwsA(
            isA<AuthFailure>().having((e) => e.statusCode, 'status', status),
          ),
        );
        final row = (await local.database.query('readings')).single;
        expect(row['sync_status'], 'failed');
        expect(row['current_reading'], '0');
        expect(row['remarks'], 'Retain');
      }
    },
  );

  test(
    'concurrent sync shares an upload and refuses edits while syncing',
    () async {
      await service.saveReading(15, 20, '4', 'Original');
      client.gate = Completer<void>();
      final first = service.sync(15), second = service.sync(15);
      expect(identical(first, second), true);
      await client.started.future;
      expect(
        (await local.database.query('readings')).single['sync_status'],
        'syncing',
      );
      await expectLater(
        service.saveReading(15, 20, '5', 'Changed'),
        throwsA(isA<AuthFailure>()),
      );
      client.gate!.complete();
      expect((await first).synced, 1);
      expect(client.requests.length, 1);
    },
  );

  test(
    'reopen recovers interrupted syncing without clearing reading or remarks',
    () async {
      final directory = await Directory.systemTemp.createTemp('tmcwd_sync_');
      final path = '${directory.path}/reader.db';
      final file = await LocalDatabase.open(
        factory: databaseFactoryFfi,
        path: path,
      );
      try {
        await file.saveDownload(download(payload()), 10);
        await file.saveReading(15, 20, 10, '55', 'Interrupted');
        final row = (await file.syncReadings(10, 15)).first;
        expect(await file.beginSync(row), true);
        await file.close();
        final reopened = await LocalDatabase.open(
          factory: databaseFactoryFfi,
          path: path,
        );
        try {
          final stored = (await reopened.syncReadings(10, 15)).first;
          expect(stored['sync_status'], 'failed');
          expect(stored['current_reading'], '55');
          expect(stored['remarks'], 'Interrupted');
          final retry = ReadingSheetService(
            service.auth,
            session,
            database: Future.value(reopened),
          );
          expect((await retry.sync(15)).synced, 1);
        } finally {
          await reopened.close();
        }
      } finally {
        await directory.delete(recursive: true);
      }
    },
  );
}

class UploadClient implements HttpClient {
  final requests = <UploadRequest>[];
  final server = <int, Map<String, dynamic>>{};
  int status = 200;
  String? message;
  int? rejectAccount;
  bool offline = false, loseResponse = false;
  Completer<void>? gate;
  final started = Completer<void>();
  @override
  Future<HttpClientRequest> postUrl(Uri url) async {
    if (offline) throw const SocketException('Synthetic offline');
    final request = UploadRequest(this);
    requests.add(request);
    expect(url.path, '/api/reading-sheet/mobile/15/readings');
    return request;
  }

  @override
  void close({bool force = false}) {}
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class UploadRequest extends FakeRequest {
  UploadRequest(this.client) : super(FakeResponse(200, '{}'));
  final UploadClient client;
  @override
  final headers = RecordingHeaders();
  @override
  Future<HttpClientResponse> close() async {
    if (!client.started.isCompleted) client.started.complete();
    if (client.gate != null) await client.gate!.future;
    final json = jsonDecode(body.toString()) as Map<String, dynamic>;
    expect(json.containsKey('readerId'), false);
    if (client.status != 200)
      return FakeResponse(
        client.status,
        jsonEncode({'message': client.message}),
      );
    final id = json['accountId'] as int;
    if (client.rejectAccount == id) return FakeResponse(409, '{}');
    final receipt = {
      'readingId':
          client.server[id]?['readingId'] ?? 300 + client.server.length,
      'readingSheetId': 15,
      'accountId': id,
      'currentReading': json['currentReading'],
      'remarks': json['remarks'],
    };
    client.server[id] = receipt;
    if (client.loseResponse) throw TimeoutException('Synthetic lost response');
    return FakeResponse(200, jsonEncode(receipt));
  }
}
