import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../auth/auth_provider.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});
  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authProvider);
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Form(
              key: _formKey,
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  const Icon(Icons.shield_outlined, size: 64),
                  const SizedBox(height: 16),
                  const Text('ExpenseGuard', textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 24),
                  if (auth.hasError)
                    Semantics(liveRegion: true, child: Text(
                      auth.error.toString(), style: const TextStyle(color: Colors.red))),
                  TextFormField(
                    controller: _username,
                    autofillHints: const [AutofillHints.username],
                    decoration: const InputDecoration(labelText: 'Username'),
                    validator: (value) => value == null || value.trim().isEmpty ? 'Username is required' : null,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _password,
                    obscureText: true,
                    autofillHints: const [AutofillHints.password],
                    decoration: const InputDecoration(labelText: 'Password'),
                    validator: (value) => value == null || value.isEmpty ? 'Password is required' : null,
                  ),
                  const SizedBox(height: 20),
                  FilledButton(
                    onPressed: auth.isLoading ? null : () {
                      if (_formKey.currentState!.validate()) {
                        ref.read(authProvider.notifier).login(_username.text, _password.text);
                      }
                    },
                    child: Text(auth.isLoading ? 'Signing in…' : 'Sign in'),
                  ),
                ]),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
