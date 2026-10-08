import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../auth/auth_provider.dart';

class ClaimDetailScreen extends ConsumerStatefulWidget {
  final String claimId;
  const ClaimDetailScreen({super.key, required this.claimId});

  @override
  ConsumerState<ClaimDetailScreen> createState() => _ClaimDetailScreenState();
}

class _ClaimDetailScreenState extends ConsumerState<ClaimDetailScreen> {
  Map<String, dynamic>? _reimb;
  Map<String, dynamic>? _workflow;
  bool _loading = true;
  Object? _error;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final session = ref.read(authProvider).requireValue!;
      final all = await ref.read(apiProvider).getEmployeeReimbursements(session.employeeId);
      final reimb = (all.cast<Map<String, dynamic>>()).firstWhere(
        (item) => item['expenseClaimId'].toString() == widget.claimId);
      setState(() => _reimb = reimb);
      final wf = await ref.read(apiProvider).getWorkflowByClaim(int.parse(widget.claimId));
      setState(() { _workflow = wf; _loading = false; });
    } catch (error) {
      setState(() { _error = error; _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return Scaffold(appBar: AppBar(title: const Text('Claim Details')), body: const Center(child: CircularProgressIndicator()));
    }
    if (_error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Claim Details')),
        body: Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text(_error.toString()), OutlinedButton(onPressed: _load, child: const Text('Retry')),
        ])),
      );
    }
    final d = _reimb!;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Claim Details', style: TextStyle(fontWeight: FontWeight.bold)),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.go('/claims')),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Claim Information', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
            const SizedBox(height: 12),
            _Row('Claim ID', widget.claimId),
            _Row('Amount', 'LKR ${(d['amount'] as num?)?.toStringAsFixed(2)}'),
            _Row('Status', d['status']),
            _Row('Department', d['departmentId']),
            _Row('Requested', d['requestedAt']?.substring(0, 10) ?? ''),
          ]))),
          const SizedBox(height: 12),
          if (_workflow != null) Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Workflow Status', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
            const SizedBox(height: 4),
            Text('${_workflow!['workflowExecutionId']}', style: TextStyle(fontSize: 12, color: Colors.grey.shade500, fontFamily: 'monospace')),
            const SizedBox(height: 12),
            ...((_workflow!['steps'] as List?) ?? []).map((s) => _StepRow(step: s)),
          ]))),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: () => context.go(int.tryParse(widget.claimId) == null
                ? '/policy-guidance'
                : '/policy-guidance?claimId=${Uri.encodeQueryComponent(widget.claimId)}'),
            icon: const Icon(Icons.policy_outlined),
            label: const Text('Review compliance and revise'),
          ),
          const SizedBox(height: 8),
          if (d['status'] == 'PAID')
            ElevatedButton.icon(
              onPressed: () => context.go('/payment/${d['id']}'),
              icon: const Icon(Icons.receipt_long),
              label: const Text('View Payment'),
              style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF6366F1), foregroundColor: Colors.white, minimumSize: const Size(double.infinity, 48), shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12))),
            ),
        ],
      ),
    );
  }

}

class _Row extends StatelessWidget {
  final String label, value;
  const _Row(this.label, this.value);

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 5),
    child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
      Text(label, style: TextStyle(color: Colors.grey.shade500, fontSize: 13)),
      Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
    ]),
  );
}

class _StepRow extends StatelessWidget {
  final dynamic step;
  const _StepRow({required this.step});

  @override
  Widget build(BuildContext context) {
    final status = step['status'] as String? ?? 'PENDING';
    final color = switch (status) {
      'COMPLETED' => Colors.green, 'FAILED' => Colors.red,
      'WAITING_FOR_HUMAN' => Colors.orange, 'IN_PROGRESS' => Colors.indigo, _ => Colors.grey,
    };
    final icon = switch (status) {
      'COMPLETED' => Icons.check_circle,
      'FAILED' => Icons.cancel,
      'WAITING_FOR_HUMAN' => Icons.pause_circle,
      _ => Icons.radio_button_unchecked,
    };
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(children: [
        Icon(icon, size: 16, color: color),
        const SizedBox(width: 8),
        Text((step['name'] as String? ?? '').replaceAll('_', ' '), style: const TextStyle(fontSize: 13)),
        const Spacer(),
        Text(step['type'] ?? '', style: TextStyle(fontSize: 10, color: Colors.grey.shade600)),
      ]),
    );
  }
}
