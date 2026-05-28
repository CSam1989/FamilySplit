using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilySplit.Infrastructure.Migrations;
/// <inheritdoc />
public partial class Phase6SettlementsRefactorToFamilies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_settlements_users_payer_user_id",
            table: "settlements");

        migrationBuilder.DropForeignKey(
            name: "FK_settlements_users_receiver_user_id",
            table: "settlements");

        migrationBuilder.RenameColumn(
            name: "receiver_user_id",
            table: "settlements",
            newName: "receiver_family_id");

        migrationBuilder.RenameColumn(
            name: "payer_user_id",
            table: "settlements",
            newName: "payer_family_id");

        migrationBuilder.RenameIndex(
            name: "IX_settlements_receiver_user_id",
            table: "settlements",
            newName: "IX_settlements_receiver_family_id");

        migrationBuilder.RenameIndex(
            name: "IX_settlements_payer_user_id",
            table: "settlements",
            newName: "IX_settlements_payer_family_id");

        migrationBuilder.AddForeignKey(
            name: "FK_settlements_families_payer_family_id",
            table: "settlements",
            column: "payer_family_id",
            principalTable: "families",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_settlements_families_receiver_family_id",
            table: "settlements",
            column: "receiver_family_id",
            principalTable: "families",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_settlements_families_payer_family_id",
            table: "settlements");

        migrationBuilder.DropForeignKey(
            name: "FK_settlements_families_receiver_family_id",
            table: "settlements");

        migrationBuilder.RenameColumn(
            name: "receiver_family_id",
            table: "settlements",
            newName: "receiver_user_id");

        migrationBuilder.RenameColumn(
            name: "payer_family_id",
            table: "settlements",
            newName: "payer_user_id");

        migrationBuilder.RenameIndex(
            name: "IX_settlements_receiver_family_id",
            table: "settlements",
            newName: "IX_settlements_receiver_user_id");

        migrationBuilder.RenameIndex(
            name: "IX_settlements_payer_family_id",
            table: "settlements",
            newName: "IX_settlements_payer_user_id");

        migrationBuilder.AddForeignKey(
            name: "FK_settlements_users_payer_user_id",
            table: "settlements",
            column: "payer_user_id",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_settlements_users_receiver_user_id",
            table: "settlements",
            column: "receiver_user_id",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }
}

