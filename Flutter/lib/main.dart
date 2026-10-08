import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'auth/auth_provider.dart';
import 'screens/login_screen.dart';
import 'screens/home_screen.dart';
import 'screens/my_claims_screen.dart';
import 'screens/claim_detail_screen.dart';
import 'screens/reimbursement_status_screen.dart';
import 'screens/payment_status_screen.dart';
import 'screens/history_screen.dart';
import 'screens/employee_home_screen.dart';
import 'screens/expense_claim_screen.dart';
import 'screens/purchase_request_screen.dart';
import 'features/compliance/policy_guidance_screen.dart';

void main() {
  runApp(const ProviderScope(child: ReimbursementApp()));
}

class ReimbursementApp extends ConsumerWidget {
  const ReimbursementApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authProvider);
    if (auth.isLoading) {
      return MaterialApp(theme: _buildTheme(), home: const Scaffold(
        body: Center(child: CircularProgressIndicator())));
    }
    final signedIn = auth.value != null;
    final router = GoRouter(
      initialLocation: signedIn ? '/home' : '/login',
      redirect: (context, state) {
        final atLogin = state.matchedLocation == '/login';
        if (!signedIn && !atLogin) return '/login';
        if (signedIn && atLogin) return '/home';
        return null;
      },
      routes: [
        GoRoute(path: '/login', builder: (ctx, state) => const LoginScreen()),
        GoRoute(path: '/home', builder: (ctx, state) => const HomeScreen()),
        GoRoute(path: '/claims', builder: (ctx, state) => const MyClaimsScreen()),
        GoRoute(path: '/claims/:id', builder: (ctx, state) => ClaimDetailScreen(claimId: state.pathParameters['id']!)),
        GoRoute(path: '/reimbursement/:id', builder: (ctx, state) => ReimbursementStatusScreen(reimbursementId: state.pathParameters['id']!)),
        GoRoute(path: '/payment/:id', builder: (ctx, state) => PaymentStatusScreen(reimbursementId: state.pathParameters['id']!)),
        GoRoute(path: '/history', builder: (ctx, state) => const HistoryScreen()),
        GoRoute(path: '/intake', builder: (ctx, state) => const EmployeeHomeScreen()),
        GoRoute(path: '/intake/claims/new', builder: (ctx, state) => const ExpenseClaimScreen()),
        GoRoute(path: '/intake/claims/:id', builder: (ctx, state) => ExpenseClaimScreen(claimId: int.parse(state.pathParameters['id']!))),
        GoRoute(path: '/purchase-requests/new', builder: (ctx, state) => const PurchaseRequestScreen()),
        GoRoute(path: '/policy-guidance', builder: (ctx, state) => PolicyGuidanceScreen(initialClaimId: state.uri.queryParameters['claimId'])),
      ],
    );
    return MaterialApp.router(
      title: 'ReimbursementBudget',
      debugShowCheckedModeBanner: false,
      theme: _buildTheme(),
      routerConfig: router,
    );
  }

  static ThemeData _buildTheme() {
    return ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(
        seedColor: const Color(0xFF6366F1),
        brightness: Brightness.dark,
        primary: const Color(0xFF6366F1),
        secondary: const Color(0xFF8B5CF6),
        surface: const Color(0xFF1A2236),
        onSurface: const Color(0xFFF1F5F9),
      ),
      scaffoldBackgroundColor: const Color(0xFF0A0E1A),
      cardTheme: CardThemeData(
        color: const Color(0xFF1A2236),
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFF111827),
        foregroundColor: Color(0xFFF1F5F9),
        elevation: 0,
      ),
    );
  }
}
