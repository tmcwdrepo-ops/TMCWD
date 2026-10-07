import 'package:flutter/material.dart';

import '../../core/statuses.dart';
import 'reading_entry.dart';
import 'reading_sheet_service.dart';

// ── Status-filter vocabulary ─────────────────────────────────────────────────

enum _StatusFilter { all, pending, completed }

extension _StatusFilterLabel on _StatusFilter {
  String get label => switch (this) {
        _StatusFilter.all => 'All',
        _StatusFilter.pending => 'Pending',
        _StatusFilter.completed => 'Completed',
      };
}

// ── Helper: determine an account's display status ───────────────────────────

bool _isCompleted(DownloadAccount a) =>
    a.readingStatus == ReadingSheetStatus.completed.value;

bool _isPending(DownloadAccount a) => !_isCompleted(a);

// ── AccountListScreen ────────────────────────────────────────────────────────

/// Shows the downloaded accounts for a reading sheet with:
///   • full-text search (account number, name, address, meter number)
///   • zone-and-book dropdown filter
///   • all / pending / completed status filter
///   • touch-friendly cards with P / C badges
///   • tap to open the existing [ReadingEntry] flow
class AccountListScreen extends StatefulWidget {
  const AccountListScreen({
    super.key,
    required this.service,
    required this.sheet,
    required this.accounts,
    required this.onClose,
    this.onEntryStateChanged,
  });

  /// The service used to persist reading entries and refresh the account list.
  final ReadingSheetService service;

  /// The assigned reading sheet whose accounts are being displayed.
  final AssignedSheet sheet;

  /// Pre-loaded accounts (from [ReadingSheetService.openDownloaded]).
  final List<DownloadAccount> accounts;

  /// Called when the user presses the back/close button to return to the
  /// sheet list.
  final VoidCallback onClose;

  /// Optional callback fired whenever reading-entry open/close state changes.
  /// [true] = entry screen is now open; [false] = returned to account list.
  /// Used by parent widgets to suppress header chrome while entry is open.
  final ValueChanged<bool>? onEntryStateChanged;

  @override
  State<AccountListScreen> createState() => _AccountListScreenState();
}

class _AccountListScreenState extends State<AccountListScreen> {
  // ── Reading-entry state ───────────────────────────────────────────────────
  List<DownloadAccount> _accounts = [];
  int? _entryIndex; // null  → account list, non-null → ReadingEntry
  bool _entryBusy = false;

  // ── Filter state ──────────────────────────────────────────────────────────
  final _searchController = TextEditingController();
  String _searchText = '';
  String? _selectedZoneBook; // e.g. "Zone 12 - 1", null = all
  _StatusFilter _statusFilter = _StatusFilter.all;

  // ── Derived: sorted list of unique "Zone Z - B" labels ───────────────────
  late List<String> _zoneBookOptions;

