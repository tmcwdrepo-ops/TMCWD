/// Mirrors backend decimal/non-negative/previous-reading validation without
/// floating-point subtraction. Empty input is an offline draft, not zero.
String? readingUsage(String? current, String previous) {
  if (current == null || current.trim().isEmpty) return null;
  (BigInt, int) parse(String text) {
    final match = RegExp(
      r'^([+-]?)(\d+)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$',
    ).firstMatch(text.trim());
    if (match == null)
      throw const FormatException('Enter a valid numeric reading.');
    final fraction = match[3] ?? '';
    var value = BigInt.parse('${match[2]}$fraction');
    var scale = fraction.length - int.parse(match[4] ?? '0');
    if (scale.abs() > 100)
      throw const FormatException(
        'Reading is outside the supported decimal range.',
      );
    if (scale < 0) {
      value *= BigInt.from(10).pow(-scale);
      scale = 0;
    }
    while (scale > 0 && value % BigInt.from(10) == BigInt.zero) {
      value ~/= BigInt.from(10);
      scale--;
    }
    if (scale > 28 || value > BigInt.parse('79228162514264337593543950335')) {
      throw const FormatException(
        'Reading is outside the supported decimal range.',
      );
    }
    if (match[1] == '-' && value != BigInt.zero)
      throw const FormatException('Present reading cannot be negative.');
    return (value, scale);
  }

  final (present, presentScale) = parse(current);
  final (prior, priorScale) = parse(previous);
  final scale = presentScale > priorScale ? presentScale : priorScale;
  final usage =
      present * BigInt.from(10).pow(scale - presentScale) -
      prior * BigInt.from(10).pow(scale - priorScale);
  if (usage < BigInt.zero)
    throw const FormatException(
      'Present reading cannot be less than the previous reading.',
    );
  if (scale == 0) return usage.toString();
  final digits = usage.toString().padLeft(scale + 1, '0');
  return '${digits.substring(0, digits.length - scale)}.${digits.substring(digits.length - scale)}'
      .replaceFirst(RegExp(r'0+$'), '')
      .replaceFirst(RegExp(r'\.$'), '');
}
