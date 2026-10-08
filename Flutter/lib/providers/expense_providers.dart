import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../auth/auth_provider.dart' as app_auth;
import '../config/api_config.dart';
import '../models/expense_models.dart';
import '../repositories/expense_repository.dart';

final apiBaseUrl = expenseGuardApiBaseUrl();

class BridgeAuthSession implements AuthSession {
  const BridgeAuthSession(this.ref);
  final Ref ref;

  @override
  Future<String?> getAccessToken() async => ref.read(app_auth.authProvider).value?.token;

  @override
  Future<String?> getEmployeeId() async {
    final id = ref.read(app_auth.authProvider).value?.employeeId;
    return id?.toString();
  }
}

final expenseRepositoryProvider = Provider<ExpenseRepository>((ref) => HttpExpenseRepository(
  baseUrl: apiBaseUrl,
  auth: BridgeAuthSession(ref),
));

final profileProvider = FutureProvider<EmployeeProfile>((ref) =>
  ref.watch(expenseRepositoryProvider).getProfile());
final purchaseRequestsProvider = FutureProvider<List<PurchaseRequest>>((ref) =>
  ref.watch(expenseRepositoryProvider).getPurchaseRequests());

class ClaimFilter {
  const ClaimFilter({this.status, this.category});
  final String? status;
  final String? category;
}

final claimFilterProvider = StateProvider<ClaimFilter>((ref) => const ClaimFilter());
final claimsProvider = FutureProvider<List<ExpenseClaim>>((ref) {
  final filter = ref.watch(claimFilterProvider);
  return ref.watch(expenseRepositoryProvider).getClaims(status: filter.status, category: filter.category);
});
final claimProvider = FutureProvider.family<ExpenseClaim, int>((ref, id) =>
  ref.watch(expenseRepositoryProvider).getClaim(id));
final claimHistoryProvider = FutureProvider.family<List<ClaimHistory>, int>((ref, id) =>
  ref.watch(expenseRepositoryProvider).getHistory(id));
