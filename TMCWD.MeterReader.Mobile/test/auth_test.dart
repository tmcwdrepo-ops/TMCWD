import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tmcwd_meter_reader_mobile/data/models/auth_session.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/auth_service.dart';
import 'package:tmcwd_meter_reader_mobile/features/auth/login_screen.dart';
import 'package:tmcwd_meter_reader_mobile/main.dart';

const baseUrl = 'https://tmcwd.example.invalid/';
const sessionKey = 'tmcwd.meter_reader.session.v1';
Map<String, dynamic> responseJson() => {
  'token': 'synthetic-token',
  'userId': 10,
  'role': 4,
  'name': 'Synthetic reader',
  'email': 'reader@example.invalid',
  'expiresAt': DateTime.now()
      .toUtc()
      .add(const Duration(days: 1))
      .toIso8601String(),
};

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  testWidgets(
    'saved sessions cannot keep the reader screen open after expiry',
    (tester) async {
      FlutterSecureStorage.setMockInitialValues({
        sessionKey: jsonEncode({
          ...responseJson(),
          'apiBaseUrl': baseUrl,
          'expiresAt': DateTime.now()
              .toUtc()
              .add(const Duration(seconds: 2))
              .toIso8601String(),
        }),
      });
      await tester.pumpWidget(
        MeterReaderApp(auth: AuthService(apiBaseUrl: baseUrl)),
      );
      await tester.pumpAndSettle();
      expect(find.text('Signed in as Synthetic reader'), findsOneWidget);
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();
      expect(find.text('Signed in as Synthetic reader'), findsNothing);
      expect(find.text('Meter reader login'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    },
  );

  test(
    'login posts credentials, saves only session and restores offline',
    () async {
      final client = FakeClient(200, jsonEncode(responseJson()));
      final auth = AuthService(apiBaseUrl: baseUrl, client: client);
      addTearDown(auth.close);
      final session = await auth.login(' 10 ', 'Synthetic password');
      expect(client.uri.toString(), '${baseUrl}api/auth/meter-reader/login');
      expect(client.request.followRedirects, false);
      expect(jsonDecode(client.request.body.toString()), {
        'identifier': '10',
        'password': 'Synthetic password',
      });
      final saved = await const FlutterSecureStorage().readAll();
      expect(saved.length, 1);
      expect(saved.values.single, isNot(contains('Synthetic password')));
      final offline = AuthService(
        apiBaseUrl: baseUrl,
        client: FakeClient(0, '', error: const SocketException('offline')),
      );
      addTearDown(offline.close);
      expect((await offline.restore())!.userId, session.userId);
      await offline.logout();
      expect(await const FlutterSecureStorage().readAll(), isEmpty);
    },
  );

  test(
    'HTTP failures, redirects, offline and malformed responses do not save login',
    () async {
      for (final status in [401, 403, 500, 302]) {
        final auth = AuthService(
          apiBaseUrl: baseUrl,
          client: FakeClient(status, '{}'),
        );
        await expectLater(
          auth.login('10', 'Synthetic'),
          throwsA(isA<AuthFailure>()),
        );
        expect(await const FlutterSecureStorage().readAll(), isEmpty);
        auth.close();
      }
      for (final body in [
        'invalid json',
        '[]',
        jsonEncode({...responseJson(), 'role': 1}),
        jsonEncode({...responseJson(), 'userId': 0}),
        jsonEncode({...responseJson(), 'expiresAt': '2000-01-01T00:00:00Z'}),
      ]) {
        final auth = AuthService(
          apiBaseUrl: baseUrl,
          client: FakeClient(200, body),
        );
        await expectLater(
          auth.login('10', 'Synthetic'),
          throwsA(isA<AuthFailure>()),
        );
        expect(await const FlutterSecureStorage().readAll(), isEmpty);
        auth.close();
      }
      final auth = AuthService(
        apiBaseUrl: baseUrl,
        client: FakeClient(0, '', error: const SocketException('offline')),
      );
      addTearDown(auth.close);
      await expectLater(
        auth.login('10', 'Synthetic'),
        throwsA(isA<AuthFailure>()),
      );
      expect(await auth.restore(), isNull);
    },
  );

  test(
    'expired, corrupt or different-deployment sessions are rejected',
    () async {
      final auth = AuthService(apiBaseUrl: baseUrl);
      addTearDown(auth.close);
      for (final value in [
        '[]',
        'invalid',
        jsonEncode({...responseJson(), 'apiBaseUrl': baseUrl, 'role': 1}),
        jsonEncode({
          ...responseJson(),
          'apiBaseUrl': baseUrl,
          'expiresAt': '2000-01-01T00:00:00Z',
        }),
        jsonEncode({
          ...responseJson(),
          'apiBaseUrl': 'https://other.example.invalid/',
        }),
      ]) {
        FlutterSecureStorage.setMockInitialValues({sessionKey: value});
        expect(await auth.restore(), isNull);
        expect(await const FlutterSecureStorage().readAll(), isEmpty);
      }
    },
  );

  test('HTTPS configuration and reader/deployment partitioning', () async {
    for (final value in [
      '',
      'http://tmcwd.example.invalid',
      'https://user:pass@tmcwd.example.invalid/',
      '${baseUrl}?token=x',
    ]) {
      final auth = AuthService(apiBaseUrl: value);
      await expectLater(auth.restore(), throwsA(isA<AuthFailure>()));
      auth.close();
    }
    final first = AuthSession.fromJson(responseJson(), baseUrl);
    final second = AuthSession.fromJson({
      ...responseJson(),
      'userId': 11,
    }, baseUrl);
    final other = AuthSession.fromJson(
      responseJson(),
      'https://other.example.invalid/',
    );
    expect(first.databaseFileName, isNot(second.databaseFileName));
    expect(first.databaseFileName, isNot(other.databaseFileName));
    expect(first.databaseFileName, isNot(contains('/')));
  });

  test('secure deletion failure propagates', () async {
    final auth = AuthService(apiBaseUrl: baseUrl, storage: FailingStorage());
    addTearDown(auth.close);
    await expectLater(auth.logout(), throwsA(isA<PlatformException>()));
  });

  testWidgets(
    'empty inputs and secure write failure never report login success',
    (tester) async {
      final client = FakeClient(200, jsonEncode(responseJson()));
      final auth = AuthService(
        apiBaseUrl: baseUrl,
        storage: FailingStorage(),
        client: client,
      );
      addTearDown(auth.close);
      var signedIn = false;
      await tester.pumpWidget(
        MaterialApp(
          home: LoginScreen(auth: auth, onLogin: (_) => signedIn = true),
        ),
      );
      await tester.tap(find.text('Sign in'));
      await tester.pump();
      expect(find.text('Enter your login.'), findsOneWidget);
      expect(client.uri, isNull);
      await tester.enterText(find.byType(TextFormField).first, '10');
      await tester.enterText(find.byType(TextFormField).last, 'Synthetic');
      await tester.tap(find.text('Sign in'));
      await tester.pumpAndSettle();
      expect(signedIn, false);
      expect(
        find.text('Could not securely save your login. Please retry.'),
        findsOneWidget,
      );
      expect(
        tester
            .widget<TextFormField>(find.byType(TextFormField).last)
            .controller!
            .text,
        '',
      );
    },
  );
}

class FakeClient implements HttpClient {
  FakeClient(int status, String body, {this.error})
    : request = FakeRequest(FakeResponse(status, body));
  final FakeRequest request;
  final Object? error;
  Uri? uri;
  @override
  Future<HttpClientRequest> postUrl(Uri url) async {
    uri = url;
    if (error != null) throw error!;
    return request;
  }

  @override
  void close({bool force = false}) {}
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeRequest implements HttpClientRequest {
  FakeRequest(this.response);
  final FakeResponse response;
  final body = StringBuffer();
  @override
  bool followRedirects = true;
  @override
  final HttpHeaders headers = FakeHeaders();
  @override
  void write(Object? object) => body.write(object);
  @override
  Future<HttpClientResponse> close() async => response;
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeHeaders implements HttpHeaders {
  @override
  set contentType(ContentType? value) {}
  @override
  void set(String name, Object value, {bool preserveHeaderCase = false}) {}
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FakeResponse extends Stream<List<int>> implements HttpClientResponse {
  FakeResponse(this.statusCode, this.body);
  @override
  final int statusCode;
  final String body;
  @override
  StreamSubscription<List<int>> listen(
    void Function(List<int>)? onData, {
    Function? onError,
    void Function()? onDone,
    bool? cancelOnError,
  }) => Stream.value(utf8.encode(body)).listen(
    onData,
    onError: onError,
    onDone: onDone,
    cancelOnError: cancelOnError,
  );
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class FailingStorage implements FlutterSecureStorage {
  @override
  dynamic noSuchMethod(Invocation invocation) =>
      Future<void>.error(PlatformException(code: 'synthetic_storage_failure'));
}
