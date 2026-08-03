using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilySplit.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSettlementUniqueIndexAndActiveEmailFilter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_settlements_activity_id",
            table: "settlements");

        migrationBuilder.DropIndex(
            name: "IX_family_members_email",
            table: "family_members");

        migrationBuilder.CreateIndex(
            name: "IX_settlements_activity_id_payer_family_id_receiver_family_id",
            table: "settlements",
            columns: new[] { "activity_id", "payer_family_id", "receiver_family_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_family_members_email",
            table: "family_members",
            column: "email",
            unique: true,
            filter: "email IS NOT NULL AND is_active");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_settlements_activity_id_payer_family_id_receiver_family_id",
            table: "settlements");

        migrationBuilder.DropIndex(
            name: "IX_family_members_email",
            table: "family_members");

        migrationBuilder.CreateIndex(
            name: "IX_settlements_activity_id",
            table: "settlements",
            column: "activity_id");

        migrationBuilder.CreateIndex(
            name: "IX_family_members_email",
            table: "family_members",
            column: "email",
            unique: true,
            filter: "email IS NOT NULL");
    }
}
