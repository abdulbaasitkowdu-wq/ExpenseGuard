using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseGuard.Api.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseRequestReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalJson",
                table: "PurchaseRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PurchaseRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentRequiredRole",
                table: "PurchaseRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewJson",
                table: "PurchaseRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_Status_CurrentRequiredRole",
                table: "PurchaseRequests",
                columns: new[] { "Status", "CurrentRequiredRole" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_Status_CurrentRequiredRole",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalJson",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "CurrentRequiredRole",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "ReviewJson",
                table: "PurchaseRequests");
        }
    }
}
