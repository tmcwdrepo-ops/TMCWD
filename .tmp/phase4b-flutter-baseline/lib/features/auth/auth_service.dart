import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../data/models/auth_session.dart';

class AuthFailure implements Exception {
  const AuthFailure(this.message, {this.statusCode});
  final String message;
  final int? statusCode;
}

class AuthService {
  AuthService({
    String apiBaseUrl = const String.fromEnvironment('API_BASE_URL'),
    FlutterSecureStorage? storage,
    HttpClient? client,
  }) : _configuredUrl = apiBaseUrl,
       _storage = storage ?? const FlutterSecureStorage(),
       _client = client ?? HttpClient();

  static const _sessionKey = 'tmcwd.meter_reader.session.v1';
  static const _timeout = Duration(seconds: 20);
  final String _configuredUrl;
  final FlutterSecureStorage _storage;
  final HttpClient _client;

  Uri get baseUri {
    final uri = Uri.tryParse(_configuredUrl.trim());

    if (uri == null ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        uri.hasQuery ||
        uri.hasFragment ||
        uri.toString().length > 120) {
      throw const AuthFailure(
        'Configure API_BASE_URL with the TMCWD API address.',
      );
    }

    final isHttps = uri.scheme == 'https';

    final isLocalDevelopment =
        uri.scheme == 'http' &&
        (uri.host == '10.0.2.2' ||
            uri.host == 'localhost' ||
            uri.host == '127.0.0.1');

    if (!isHttps && !isLocalDevelopment) {
      throw const AuthFailure(
        'API_BASE_URL must use HTTPS except for local development.',
      );
    }

    return uri.replace(
      path: uri.path.endsWith('/') ? uri.path : '${uri.path}/',
    );
  }

  Future<AuthSession?> restore() async {
    final url = baseUri.toString();
    final value = await _storage.read(key: _sessionKey);
    if (value == null) return null;
    AuthSession session;
    try {
      final json = jsonDecode(value);
      if (json is! Map<String, dynamic> || json['apiBaseUrl'] != url) {
        throw const FormatException('Invalid session deployment.');
      }
      session = AuthSession.fromJson(json, url);
    } on FormatException {
      await logout();
      return null;
    }
    if (session.isExpired) {
      await logout();
      return null;
    }
    return session;
  }

  Future<AuthSession> login(String identifier, String password) async {
    identifier = identifier.trim();
    if (identifier.isEmpty ||
        identifier.length > 100 ||
        password.trim().isEmpty ||
        password.length > 256) {
      throw const AuthFailure('Enter your TMCWD login and password.');
    }
    final uri = baseUri;
    AuthSession session;
    try {
      final request = await _client
          .postUrl(uri.resolve('api/auth/meter-reader/login'))
          .timeout(_timeout);
      // Never resend credentials to a redirect destination.
      request.followRedirects = false;
      request.headers.contentType = ContentType.json;
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');
      request.write(
        jsonEncode({'identifier': identifier, 'password': password}),
      );
      final response = await request.close().timeout(_timeout);
      if (response.statusCode != HttpStatus.ok) {
        await response.drain<void>().timeout(_timeout);
        if (response.statusCode == HttpStatus.unauthorized) {
          throw const AuthFailure('Invalid login or password.');
        }
        if (response.statusCode == HttpStatus.forbidden) {
          throw const AuthFailure(
            'An active meter-reader account is required.',
          );
        }
        throw const AuthFailure('Login is unavailable. Please try again.');
      }
      final body = await response
          .transform(utf8.decoder)
          .join()
          .timeout(_timeout);
      final json = jsonDecode(body);
      if (json is! Map<String, dynamic>) {
        throw const FormatException('Invalid login response.');
      }
      session = AuthSession.fromJson(json, uri.toString());
      if (session.isExpired) {
        throw const FormatException('Expired login response.');
      }
    } on TimeoutException {
      throw const AuthFailure('Connection timed out. Please try again.');
    } on IOException {
      throw const AuthFailure(
        'Cannot reach TMCWD. Check your internet connection.',
      );
    } on FormatException {
      throw const AuthFailure('TMCWD returned an invalid login response.');
    }
    // A failed secure write must never be reported as a successful login.
    await _storage.write(key: _sessionKey, value: jsonEncode(session.toJson()));
    return session;
  }

  Future<Object?> getJson(AuthSession session, String path) async {
    if (session.isExpired) {
      throw const AuthFailure(
        'Session expired. Sign in again.',
        statusCode: 401,
      );
    }
    final uri = baseUri;
    if (session.apiBaseUrl != uri.toString()) {
      throw const AuthFailure('This session belongs to another TMCWD server.');
    }
    try {
      final request = await _client.getUrl(uri.resolve(path)).timeout(_timeout);
      request.followRedirects = false;
      request.headers.set(
        HttpHeaders.authorizationHeader,
        'Bearer ${session.token}',
      );
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');

      final response = await request.close().timeout(_timeout);
      if (response.statusCode != HttpStatus.ok) {
        await response.drain<void>().timeout(_timeout);
        throw AuthFailure(switch (response.statusCode) {
          401 => 'Session expired. Sign in again.',
          403 => 'You are not authorized to access this reading sheet.',
          404 => 'This reading sheet is no longer available.',
          409 => 'This reading sheet cannot be downloaded yet. Contact your administrator.',
          _ => 'TMCWD is unavailable. Please retry.',
        }, statusCode: response.statusCode);
      }
      return jsonDecode(
        await response.transform(utf8.decoder).join().timeout(_timeout),
      );
    } on TimeoutException {
      throw const AuthFailure('Connection timed out. Please retry.');
    } on IOException {
      throw const AuthFailure(
        'Cannot reach TMCWD. Check your connection and retry.',
      );
    } on FormatException {
      throw const AuthFailure(
        'TMCWD returned an invalid reading sheet response.',
      );
    }
  }

  Future<void> logout() => _storage.delete(key: _sessionKey);

  void close() => _client.close(force: true);
}
