import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../auth/auth_provider.dart';

class MyClaimsScreen extends ConsumerWidget {
  const MyClaimsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final claims = ref.watch(reimbursementsProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Claims', style: TextStyle(fontWeight: FontWeight.bold)),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.go('/home')),
        actions: [
          IconButton(tooltip: 'Refresh', icon: const Icon(Icons.refresh),
              onPressed: () => ref.invalidate(reimbursementsProvider)),
        ],
      ),
      body: claims.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorState(
          message: error.toString(),
          onRetry: () => ref.invalidate(reimbursementsProvider),
        ),
        data: (items) => items.isEmpty
              ? _buildEmpty()
              : RefreshIndicator(
                  onRefresh: () async => ref.refresh(reimbursementsProvider.future),
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: items.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (ctx, i) => _ClaimCard(claim: items[i]),
                  ),
                ),
      ),
    );
  }

  Widget _buildEmpty() => Center(
    child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
      Icon(Icons.receipt_long_outlined, size: 64, color: Colors.grey.shade600),
      const SizedBox(height: 16),
      Text('No claims found', style: TextStyle(color: Colors.grey.shade500, fontSize: 16)),
    ]),
  );
}

class _ClaimCard extends StatelessWidget {
  final dynamic claim;
  const _ClaimCard({required this.claim});

  @override
  Widget build(BuildContext context) {
    final status = claim['status'] as String? ?? 'UNKNOWN';
    final color = _statusColor(status);
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.go('/reimbursement/${claim['id']}'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
              Text('Claim #${claim['expenseClaimId']}',
                  style: const TextStyle(fontFamily: 'monospace', fontSize: 12, color: Colors.grey)),
              _StatusChip(status: status, color: color),
            ]),
            const SizedBox(height: 10),
            Text(
              _formatAmount(claim['amount'], claim['currency']),
              style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.5),
            ),
            const SizedBox(height: 4),
            Row(children: [
              Icon(Icons.business_outlined, size: 14, color: Colors.grey.shade500),
              const SizedBox(width: 4),
              Text('${claim['departmentId'] ?? ''}', style: TextStyle(fontSize: 12, color: Colors.grey.shade500)),
              const Spacer(),
              Text(_formatDate(claim['requestedAt']), style: TextStyle(fontSize: 11, color: Colors.grey.shade600)),
            ]),
            if (claim['paymentReference'] != null) ...[
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: Colors.green.shade900.withValues(alpha: 0.3),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text('Ref: ${claim['paymentReference']}',
                    style: TextStyle(fontFamily: 'monospace', fontSize: 11, color: Colors.green.shade300)),
              ),
            ],
          ]),
        ),
      ),
    );
  }

  Color _statusColor(String s) => switch (s) {
    'PAID' => Colors.green,
    'PROCESSING' || 'PAYMENT_PENDING' => Colors.indigo,
    'PAYMENT_FAILED' || 'REJECTED' => Colors.red,
    'BUDGET_REVIEW_REQUIRED' => Colors.blue,
    _ => Colors.amber,
  };

  String _formatAmount(dynamic amount, dynamic currency) =>
      '${currency ?? 'LKR'} ${_formatNum(amount)}';

  String _formatNum(dynamic n) {
    if (n == null) return '0.00';
    final num val = n is num ? n : num.tryParse(n.toString()) ?? 0;
    return val.toStringAsFixed(2).replaceAllMapped(
        RegExp(r'(\d)(?=(\d{3})+\.)'), (m) => '${m[1]},');
  }

  String _formatDate(dynamic d) {
    if (d == null) return '';
    try {
      return DateTime.parse(d).toLocal().toString().substring(0, 10);
    } catch (_) { return d.toString(); }
  }
}

class _StatusChip extends StatelessWidget {
  final String status;
  final Color color;
  const _StatusChip({required this.status, required this.color});

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.15),
      borderRadius: BorderRadius.circular(20),
      border: Border.all(color: color.withValues(alpha: 0.4)),
    ),
    child: Text(status.replaceAll('_', ' '),
        style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: color, letterSpacing: 0.5)),
  );
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Center(child: Padding(
    padding: const EdgeInsets.all(24),
    child: Column(mainAxisSize: MainAxisSize.min, children: [
      Text(message, textAlign: TextAlign.center),
      const SizedBox(height: 12),
      OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
    ]),
  ));
}
