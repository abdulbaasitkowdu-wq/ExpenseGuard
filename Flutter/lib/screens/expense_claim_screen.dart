import 'dart:typed_data';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import '../models/expense_models.dart';
import '../providers/expense_providers.dart';
import 'purchase_request_screen.dart' show currencyCode, positiveAmount, requiredField;

class ExpenseClaimScreen extends ConsumerStatefulWidget {
  const ExpenseClaimScreen({super.key, this.claimId});
  final int? claimId;
  @override
  ConsumerState<ExpenseClaimScreen> createState() => _ExpenseClaimScreenState();
}

class _ExpenseClaimScreenState extends ConsumerState<ExpenseClaimScreen> {
  final formKey = GlobalKey<FormState>();
  final category = TextEditingController();
  final description = TextEditingController();
  final amount = TextEditingController();
  final currency = TextEditingController(text: 'LKR');
  final vendor = TextEditingController();
  final resubmitReason = TextEditingController();
  DateTime? purchaseDate;
  String flow = 'OutOfPocket';
  int? purchaseRequestId;
  ExpenseClaim? claim;
  ReceiptResult? receipt;
  bool loading = false;
  String? error;

  bool get editable => claim == null || claim!.status == 'Draft' || claim!.status == 'NeedsCorrection';

  @override
  void initState() {
    super.initState();
    if (widget.claimId != null) _load();
  }