  @override
  void initState() {
    super.initState();
    _accounts = widget.accounts;
    _zoneBookOptions = _buildZoneBookOptions(_accounts);
    _searchController.addListener(() {
      if (mounted) setState(() => _searchText = _searchController.text.trim());
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  // ── Zone/book helpers ─────────────────────────────────────────────────────

  static String _zoneBookLabel(DownloadAccount a) =>
      'Zone ${a.zone} - ${a.book}';

  static List<String> _buildZoneBookOptions(List<DownloadAccount> accounts) {
    final seen = <String>{};
    final result = <String>[];
    for (final a in accounts) {
      final label = _zoneBookLabel(a);
      if (seen.add(label)) result.add(label);
    }
    result.sort();
    return result;
  }

  // ── Filtering logic ───────────────────────────────────────────────────────

  List<DownloadAccount> get _filtered {
    final query = _searchText.toLowerCase();
    return _accounts.where((a) {
      // Zone/book filter
      if (_selectedZoneBook != null &&
          _zoneBookLabel(a) != _selectedZoneBook) {
        return false;
      }
      // Status filter
      if (_statusFilter == _StatusFilter.completed && !_isCompleted(a)) {
        return false;
      }
      if (_statusFilter == _StatusFilter.pending && !_isPending(a)) {
        return false;
      }
      // Text search
      if (query.isNotEmpty) {
        final haystack = '${a.accountNumber} ${a.name} '
            '${a.address} ${a.meterNumber}'
            .toLowerCase();
        if (!haystack.contains(query)) return false;
      }
      return true;
    }).toList();
  }

  // ── Entry open/close ──────────────────────────────────────────────────────

  bool get _canEnterReading =>
      [ReadingSheetStatus.created, ReadingSheetStatus.inProgress]
          .map((s) => s.value)
          .contains(widget.sheet.status.value);

  void _openEntry(int indexInFiltered) {
    // Resolve to the index in the full _accounts list so ReadingEntry can
    // navigate prev/next through the *full* roster, not just the filtered one.
    final account = _filtered[indexInFiltered];
    final fullIndex = _accounts.indexOf(account);
    if (fullIndex < 0) return;
    setState(() => _entryIndex = fullIndex);
    widget.onEntryStateChanged?.call(true);
  }

  // ── Build ─────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    // While ReadingEntry is active, hand the whole body over to it so it can
    // manage prev/next navigation through the full account roster.
    if (_entryIndex != null && _accounts.isNotEmpty) {
      return PopScope(
        canPop: false,
        child: ReadingEntry(
          key: ValueKey('${widget.sheet.id}:$_entryIndex'),
          service: widget.service,
          sheetId: widget.sheet.id,
          accounts: _accounts,
          initialIndex: _entryIndex!,
          onBusyChanged: (busy) {
            if (mounted) setState(() => _entryBusy = busy);
          },
          onClose: () => setState(() {
            _entryIndex = null;
            // Refresh the list after entry so the badge updates immediately.
            _reloadAccounts();
            widget.onEntryStateChanged?.call(false);
          }),
        ),
      );
    }

    final filtered = _filtered;

    return Column(
      children: [
        _FilterBar(
          searchController: _searchController,
          zoneBookOptions: _zoneBookOptions,
          selectedZoneBook: _selectedZoneBook,
          statusFilter: _statusFilter,
          onZoneBookChanged: (v) => setState(() => _selectedZoneBook = v),
          onStatusFilterChanged: (v) => setState(() => _statusFilter = v),
        ),
        Expanded(
          child: filtered.isEmpty
              ? const Center(child: Text('No accounts match the current filter.'))
              : ListView.builder(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 8,
                  ),
                  itemCount: filtered.length,
                  itemBuilder: (context, index) {
                    return _AccountCard(
                      account: filtered[index],
                      canEnterReading: _canEnterReading && !_entryBusy,
                      onTap: () => _openEntry(index),
                    );
                  },
                ),
        ),
        // Summary row
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 12),
          child: _SummaryRow(accounts: _accounts),
        ),
      ],
    );
  }

  void _reloadAccounts() {
    widget.service.openDownloaded(widget.sheet.id).then((fresh) {
      if (mounted) {
        setState(() {
          _accounts = fresh;
          _zoneBookOptions = _buildZoneBookOptions(fresh);
        });
      }
    }).ignore();
  }
}

// ── Filter bar ───────────────────────────────────────────────────────────────

class _FilterBar extends StatelessWidget {
  const _FilterBar({
    required this.searchController,
    required this.zoneBookOptions,
    required this.selectedZoneBook,
    required this.statusFilter,
    required this.onZoneBookChanged,
    required this.onStatusFilterChanged,
  });

  final TextEditingController searchController;
  final List<String> zoneBookOptions;
  final String? selectedZoneBook;
  final _StatusFilter statusFilter;
  final ValueChanged<String?> onZoneBookChanged;
  final ValueChanged<_StatusFilter> onStatusFilterChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        // ── Search field ───────────────────────────────────────────────────
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 8, 12, 0),
          child: TextField(
            controller: searchController,
            decoration: const InputDecoration(
              hintText: 'Search..',
              prefixIcon: Icon(Icons.search),
              border: UnderlineInputBorder(),
              contentPadding: EdgeInsets.symmetric(vertical: 10),
            ),
          ),
        ),
        // ── Zone/book  +  status filter row ───────────────────────────────
        Padding(
          padding: const EdgeInsets.fromLTRB(12, 6, 12, 4),
          child: Row(
            children: [
              // Zone/book dropdown
              Expanded(
                flex: 5,
                child: _ZoneBookDropdown(
                  options: zoneBookOptions,
                  selected: selectedZoneBook,
                  onChanged: onZoneBookChanged,
                ),
              ),
              const SizedBox(width: 10),
              // All / Pending / Completed dropdown
              Expanded(
                flex: 4,
                child: _StatusDropdown(
                  value: statusFilter,
                  onChanged: onStatusFilterChanged,
                ),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
      ],
    );
  }
}

