using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExpenseGuard.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidatedMergedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FraudFlags_ExpenseClaims_ExpenseClaimId",
                table: "FraudFlags");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_DepartmentId_Period",
                table: "Budgets");

            migrationBuilder.RenameColumn(
                name: "Period",
                table: "Budgets",
                newName: "Name");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Budgets",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageRoles",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanProcessPayments",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PermissionsJson",
                table: "Roles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Reimbursements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Reimbursements",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Reimbursements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Reimbursements",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "Reimbursements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProvider",
                table: "Reimbursements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Reimbursements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "Reimbursements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Reimbursements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxAmount",
                table: "Policies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Policies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Policies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "Policies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                table: "Policies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinAmount",
                table: "Policies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyCode",
                table: "Policies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Policies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Policies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceJson",
                table: "FraudFlags",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "FraudEvaluationId",
                table: "FraudFlags",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "FraudFlags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "FraudFlags",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedBy",
                table: "FraudFlags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleCode",
                table: "FraudFlags",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "FraudFlags",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "FraudFlags",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FraudFlags",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ExpenseClaims",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ExpenseClaims",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Flow",
                table: "ExpenseClaims",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseRequestId",
                table: "ExpenseClaims",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "ExpenseClaims",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ExpenseClaims",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "DesignationId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Employees",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Employees",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUsername",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentName",
                table: "Departments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Departments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Departments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Departments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Budgets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Budgets",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Budgets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "Budgets",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "Budgets",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedAmount",
                table: "Budgets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Budgets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Budgets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE "Employees"
                SET "FullName" = CASE WHEN "FullName" = '' THEN "Username" ELSE "FullName" END,
                    "NormalizedUsername" = CASE WHEN "NormalizedUsername" = '' THEN UPPER("Username") ELSE "NormalizedUsername" END,
                    "Email" = CASE WHEN "Email" = '' THEN
                        'employee-' || "EmployeeId"::text || '@migration.invalid' ELSE "Email" END,
                    "IsActive" = TRUE;

                UPDATE "ExpenseClaims"
                SET "Status" = CASE LOWER("Status")
                    WHEN 'draft' THEN 'Draft'
                    WHEN 'submitted' THEN 'Submitted'
                    WHEN 'underreview' THEN 'UnderReview'
                    WHEN 'under_review' THEN 'UnderReview'
                    WHEN 'approved' THEN 'Approved'
                    WHEN 'rejected' THEN 'Rejected'
                    WHEN 'needscorrection' THEN 'NeedsCorrection'
                    WHEN 'needs_correction' THEN 'NeedsCorrection'
                    WHEN 'cancelled' THEN 'Cancelled'
                    ELSE "Status"
                END,
                "Flow" = CASE WHEN "Flow" = '' THEN 'OutOfPocket' ELSE "Flow" END,
                "Currency" = CASE WHEN "Currency" = '' THEN 'USD' ELSE UPPER("Currency") END;

                UPDATE "Reimbursements"
                SET "IdempotencyKey" = CASE WHEN "IdempotencyKey" = '' THEN 'legacy:' || "ReimbursementId" ELSE "IdempotencyKey" END,
                    "Currency" = CASE WHEN "Currency" = '' THEN 'LKR' ELSE "Currency" END,
                    "RequestedAt" = CASE WHEN "RequestedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN "CreatedAt" ELSE "RequestedAt" END;

                UPDATE "Departments"
                SET "Code" = CASE WHEN "Code" = '' THEN 'DEPT-' || "DepartmentId" ELSE "Code" END,
                    "IsActive" = TRUE,
                    "CreatedAt" = CASE WHEN "CreatedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN CURRENT_TIMESTAMP ELSE "CreatedAt" END,
                    "UpdatedAt" = CASE WHEN "UpdatedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN CURRENT_TIMESTAMP ELSE "UpdatedAt" END,
                    "Version" = CASE WHEN "Version" = '00000000-0000-0000-0000-000000000000' THEN md5(random()::text || clock_timestamp()::text)::uuid ELSE "Version" END;

                UPDATE "Budgets"
                SET "Currency" = CASE WHEN btrim("Currency") = '' THEN 'LKR' ELSE "Currency" END,
                    "Name" = CASE WHEN "Name" = '' THEN 'Budget ' || "BudgetId" ELSE "Name" END,
                    "IsActive" = TRUE,
                    "PeriodStart" = CASE WHEN "PeriodStart" = DATE '0001-01-01' THEN DATE '2000-01-01' + "BudgetId" ELSE "PeriodStart" END,
                    "PeriodEnd" = CASE WHEN "PeriodEnd" = DATE '0001-01-01' THEN DATE '2000-01-01' + "BudgetId" ELSE "PeriodEnd" END,
                    "CreatedAt" = CASE WHEN "CreatedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN CURRENT_TIMESTAMP ELSE "CreatedAt" END,
                    "UpdatedAt" = CASE WHEN "UpdatedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN CURRENT_TIMESTAMP ELSE "UpdatedAt" END,
                    "Version" = CASE WHEN "Version" = '00000000-0000-0000-0000-000000000000' THEN md5(random()::text || clock_timestamp()::text)::uuid ELSE "Version" END;

                UPDATE "Policies"
                SET "PolicyCode" = CASE WHEN "PolicyCode" = '' THEN 'LEGACY-' || "PolicyId" ELSE "PolicyCode" END,
                    "Version" = CASE WHEN "Version" = 0 THEN 1 ELSE "Version" END,
                    "Currency" = CASE WHEN "Currency" = '' THEN 'USD' ELSE "Currency" END,
                    "EffectiveFrom" = CASE WHEN "EffectiveFrom" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN TIMESTAMPTZ '1970-01-01 00:00:00+00' ELSE "EffectiveFrom" END,
                    "CreatedAt" = CASE WHEN "CreatedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00' THEN NOW() ELSE "CreatedAt" END,
                    "IsActive" = TRUE;

                UPDATE "FraudFlags"
                SET "EvidenceJson" = CASE WHEN "EvidenceJson" IS NULL OR "EvidenceJson"::text IN ('', '""', 'null') THEN '{}'::jsonb ELSE "EvidenceJson" END,
                    "RuleCode" = CASE WHEN "RuleCode" = '' THEN 'LEGACY_FLAG' ELSE "RuleCode" END,
                    "Severity" = CASE WHEN "Severity" = '' THEN 'medium' ELSE "Severity" END,
                    "Source" = CASE WHEN "Source" = '' THEN 'legacy' ELSE "Source" END,
                    "Status" = CASE WHEN "Status" = '' THEN 'open' ELSE "Status" END;
                """);

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowTemplates",
                columns: table => new
                {
                    ApprovalWorkflowTemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowTemplates", x => x.ApprovalWorkflowTemplateId);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowTemplates_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId");
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BudgetAlerts",
                columns: table => new
                {
                    BudgetAlertId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetId = table.Column<int>(type: "integer", nullable: false),
                    ThresholdPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    UtilizationPercent = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetAlerts", x => x.BudgetAlertId);
                    table.ForeignKey(
                        name: "FK_BudgetAlerts_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetTransactions",
                columns: table => new
                {
                    BudgetTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReservedBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SpentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetTransactions", x => x.BudgetTransactionId);
                    table.CheckConstraint("CK_BudgetTransactions_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_BudgetTransactions_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimStatusHistories",
                columns: table => new
                {
                    ClaimStatusHistoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpenseClaimId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusHistories", x => x.ClaimStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Designations",
                columns: table => new
                {
                    DesignationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Designations", x => x.DesignationId);
                });

            migrationBuilder.CreateTable(
                name: "FraudEvaluations",
                columns: table => new
                {
                    FraudEvaluationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpenseClaimId = table.Column<int>(type: "integer", nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedInvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClaimAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceiptAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RiskScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraudEvaluations", x => x.FraudEvaluationId);
                    table.ForeignKey(
                        name: "FK_FraudEvaluations_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReimbursementId = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.PaymentTransactionId);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Reimbursements_ReimbursementId",
                        column: x => x.ReimbursementId,
                        principalTable: "Reimbursements",
                        principalColumn: "ReimbursementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyDesignations",
                columns: table => new
                {
                    PolicyDesignationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyId = table.Column<int>(type: "integer", nullable: false),
                    Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyDesignations", x => x.PolicyDesignationId);
                    table.ForeignKey(
                        name: "FK_PolicyDesignations_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "PolicyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyEvaluations",
                columns: table => new
                {
                    PolicyEvaluationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpenseClaimId = table.Column<int>(type: "integer", nullable: false),
                    PolicyId = table.Column<int>(type: "integer", nullable: true),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyEvaluations", x => x.PolicyEvaluationId);
                    table.ForeignKey(
                        name: "FK_PolicyEvaluations_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PolicyEvaluations_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "PolicyId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequests",
                columns: table => new
                {
                    PurchaseRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Vendor = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequests", x => x.PurchaseRequestId);
                    table.ForeignKey(
                        name: "FK_PurchaseRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    ReceiptId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpenseClaimId = table.Column<int>(type: "integer", nullable: false),
                    StorageUrl = table.Column<string>(type: "text", nullable: false),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExtractedVendor = table.Column<string>(type: "text", nullable: true),
                    ExtractedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExtractedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtractedCurrency = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    RequiresManualReview = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectedByEmployeeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.ReceiptId);
                    table.ForeignKey(
                        name: "FK_Receipts_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowExecutions",
                columns: table => new
                {
                    WorkflowExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseClaimId = table.Column<int>(type: "integer", nullable: false),
                    Objective = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowExecutions", x => x.WorkflowExecutionId);
                    table.ForeignKey(
                        name: "FK_WorkflowExecutions_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalTable: "ExpenseClaims",
                        principalColumn: "ExpenseClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalProcesses",
                columns: table => new
                {
                    ApprovalProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReimbursementId = table.Column<int>(type: "integer", nullable: false),
                    ApprovalWorkflowTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentSequence = table.Column<int>(type: "integer", nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalProcesses", x => x.ApprovalProcessId);
                    table.ForeignKey(
                        name: "FK_ApprovalProcesses_ApprovalWorkflowTemplates_ApprovalWorkflo~",
                        column: x => x.ApprovalWorkflowTemplateId,
                        principalTable: "ApprovalWorkflowTemplates",
                        principalColumn: "ApprovalWorkflowTemplateId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalProcesses_Reimbursements_ReimbursementId",
                        column: x => x.ReimbursementId,
                        principalTable: "Reimbursements",
                        principalColumn: "ReimbursementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalStageDefinitions",
                columns: table => new
                {
                    ApprovalStageDefinitionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalWorkflowTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RequiredRole = table.Column<string>(type: "text", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalStageDefinitions", x => x.ApprovalStageDefinitionId);
                    table.ForeignKey(
                        name: "FK_ApprovalStageDefinitions_ApprovalWorkflowTemplates_Approval~",
                        column: x => x.ApprovalWorkflowTemplateId,
                        principalTable: "ApprovalWorkflowTemplates",
                        principalColumn: "ApprovalWorkflowTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyViolations",
                columns: table => new
                {
                    PolicyViolationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyEvaluationId = table.Column<int>(type: "integer", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyViolations", x => x.PolicyViolationId);
                    table.ForeignKey(
                        name: "FK_PolicyViolations_PolicyEvaluations_PolicyEvaluationId",
                        column: x => x.PolicyEvaluationId,
                        principalTable: "PolicyEvaluations",
                        principalColumn: "PolicyEvaluationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                columns: table => new
                {
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    OutputSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSteps", x => x.WorkflowStepId);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_WorkflowExecutions_WorkflowExecutionId",
                        column: x => x.WorkflowExecutionId,
                        principalTable: "WorkflowExecutions",
                        principalColumn: "WorkflowExecutionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    ApprovalStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RequiredRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DecidedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    StageSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.ApprovalStepId);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ApprovalProcesses_ApprovalProcessId",
                        column: x => x.ApprovalProcessId,
                        principalTable: "ApprovalProcesses",
                        principalColumn: "ApprovalProcessId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_Employees_DecidedByEmployeeId",
                        column: x => x.DecidedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolExecutions",
                columns: table => new
                {
                    ToolExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolExecutions", x => x.ToolExecutionId);
                    table.ForeignKey(
                        name: "FK_ToolExecutions_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "WorkflowStepId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationResults",
                columns: table => new
                {
                    ValidationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Validator = table.Column<string>(type: "text", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationResults", x => x.ValidationResultId);
                    table.ForeignKey(
                        name: "FK_ValidationResults_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "WorkflowStepId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleId", "ApprovalLimit", "CanApprove", "CanManageRoles", "CanProcessPayments", "PermissionsJson", "RoleName" },
                values: new object[,]
                {
                    { 1, 0m, false, false, false, "[\"reimbursement:self\"]", "Employee" },
                    { 2, 100000m, true, false, false, "[\"reimbursement:approve\"]", "Manager" },
                    { 3, 1000000m, true, false, false, "[\"reimbursement:approve\"]", "DepartmentHead" },
                    { 4, 0m, false, false, true, "[\"payment:process\"]", "Finance" },
                    { 5, 9999999999999999.99m, true, true, true, "[\"*\"]", "Admin" }
                });

            migrationBuilder.Sql("""
                SELECT setval(
                    pg_get_serial_sequence('"Roles"', 'RoleId'),
                    (SELECT COALESCE(MAX("RoleId"), 1) FROM "Roles"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_RoleName",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reimbursements_IdempotencyKey",
                table: "Reimbursements",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_IsActive_Category_Currency_DepartmentId_EffectiveF~",
                table: "Policies",
                columns: new[] { "IsActive", "Category", "Currency", "DepartmentId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PolicyCode_Version",
                table: "Policies",
                columns: new[] { "PolicyCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FraudFlags_FraudEvaluationId",
                table: "FraudFlags",
                column: "FraudEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_FraudFlags_Status_Severity_CreatedAt",
                table: "FraudFlags",
                columns: new[] { "Status", "Severity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_PurchaseRequestId",
                table: "ExpenseClaims",
                column: "PurchaseRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DesignationId",
                table: "Employees",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_NormalizedUsername",
                table: "Employees",
                column: "NormalizedUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_DepartmentId_PeriodStart_PeriodEnd_Currency",
                table: "Budgets",
                columns: new[] { "DepartmentId", "PeriodStart", "PeriodEnd", "Currency" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Budgets_Balances",
                table: "Budgets",
                sql: "\"AllocatedAmount\" >= 0 AND \"ReservedAmount\" >= 0 AND \"SpentAmount\" >= 0 AND \"ReservedAmount\" + \"SpentAmount\" <= \"AllocatedAmount\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Budgets_Dates",
                table: "Budgets",
                sql: "\"PeriodEnd\" >= \"PeriodStart\"");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalProcesses_ApprovalWorkflowTemplateId",
                table: "ApprovalProcesses",
                column: "ApprovalWorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalProcesses_ReimbursementId",
                table: "ApprovalProcesses",
                column: "ReimbursementId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStageDefinitions_ApprovalWorkflowTemplateId_Sequence",
                table: "ApprovalStageDefinitions",
                columns: new[] { "ApprovalWorkflowTemplateId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApprovalProcessId_Sequence",
                table: "ApprovalSteps",
                columns: new[] { "ApprovalProcessId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_DecidedByEmployeeId",
                table: "ApprovalSteps",
                column: "DecidedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowTemplates_DepartmentId",
                table: "ApprovalWorkflowTemplates",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EmployeeId",
                table: "AuditLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAlerts_BudgetId_ThresholdPercent_Status",
                table: "BudgetAlerts",
                columns: new[] { "BudgetId", "ThresholdPercent", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransactions_BudgetId_CreatedAt",
                table: "BudgetTransactions",
                columns: new[] { "BudgetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTransactions_BudgetId_IdempotencyKey",
                table: "BudgetTransactions",
                columns: new[] { "BudgetId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ExpenseClaimId",
                table: "ClaimStatusHistories",
                column: "ExpenseClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Designations_Name",
                table: "Designations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FraudEvaluations_ExpenseClaimId_InputFingerprint",
                table: "FraudEvaluations",
                columns: new[] { "ExpenseClaimId", "InputFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FraudEvaluations_NormalizedInvoiceNumber",
                table: "FraudEvaluations",
                column: "NormalizedInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_IdempotencyKey",
                table: "PaymentTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ReimbursementId",
                table: "PaymentTransactions",
                column: "ReimbursementId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyDesignations_PolicyId_Designation",
                table: "PolicyDesignations",
                columns: new[] { "PolicyId", "Designation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyEvaluations_ExpenseClaimId_InputFingerprint",
                table: "PolicyEvaluations",
                columns: new[] { "ExpenseClaimId", "InputFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyEvaluations_PolicyId",
                table: "PolicyEvaluations",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyViolations_PolicyEvaluationId",
                table: "PolicyViolations",
                column: "PolicyEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_EmployeeId",
                table: "PurchaseRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ExpenseClaimId_Sha256",
                table: "Receipts",
                columns: new[] { "ExpenseClaimId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolExecutions_WorkflowStepId",
                table: "ToolExecutions",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationResults_WorkflowStepId",
                table: "ValidationResults",
                column: "WorkflowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_ExpenseClaimId",
                table: "WorkflowExecutions",
                column: "ExpenseClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowExecutionId",
                table: "WorkflowSteps",
                column: "WorkflowExecutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Designations_DesignationId",
                table: "Employees",
                column: "DesignationId",
                principalTable: "Designations",
                principalColumn: "DesignationId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseClaims_PurchaseRequests_PurchaseRequestId",
                table: "ExpenseClaims",
                column: "PurchaseRequestId",
                principalTable: "PurchaseRequests",
                principalColumn: "PurchaseRequestId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FraudFlags_ExpenseClaims_ExpenseClaimId",
                table: "FraudFlags",
                column: "ExpenseClaimId",
                principalTable: "ExpenseClaims",
                principalColumn: "ExpenseClaimId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FraudFlags_FraudEvaluations_FraudEvaluationId",
                table: "FraudFlags",
                column: "FraudEvaluationId",
                principalTable: "FraudEvaluations",
                principalColumn: "FraudEvaluationId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Designations_DesignationId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseClaims_PurchaseRequests_PurchaseRequestId",
                table: "ExpenseClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_FraudFlags_ExpenseClaims_ExpenseClaimId",
                table: "FraudFlags");

            migrationBuilder.DropForeignKey(
                name: "FK_FraudFlags_FraudEvaluations_FraudEvaluationId",
                table: "FraudFlags");

            migrationBuilder.DropTable(
                name: "ApprovalStageDefinitions");

            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BudgetAlerts");

            migrationBuilder.DropTable(
                name: "BudgetTransactions");

            migrationBuilder.DropTable(
                name: "ClaimStatusHistories");

            migrationBuilder.DropTable(
                name: "Designations");

            migrationBuilder.DropTable(
                name: "FraudEvaluations");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PolicyDesignations");

            migrationBuilder.DropTable(
                name: "PolicyViolations");

            migrationBuilder.DropTable(
                name: "PurchaseRequests");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "ToolExecutions");

            migrationBuilder.DropTable(
                name: "ValidationResults");

            migrationBuilder.DropTable(
                name: "ApprovalProcesses");

            migrationBuilder.DropTable(
                name: "PolicyEvaluations");

            migrationBuilder.DropTable(
                name: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowTemplates");

            migrationBuilder.DropTable(
                name: "WorkflowExecutions");

            migrationBuilder.DropIndex(
                name: "IX_Roles_RoleName",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Reimbursements_IdempotencyKey",
                table: "Reimbursements");

            migrationBuilder.DropIndex(
                name: "IX_Policies_IsActive_Category_Currency_DepartmentId_EffectiveF~",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_Policies_PolicyCode_Version",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_FraudFlags_FraudEvaluationId",
                table: "FraudFlags");

            migrationBuilder.DropIndex(
                name: "IX_FraudFlags_Status_Severity_CreatedAt",
                table: "FraudFlags");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseClaims_PurchaseRequestId",
                table: "ExpenseClaims");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DesignationId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_Email",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_NormalizedUsername",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Code",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_DepartmentId_PeriodStart_PeriodEnd_Currency",
                table: "Budgets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Budgets_Balances",
                table: "Budgets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Budgets_Dates",
                table: "Budgets");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "CanManageRoles",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CanProcessPayments",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PermissionsJson",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "PaymentProvider",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Reimbursements");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "MinAmount",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "PolicyCode",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "EvidenceJson",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "FraudEvaluationId",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "ResolvedBy",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "RuleCode",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FraudFlags");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "Flow",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "PurchaseRequestId",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ExpenseClaims");

            migrationBuilder.DropColumn(
                name: "DesignationId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "NormalizedUsername",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "ReservedAmount",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Budgets");

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxAmount",
                table: "Policies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentName",
                table: "Departments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Budgets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Budgets",
                newName: "Period");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_DepartmentId_Period",
                table: "Budgets",
                columns: new[] { "DepartmentId", "Period" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FraudFlags_ExpenseClaims_ExpenseClaimId",
                table: "FraudFlags",
                column: "ExpenseClaimId",
                principalTable: "ExpenseClaims",
                principalColumn: "ExpenseClaimId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
