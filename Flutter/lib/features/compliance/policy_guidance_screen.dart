import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../models/expense_models.dart';
import '../../providers/expense_providers.dart';
import 'compliance_providers.dart';

class PolicyGuidanceScreen extends ConsumerStatefulWidget {
  final String? initialClaimId;
  const PolicyGuidanceScreen({super.key, this.initialClaimId});

  @override
  ConsumerState<PolicyGuidanceScreen> createState() => _PolicyGuidanceScreenState();
}

class _PolicyGuidanceScreenState extends ConsumerState<PolicyGuidanceScreen> {
  String _kind = 'claims';
  ExpenseClaim? _claim;
  PurchaseRequest? _request;
  bool _didAutoSelect = false;

  @override
  void initState() {
    super.initState();
    if (widget.initialClaimId != null) _kind = 'claims';
  }

  Future<void> _checkClaim(ExpenseClaim claim) async {
    setState(() { _claim = claim; _request = null; });
    final profile = ref.read(profileProvider).valueOrNull;
    await ref.read(complianceControllerProvider.notifier).evaluate(ComplianceInput(
      claimId: claim.id,
      currency: claim.currency,
      designation: profile?.designation ?? '',
      receiptAmount: null,
    ));
  }

  void _showRequest(PurchaseRequest request) {
    setState(() { _request = request; _claim = null; });
  }

  @override
  Widget build(BuildContext context) {
    final claims = ref.watch(claimsProvider);
    final requests = ref.watch(purchaseRequestsProvider);
    final result = ref.watch(complianceControllerProvider);
    final initialId = int.tryParse(widget.initialClaimId ?? '');
    if (!_didAutoSelect && initialId != null && claims.hasValue) {
      final match = claims.requireValue.where((item) => item.id == initialId);
      if (match.isNotEmpty) {
        _didAutoSelect = true;
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) _checkClaim(match.first);
        });
      }
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Policy guidance'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.go('/home')),
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Text('Review before you resubmit', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
          const SizedBox(height: 6),
          Text(
            'Pick one of your claims or purchase requests. Policy and AI findings come from the same intake workflow used on submit.',
            style: TextStyle(color: Colors.grey.shade400),
          ),
          const SizedBox(height: 16),
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: 'claims', label: Text('Claims'), icon: Icon(Icons.receipt_long)),
              ButtonSegment(value: 'requests', label: Text('Requests'), icon: Icon(Icons.shopping_cart_outlined)),
            ],
            selected: {_kind},
            onSelectionChanged: (value) => setState(() {
              _kind = value.first;
              _claim = null;
              _request = null;
            }),
          ),
          const SizedBox(height: 16),
          if (_kind == 'claims')
            claims.when(
              loading: () => const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator())),
              error: (error, _) => _MessageCard(error.toString(), retry: () => ref.invalidate(claimsProvider)),
              data: (items) => items.isEmpty
                ? const _MessageCard('No claims yet. Create one from Expense intake, then check it here.')
                : Column(children: items.map((claim) => _ItemButton(
                    selected: _claim?.id == claim.id,
                    title: 'Claim #${claim.id} · ${claim.category}',
                    subtitle: '${claim.currency} ${claim.amount.toStringAsFixed(2)} · ${claim.status}',
                    onTap: () => _checkClaim(claim),
                  )).toList()),
            )
          else
            requests.when(
              loading: () => const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator())),
              error: (error, _) => _MessageCard(error.toString(), retry: () => ref.invalidate(purchaseRequestsProvider)),
              data: (items) => items.isEmpty
                ? const _MessageCard('No purchase requests yet. Submit one from Expense intake to see AI policy, fraud, and budget findings.')
                : Column(children: items.map((request) => _ItemButton(
                    selected: _request?.id == request.id,
                    title: 'PR #${request.id} · ${request.category ?? 'Uncategorized'}',
                    subtitle: '${request.currency} ${request.amount.toStringAsFixed(2)} · ${request.status}',
                    onTap: () => _showRequest(request),
                  )).toList()),
            ),
          const SizedBox(height: 16),
          if (_kind == 'claims' && _claim != null)
            result.when(
              loading: () => const Center(child: Padding(padding: EdgeInsets.all(16), child: CircularProgressIndicator())),
              error: (error, _) => _MessageCard(
                error is ApiFailure ? error.message : error.toString(),
                retry: () { if (_claim != null) _checkClaim(_claim!); },
              ),
              data: (feedback) => feedback == null
                ? const _MessageCard('Select a claim to run the policy check used during claim intake.')
                : _ClaimFeedback(claim: _claim!, feedback: feedback),
            )
          else if (_kind == 'requests' && _request != null)
            _RequestFeedback(request: _request!)
          else
            const _MessageCard('Select an item to see policy and AI findings from the intake workflow.'),
        ],
      ),
    );
  }
}

