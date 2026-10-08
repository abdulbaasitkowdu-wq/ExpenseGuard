import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:reimbursement_budget/auth/auth_provider.dart';
import 'package:reimbursement_budget/services/api_service.dart';

void main() {
  test('auth restores session from mocked secure storage', () async {
    final storage = MemoryStorage(
      '{"token":"test","expiresAt":"2099-01-01T00:00:00.000Z",'
      '"employeeId":7,"username":"alice","role":"Employee"}',
    );
    final container = ProviderContainer(overrides: [
      sessionStorageProvider.overrideWithValue(storage),
      unauthenticatedApiProvider.overrideWithValue(FakeApi()),
    ]);
    addTearDown(container.dispose);

    final session = await container.read(authProvider.future);
    expect(session?.employeeId, 7);
    expect(session?.username, 'alice');
  });

  test('login persists API session in mocked storage', () async {
    final storage = MemoryStorage(null);
    final container = ProviderContainer(overrides: [
      sessionStorageProvider.overrideWithValue(storage),
      unauthenticatedApiProvider.overrideWithValue(FakeApi()),
    ]);
    addTearDown(container.dispose);
    await container.read(authProvider.future);

    await container.read(authProvider.notifier).login('alice', 'password');
    expect(container.read(authProvider).requireValue?.token, 'test');
    expect(storage.value, contains('"employeeId":7'));
  });
}

class MemoryStorage implements SessionStorage {
  MemoryStorage(this.value);
  String? value;
  @override
  Future<void> clear() async => value = null;
  @override
  Future<String?> read() async => value;
  @override
  Future<void> write(String newValue) async => value = newValue;
}

class FakeApi extends ApiService {
  @override
  Future<Map<String, dynamic>> login(String username, String password) async => {
    'token': 'test',
    'expiresAt': '2099-01-01T00:00:00.000Z',
    'employeeId': 7,
    'username': username,
    'role': 'Employee',
  };
}
