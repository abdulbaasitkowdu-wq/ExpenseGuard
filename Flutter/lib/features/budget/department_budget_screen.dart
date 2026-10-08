import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import 'budget_api.dart';
import 'budget_providers.dart';

class DepartmentBudgetScreen extends ConsumerStatefulWidget {
  const DepartmentBudgetScreen({super.key});

  @override
  ConsumerState<DepartmentBudgetScreen> createState() =>
      _DepartmentBudgetScreenState();
}

class _DepartmentBudgetScreenState
    extends ConsumerState<DepartmentBudgetScreen> {
  final _amountController = TextEditingController();
  double? _amount;
  String? _validation;

  @override
  void dispose() {
    _amountController.dispose();
    super.dispose();
  }

  void _checkImpact(int budgetId) {
    final amount = double.tryParse(_amountController.text.trim());
    if (amount == null || amount <= 0) {
      setState(() => _validation = 'Enter an amount greater than zero.');
      return;
    }
    setState(() {
      _validation = null;
      _amount = amount;
    });
    ref.invalidate(
      budgetImpactProvider((budgetId: budgetId, amount: amount)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final budget = ref.watch(departmentBudgetProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Department budget'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            onPressed: () => ref.invalidate(departmentBudgetProvider),
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: budget.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorState(
          error: error,
          onRetry: () => ref.invalidate(departmentBudgetProvider),
        ),
        data: (value) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(value.name,
                style:
                    const TextStyle(fontSize: 24, fontWeight: FontWeight.bold)),
            const SizedBox(height: 16),
            _BudgetSummary(budget: value),
            const SizedBox(height: 24),
            Text('Check request impact',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            const Text(
              'Preview whether an expense request fits the current available '
              'balance. This does not reserve or spend funds.',
            ),
            const SizedBox(height: 16),
            TextField(
              key: const Key('impactAmount'),
              controller: _amountController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'Request amount (${value.currency})',
                errorText: _validation,
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: () => _checkImpact(value.id),
              icon: const Icon(Icons.calculate_outlined),
              label: const Text('Check budget impact'),
            ),
            if (_amount != null) ...[
              const SizedBox(height: 16),
              _ImpactResult(
                budgetId: value.id,
                amount: _amount!,
                currency: value.currency,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _BudgetSummary extends StatelessWidget {
  const _BudgetSummary({required this.budget});
  final DepartmentBudget budget;

  @override
  Widget build(BuildContext context) {
    final format = NumberFormat.currency(
      name: budget.currency,
      symbol: '${budget.currency} ',
    );
    final used = budget.allocated <= 0
        ? 0.0
        : ((budget.spent + budget.reserved) / budget.allocated)
            .clamp(0, 1)
            .toDouble();
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Available', style: Theme.of(context).textTheme.labelLarge),
            Text(format.format(budget.available),
                style:
                    const TextStyle(fontSize: 28, fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            LinearProgressIndicator(value: used),
            const SizedBox(height: 8),
            Text(
              '${format.format(budget.spent)} spent · '
              '${format.format(budget.reserved)} reserved · '
              '${format.format(budget.allocated)} allocated',
            ),
          ],
        ),
      ),
    );
  }
}

class _ImpactResult extends ConsumerWidget {
  const _ImpactResult({
    required this.budgetId,
    required this.amount,
    required this.currency,
  });
  final int budgetId;
  final double amount;
  final String currency;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final request = (budgetId: budgetId, amount: amount);
    return ref.watch(budgetImpactProvider(request)).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => _ErrorState(
            error: error,
            onRetry: () => ref.invalidate(budgetImpactProvider(request)),
          ),
          data: (impact) {
            final color = impact.isAvailable ? Colors.green : Colors.orange;
            return Card(
              color: color.withValues(alpha: 0.12),
              child: ListTile(
                leading: Icon(
                  impact.isAvailable ? Icons.check_circle : Icons.warning,
                  color: color,
                ),
                title: Text(impact.isAvailable
                    ? 'Budget is currently available'
                    : 'Request exceeds available budget'),
                subtitle: Text(
                  '$currency ${impact.available.toStringAsFixed(2)} available '
                  'before this request.',
                ),
              ),
            );
          },
        );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.error, required this.onRetry});
  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final failure = error is ApiFailure ? error as ApiFailure : null;
    final message = failure?.isForbidden == true
        ? 'Your account cannot view this budget. Ask an administrator to '
            'enable employee budget visibility.'
        : failure?.isConflict == true
            ? 'The budget changed. Refresh and check again.'
            : failure?.message ?? 'Unable to load budget information.';
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 48),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }
}
