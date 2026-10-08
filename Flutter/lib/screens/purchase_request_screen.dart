import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../providers/expense_providers.dart';

class PurchaseRequestScreen extends ConsumerStatefulWidget {
  const PurchaseRequestScreen({super.key});
  @override
  ConsumerState<PurchaseRequestScreen> createState() => _PurchaseRequestScreenState();
}

class _PurchaseRequestScreenState extends ConsumerState<PurchaseRequestScreen> {
  final formKey = GlobalKey<FormState>();
  final description = TextEditingController();
  final amount = TextEditingController();
  final vendor = TextEditingController();
  final currency = TextEditingController(text: 'LKR');
  bool saving = false;
  String? error;

  @override
  void dispose() {
    description.dispose(); amount.dispose(); vendor.dispose(); currency.dispose();
    super.dispose();
  }

  Future<void> save({required bool submit}) async {
    if (!formKey.currentState!.validate()) return;
    setState(() { saving = true; error = null; });
    try {
      final repository = ref.read(expenseRepositoryProvider);
      final request = await repository.createPurchaseRequest({
        'description': description.text.trim(), 'estimatedAmount': double.parse(amount.text),
        'currency': currency.text.trim().toUpperCase(), 'vendor': vendor.text.trim().isEmpty ? null : vendor.text.trim(),
        'version': 0,
      });
      if (submit) await repository.submitPurchaseRequest(request.id);
      ref.invalidate(purchaseRequestsProvider);
      if (mounted) context.pop();
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Purchase request')),
    body: Form(key: formKey, child: ListView(padding: const EdgeInsets.all(16), children: [
      TextFormField(controller: description, maxLines: 3, decoration: const InputDecoration(labelText: 'Business need'), validator: requiredField),
      const SizedBox(height: 12),
      TextFormField(controller: vendor, decoration: const InputDecoration(labelText: 'Preferred vendor (optional)')),
      const SizedBox(height: 12),
      Row(children: [
        Expanded(flex: 2, child: TextFormField(controller: amount, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Estimated amount'), validator: positiveAmount)),
        const SizedBox(width: 12),
        Expanded(child: TextFormField(controller: currency, maxLength: 3, decoration: const InputDecoration(labelText: 'Currency'), validator: currencyCode)),
      ]),
      if (error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
      const SizedBox(height: 20),
      FilledButton(onPressed: saving ? null : () => save(submit: true), child: Text(saving ? 'Submitting…' : 'Save and submit')),
      TextButton(onPressed: saving ? null : () => save(submit: false), child: const Text('Save draft')),
    ])),
  );
}

String? requiredField(String? value) => value == null || value.trim().isEmpty ? 'Required' : null;
String? positiveAmount(String? value) => (double.tryParse(value ?? '') ?? 0) <= 0 ? 'Enter a positive amount' : null;
String? currencyCode(String? value) => !RegExp(r'^[A-Za-z]{3}$').hasMatch(value ?? '') ? 'Use 3 letters' : null;