  Future<void> _load() async {
    setState(() => loading = true);
    try {
      final value = await ref.read(expenseRepositoryProvider).getClaim(widget.claimId!);
      claim = value;
      category.text = value.category; description.text = value.description;
      amount.text = value.amount.toString(); currency.text = value.currency;
      vendor.text = value.vendor ?? ''; purchaseDate = value.purchaseDate;
      flow = value.flow; purchaseRequestId = value.purchaseRequestId;
    } catch (e) {
      error = e.toString();
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  @override
  void dispose() {
    category.dispose(); description.dispose(); amount.dispose(); currency.dispose();
    vendor.dispose(); resubmitReason.dispose();
    super.dispose();
  }

  ClaimDraft _draft() => ClaimDraft(
    amount: double.parse(amount.text), category: category.text.trim(), description: description.text.trim(),
    currency: currency.text.trim().toUpperCase(), vendor: vendor.text.trim().isEmpty ? null : vendor.text.trim(),
    purchaseDate: purchaseDate, flow: flow, purchaseRequestId: flow == 'PrePurchase' ? purchaseRequestId : null,
    version: claim?.version ?? 0,
  );

  Future<bool> _save() async {
    if (!formKey.currentState!.validate()) return false;
    if (flow == 'PrePurchase' && purchaseRequestId == null) {
      setState(() => error = 'Choose an approved purchase request.');
      return false;
    }
    setState(() { loading = true; error = null; });
    try {
      claim = await ref.read(expenseRepositoryProvider).saveClaim(_draft(), id: claim?.id);
      ref.invalidate(claimsProvider);
      if (mounted) setState(() {});
      return true;
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
      return false;
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _submit() async {
    if (!await _save()) return;
    setState(() => loading = true);
    try {
      await ref.read(expenseRepositoryProvider).submitClaim(claim!.id);
      await _load();
      ref.invalidate(claimsProvider);
      ref.invalidate(claimHistoryProvider(claim!.id));
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _resubmit() async {
    if (!await _save()) return;
    setState(() => loading = true);
    try {
      await ref.read(expenseRepositoryProvider).resubmitClaim(claim!.id, resubmitReason.text.trim().isEmpty ? null : resubmitReason.text.trim());
      await _load();
      ref.invalidate(claimsProvider);
      ref.invalidate(claimHistoryProvider(claim!.id));
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _pickImage(ImageSource source) async {
    final file = await ImagePicker().pickImage(source: source, imageQuality: 85);
    if (file != null) await _upload(await file.readAsBytes(), file.name);
  }

  Future<void> _pickPdf() async {
    final result = await FilePicker.platform.pickFiles(type: FileType.custom, allowedExtensions: const ['pdf'], withData: true);
    final file = result?.files.single;
    if (file != null && file.bytes != null) await _upload(file.bytes!, file.name);
  }

  Future<void> _upload(Uint8List bytes, String name) async {
    if (claim == null && !await _save()) return;
    setState(() { loading = true; error = null; });
    try {
      receipt = await ref.read(expenseRepositoryProvider).uploadReceipt(claim!.id, bytes, name);
      if (mounted) setState(() {});
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> _correctReceipt() async {
    if (receipt == null) return;
    setState(() => loading = true);
    try {
      receipt = await ref.read(expenseRepositoryProvider).correctReceipt(claim!.id, receipt!.id, {
        'vendor': vendor.text.trim().isEmpty ? null : vendor.text.trim(),
        'amount': double.tryParse(amount.text), 'purchaseDate': purchaseDate?.toIso8601String(),
        'currency': currency.text.trim().toUpperCase(),
      });
      if (mounted) setState(() {});
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (loading && claim == null && widget.claimId != null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    final approvedRequests = ref.watch(purchaseRequestsProvider).valueOrNull?.where((request) => request.status == 'Approved').toList() ?? [];
    return Scaffold(
      appBar: AppBar(title: Text(claim == null ? 'New claim' : 'Claim #${claim!.id}')),
      body: Form(key: formKey, child: ListView(padding: const EdgeInsets.all(16), children: [
        if (claim != null) _StatusBanner(status: claim!.status),
        TextFormField(controller: category, enabled: editable, decoration: const InputDecoration(labelText: 'Category'), validator: requiredField),
        const SizedBox(height: 12),
        TextFormField(controller: description, enabled: editable, maxLines: 3, decoration: const InputDecoration(labelText: 'Business purpose'), validator: requiredField),
        const SizedBox(height: 12),
        TextFormField(controller: vendor, enabled: editable, decoration: const InputDecoration(labelText: 'Vendor')),
        const SizedBox(height: 12),
        Row(children: [
          Expanded(flex: 2, child: TextFormField(controller: amount, enabled: editable, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Amount'), validator: positiveAmount)),
          const SizedBox(width: 12),
          Expanded(child: TextFormField(controller: currency, enabled: editable, maxLength: 3, decoration: const InputDecoration(labelText: 'Currency'), validator: currencyCode)),
        ]),
        ListTile(
          contentPadding: EdgeInsets.zero, title: const Text('Purchase date'),
          subtitle: Text(purchaseDate == null ? 'Not selected' : purchaseDate!.toLocal().toString().split(' ').first),
          trailing: const Icon(Icons.calendar_today),
          onTap: !editable ? null : () async {
            final date = await showDatePicker(context: context, firstDate: DateTime(2000), lastDate: DateTime.now(), initialDate: purchaseDate ?? DateTime.now());
            if (date != null) setState(() => purchaseDate = date);
          },
        ),
        DropdownButtonFormField<String>(
          initialValue: flow, decoration: const InputDecoration(labelText: 'Claim flow'),
          items: const [DropdownMenuItem(value: 'OutOfPocket', child: Text('Out of pocket')), DropdownMenuItem(value: 'PrePurchase', child: Text('Pre-purchase'))],
          onChanged: !editable ? null : (value) => setState(() { flow = value!; purchaseRequestId = null; }),
        ),
        if (flow == 'PrePurchase') Padding(
          padding: const EdgeInsets.only(top: 12),
          child: DropdownButtonFormField<int>(
            initialValue: purchaseRequestId, decoration: const InputDecoration(labelText: 'Approved purchase request'),
            items: approvedRequests.map((request) => DropdownMenuItem(value: request.id, child: Text(request.description, overflow: TextOverflow.ellipsis))).toList(),
            onChanged: !editable ? null : (value) => setState(() => purchaseRequestId = value),
          ),
        ),
        const SizedBox(height: 20),
        if (editable) _ReceiptPicker(onCamera: () => _pickImage(ImageSource.camera), onGallery: () => _pickImage(ImageSource.gallery), onPdf: _pickPdf),
        if (receipt != null) _OcrReview(receipt: receipt!, onApply: () {
          vendor.text = receipt!.vendor ?? vendor.text; amount.text = receipt!.amount?.toString() ?? amount.text;
          currency.text = receipt!.currency ?? currency.text; setState(() => purchaseDate = receipt!.purchaseDate ?? purchaseDate);
        }, onConfirm: _correctReceipt),
        if (claim?.status == 'NeedsCorrection') TextField(controller: resubmitReason, maxLines: 2, decoration: const InputDecoration(labelText: 'Correction notes')),
        if (error != null) Padding(padding: const EdgeInsets.symmetric(vertical: 12), child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
        if (editable) Row(children: [
          Expanded(child: OutlinedButton(onPressed: loading ? null : _save, child: const Text('Save draft'))),
          const SizedBox(width: 12),
          Expanded(child: FilledButton(onPressed: loading ? null : claim?.status == 'NeedsCorrection' ? _resubmit : _submit, child: Text(claim?.status == 'NeedsCorrection' ? 'Resubmit' : 'Submit'))),
        ]),
        if (claim != null) _History(claimId: claim!.id),
      ])),
    );
  }
}

class _ReceiptPicker extends StatelessWidget {
  const _ReceiptPicker({required this.onCamera, required this.onGallery, required this.onPdf});
  final VoidCallback onCamera;
  final VoidCallback onGallery;
  final VoidCallback onPdf;
  @override
  Widget build(BuildContext context) => Card(child: Padding(
    padding: const EdgeInsets.all(12),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text('Receipt', style: Theme.of(context).textTheme.titleMedium),
      const Text('Attach a photo, gallery image, or PDF before submitting.'),
      Wrap(spacing: 8, children: [
        TextButton.icon(onPressed: onCamera, icon: const Icon(Icons.camera_alt), label: const Text('Camera')),
        TextButton.icon(onPressed: onGallery, icon: const Icon(Icons.photo_library), label: const Text('Gallery')),
        TextButton.icon(onPressed: onPdf, icon: const Icon(Icons.picture_as_pdf), label: const Text('PDF')),
      ]),
    ]),
  ));
}

class _OcrReview extends StatelessWidget {
  const _OcrReview({required this.receipt, required this.onApply, required this.onConfirm});
  final ReceiptResult receipt;
  final VoidCallback onApply;
  final VoidCallback onConfirm;
  @override
  Widget build(BuildContext context) => Card(child: Padding(
    padding: const EdgeInsets.all(12),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(receipt.requiresManualReview ? 'OCR needs manual review' : 'OCR result', style: Theme.of(context).textTheme.titleMedium),
      Text('Vendor: ${receipt.vendor ?? 'Not detected'}'),
      Text('Amount: ${receipt.amount?.toStringAsFixed(2) ?? 'Not detected'} ${receipt.currency ?? ''}'),
      Text('Date: ${receipt.purchaseDate?.toLocal().toString().split(' ').first ?? 'Not detected'}'),
      if (receipt.confidence != null) Text('Confidence: ${(receipt.confidence! * 100).toStringAsFixed(0)}%'),
      Wrap(spacing: 8, children: [
        TextButton(onPressed: onApply, child: const Text('Apply to claim')),
        FilledButton.tonal(onPressed: onConfirm, child: const Text('Confirm corrections')),
      ]),
    ]),
  ));
}

class _StatusBanner extends StatelessWidget {
  const _StatusBanner({required this.status});
  final String status;
  @override
  Widget build(BuildContext context) => Card(child: ListTile(
    leading: const Icon(Icons.info_outline), title: Text(status),
    subtitle: Text(status == 'NeedsCorrection' ? 'Update the claim and receipt, then resubmit.' : 'Current workflow status'),
  ));
}

class _History extends ConsumerWidget {
  const _History({required this.claimId});
  final int claimId;
  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(claimHistoryProvider(claimId)).when(
    loading: () => const Padding(padding: EdgeInsets.all(20), child: Center(child: CircularProgressIndicator())),
    error: (error, _) => ListTile(title: const Text('History unavailable'), subtitle: Text(error.toString())),
    data: (items) => ExpansionTile(
      title: const Text('Status history'),
      children: items.isEmpty ? const [ListTile(title: Text('No status changes yet.'))] : items.map((item) => ListTile(
        leading: const Icon(Icons.timeline), title: Text('${item.from} → ${item.to}'),
        subtitle: Text('${item.changedAt.toLocal()}${item.reason == null ? '' : '\n${item.reason}'}'),
      )).toList(),
    ),
  );
}
