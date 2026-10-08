import 'dart:convert';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../services/api_service.dart';

abstract class SessionStorage {
  Future<String?> read();
  Future<void> write(String value);
  Future<void> clear();
}

class SecureSessionStorage implements SessionStorage {
  const SecureSessionStorage(this.storage);
  final FlutterSecureStorage storage;
  static const key = 'expenseguard.session';
  @override
  Future<String?> read() => storage.read(key: key);
  @override
  Future<void> write(String value) => storage.write(key: key, value: value);
  @override
  Future<void> clear() => storage.delete(key: key);
}

class AuthSession {
  const AuthSession({
    required this.token, required this.expiresAt, required this.employeeId,
    required this.username, required this.role,
  });
  final String token;
  final DateTime expiresAt;
  final int employeeId;
  final String username;
  final String role;

  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
    token: json['token'] as String,
    expiresAt: DateTime.parse(json['expiresAt'] as String),
    employeeId: json['employeeId'] as int,
    username: json['username'] as String,
    role: json['role'] as String,
  );
  Map<String, dynamic> toJson() => {
    'token': token, 'expiresAt': expiresAt.toIso8601String(), 'employeeId': employeeId,
    'username': username, 'role': role,
  };
}

final sessionStorageProvider = Provider<SessionStorage>((ref) =>
    const SecureSessionStorage(FlutterSecureStorage()));

final unauthenticatedApiProvider = Provider<ApiService>((ref) => ApiService());

final authProvider = AsyncNotifierProvider<AuthController, AuthSession?>(AuthController.new);

class AuthController extends AsyncNotifier<AuthSession?> {
  @override
  Future<AuthSession?> build() async {
    final raw = await ref.read(sessionStorageProvider).read();
    if (raw == null) return null;
    try {
      final session = AuthSession.fromJson(jsonDecode(raw) as Map<String, dynamic>);
      if (session.expiresAt.isAfter(DateTime.now())) return session;
    } catch (_) {}
    await ref.read(sessionStorageProvider).clear();
    return null;
  }

  Future<void> login(String username, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final json = await ref.read(unauthenticatedApiProvider).login(username.trim(), password);
      final session = AuthSession.fromJson(json);
      await ref.read(sessionStorageProvider).write(jsonEncode(session.toJson()));
      return session;
    });
  }

  Future<void> logout() async {
    await ref.read(sessionStorageProvider).clear();
    state = const AsyncData(null);
  }
}

final apiProvider = Provider<ApiService>((ref) {
  final session = ref.watch(authProvider).value;
  return ApiService(token: session?.token);
});

final reimbursementsProvider = FutureProvider<List<dynamic>>((ref) async {
  final session = ref.watch(authProvider).requireValue;
  if (session == null) return [];
  return ref.read(apiProvider).getEmployeeReimbursements(session.employeeId);
});

final reimbursementProvider = FutureProvider.family<Map<String, dynamic>, int>(
  (ref, id) => ref.read(apiProvider).getReimbursement(id),
);

final workflowProvider = FutureProvider.family<Map<String, dynamic>?, int>((ref, claimId) async {
  try {
    return await ref.read(apiProvider).getWorkflowByClaim(claimId);
  } on ApiException catch (error) {
    if (error.statusCode == 404) return null;
    rethrow;
  }
});
