import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../config/api_config.dart';
import 'budget_api.dart';

final _environmentApiUrl = expenseGuardApiBaseUrl();
const _environmentToken = String.fromEnvironment('EXPENSEGUARD_AUTH_TOKEN');
const _environmentBudgetId =
    String.fromEnvironment('EXPENSEGUARD_DEPARTMENT_BUDGET_ID');

final apiBaseUrlProvider = Provider<String>((ref) => _environmentApiUrl);

/// Override this provider from the app-owned authentication layer after login.
final authTokenProvider = Provider<String?>(
  (ref) => _environmentToken.isEmpty ? null : _environmentToken,
);

/// Override from the authenticated employee profile. No employee ID is stored
/// or inferred by this feature.
final departmentBudgetIdProvider = Provider<int?>(
  (ref) => int.tryParse(_environmentBudgetId),
);

final budgetApiProvider = Provider<BudgetApi>(
  (ref) => BudgetApi(
    baseUrl: ref.watch(apiBaseUrlProvider),
    token: ref.watch(authTokenProvider),
  ),
);

final departmentBudgetProvider = FutureProvider<DepartmentBudget>((ref) {
  final id = ref.watch(departmentBudgetIdProvider);
  if (id == null) {
    throw const ApiFailure(
      0,
      'Your employee profile is not linked to a department budget.',
    );
  }
  return ref.watch(budgetApiProvider).getBudget(id);
});

typedef BudgetImpactRequest = ({int budgetId, double amount});

final budgetImpactProvider =
    FutureProvider.family<BudgetImpact, BudgetImpactRequest>((ref, request) {
  return ref.watch(budgetApiProvider).checkImpact(
        request.budgetId,
        request.amount,
      );
});
