import 'package:flutter/material.dart';
import '../../core/reading_validation.dart';
import 'reading_sheet_service.dart';
import '../auth/auth_service.dart';

// ── Remarks options ──────────────────────────────────────────────────────────

const List<String> _kRemarks = [
  'Broken Pipe',
  'Change Meter - Aging/Old',
  'Change Meter - Blurred',
  'Change Meter - Stuck Up',
  'Meter Reset',
  'Normal Reading',
  'Rehab',
  'Service Line Leak',
  'Zero-NSC',
  'Zero-No Occupant',
];

// ── Brand colours ────────────────────────────────────────────────────────────

const Color _kProceed = Color(0xFF3F3FA3);
const Color _kNavBtn = Color(0xFFE8E8F4);
const Color _kNavLabel = Color(0xFF3F3FA3);

// ── Widget ───────────────────────────────────────────────────────────────────

class ReadingEntry extends StatefulWidget {
  const ReadingEntry({
    super.key,
    required this.service,
    required this.sheetId,
    required this.accounts,
    required this.initialIndex,
    required this.onClose,
    required this.onBusyChanged,
  });
  final ReadingSheetService service;
  final int sheetId, initialIndex;
  final List<DownloadAccount> accounts;
  final VoidCallback onClose;
  final ValueChanged<bool> onBusyChanged;

  @override
  State<ReadingEntry> createState() => _ReadingEntryState();
}

class _ReadingEntryState extends State<ReadingEntry> {
  final _current = TextEditingController();
  String _selectedRemark = '';

  late List<DownloadAccount> _accounts;
  late int _index;
  bool _saving = false;
  String? _error;

  DownloadAccount get _account => _accounts[_index];

  @override
  void initState() {
    super.initState();
    _accounts = widget.accounts;
    _index = widget.initialIndex;
    _restoreFields();
  }

  void _restoreFields() {
    _current.text = _account.currentReadingText ?? '';
    _selectedRemark = _account.remarks;
    _error = null;
  }

  @override
  void dispose() {
    _current.dispose();
    super.dispose();
  }

