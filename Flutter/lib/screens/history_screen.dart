import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../auth/auth_provider.dart';

class HistoryScreen extends ConsumerStatefulWidget {
  const HistoryScreen({super.key});

  @override
  ConsumerState<HistoryScreen> createState() => _HistoryScreenState();
}

class _HistoryScreenState extends ConsumerState<HistoryScreen> {
  List<dynamic> _history = [];
  bool _loading = true;
  Object? _error;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final session = ref.read(authProvider).requireValue!;
      final data = await ref.read(apiProvider).getEmployeeReimbursements(session.employeeId);
      setState(() { _history = data.where((d) => d['status'] == 'PAID').toList(); _loading = false; });
    } catch (error) {
      setState(() { _error = error; _loading = false; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final totalPaid = _history.fold<double>(0, (sum, h) => sum + ((h['amount'] as num?)?.toDouble() ?? 0));
    return Scaffold(
      appBar: AppBar(
        title: const Text('Reimbursement History', style: TextStyle(fontWeight: FontWeight.bold)),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.go('/home')),
      ),
      body: Column(children: [
        // Summary Banner
        Container(
          width: double.infinity, padding: const EdgeInsets.all(20),
          decoration: const BoxDecoration(gradient: LinearGradient(colors: [Color(0xFF6366F1), Color(0xFF8B5CF6)])),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Total Reimbursed', style: TextStyle(color: Colors.white70, fontSize: 13)),
            const SizedBox(height: 4),
            Text('LKR ${totalPaid.toStringAsFixed(2)}', style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w800)),
            Text('${_history.length} paid reimbursements', style: const TextStyle(color: Colors.white70, fontSize: 12)),
          ]),
        ),
        // History List
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                ? Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
                    Text(_error.toString()), OutlinedButton(onPressed: _load, child: const Text('Retry')),
                  ]))
                : _history.isEmpty
                  ? const Center(child: Text('No paid reimbursements yet.'))
              : ListView.builder(
                  padding: const EdgeInsets.all(16),
                  itemCount: _history.length,
                  itemBuilder: (ctx, i) {
                    final h = _history[i];
                    return Card(
                      margin: const EdgeInsets.only(bottom: 10),
                      child: ListTile(
                        leading: const CircleAvatar(
                          backgroundColor: Color(0xFF10B981),
                          child: Icon(Icons.check, color: Colors.white, size: 18),
                        ),
                        title: Text('LKR ${(h['amount'] as num?)?.toStringAsFixed(2) ?? '0.00'}',
                            style: const TextStyle(fontWeight: FontWeight.bold)),
                        subtitle: Text('${h['paymentReference'] ?? '—'} • ${h['departmentId'] ?? ''}'),
                        trailing: Text(h['completedAt']?.substring(0, 10) ?? '', style: TextStyle(fontSize: 11, color: Colors.grey.shade500)),
                        onTap: () => context.go('/reimbursement/${h['id']}'),
                      ),
                    );
                  },
                ),
        ),
      ]),
    );
  }

}
