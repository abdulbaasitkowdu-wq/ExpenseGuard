import 'package:flutter/foundation.dart';

String expenseGuardApiBaseUrl() {
  const defined = String.fromEnvironment('EXPENSE_GUARD_API_URL');
  const legacy = String.fromEnvironment('EXPENSEGUARD_API_URL');
  if (defined.isNotEmpty) return defined;
  if (legacy.isNotEmpty) return legacy;
  if (kIsWeb) return 'http://localhost:5000/api';
  return 'http://10.0.2.2:5000/api';
}
