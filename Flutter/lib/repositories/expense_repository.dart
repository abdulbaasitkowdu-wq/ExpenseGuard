import 'dart:convert';
import 'dart:typed_data';
import 'package:http/http.dart' as http;
import '../models/expense_models.dart';

abstract interface class AuthSession {
  Future<String?> getAccessToken();
  Future<String?> getEmployeeId();
}

class EnvironmentAuthSession implements AuthSession {
  const EnvironmentAuthSession();
  @override
  Future<String?> getAccessToken() async {
    const value = String.fromEnvironment('EXPENSE_GUARD_TOKEN');
    return value.isEmpty ? null : value;
  }

  @override
  Future<String?> getEmployeeId() async {
    const value = String.fromEnvironment('EXPENSE_GUARD_EMPLOYEE_ID');
    return value.isEmpty ? null : value;
  }
}

abstract interface class ExpenseRepository {
  Future<EmployeeProfile> getProfile();
  Future<List<PurchaseRequest>> getPurchaseRequests();
  Future<PurchaseRequest> createPurchaseRequest(Map<String, dynamic> draft);
  Future<void> submitPurchaseRequest(int id);
  Future<List<ExpenseClaim>> getClaims({String? status, String? category});
  Future<ExpenseClaim> getClaim(int id);
  Future<ExpenseClaim> saveClaim(ClaimDraft draft, {int? id});
  Future<void> submitClaim(int id);
  Future<void> resubmitClaim(int id, String? reason);
  Future<ReceiptResult> uploadReceipt(int claimId, Uint8List bytes, String fileName);
  Future<ReceiptResult> correctReceipt(int claimId, int receiptId, Map<String, dynamic> correction);
  Future<List<ClaimHistory>> getHistory(int claimId);
}

class ApiException implements Exception {
  const ApiException(this.message);
  final String message;
  @override
  String toString() => message;
}

class HttpExpenseRepository implements ExpenseRepository {
  HttpExpenseRepository({required this.baseUrl, required this.auth, http.Client? client})
      : client = client ?? http.Client();
  final String baseUrl;
  final AuthSession auth;
  final http.Client client;

  Future<Map<String, String>> _headers({bool json = true}) async {
    final token = await auth.getAccessToken();
    final employeeId = await auth.getEmployeeId();
    return {
      if (json) 'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
      // Transitional contract; remove when shared JWT identity is authoritative.
      if (employeeId != null) 'X-Employee-Id': employeeId,
    };
  }

  Uri _uri(String path, [Map<String, String?>? query]) => Uri.parse('$baseUrl$path').replace(
    queryParameters: query == null ? null : Map.fromEntries(query.entries.where((e) => e.value?.isNotEmpty == true).map((e) => MapEntry(e.key, e.value!))),
  );

  dynamic _decode(http.Response response) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      String message = 'Request failed (${response.statusCode})';
      try {
        final body = jsonDecode(response.body);
        message = body['message'] ?? body['title'] ?? message;
      } catch (_) {}
      throw ApiException(message);
    }
    return response.body.isEmpty ? null : jsonDecode(response.body);
  }

  @override
  Future<EmployeeProfile> getProfile() async => EmployeeProfile.fromJson(
    _decode(await client.get(_uri('/employees/me'), headers: await _headers())) as Map<String, dynamic>,
  );

  @override
  Future<List<PurchaseRequest>> getPurchaseRequests() async => (
    _decode(await client.get(_uri('/purchase-requests'), headers: await _headers())) as List
  ).map((e) => PurchaseRequest.fromJson(e as Map<String, dynamic>)).toList();

  @override
  Future<PurchaseRequest> createPurchaseRequest(Map<String, dynamic> draft) async => PurchaseRequest.fromJson(
    _decode(await client.post(_uri('/purchase-requests'), headers: await _headers(), body: jsonEncode(draft))) as Map<String, dynamic>,
  );

  @override
  Future<void> submitPurchaseRequest(int id) async {
    _decode(await client.post(_uri('/purchase-requests/$id/submit'), headers: await _headers()));
  }

  @override
  Future<List<ExpenseClaim>> getClaims({String? status, String? category}) async => (
    _decode(await client.get(_uri('/claims', {'status': status, 'category': category}), headers: await _headers())) as List
  ).map((e) => ExpenseClaim.fromJson(e as Map<String, dynamic>)).toList();

  @override
  Future<ExpenseClaim> getClaim(int id) async => ExpenseClaim.fromJson(
    _decode(await client.get(_uri('/claims/$id'), headers: await _headers())) as Map<String, dynamic>,
  );

  @override
  Future<ExpenseClaim> saveClaim(ClaimDraft draft, {int? id}) async {
    final response = id == null
        ? await client.post(_uri('/claims'), headers: await _headers(), body: jsonEncode(draft.toJson()))
        : await client.put(_uri('/claims/$id'), headers: await _headers(), body: jsonEncode(draft.toJson()));
    return ExpenseClaim.fromJson(_decode(response) as Map<String, dynamic>);
  }

  @override
  Future<void> submitClaim(int id) async {
    _decode(await client.post(_uri('/claims/$id/submit'), headers: await _headers()));
  }

  @override
  Future<void> resubmitClaim(int id, String? reason) async {
    _decode(await client.post(_uri('/claims/$id/resubmit'), headers: await _headers(), body: jsonEncode(reason)));
  }

  @override
  Future<ReceiptResult> uploadReceipt(int claimId, Uint8List bytes, String fileName) async {
    final request = http.MultipartRequest('POST', _uri('/claims/$claimId/receipts'));
    request.headers.addAll(await _headers(json: false));
    request.files.add(http.MultipartFile.fromBytes('file', bytes, filename: fileName));
    final streamed = await client.send(request);
    final response = await http.Response.fromStream(streamed);
    return ReceiptResult.fromJson(_decode(response) as Map<String, dynamic>);
  }

  @override
  Future<ReceiptResult> correctReceipt(int claimId, int receiptId, Map<String, dynamic> correction) async => ReceiptResult.fromJson(
    _decode(await client.patch(_uri('/claims/$claimId/receipts/$receiptId'), headers: await _headers(), body: jsonEncode(correction))) as Map<String, dynamic>,
  );

  @override
  Future<List<ClaimHistory>> getHistory(int claimId) async => (
    _decode(await client.get(_uri('/claims/$claimId/history'), headers: await _headers())) as List
  ).map((e) => ClaimHistory.fromJson(e as Map<String, dynamic>)).toList();
}
