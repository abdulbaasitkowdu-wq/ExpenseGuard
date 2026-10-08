import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../auth/auth_provider.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authProvider).value;
    return Scaffold(
      appBar: AppBar(
        title: const Text('ExpenseGuard', style: TextStyle(fontWeight: FontWeight.bold)),
        actions: [
          IconButton(
            tooltip: 'Sign out',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(authProvider.notifier).logout(),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Text(
            session == null ? 'Welcome' : 'Welcome, ${session.username}',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 6),
          Text(
            'Choose where you want to go.',
            style: TextStyle(color: Colors.grey.shade400),
          ),
          const SizedBox(height: 20),
          _HomeButton(
            icon: Icons.receipt_long,
            title: 'My claims',
            subtitle: 'Track reimbursements and payments',
            onTap: () => context.go('/claims'),
          ),
          _HomeButton(
            icon: Icons.add_box_outlined,
            title: 'Expense intake',
            subtitle: 'Draft and submit claims or purchase requests',
            onTap: () => context.go('/intake'),
          ),
          _HomeButton(
            icon: Icons.policy_outlined,
            title: 'Policy guidance',
            subtitle: 'See AI and policy findings before you resubmit',
            onTap: () => context.go('/policy-guidance'),
          ),
          _HomeButton(
            icon: Icons.history,
            title: 'History',
            subtitle: 'See paid reimbursements',
            onTap: () => context.go('/history'),
          ),
        ],
      ),
    );
  }
}

class _HomeButton extends StatelessWidget {
  const _HomeButton({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: SizedBox(
        width: double.infinity,
        child: FilledButton.tonal(
          onPressed: onTap,
          style: FilledButton.styleFrom(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 18),
            alignment: Alignment.centerLeft,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          ),
          child: Row(
            children: [
              Icon(icon, size: 28),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                    const SizedBox(height: 2),
                    Text(subtitle, style: TextStyle(fontSize: 13, color: Colors.grey.shade400)),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}