class _ItemButton extends StatelessWidget {
  const _ItemButton({required this.title, required this.subtitle, required this.onTap, required this.selected});
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: SizedBox(
        width: double.infinity,
        child: FilledButton.tonal(
          onPressed: onTap,
          style: FilledButton.styleFrom(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
            alignment: Alignment.centerLeft,
            backgroundColor: selected ? Theme.of(context).colorScheme.primary.withValues(alpha: 0.28) : null,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          ),
          child: Row(children: [
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 2),
              Text(subtitle, style: TextStyle(fontSize: 13, color: Colors.grey.shade400)),
            ])),
            const Icon(Icons.chevron_right),
          ]),
        ),
      ),
    );
  }
}

class _ClaimFeedback extends StatelessWidget {
  const _ClaimFeedback({required this.claim, required this.feedback});
  final ExpenseClaim claim;
  final ComplianceFeedback feedback;

  @override
  Widget build(BuildContext context) {
    final ok = feedback.violations.isEmpty && feedback.outcome == 'compliant';
    return Card(child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Icon(ok ? Icons.check_circle : Icons.edit_note, color: ok ? Colors.green : Colors.orange),
          const SizedBox(width: 8),
          Text(ok ? 'Ready for approval' : 'Needs revision', style: const TextStyle(fontWeight: FontWeight.w700)),
        ]),
        const SizedBox(height: 6),
        Text('Claim #${claim.id} · ${feedback.outcome.replaceAll('_', ' ')}', style: TextStyle(color: Colors.grey.shade400)),
        const SizedBox(height: 12),
        if (ok)
          const Text('No policy violations were returned. Fraud details stay with authorized reviewers.')
        else ...[
          const Text('Fix these items in Expense intake, then submit or resubmit:'),
          const SizedBox(height: 8),
          ...feedback.violations.map((item) => ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.warning_amber),
            title: Text(item.message),
            subtitle: Text('${item.ruleCode} · ${item.severity}'),
          )),
        ],
        const SizedBox(height: 8),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: () => context.go('/intake/claims/${claim.id}'),
            child: Text(claim.status == 'NeedsCorrection' ? 'Open and revise claim' : 'Open claim'),
          ),
        ),
      ]),
    ));
  }
}

class _RequestFeedback extends StatelessWidget {
  const _RequestFeedback({required this.request});
  final PurchaseRequest request;

  @override
  Widget build(BuildContext context) {
    final review = request.review;
    if (review == null) {
      return const _MessageCard('This request has no AI review yet. Submit it from Expense intake to run policy, fraud, and budget agents.');
    }
    return Card(child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Icon(review.hasFlags ? Icons.flag : Icons.check_circle, color: review.hasFlags ? Colors.orange : Colors.green),
          const SizedBox(width: 8),
          Text(review.hasFlags ? 'Flagged for reviewers' : 'No intake flags', style: const TextStyle(fontWeight: FontWeight.w700)),
        ]),
        const SizedBox(height: 6),
        Text(review.summary, style: TextStyle(color: Colors.grey.shade400)),
        const SizedBox(height: 12),
        _Section(title: 'Policy', section: review.policy),
        _Section(title: 'Fraud risk', section: review.fraud),
        _Section(title: 'Budget', section: review.budget),
        const SizedBox(height: 8),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: () => context.go('/intake'),
            child: Text(request.status == 'Draft' ? 'Open expense intake to revise' : 'Open expense intake'),
          ),
        ),
      ]),
    ));
  }
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.section});
  final String title;
  final ReviewSection section;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('$title · ${section.outcome.replaceAll('_', ' ')}', style: const TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: 4),
        Text(section.summary),
        ...section.flags.map((flag) => Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Text('${flag.severity}: ${flag.message}', style: TextStyle(color: Colors.orange.shade200)),
        )),
      ]),
    );
  }
}

class _MessageCard extends StatelessWidget {
  const _MessageCard(this.message, {this.retry});
  final String message;
  final VoidCallback? retry;

  @override
  Widget build(BuildContext context) => Card(child: Padding(
    padding: const EdgeInsets.all(16),
    child: Column(children: [
      Text(message, textAlign: TextAlign.center),
      if (retry != null) ...[
        const SizedBox(height: 8),
        OutlinedButton(onPressed: retry, child: const Text('Retry')),
      ],
    ]),
  ));
}