// ── Zone/book dropdown ───────────────────────────────────────────────────────

class _ZoneBookDropdown extends StatelessWidget {
  const _ZoneBookDropdown({
    required this.options,
    required this.selected,
    required this.onChanged,
  });

  final List<String> options;
  final String? selected;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonHideUnderline(
      child: DropdownButton<String?>(
        value: selected,
        isExpanded: true,
        style: TextStyle(
          color: Theme.of(context).colorScheme.primary,
          fontWeight: FontWeight.w600,
          fontSize: 14,
        ),
        hint: Text(
          'All zones',
          style: TextStyle(color: Theme.of(context).colorScheme.primary),
        ),
        items: [
          const DropdownMenuItem<String?>(
            value: null,
            child: Text('All zones'),
          ),
          ...options.map(
            (label) => DropdownMenuItem<String?>(
              value: label,
              child: Text(label),
            ),
          ),
        ],
        onChanged: onChanged,
      ),
    );
  }
}

// ── Status dropdown ──────────────────────────────────────────────────────────

class _StatusDropdown extends StatelessWidget {
  const _StatusDropdown({required this.value, required this.onChanged});

  final _StatusFilter value;
  final ValueChanged<_StatusFilter> onChanged;

  @override
  Widget build(BuildContext context) {
    return DropdownButtonHideUnderline(
      child: DropdownButton<_StatusFilter>(
        value: value,
        isExpanded: true,
        style: TextStyle(
          color: Theme.of(context).colorScheme.primary,
          fontWeight: FontWeight.w600,
          fontSize: 14,
        ),
        items: _StatusFilter.values
            .map(
              (f) => DropdownMenuItem(
                value: f,
                child: Text(f.label),
              ),
            )
            .toList(),
        onChanged: (v) {
          if (v != null) onChanged(v);
        },
      ),
    );
  }
}

// ── Account card ─────────────────────────────────────────────────────────────

class _AccountCard extends StatelessWidget {
  const _AccountCard({
    required this.account,
    required this.canEnterReading,
    required this.onTap,
  });

  final DownloadAccount account;
  final bool canEnterReading;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final completed = _isCompleted(account);

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: canEnterReading ? onTap : null,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              // ── Left: account info ─────────────────────────────────────
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      account.accountNumber,
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      account.name,
                      style: const TextStyle(fontSize: 13),
                    ),
                    Text(
                      account.address,
                      style: const TextStyle(fontSize: 13),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              // ── Right: status badge + overflow ────────────────────────
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  _StatusBadge(completed: completed),
                  const SizedBox(width: 6),
                  Icon(
                    Icons.more_vert,
                    color: Colors.grey[600],
                    size: 20,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ── Status badge  (orange P  /  green C) ─────────────────────────────────────

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.completed});

  final bool completed;

  @override
  Widget build(BuildContext context) {
    final color =
        completed ? const Color(0xFF388E3C) : const Color(0xFFE65100);

    final label =
        completed ? 'Completed' : 'Pending';

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 14,
        vertical: 8,
      ),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white,
          fontWeight: FontWeight.bold,
          fontSize: 13,
        ),
      ),
    );
  }
}

// ── Summary row ──────────────────────────────────────────────────────────────

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.accounts});

  final List<DownloadAccount> accounts;

  @override
  Widget build(BuildContext context) {
    final total = accounts.length;
    final done = accounts.where(_isCompleted).length;
    return Text(
      '$done / $total completed',
      style: Theme.of(context).textTheme.bodySmall,
    );
  }
}
