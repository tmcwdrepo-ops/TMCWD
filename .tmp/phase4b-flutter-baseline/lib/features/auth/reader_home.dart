import 'package:flutter/material.dart';

import '../../data/local/local_database.dart';
import '../../data/models/auth_session.dart';
import '../reading_sheets/account_list_screen.dart';
import '../reading_sheets/reading_sheet_service.dart';
import 'auth_service.dart';

class ReaderHome extends StatefulWidget {
  const ReaderHome({
    super.key,
    required this.session,
    required this.auth,
    required this.onLogout,
    this.database,
  });
  final AuthSession session;
  final AuthService auth;
  final Future<void> Function() onLogout;
  final Future<LocalDatabase>? database;

  @override
  State<ReaderHome> createState() => _ReaderHomeState();
}

class _ReaderHomeState extends State<ReaderHome> {
  late Future<LocalDatabase> _database;
  late ReadingSheetService _service;
  late Future<({List<AssignedSheet> sheets, String? notice})> _sheets;
  Future<List<DownloadAccount>>? _download;
  AssignedSheet? _selected;
  bool _loggingOut = false;
  String? _error;
  String? _downloadNotice;
  bool _offlineAvailable = false;
  // True while AccountListScreen has a ReadingEntry open — hides header chrome.
  bool _entryOpen = false;

  @override
  void initState() {
    super.initState();
    _database =
        widget.database ??
        LocalDatabase.open(fileName: widget.session.databaseFileName);
    _service = ReadingSheetService(
      widget.auth,
      widget.session,
      database: _database,
    );
    _sheets = _service.loadAssigned()..ignore();
  }

  @override
  void dispose() {
    _database.then((db) => db.close(), onError: (Object _) {});
    super.dispose();
  }

  Future<void> _logout() async {
    if (_loggingOut) return;
    setState(() {
      _loggingOut = true;
      _error = null;
    });
    try {
      await widget.onLogout();
    } catch (_) {
      if (mounted) setState(() => _error = 'Sign out failed. Please retry.');
    } finally {
      if (mounted) setState(() => _loggingOut = false);
    }
  }

  void _reload() => setState(() {
    if (_selected == null) {
      _sheets = _service.loadAssigned()..ignore();
    } else {
      _download = _downloadSheet(_selected!.id)..ignore();
    }
  });

  Future<List<DownloadAccount>> _downloadSheet(int id) async {
    try {
      final accounts = await _service.download(id);
      if (mounted && _selected?.id == id)
        setState(() {
          _offlineAvailable = true;
          _downloadNotice = null;
        });
      return accounts;
    } on AuthFailure catch (error) {
      if (error.statusCode == 401 || error.statusCode == 403) rethrow;
      try {
        final accounts = await _service.openDownloaded(id);
        if (mounted && _selected?.id == id)
          setState(() {
            _offlineAvailable = true;
            _downloadNotice = '${error.message} Showing saved accounts.';
          });
        return accounts;
      } catch (_) {
        throw error;
      }
    }
  }

  String _date(DateTime? date) =>
      date == null ? 'Not available' : date.toIso8601String().split('T').first;