  // ── Navigation / save — logic unchanged ─────────────────────────────────
  Future<void> _move(
    int direction, {
    bool close = false,
    bool explicitSave = false,
  }) async {
    if (_saving) return;
    final changed =
        _current.text != (_account.currentReadingText ?? '') ||
        _selectedRemark != _account.remarks;
    setState(() {
      _saving = true;
      _error = null;
    });
    widget.onBusyChanged(true);
    try {
      if (_account.canEdit && (changed || explicitSave)) {
        _accounts = await widget.service.saveReading(
          widget.sheetId,
          _account.accountId,
          _current.text,
          _selectedRemark,
        );
      }
      if (!mounted) return;
      if (close) {
        widget.onClose();
        return;
      }
      setState(() {
        _index = (_index + direction).clamp(0, _accounts.length - 1);
        _restoreFields();
      });
      if (explicitSave && direction == 0) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(const SnackBar(content: Text('Saved locally')));
      }
    } on AuthFailure catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      widget.onBusyChanged(false);
      if (mounted) setState(() => _saving = false);
    }
  }

  // ── Build ────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    // Usage display: null/empty input → '—', valid → computed, invalid → '—'
    String usageText;
    try {
      usageText =
          readingUsage(_current.text, _account.previousReadingText) ?? '—';
    } on FormatException {
      usageText = '—';
    }

    // Present display: null/empty → '—', otherwise the raw text
    final String presentDisplay = _current.text.isEmpty ? '—' : _current.text;

    final bool isCompleted = !_account.canEdit;
    final String statusLabel = isCompleted ? 'Completed' : 'Pending';
    final int seqDisplay = _account.sequence;

    return SafeArea(
      child: Column(
        children: [
          // ── Top nav: [← Previous]  sequence  [Next →] ───────────────
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
            child: Row(
              children: [
                Expanded(
                  child: _NavButton(
                    label: 'Previous',
                    icon: Icons.arrow_back,
                    iconLeft: true,
                    onPressed: _saving || _index == 0 ? null : () => _move(-1),
                  ),
                ),
                // Sequence number — larger and bolder than before
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: Text(
                    '$seqDisplay',
                    style: const TextStyle(
                      fontSize: 34,
                      fontWeight: FontWeight.w900,
                      letterSpacing: -0.5,
                    ),
                  ),
                ),
                Expanded(
                  child: _NavButton(
                    label: 'Next',
                    icon: Icons.arrow_forward,
                    iconLeft: false,
                    onPressed: _saving || _index == _accounts.length - 1
                        ? null
                        : () => _move(1),
                  ),
                ),
              ],
            ),
          ),

          // ── Scrollable body ──────────────────────────────────────────
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // ── Account identity ─────────────────────────────────
                  _AccountCard(account: _account, statusLabel: statusLabel),

                  const SizedBox(height: 12),

                  // ── Previous / usage / Present ───────────────────────
                  _ReadingSummaryRow(
                    previousText: _account.previousReadingText,
                    usageText: usageText,
                    presentText: presentDisplay,
                  ),

                  const SizedBox(height: 14),

                  // ── Present Reading input ────────────────────────────
                  _PresentReadingField(
                    controller: _current,
                    enabled: !_saving && !isCompleted,
                    onChanged: () => setState(() {}),
                  ),

                  const SizedBox(height: 14),

                  // ── Proceed button ───────────────────────────────────
                  SizedBox(
                    height: 56,
                    child: ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: _kProceed,
                        foregroundColor: Colors.white,
                        disabledBackgroundColor: _kProceed.withAlpha(120),
                        disabledForegroundColor: Colors.white70,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(10),
                        ),
                        textStyle: const TextStyle(
                          fontSize: 17,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      onPressed: _saving || isCompleted
                          ? null
                          : () => _move(
                              _index == _accounts.length - 1 ? 0 : 1,
                              explicitSave: true,
                            ),
                      child: Text(_saving ? 'Saving…' : 'Proceed'),
                    ),
                  ),

                  const SizedBox(height: 20),

                  // ── Reprint + Remarks ────────────────────────────────
                  // Aligned: Reprint circle vertically centred with the
                  // dropdown trigger row; Remarks label above the dropdown.
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.center,
                    children: [
                      // ponytail: Reprint placeholder; printing is a later phase
                      _ReprintButton(onPressed: null),
                      const SizedBox(width: 20),
                      Expanded(
                        child: _RemarksDropdown(
                          value: _selectedRemark,
                          enabled: !_saving && !isCompleted,
                          onChanged: (v) {
                            if (v != null) setState(() => _selectedRemark = v);
                          },
                        ),
                      ),
                    ],
                  ),

                  // ── Error message ────────────────────────────────────
                  if (_error != null) ...[
                    const SizedBox(height: 10),
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

                  // ── Back to accounts ─────────────────────────────────
                  const SizedBox(height: 4),
                  TextButton(
                    onPressed: _saving ? null : () => _move(0, close: true),
                    child: const Text('Back to accounts'),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ── Sub-widgets ──────────────────────────────────────────────────────────────

class _NavButton extends StatelessWidget {
  const _NavButton({
    required this.label,
    required this.icon,
    required this.iconLeft,
    required this.onPressed,
  });
  final String label;
  final IconData icon;
  final bool iconLeft;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final enabled = onPressed != null;
    final color = enabled ? _kNavLabel : Colors.grey.shade400;
    final bgColor = enabled ? _kNavBtn : Colors.grey.shade100;
    final children = <Widget>[
      if (iconLeft) ...[
        Icon(icon, size: 18, color: color),
        const SizedBox(width: 4),
      ],
      Text(
        label,
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.w600,
          fontSize: 15,
        ),
      ),
      if (!iconLeft) ...[
        const SizedBox(width: 4),
        Icon(icon, size: 18, color: color),
      ],
    ];
    return SizedBox(
      height: 44,
      child: Material(
        color: bgColor,
        borderRadius: BorderRadius.circular(22),
        child: InkWell(
          borderRadius: BorderRadius.circular(22),
          onTap: onPressed,
          child: Center(
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              mainAxisSize: MainAxisSize.min,
              children: children,
            ),
          ),
        ),
      ),
    );
  }
}

