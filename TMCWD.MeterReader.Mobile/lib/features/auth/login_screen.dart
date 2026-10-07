import 'package:flutter/material.dart';

import 'auth_service.dart';
import '../../data/models/auth_session.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.auth, required this.onLogin});
  final AuthService auth;
  final ValueChanged<AuthSession> onLogin;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final _identifier = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  bool _showPassword = false;
  String? _error;

  @override
  void dispose() {
    _identifier.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (_busy || !_form.currentState!.validate()) return;
    FocusScope.of(context).unfocus();
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final session = await widget.auth.login(_identifier.text, _password.text);
      if (mounted) widget.onLogin(session);
    } on AuthFailure catch (error) {
      if (mounted) setState(() => _error = error.message);
    } catch (_) {
      if (mounted) {
        setState(
          () => _error = 'Could not securely save your login. Please retry.',
        );
      }
    } finally {
      if (mounted) {
        _password.clear();
        setState(() {
          _busy = false;
          _showPassword = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('TMCWD Meter Reader')),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Form(
                key: _form,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'Meter reader login',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _identifier,
                      enabled: !_busy,
                      autocorrect: false,
                      textCapitalization: TextCapitalization.none,
                      autofillHints: const [AutofillHints.username],
                      textInputAction: TextInputAction.next,
                      decoration: const InputDecoration(
                        labelText: 'Username or TMCWD user ID',
                        helperText:
                            'Use your login email or numeric TMCWD user ID.',
                        border: OutlineInputBorder(),
                      ),
                      validator: (value) =>
                          value == null || value.trim().isEmpty
                          ? 'Enter your login.'
                          : value.trim().length > 100
                          ? 'Login is too long.'
                          : null,
                    ),
                    const SizedBox(height: 20),
                    TextFormField(
                      controller: _password,
                      enabled: !_busy,
                      obscureText: !_showPassword,
                      autocorrect: false,
                      enableSuggestions: false,
                      autofillHints: const [AutofillHints.password],
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => _login(),
                      decoration: InputDecoration(
                        labelText: 'Password',
                        border: const OutlineInputBorder(),
                        suffixIcon: IconButton(
                          tooltip: _showPassword
                              ? 'Hide password'
                              : 'Show password',
                          onPressed: _busy
                              ? null
                              : () => setState(() {
                                  _showPassword = !_showPassword;
                                }),
                          icon: Icon(
                            _showPassword
                                ? Icons.visibility_off
                                : Icons.visibility,
                          ),
                        ),
                      ),
                      validator: (value) =>
                          value == null || value.trim().isEmpty
                          ? 'Enter your password.'
                          : value.length > 256
                          ? 'Password is too long.'
                          : null,
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 16),
                      Semantics(
                        liveRegion: true,
                        child: Text(
                          _error!,
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 24),
                    FilledButton(
                      style: FilledButton.styleFrom(
                        minimumSize: const Size.fromHeight(56),
                      ),
                      onPressed: _busy ? null : _login,
                      child: Text(_busy ? 'Signing in…' : 'Sign in'),
                    ),
                    const SizedBox(height: 16),
                    const Text(
                      'Internet is required for your first login. '
                      'An unexpired saved session can be reopened offline.',
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