  Widget _failure(Object? error) {
    final failure = error is AuthFailure
        ? error
        : const AuthFailure('Could not load reading sheets. Please retry.');
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Semantics(
              liveRegion: true,
              child: Text(failure.message, textAlign: TextAlign.center),
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: _loggingOut
                  ? null
                  : failure.statusCode == 401
                  ? _logout
                  : _reload,
              child: Text(
                failure.statusCode == 401 ? 'Sign in again' : 'Retry',
              ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Text(
        _selected == null
            ? 'Assigned reading sheets'
            : 'Sheet ${_selected!.id} accounts',
      ),
      leading: _selected == null
          ? null
          : IconButton(
              tooltip: 'Back to assigned sheets',
              icon: const Icon(Icons.arrow_back),
              onPressed: () => setState(() {
                _selected = null;
                _download = null;
                _sheets = _service.loadAssigned()..ignore();
              }),
            ),
      actions: [
        IconButton(
          onPressed: _reload,
          tooltip: 'Refresh',
          icon: const Icon(Icons.refresh),
        ),
        TextButton(
          onPressed: _loggingOut ? null : _logout,
          child: Text(_loggingOut ? 'Signing out…' : 'Sign out'),
        ),
      ],
    ),
    body: SafeArea(
      child: Column(
        children: [
          // Header chrome is hidden while a reading entry is open so the
          // entry screen has maximum vertical space.
          if (!_entryOpen) ...[
            Padding(
              padding: const EdgeInsets.all(12),
              child: Text('Signed in as ${widget.session.name}'),
            ),
            FutureBuilder<LocalDatabase>(
              future: _database,
              builder: (context, snapshot) {
                if (!snapshot.hasError) return const SizedBox.shrink();
                return TextButton(
                  onPressed: () => setState(() {
                    _database = LocalDatabase.open(
                      fileName: widget.session.databaseFileName,
                    );
                    _service = ReadingSheetService(
                      widget.auth,
                      widget.session,
                      database: _database,
                    );
                    _sheets = _service.loadAssigned()..ignore();
                  }),
                  child: const Text(
                    'Local storage unavailable. Retry opening storage.',
                  ),
                );
              },
            ),
            if (_error != null) Semantics(liveRegion: true, child: Text(_error!)),
            if (_selected != null && _offlineAvailable)
              const Padding(
                padding: EdgeInsets.all(8),
                child: Text('Downloaded / Offline available'),
              ),
            if (_downloadNotice != null)
              Padding(
                padding: const EdgeInsets.all(8),
                child: Text(_downloadNotice!),
              ),
          ],
          Expanded(child: _selected == null ? _sheetList() : _accountSection()),
        ],
      ),
    ),
  );

  // ── Sheet list ──────────────────────────────────────────────────────────────

  Widget _sheetList() =>
      FutureBuilder<({List<AssignedSheet> sheets, String? notice})>(
        future: _sheets,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(
              child: CircularProgressIndicator(
                semanticsLabel: 'Loading assigned sheets',
              ),
            );
          }
          if (snapshot.hasError) return _failure(snapshot.error);
          final sheets = snapshot.data!.sheets;
          if (sheets.isEmpty)
            return const Center(
              child: Text('No reading sheets are assigned to you.'),
            );
          return Column(
            children: [
              if (snapshot.data!.notice != null)
                Padding(
                  padding: const EdgeInsets.all(8),
                  child: Text(snapshot.data!.notice!),
                ),
              Expanded(
                child: ListView.builder(
                  itemCount: sheets.length,
                  itemBuilder: (context, index) {
                    final sheet = sheets[index];
                    return Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Sheet ${sheet.id}: ${sheet.name ?? 'Unnamed'}',
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                            Text(
                              '${sheet.statusLabel} • Billing: ${_date(sheet.billingDate)}',
                            ),
                            Text('Zone/book ID: ${sheet.zoneBookId}'),
                            if (sheet.isDownloaded)
                              const Text('Downloaded / Offline available'),
                            FilledButton(
                              onPressed: () => setState(() {
                                _selected = sheet;
                                _offlineAvailable = sheet.isDownloaded;
                                _downloadNotice = null;
                                _download = sheet.isDownloaded
                                    ? (_service.openDownloaded(sheet.id)
                                          ..ignore())
                                    : (_downloadSheet(sheet.id)..ignore());
                              }),
                              child: Text(
                                sheet.isDownloaded
                                    ? 'Open offline'
                                    : 'Download / Open',
                              ),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
            ],
          );
        },
      );

  // ── Account section: loading shell → AccountListScreen ─────────────────────

  Widget _accountSection() => FutureBuilder<List<DownloadAccount>>(
    future: _download,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const Center(
          child: CircularProgressIndicator(
            semanticsLabel: 'Downloading sheet accounts',
          ),
        );
      }
      if (snapshot.hasError) return _failure(snapshot.error);
      final accounts = snapshot.data!;
      if (accounts.isEmpty) {
        return const Center(child: Text('This sheet has no accounts.'));
      }
      return AccountListScreen(
        service: _service,
        sheet: _selected!,
        accounts: accounts,
        onEntryStateChanged: (open) {
          if (mounted) setState(() => _entryOpen = open);
        },
        onClose: () => setState(() {
          _selected = null;
          _download = null;
          _entryOpen = false;
          _sheets = _service.loadAssigned()..ignore();
        }),
      );
    },
  );
}
