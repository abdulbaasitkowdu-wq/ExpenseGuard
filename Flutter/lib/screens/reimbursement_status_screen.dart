import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../auth/auth_provider.dart';

class ReimbursementStatusScreen extends ConsumerStatefulWidget {
  final String reimbursementId;
  const ReimbursementStatusScreen({super.key, required this.reimbursementId});

  @override
  ConsumerState<ReimbursementStatusScreen> createState() => _ReimbursementStatusScreenState();
}

class _ReimbursementStatusScreenState extends ConsumerState<ReimbursementStatusScreen> {
  Map<String, dynamic>? _data;
  Map<String, dynamic>? _workflow;
  bool _loading = true;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final reimb = await ref.read(apiProvider).getReimbursement(int.parse(widget.reimbursementId));
      setState(() => _data = reimb);
      try {
        final wf = await ref.read(apiProvider).getWorkflowByClaim(reimb['expenseClaimId'] as int);
        setState(() => _workflow = wf);
      } catch (_) {}
    } catch (error) {
      setState(() => _error = error);
    }
    setState(() => _loading = false);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return Scaffold(appBar: AppBar(title: const Text('Reimbursement Status')), body: const Center(child: CircularProgressIndicator()));
    }
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Reimbursement Status')),
        body: Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text(_error.toString(), textAlign: TextAlign.center),
          OutlinedButton(onPressed: _load, child: const Text('Retry')),
        ])),
      );
    }

    final d = _data!;
    final status = d['status'] as String? ?? '';
    final statusColor = _statusColor(status);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Reimbursement Status', style: TextStyle(fontWeight: FontWeight.bold)),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.go('/claims')),
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Status Card
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Expanded(child: Text(
                      _formatAmount(d['amount'], d['currency']),
                      style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w800),
                    )),
                    _StatusChip(status: status, color: statusColor),
                  ]),
                  const SizedBox(height: 16),
                  if (d['paymentReference'] != null)
                    _InfoRow(icon: Icons.receipt, label: 'Payment Ref', value: d['paymentReference']),
                  _InfoRow(icon: Icons.person_outline, label: 'Employee', value: d['employeeId']),
                  _InfoRow(icon: Icons.business_outlined, label: 'Department', value: d['departmentId']),
                  _InfoRow(icon: Icons.calendar_today_outlined, label: 'Requested', value: _formatDate(d['requestedAt'])),
                  if (d['completedAt'] != null)
                    _InfoRow(icon: Icons.check_circle_outline, label: 'Completed', value: _formatDate(d['completedAt'])),
                  if (d['failureReason'] != null) ...[
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(color: Colors.red.shade900.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(8), border: Border.all(color: Colors.red.shade700.withValues(alpha: 0.4))),
                      child: Row(children: [
                        Icon(Icons.error_outline, color: Colors.red.shade400, size: 18),
                        const SizedBox(width: 8),
                        Expanded(child: Text(d['failureReason'], style: TextStyle(color: Colors.red.shade300, fontSize: 13))),
                      ]),
                    ),
                  ],
                  if (status == 'BUDGET_REVIEW_REQUIRED') ...[
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(color: Colors.blue.shade900.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(8), border: Border.all(color: Colors.blue.shade700.withValues(alpha: 0.4))),
                      child: Row(children: [
                        Icon(Icons.info_outline, color: Colors.blue.shade400, size: 18),
                        const SizedBox(width: 8),
                        const Expanded(child: Text('Insufficient budget — Finance team review required', style: TextStyle(fontSize: 13))),
                      ]),
                    ),
                  ],
                ]),
              ),
            ),
            const SizedBox(height: 16),

            // Workflow Steps
            if (_workflow != null) _WorkflowCard(workflow: _workflow!),

            const SizedBox(height: 16),
            // Payment Button
            if (status == 'PAID')
              ElevatedButton.icon(
                onPressed: () => context.go('/payment/${d['id']}'),
                icon: const Icon(Icons.credit_card),
                label: const Text('View Payment Details'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF6366F1),
                  foregroundColor: Colors.white,
                  minimumSize: const Size(double.infinity, 48),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
              ),
          ],
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
      '${currency ?? 'LKR'} ${(amount as num?)?.toStringAsFixed(2) ?? '0.00'}';

  String _formatDate(dynamic d) {
    if (d == null) return '—';
    try { return DateTime.parse(d).toLocal().toString().substring(0, 16); } catch (_) { return d.toString(); }
  }

}

class _InfoRow extends StatelessWidget {
  final IconData icon;
  final String label, value;
  const _InfoRow({required this.icon, required this.label, required this.value});

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(children: [
      Icon(icon, size: 16, color: Colors.grey.shade500),
      const SizedBox(width: 8),
      Text(label, style: TextStyle(color: Colors.grey.shade500, fontSize: 13)),
      const Spacer(),
      Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
    ]),
  );
}

class _StatusChip extends StatelessWidget {
  final String status;
  final Color color;
  const _StatusChip({required this.status, required this.color});

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
    decoration: BoxDecoration(color: color.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(20), border: Border.all(color: color.withValues(alpha: 0.5))),
    child: Text(status.replaceAll('_', ' '), style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: color)),
  );
}

class _WorkflowCard extends StatelessWidget {
  final Map<String, dynamic> workflow;
  const _WorkflowCard({required this.workflow});

  @override
  Widget build(BuildContext context) {
    final steps = (workflow['steps'] as List?) ?? [];
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Workflow Progress', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
          const SizedBox(height: 4),
          Text('${workflow['workflowExecutionId']} • ${workflow['status']}', style: TextStyle(fontSize: 12, color: Colors.grey.shade500)),
          const SizedBox(height: 16),
          ...steps.map((step) => _StepRow(step: step)),
        ]),
      ),
    );
  }
}

class _StepRow extends StatelessWidget {
  final dynamic step;
  const _StepRow({required this.step});

  @override
  Widget build(BuildContext context) {
    final status = step['status'] as String? ?? 'PENDING';
    final icon = switch (status) {
      'COMPLETED' => const Icon(Icons.check_circle, color: Colors.green, size: 18),
      'FAILED' => const Icon(Icons.cancel, color: Colors.red, size: 18),
      'WAITING_FOR_HUMAN' => const Icon(Icons.pause_circle, color: Colors.orange, size: 18),
      'IN_PROGRESS' => const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)),
      _ => Icon(Icons.radio_button_unchecked, color: Colors.grey.shade600, size: 18),
    };
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(children: [
        icon, const SizedBox(width: 10),
        Text((step['name'] as String? ?? '').replaceAll('_', ' '), style: const TextStyle(fontSize: 13)),
        const Spacer(),
        Text(step['type'] ?? '', style: TextStyle(fontSize: 10, color: Colors.grey.shade600)),
      ]),
    );
  }
}
