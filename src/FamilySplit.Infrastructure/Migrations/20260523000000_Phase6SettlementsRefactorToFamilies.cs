using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilySplit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6SettlementsRefactorToFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old User-based FK indexes and constraints.
            migrationBuilder.DropIndex(
                name: "IX_settlements_payer_user_id",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_settlements_receiver_user_id",
                table: "settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_settlements_users_payer_user_id",
                table: "settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_settlements_users_receiver_user_id",
                table: "settlements");

            // Remove old columns.
            migrationBuilder.DropColumn(
                name: "payer_user_id",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "receiver_user_id",
                table: "settlements");

            // Add Family-based columns.
            migrationBuilder.AddColumn<Guid>(
                name: "payer_family_id",
                table: "settlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "receiver_family_id",
                table: "settlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Add Family FK constraints.
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

            // Add indexes for the new FK columns.
            migrationBuilder.CreateIndex(
                name: "IX_settlements_payer_family_id",
                table: "settlements",
                column: "payer_family_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_receiver_family_id",
                table: "settlements",
                column: "receiver_family_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_settlements_payer_family_id",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "IX_settlements_receiver_family_id",
                table: "settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_settlements_families_payer_family_id",
                table: "settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_settlements_families_receiver_family_id",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "payer_family_id",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "receiver_family_id",
                table: "settlements");

            migrationBuilder.AddColumn<Guid>(
                name: "payer_user_id",
                table: "settlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "receiver_user_id",
                table: "settlements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.CreateIndex(
                name: "IX_settlements_payer_user_id",
                table: "settlements",
                column: "payer_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_receiver_user_id",
                table: "settlements",
                column: "receiver_user_id");
        }
    }
}
