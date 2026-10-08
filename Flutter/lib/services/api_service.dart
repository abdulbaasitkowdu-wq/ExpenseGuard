import 'dart:convert';
import 'package:http/http.dart' as http;
import '../config/api_config.dart';

class ApiException implements Exception {
  final int statusCode;
  final String message;
  const ApiException(this.statusCode, this.message);
  @override
  String toString() => message;
}

class ApiService {
  ApiService({http.Client? client, String? baseUrl, this.token})
      : client = client ?? http.Client(),
        baseUrl = baseUrl ?? expenseGuardApiBaseUrl();

  final http.Client client;
  final String baseUrl;
  final String? token;

  Map<String, String> get _headers => {
    'Content-Type': 'application/json',
    if (token != null) 'Authorization': 'Bearer $token',
  };

  Future<Map<String, dynamic>> login(String username, String password) async {
    final response = await client.post(Uri.parse('$baseUrl/auth/login'),
        headers: _headers, body: jsonEncode({'username': username, 'password': password}));
    return _object(response);
  }

  Future<List<dynamic>> getEmployeeReimbursements(int employeeId) async {
    final response = await client.get(
      Uri.parse('$baseUrl/reimbursements/employee/$employeeId'), headers: _headers);
    return _list(response);
  }

  Future<Map<String, dynamic>> getReimbursement(int id) async {
    final response = await client.get(
      Uri.parse('$baseUrl/reimbursements/$id'), headers: _headers);
    return _object(response);
  }

  Future<Map<String, dynamic>> getWorkflowByClaim(int claimId) async {
    final response = await client.get(
      Uri.parse('$baseUrl/workflows/claim/$claimId'), headers: _headers);
    return _object(response);
  }

  dynamic _decode(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return response.body.isEmpty ? null : jsonDecode(response.body);
    }
    var message = 'Request failed (${response.statusCode}).';
    try {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      message = (body['detail'] ?? body['error'] ?? body['title'] ?? message).toString();
    } catch (_) {}
    throw ApiException(response.statusCode, message);
  }

  Map<String, dynamic> _object(http.Response response) =>
      _decode(response) as Map<String, dynamic>;
  List<dynamic> _list(http.Response response) => _decode(response) as List<dynamic>;
}
