import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:tmcwd_meter_reader_mobile/data/local/local_database.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/auth_session.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/reader_home.dart';
import 'package:tmcwd_meter_reader_mobile/features/reading_sheets/reading_sheet_service.dart';
import 'auth_test.dart' show FakeRequest, FakeResponse, FakeHeaders;

final session = AuthSession(
  token: 'synthetic-token',
  userId: 10,
  name: 'Reader',
  email: 'reader@example.invalid',
  expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
  apiBaseUrl: 'http://localhost:5178/',
);
const sheet = {
  'id': 1,
  'name': 'Test sheet',
  'status': 1,
  'billingDate': '2026-02-01T00:00:00',
  'zoneBookId': 5,
};
const account = {
  'accountId': 20,
  'accountNumber': 'TEST-20',
  'name': 'Synthetic account',
  'meterNumber': 'M20',
  'address': 'Test Street',
  'zone': 2,
  'book': 3,
  'sequence': 1,
  'previousReading': 7,
  'previousReadingDate': null,
  'currentReading': null,
  'classification': 1,
  'meterSize': 0.5,
  'readingId': null,
  'readingStatus': null,
  'readingDate': null,
};

class GetClient implements HttpClient {
  int status = 200;
  Object? error;
  Object assigned = [sheet];
  Object download = {
    'readingSheet': {...sheet, 'assignedTo': 10},
    'accounts': [account],
  };
  final paths = <String>[];
  final headers = RecordingHeaders();
  final requests = <GetRequest>[];
  @override
  Future<HttpClientRequest> getUrl(Uri uri) async {
    paths.add(uri.path);
    if (error != null) throw error!;
    final request = GetRequest(
      FakeResponse(
        status,
        jsonEncode(uri.path.endsWith('assigned') ? assigned : download),
      ),
      headers,
    );
    requests.add(request);
    return request;
  }

  @override
  void close({bool force = false}) {}
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class GetRequest extends FakeRequest {
  GetRequest(super.response, this.recording);
  final RecordingHeaders recording;
  @override
  HttpHeaders get headers => recording;
}

class RecordingHeaders extends FakeHeaders {
  final values = <String, Object>{};
  @override
  void set(String name, Object value, {bool preserveHeaderCase = false}) {
    values[name] = value;
  }
}

Future<void> settleIO(WidgetTester tester) async {
  for (var attempt = 0; attempt < 100; attempt++) {
    await Future<void>.delayed(const Duration(milliseconds: 10));
    await tester.pump();
    if (find.byType(CircularProgressIndicator).evaluate().isEmpty) {
      await tester.pumpAndSettle();
      return;
    }
  }
  fail('The SQLite-backed screen did not finish loading.');
}

void main() {
  test(
    'bearer GETs parse assigned sheets and baseline accounts without persistence',
    () async {
      final client = GetClient();
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(auth, session);
      expect((await service.assigned()).single.statusLabel, 'Created');
      final row = (await service.download(1)).single;
      expect(row.accountId, 20);
      expect(row.previousReading, 7);
      expect(row.previousReadingDate, isNull);
      expect(row.currentReading, isNull);
      expect(client.paths, [
        '/api/reading-sheet/mobile/assigned',
        '/api/reading-sheet/mobile/1/download',
      ]);
      expect(
        client.headers.values[HttpHeaders.authorizationHeader],
        'Bearer synthetic-token',
      );
      expect(
        client.requests.every((request) => !request.followRedirects),
        true,
      );
      client.assigned = [];
      expect(await service.assigned(), isEmpty);
      client.download = {
        'readingSheet': {'id': 1, 'assignedTo': 11},
        'accounts': [account],
      };
      await expectLater(service.download(1), throwsA(isA<AuthFailure>()));
    },
  );

  test(
    '401, 403, offline and malformed data are recoverable failures',
    () async {
      final client = GetClient();
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      final service = ReadingSheetService(auth, session);
      for (final code in [401, 403, 404, 409, 500, 302]) {
        client.status = code;
        await expectLater(
          service.assigned(),
          throwsA(
            isA<AuthFailure>().having((e) => e.statusCode, 'status', code),
          ),
        );
      }
      client.status = 200;
      client.assigned = [{}];
      await expectLater(service.assigned(), throwsA(isA<AuthFailure>()));
      client.error = const SocketException('offline');
      await expectLater(service.assigned(), throwsA(isA<AuthFailure>()));
    },
  );

  testWidgets('home opens account preview and retries visible errors', (
    tester,
  ) async {
    sqfliteFfiInit();
    final local = await tester.runAsync(
      () => LocalDatabase.open(
        factory: databaseFactoryFfi,
        path: inMemoryDatabasePath,
      ),
    );
    await tester.runAsync(() async {
      final client = GetClient();
      final auth = AuthService(apiBaseUrl: session.apiBaseUrl, client: client);
      addTearDown(auth.close);
      var loggedOut = false;
      await tester.pumpWidget(
        MaterialApp(
          home: ReaderHome(
            session: session,
            auth: auth,
            onLogout: () async {
              loggedOut = true;
            },
            database: Future.value(local!),
          ),
        ),
      );
      await settleIO(tester);
      expect(find.text('Sheet 1: Test sheet'), findsOneWidget);
      await tester.tap(find.text('Download / Open'));
      await settleIO(tester);
      expect(find.text('TEST-20'), findsOneWidget);
      expect(find.text('Synthetic account'), findsOneWidget);
      // AccountListScreen shows account cards (number + name); previous reading
      // date detail is only visible inside ReadingEntry on tap.
      await tester.tap(find.byTooltip('Back to assigned sheets'));
      client.status = 403;
      await tester.tap(find.byTooltip('Refresh'));
      await settleIO(tester);
      expect(
        find.text('You are not authorized to access this reading sheet.'),
        findsOneWidget,
      );
      client.status = 200;
      client.assigned = [];
      await tester.tap(find.text('Retry'));
      await settleIO(tester);
      expect(find.text('Sheet 1: Test sheet'), findsOneWidget);
      expect(find.text('Open offline'), findsOneWidget);
      client.status = 401;
      await tester.tap(find.byTooltip('Refresh'));
      await settleIO(tester);
      await tester.tap(find.text('Sign in again'));
      await settleIO(tester);
      expect(loggedOut, true);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
