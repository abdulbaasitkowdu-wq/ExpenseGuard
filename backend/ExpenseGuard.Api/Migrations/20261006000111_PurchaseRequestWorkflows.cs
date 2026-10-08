using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseGuard.Api.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseRequestWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ExpenseClaimId",
                table: "WorkflowExecutions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseRequestId",
                table: "WorkflowExecutions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                table: "WorkflowExecutions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "claim");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_PurchaseRequestId",
                table: "WorkflowExecutions",
                column: "PurchaseRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowExecutions_PurchaseRequests_PurchaseRequestId",
                table: "WorkflowExecutions",
                column: "PurchaseRequestId",
                principalTable: "PurchaseRequests",
                principalColumn: "PurchaseRequestId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowExecutions_PurchaseRequests_PurchaseRequestId",
                table: "WorkflowExecutions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowExecutions_PurchaseRequestId",
                table: "WorkflowExecutions");

            migrationBuilder.DropColumn(
                name: "PurchaseRequestId",
                table: "WorkflowExecutions");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "WorkflowExecutions");

            migrationBuilder.AlterColumn<int>(
                name: "ExpenseClaimId",
                table: "WorkflowExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
