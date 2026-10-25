using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAttemptQrCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "PaymentAttempts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "PaymentAttempts");
        }
    }
}
