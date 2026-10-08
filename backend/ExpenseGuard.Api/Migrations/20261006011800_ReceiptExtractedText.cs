using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ExpenseGuard.Api.Data;

#nullable disable

namespace ExpenseGuard.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261006011800_ReceiptExtractedText")]
public partial class ReceiptExtractedText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExtractedText",
            table: "Receipts",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExtractedText",
            table: "Receipts");
    }
}
