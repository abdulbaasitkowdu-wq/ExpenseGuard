import 'package:flutter_test/flutter_test.dart';
import 'package:reimbursement_budget/models/expense_models.dart';

void main() {
  test('claim parses numeric API enums and serializes flow', () {
    final claim = ExpenseClaim.fromJson({
      'expenseClaimId': 12, 'amount': 45.5, 'category': 'Travel',
      'description': 'Taxi', 'currency': 'LKR', 'status': 5, 'flow': 1,
      'version': 2, 'vendor': 'Cab Co', 'purchaseDate': null, 'purchaseRequestId': 8,
    });
    expect(claim.status, 'NeedsCorrection');
    expect(claim.flow, 'PrePurchase');

    const draft = ClaimDraft(
      amount: 45.5, category: 'Travel', description: 'Taxi',
      currency: 'LKR', flow: 'PrePurchase', purchaseRequestId: 8,
    );
    expect(draft.toJson()['flow'], 1);
  });
}