class _AccountCard extends StatelessWidget {
  const _AccountCard({required this.account, required this.statusLabel});
  final DownloadAccount account;
  final String statusLabel;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Text(
                account.accountNumber,
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            Text(
              '${account.sequence}',
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w600,
                color: Colors.black54,
              ),
            ),
          ],
        ),
        const SizedBox(height: 2),
        Row(
          children: [
            const Icon(Icons.person_outline, size: 16, color: Colors.black54),
            const SizedBox(width: 4),
            Expanded(
              child: Text(
                account.name.toUpperCase(),
                style: const TextStyle(fontWeight: FontWeight.w600),
                overflow: TextOverflow.ellipsis,
              ),
            ),
            Text(
              account.meterNumber,
              style: const TextStyle(color: Colors.black54, fontSize: 13),
            ),
          ],
        ),
        const SizedBox(height: 2),
        Row(
          children: [
            const Icon(
              Icons.location_on_outlined,
              size: 16,
              color: Colors.black54,
            ),
            const SizedBox(width: 4),
            Expanded(
              child: Text(
                account.address.toUpperCase(),
                style: const TextStyle(fontSize: 13),
                overflow: TextOverflow.ellipsis,
              ),
            ),
            Text(
              statusLabel,
              style: TextStyle(
                color: statusLabel == 'Completed'
                    ? Colors.green.shade700
                    : Colors.orange.shade700,
                fontWeight: FontWeight.w600,
                fontSize: 13,
              ),
            ),
          ],
        ),
        const Divider(height: 14),
      ],
    );
  }
}

class _ReadingSummaryRow extends StatelessWidget {
  const _ReadingSummaryRow({
    required this.previousText,
    required this.usageText,
    required this.presentText,
  });
  final String previousText;
  final String usageText;
  final String presentText;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
      children: [
        _ReadingCol(label: 'Previous', value: previousText),
        Column(
          children: [
            const Icon(Icons.timelapse, size: 22, color: Colors.black45),
            Text(
              usageText,
              style: const TextStyle(fontSize: 28, fontWeight: FontWeight.bold),
            ),
          ],
        ),
        _ReadingCol(label: 'Present', value: presentText),
      ],
    );
  }
}

class _ReadingCol extends StatelessWidget {
  const _ReadingCol({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          label,
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
        ),
        Text(value, style: const TextStyle(fontSize: 18)),
      ],
    );
  }
}

class _PresentReadingField extends StatelessWidget {
  const _PresentReadingField({
    required this.controller,
    required this.enabled,
    required this.onChanged,
  });
  final TextEditingController controller;
  final bool enabled;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      enabled: enabled,
      keyboardType: const TextInputType.numberWithOptions(decimal: true),
      style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w600),
      decoration: InputDecoration(
        hintText: 'Present Reading',
        hintStyle: const TextStyle(
          fontSize: 17,
          fontWeight: FontWeight.w600,
          color: Colors.black87,
        ),
        filled: true,
        fillColor: enabled ? const Color(0xFFE8E8F4) : Colors.grey.shade200,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 18,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: const BorderSide(color: _kProceed),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: const BorderSide(color: _kProceed),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: const BorderSide(color: _kProceed, width: 2),
        ),
        disabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: BorderSide(color: Colors.grey.shade400),
        ),
      ),
      onChanged: (_) => onChanged(),
    );
  }
}

class _ReprintButton extends StatelessWidget {
  const _ReprintButton({required this.onPressed});
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Material(
          shape: const CircleBorder(),
          color: Colors.grey.shade200,
          child: InkWell(
            customBorder: const CircleBorder(),
            onTap: onPressed,
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Icon(
                Icons.print_outlined,
                size: 26,
                color: Colors.indigo.shade700,
              ),
            ),
          ),
        ),
        const SizedBox(height: 4),
        const Text(
          'Reprint',
          style: TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
        ),
      ],
    );
  }
}

/// Remarks: label row + full-width dropdown button with generous tap target.
class _RemarksDropdown extends StatelessWidget {
  const _RemarksDropdown({
    required this.value,
    required this.enabled,
    required this.onChanged,
  });
  final String value;
  final bool enabled;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        const Text(
          'Remarks',
          style: TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 14,
            color: Colors.black87,
          ),
        ),
        const SizedBox(height: 2),
        DropdownButton<String>(
          value: value.isEmpty ? null : value,
          hint: const Text('Select remark'),
          isExpanded: true,
          onChanged: enabled ? onChanged : null,
          underline: Container(
            height: 2,
            color: enabled ? _kProceed : Colors.grey.shade400,
          ),
          style: const TextStyle(
            color: _kProceed,
            fontWeight: FontWeight.w600,
            fontSize: 16,
          ),
          items:
              [
                    if (value.isNotEmpty && !_kRemarks.contains(value)) value,
                    ..._kRemarks,
                  ]
                  .map(
                    (r) => DropdownMenuItem(
                      value: r,
                      child: Text(
                        r,
                        style: const TextStyle(
                          color: _kProceed,
                          fontWeight: FontWeight.w600,
                          fontSize: 16,
                        ),
                      ),
                    ),
                  )
                  .toList(),
        ),
      ],
    );
  }
}
