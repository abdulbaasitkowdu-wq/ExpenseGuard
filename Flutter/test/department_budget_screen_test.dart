import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:reimbursement_budget/features/budget/budget_api.dart';
import 'package:reimbursement_budget/features/budget/budget_providers.dart';
import 'package:reimbursement_budget/features/budget/department_budget_screen.dart';

class _FakeBudgetApi extends BudgetApi {
  _FakeBudgetApi() : super(baseUrl: 'https://example.test/api', token: 'test');

  @override
  Future<DepartmentBudget> getBudget(int id) async => DepartmentBudget(
        id: id,
        name: 'Engineering budget',
        currency: 'LKR',
        allocated: 1000,
        reserved: 100,
        spent: 200,
        available: 700,
      );

  @override
  Future<BudgetImpact> checkImpact(int id, double amount) async => BudgetImpact(
        requested: amount,
        available: 700,
        isAvailable: amount <= 700,
      );
}

void main() {
  testWidgets('shows budget and validates request impact', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          departmentBudgetIdProvider.overrideWithValue(7),
          budgetApiProvider.overrideWithValue(_FakeBudgetApi()),
        ],
        child: const MaterialApp(home: DepartmentBudgetScreen()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Engineering budget'), findsOneWidget);
    await tester.tap(find.text('Check budget impact'));
    await tester.pump();
    expect(find.text('Enter an amount greater than zero.'), findsOneWidget);

    await tester.enterText(find.byKey(const Key('impactAmount')), '250');
    await tester.tap(find.text('Check budget impact'));
    await tester.pumpAndSettle();
    expect(find.text('Budget is currently available'), findsOneWidget);
  });
}
