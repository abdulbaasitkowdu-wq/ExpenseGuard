# ExpenseGuard

A full-stack corporate expense management platform with a multi-agent AI system that automates policy compliance and fraud checks, routing high-risk claims for human approval before reimbursement.

## Architecture Overview

ExpenseGuard uses a modern, distributed architecture:
- **Backend**: ASP.NET Core Web API (.NET 8), Entity Framework Core, PostgreSQL
- **Web App**: React with Vite, React Router, Recharts, Axios
- **Mobile App**: Flutter / Dart
- **AI Agentic System**: Multi-Agent orchestration including Coordinator/Planner Agent, Policy Compliance Agent, and Fraud Detection Agent
- **Auth**: JWT-based authentication with Role-Based Access Control (`FinanceAdmin`, `Manager`, `Employee`)
- **CI/CD**: GitHub Actions workflow for continuous build and test verification

---

## Repository Structure

```
ExpenseGuard/
├── .github/workflows/               # Backend, React, Flutter, and agents CI
├── backend/
│   ├── ExpenseGuard.sln             # Unified solution (.NET 8)
│   ├── ExpenseGuard.Api/            # Expense, budget, policy, and workflow API
│   └── ExpenseGuard.Api.Tests/      # API unit and acceptance tests
├── Flutter/                         # Employee mobile / Chrome app
├── React/                           # Web dashboard for approvers and finance
├── ai/expenseguard-agents/          # LangGraph coordinator, policy, and fraud agents
└── docker-compose.yml
```

---

## Getting Started

### 1. Backend (.NET 8 Web API)

**Prerequisites:** .NET 8 SDK, PostgreSQL (or InMemory for testing)

```bash
cd backend
dotnet restore ExpenseGuard.sln
dotnet build ExpenseGuard.sln
dotnet test ExpenseGuard.sln
```

To run the API:
```bash
cd backend/ExpenseGuard.Api
dotnet run --launch-profile http
```
Swagger UI available at: `http://localhost:5000/swagger`

### 2. React Web App

**Prerequisites:** Node.js 18+

```bash
cd React
npm install
npm run dev
```
Dashboard available at: `http://localhost:5173`

### 3. Flutter Mobile App

**Prerequisites:** Flutter SDK 3.x

```bash
cd Flutter
flutter pub get
flutter run
```

---

## Expense, budget, and approval workflow

- **Workflow coordination:** ExpenseGuard.Api is the source of truth for claims and purchase requests. LangGraph agents return advisory policy/fraud results only.
- **Budget tracking:** Department budget caps, reservations, utilization, and fiscal periods live in the API.
- **Human approval:** Managers, department heads, and finance review queued work in the React dashboard.
