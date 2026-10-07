import 'dart:convert';

class AuthSession {
  const AuthSession({
    required this.token,
    required this.userId,
    required this.name,
    required this.email,
    required this.expiresAt,
    required this.apiBaseUrl,
  });

  final String token;
  final int userId;
  final String name;
  final String email;
  final DateTime expiresAt;
  final String apiBaseUrl;

  bool get isExpired => !DateTime.now().toUtc().isBefore(expiresAt);

  // Partition by deployment and reader; changing readers never deletes data.
  String get databaseFileName =>
      'tmcwd_${base64Url.encode(utf8.encode(apiBaseUrl)).replaceAll('=', '')}'
      '_reader_$userId.db';

  Map<String, Object?> toJson() => {
    'token': token,
    'userId': userId,
    'role': 4, // TMCWD.Model.UserRole.MeterReader
    'name': name,
    'email': email,
    'expiresAt': expiresAt.toUtc().toIso8601String(),
    'apiBaseUrl': apiBaseUrl,
  };

  factory AuthSession.fromJson(Map<String, dynamic> json, String apiBaseUrl) {
    final token = json['token'];
    final userId = json['userId'];
    final email = json['email'];
    final name = json['name'];
    final expiry = json['expiresAt'];
    final expiresAt = expiry is String ? DateTime.tryParse(expiry) : null;
    if (token is! String ||
        token.trim().isEmpty ||
        userId is! int ||
        userId <= 0 ||
        json['role'] != 4 ||
        email is! String ||
        email.trim().isEmpty ||
        expiresAt == null ||
        !expiresAt.isUtc ||
        (name != null && name is! String)) {
      throw const FormatException('Invalid meter-reader session.');
    }
    return AuthSession(
      token: token,
      userId: userId,
      name: name is String && name.isNotEmpty ? name : email,
      email: email,
      expiresAt: expiresAt,
      apiBaseUrl: apiBaseUrl,
    );
  }
}
