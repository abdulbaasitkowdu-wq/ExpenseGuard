import 'dart:convert';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:http/http.dart' as http;
import '../../auth/auth_provider.dart';
import '../../config/api_config.dart';

class ApiConfig {
  final String baseUrl;
  final String? token;
  const ApiConfig({required this.baseUrl, this.token});

  factory ApiConfig.environment() {
    final baseUrl = expenseGuardApiBaseUrl();
    const token = String.fromEnvironment('AUTH_TOKEN');
    return ApiConfig(
      baseUrl: baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl,
      token: token.isEmpty ? null : token,
    );
  }
}

class ApiFailure implements Exception {
  final int? statusCode;
  final String message;
  const ApiFailure(this.message, {this.statusCode});
  bool get isUnauthorized => statusCode == 401;
  bool get isForbidden => statusCode == 403;
  @override
  String toString() => message;
}

class PolicyViolation {
  final String ruleCode;
  final String message;
  final String severity;
  const PolicyViolation({required this.ruleCode, required this.message, required this.severity});

  factory PolicyViolation.fromJson(Map<String, dynamic> json) => PolicyViolation(
    ruleCode: json['ruleCode']?.toString() ?? 'POLICY',
    message: json['message']?.toString() ?? 'Policy review required.',
    severity: json['severity']?.toString() ?? 'warning',
  );
}

class ComplianceFeedback {
  final String outcome;
  final List<PolicyViolation> violations;
  const ComplianceFeedback({required this.outcome, required this.violations});

  factory ComplianceFeedback.fromJson(Map<String, dynamic> json) => ComplianceFeedback(
    outcome: json['outcome']?.toString() ?? 'unknown',
    violations: (json['violations'] as List<dynamic>? ?? [])
        .whereType<Map<String, dynamic>>()
        .map(PolicyViolation.fromJson)
        .toList(),
  );
}

class ComplianceInput {
  final int claimId;
  final String currency;
  final String designation;
  final double? receiptAmount;
  const ComplianceInput({required this.claimId, required this.currency, required this.designation, this.receiptAmount});
}

abstract class ComplianceRepository {
  Future<ComplianceFeedback> evaluate(ComplianceInput input);
}

class HttpComplianceRepository implements ComplianceRepository {
  final ApiConfig config;
  final http.Client client;
  HttpComplianceRepository(this.config, this.client);

  @override
  Future<ComplianceFeedback> evaluate(ComplianceInput input) async {
    if (config.baseUrl.isEmpty) {
      throw const ApiFailure('The API URL is not configured.');
    }
    if (config.token == null) {
      throw const ApiFailure('Sign in before requesting guidance.', statusCode: 401);
    }
    final response = await client.post(
      Uri.parse('${config.baseUrl}/policies/evaluate'),
      headers: {'Content-Type': 'application/json', 'Authorization': 'Bearer ${config.token}'},
      body: jsonEncode({
        'expenseClaimId': input.claimId,
        'currency': input.currency,
        'designation': input.designation.trim().isEmpty ? null : input.designation.trim(),
        'receiptAmount': input.receiptAmount,
      }),
    );
    if (response.statusCode == 200) {
      return ComplianceFeedback.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    }
    if (response.statusCode == 401) throw const ApiFailure('Your session expired. Sign in and retry.', statusCode: 401);
    if (response.statusCode == 403) throw const ApiFailure('Your account cannot evaluate this claim.', statusCode: 403);
    if (response.statusCode == 404) throw const ApiFailure('The claim was not found.', statusCode: 404);
    if (response.statusCode == 400) throw const ApiFailure('The claim details are invalid. Review and retry.', statusCode: 400);
    throw ApiFailure('The compliance service is unavailable (${response.statusCode}). No result was assumed.', statusCode: response.statusCode);
  }
}

final apiConfigProvider = Provider<ApiConfig>((ref) {
  final session = ref.watch(authProvider).value;
  final fallback = ApiConfig.environment();
  return ApiConfig(baseUrl: fallback.baseUrl, token: session?.token ?? fallback.token);
});
final httpClientProvider = Provider<http.Client>((ref) {
  final client = http.Client();
  ref.onDispose(client.close);
  return client;
});
final complianceRepositoryProvider = Provider<ComplianceRepository>(
  (ref) => HttpComplianceRepository(ref.watch(apiConfigProvider), ref.watch(httpClientProvider)),
);
final complianceControllerProvider = AsyncNotifierProvider<ComplianceController, ComplianceFeedback?>(ComplianceController.new);

class ComplianceController extends AsyncNotifier<ComplianceFeedback?> {
  @override
  Future<ComplianceFeedback?> build() async => null;

  Future<void> evaluate(ComplianceInput input) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(complianceRepositoryProvider).evaluate(input));
  }
}

