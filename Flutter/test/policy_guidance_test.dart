import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:reimbursement_budget/features/compliance/compliance_providers.dart';
import 'package:reimbursement_budget/features/compliance/policy_guidance_screen.dart';
import 'package:reimbursement_budget/models/expense_models.dart';
import 'package:reimbursement_budget/providers/expense_providers.dart';

class FakeComplianceRepository implements ComplianceRepository {
  @override
  Future<ComplianceFeedback> evaluate(ComplianceInput input) async => const ComplianceFeedback(
    outcome: 'non_compliant',
    violations: [PolicyViolation(ruleCode: 'RECEIPT_REQUIRED', message: 'Attach a receipt.', severity: 'high')],
  );
}

ExpenseClaim get sampleClaim => const ExpenseClaim(
  id: 42, amount: 1200, category: 'Travel', description: 'Taxi',
  currency: 'LKR', status: 'NeedsCorrection', flow: 'OutOfPocket', version: 1,
);

Widget subject() {
  final router = GoRouter(routes: [
    GoRoute(path: '/', builder: (_, __) => const PolicyGuidanceScreen()),
    GoRoute(path: '/home', builder: (_, __) => const SizedBox()),
    GoRoute(path: '/intake/claims/:id', builder: (_, __) => const SizedBox()),
    GoRoute(path: '/intake', builder: (_, __) => const SizedBox()),
  ]);
  return ProviderScope(
    overrides: [
      complianceRepositoryProvider.overrideWithValue(FakeComplianceRepository()),
      claimsProvider.overrideWith((ref) async => [sampleClaim]),
      purchaseRequestsProvider.overrideWith((ref) async => []),
      profileProvider.overrideWith((ref) async => const EmployeeProfile(
        id: 7, fullName: 'Asha Perera', email: 'asha@example.com', designation: 'Engineer',
      )),
    ],
    child: MaterialApp.router(routerConfig: router),
  );
}

void main() {
  testWidgets('asks the employee to pick a claim from their workflow', (tester) async {
    await tester.pumpWidget(subject());
    await tester.pumpAndSettle();
    expect(find.textContaining('Claim #42'), findsOneWidget);
    expect(find.textContaining('Select an item'), findsOneWidget);
  });

  testWidgets('shows intake policy feedback for the selected claim', (tester) async {
    await tester.pumpWidget(subject());
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Claim #42'));
    await tester.pumpAndSettle();
    expect(find.text('Attach a receipt.'), findsOneWidget);
    expect(find.textContaining('RECEIPT_REQUIRED'), findsOneWidget);
    expect(find.text('Open and revise claim'), findsOneWidget);
  });
}
