import 'dart:convert';

import 'package:http/http.dart' as http;

class ApiFailure implements Exception {
  const ApiFailure(this.statusCode, this.message);
  final int statusCode;
  final String message;

  bool get isForbidden => statusCode == 403;
  bool get isConflict => statusCode == 409;

  @override
  String toString() => message;
}

class DepartmentBudget {
  const DepartmentBudget({
    required this.id,
    required this.name,
    required this.currency,
    required this.allocated,
    required this.reserved,
    required this.spent,
    required this.available,
  });

  final int id;
  final String name;
  final String currency;
  final double allocated;
  final double reserved;
  final double spent;
  final double available;

  factory DepartmentBudget.fromJson(Map<String, dynamic> json) =>
      DepartmentBudget(
        id: json['id'] as int,
        name: json['name'] as String,
        currency: json['currency'] as String,
        allocated: (json['allocatedAmount'] as num).toDouble(),
        reserved: (json['reservedAmount'] as num).toDouble(),
        spent: (json['spentAmount'] as num).toDouble(),
        available: (json['availableAmount'] as num).toDouble(),
      );
}

class BudgetImpact {
  const BudgetImpact({
    required this.requested,
    required this.available,
    required this.isAvailable,
  });

  final double requested;
  final double available;
  final bool isAvailable;

  factory BudgetImpact.fromJson(Map<String, dynamic> json) => BudgetImpact(
        requested: (json['requestedAmount'] as num).toDouble(),
        available: (json['availableAmount'] as num).toDouble(),
        isAvailable: json['isAvailable'] as bool,
      );
}

class BudgetApi {
  BudgetApi({
    required this.baseUrl,
    required this.token,
    http.Client? client,
  }) : client = client ?? http.Client();

  final String baseUrl;
  final String? token;
  final http.Client client;

  Map<String, String> get _headers => {
        'Accept': 'application/json',
        if (token != null && token!.isNotEmpty)
          'Authorization': 'Bearer $token',
      };

  Future<DepartmentBudget> getBudget(int id) async {
    final response = await client.get(
      Uri.parse('$baseUrl/budgets/$id'),
      headers: _headers,
    );
    return DepartmentBudget.fromJson(
      await _decode(response) as Map<String, dynamic>,
    );
  }

  Future<BudgetImpact> checkImpact(int id, double amount) async {
    final uri = Uri.parse('$baseUrl/budgets/$id/availability').replace(
      queryParameters: {'amount': amount.toStringAsFixed(2)},
    );
    final response = await client.get(uri, headers: _headers);
    return BudgetImpact.fromJson(
      await _decode(response) as Map<String, dynamic>,
    );
  }

  Future<dynamic> _decode(http.Response response) async {
    dynamic body;
    if (response.body.isNotEmpty) {
      body = jsonDecode(response.body);
    }
    if (response.statusCode >= 200 && response.statusCode < 300) return body;
    final message = body is Map<String, dynamic>
        ? body['detail'] ?? body['title'] ?? body['message']
        : null;
    throw ApiFailure(
      response.statusCode,
      message?.toString() ?? 'Request failed (${response.statusCode}).',
    );
  }
}
