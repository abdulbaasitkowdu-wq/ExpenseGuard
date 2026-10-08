import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../models/expense_models.dart';
import '../providers/expense_providers.dart';

class EmployeeHomeScreen extends ConsumerStatefulWidget {
  const EmployeeHomeScreen({super.key});
  @override
  ConsumerState<EmployeeHomeScreen> createState() => _EmployeeHomeScreenState();
}

class _EmployeeHomeScreenState extends ConsumerState<EmployeeHomeScreen> {
  int index = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(['My claims', 'Purchase requests', 'Profile'][index]),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.go('/home')),
      ),
      body: IndexedStack(index: index, children: const [_ClaimsTab(), _RequestsTab(), _ProfileTab()]),
      floatingActionButton: index < 2 ? FloatingActionButton.extended(
        onPressed: () async {
          await context.push(index == 0 ? '/intake/claims/new' : '/purchase-requests/new');
          if (index == 0) {
            ref.invalidate(claimsProvider);
          } else {
            ref.invalidate(purchaseRequestsProvider);
          }
        },
        icon: const Icon(Icons.add), label: Text(index == 0 ? 'New claim' : 'New request'),
      ) : null,
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (value) => setState(() => index = value),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.receipt_long), label: 'Claims'),
          NavigationDestination(icon: Icon(Icons.shopping_cart_outlined), label: 'Requests'),
          NavigationDestination(icon: Icon(Icons.person_outline), label: 'Profile'),
        ],
      ),
    );
  }
}

class _ClaimsTab extends ConsumerWidget {
  const _ClaimsTab();
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final claims = ref.watch(claimsProvider);
    final filter = ref.watch(claimFilterProvider);
    return RefreshIndicator(
      onRefresh: () => ref.refresh(claimsProvider.future),
      child: CustomScrollView(slivers: [
        SliverToBoxAdapter(child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(children: [
            TextField(
              decoration: const InputDecoration(prefixIcon: Icon(Icons.search), labelText: 'Search category'),
              onSubmitted: (value) => ref.read(claimFilterProvider.notifier).state =
                ClaimFilter(category: value.trim().isEmpty ? null : value.trim(), status: filter.status),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: filter.status,
              decoration: const InputDecoration(labelText: 'Status'),
              items: [null, 'Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'NeedsCorrection']
                .map((value) => DropdownMenuItem(value: value, child: Text(value ?? 'All statuses'))).toList(),
              onChanged: (value) => ref.read(claimFilterProvider.notifier).state =
                ClaimFilter(status: value, category: filter.category),
            ),
          ]),
        )),
        claims.when(
          loading: () => const SliverFillRemaining(child: Center(child: CircularProgressIndicator())),
          error: (error, _) => SliverFillRemaining(child: _ErrorState(error: error, onRetry: () => ref.invalidate(claimsProvider))),
          data: (items) => items.isEmpty
            ? const SliverFillRemaining(child: _EmptyState(icon: Icons.receipt_long, text: 'No claims match your filters.'))
            : SliverList.builder(itemCount: items.length, itemBuilder: (context, i) => _ClaimTile(claim: items[i])),
        ),
      ]),
    );
  }
}

class _ClaimTile extends StatelessWidget {
  const _ClaimTile({required this.claim});
  final ExpenseClaim claim;
  @override
  Widget build(BuildContext context) => Card(
    margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
    child: ListTile(
      title: Text('${claim.category} · ${claim.currency} ${claim.amount.toStringAsFixed(2)}'),
      subtitle: Text('${claim.description}\n${claim.status}'),
      isThreeLine: true,
      trailing: const Icon(Icons.chevron_right),
      onTap: () => context.push('/intake/claims/${claim.id}'),
    ),
  );
}

class _RequestsTab extends ConsumerWidget {
  const _RequestsTab();
  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(purchaseRequestsProvider).when(
    loading: () => const Center(child: CircularProgressIndicator()),
    error: (error, _) => _ErrorState(error: error, onRetry: () => ref.invalidate(purchaseRequestsProvider)),
    data: (items) => items.isEmpty
      ? const _EmptyState(icon: Icons.shopping_cart_outlined, text: 'No purchase requests yet.')
      : RefreshIndicator(
          onRefresh: () => ref.refresh(purchaseRequestsProvider.future),
          child: ListView.builder(itemCount: items.length, itemBuilder: (context, i) {
            final request = items[i];
            return Card(child: ListTile(
              title: Text(request.description),
              subtitle: Text('${request.status}${request.vendor == null ? '' : ' · ${request.vendor}'}'),
              trailing: Text('${request.currency} ${request.amount.toStringAsFixed(2)}'),
            ));
          }),
        ),
  );
}

class _ProfileTab extends ConsumerWidget {
  const _ProfileTab();
  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(profileProvider).when(
    loading: () => const Center(child: CircularProgressIndicator()),
    error: (error, _) => _ErrorState(error: error, onRetry: () => ref.invalidate(profileProvider)),
    data: (profile) => ListView(padding: const EdgeInsets.all(16), children: [
      const CircleAvatar(radius: 42, child: Icon(Icons.person, size: 42)),
      const SizedBox(height: 16),
      Center(child: Text(profile.fullName, style: Theme.of(context).textTheme.headlineSmall)),
      Center(child: Text(profile.designation ?? 'Employee')),
      const SizedBox(height: 20),
      Card(child: ListTile(leading: const Icon(Icons.email_outlined), title: Text(profile.email))),
    ]),
  );
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.error, required this.onRetry});
  final Object error;
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Center(child: Padding(
    padding: const EdgeInsets.all(24),
    child: Column(mainAxisSize: MainAxisSize.min, children: [
      const Icon(Icons.error_outline, size: 48), const SizedBox(height: 8),
      Text(error.toString(), textAlign: TextAlign.center), const SizedBox(height: 12),
      FilledButton.tonal(onPressed: onRetry, child: const Text('Try again')),
    ]),
  ));
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.icon, required this.text});
  final IconData icon;
  final String text;
  @override
  Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
    Icon(icon, size: 52, color: Theme.of(context).colorScheme.outline),
    const SizedBox(height: 12), Text(text),
  ]));
}
