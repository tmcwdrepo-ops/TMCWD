import 'dart:async';

import 'package:flutter/material.dart';

import 'data/models/auth_session.dart';
import 'features/auth/auth_service.dart';
import 'features/auth/login_screen.dart';
import 'features/auth/reader_home.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MeterReaderApp());
}

class MeterReaderApp extends StatefulWidget {
  const MeterReaderApp({super.key, this.auth});

  final AuthService? auth;

  @override
  State<MeterReaderApp> createState() => _MeterReaderAppState();
}

class _MeterReaderAppState extends State<MeterReaderApp>
    with WidgetsBindingObserver {
  late final AuthService _auth;
  late Future<AuthSession?> _session;

  AuthSession? _activeSession;
  Timer? _expiry;

  @override
  void initState() {
    super.initState();

    _auth = widget.auth ?? AuthService();

    WidgetsBinding.instance.addObserver(this);

    _session = _restore();
  }

  Future<AuthSession?> _restore() async {
    final session = await _auth.restore();

    if (mounted && session != null) {
      _startExpiry(session);
    }

    return session;
  }

  void _startExpiry(AuthSession session) {
    _expiry?.cancel();

    _activeSession = session;

    final duration = session.expiresAt.difference(DateTime.now().toUtc());

    _expiry = Timer(
      duration.isNegative ? Duration.zero : duration,
      _lockExpiredSession,
    );
  }

  void _lockExpiredSession() {
    if (!mounted) return;

    _expiry?.cancel();
    _activeSession = null;

    setState(() {
      _session = Future.value(null);
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed &&
        _activeSession?.isExpired == true) {
      _lockExpiredSession();
    }
  }

  void _signedIn(AuthSession session) {
    if (session.isExpired) {
      _lockExpiredSession();
      return;
    }

    _startExpiry(session);

    setState(() {
      _session = Future.value(session);
    });
  }

  Future<void> _logout() async {
    // Keep the current session if secure deletion fails.
    await _auth.logout();

    if (!mounted) return;

    _expiry?.cancel();
    _activeSession = null;

    setState(() {
      _session = Future.value(null);
    });
  }

  void _retryRestore() {
    setState(() {
      _session = _restore();
    });
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);

    _expiry?.cancel();
    _auth.close();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'TMCWD Meter Reader',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.blue),
        useMaterial3: true,
      ),
      home: FutureBuilder<AuthSession?>(
        future: _session,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Scaffold(
              body: Center(
                child: CircularProgressIndicator(
                  semanticsLabel: 'Opening saved session',
                ),
              ),
            );
          }

          if (snapshot.hasError) {
            final error = snapshot.error;

            return Scaffold(
              appBar: AppBar(title: const Text('TMCWD Meter Reader')),
              body: Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        error is AuthFailure
                            ? error.message
                            : 'Secure session storage could not be opened. '
                                  'Please retry.',
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        onPressed: _retryRestore,
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                ),
              ),
            );
          }

          final session = snapshot.data;

          if (session == null) {
            return LoginScreen(auth: _auth, onLogin: _signedIn);
          }

          return ReaderHome(
            key: ValueKey(session.databaseFileName),
            session: session,
            auth: _auth,
            onLogout: _logout,
          );
        },
      ),
    );
  }
}
