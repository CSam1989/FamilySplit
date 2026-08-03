using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilySplit.Infrastructure.Migrations;
/// <inheritdoc />
public partial class AddPushSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "push_subscriptions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                p256dh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                auth = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_push_subscriptions", x => x.id);
                table.ForeignKey(
                    name: "FK_push_subscriptions_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_push_subscriptions_endpoint",
            table: "push_subscriptions",
            column: "endpoint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_push_subscriptions_user_id",
            table: "push_subscriptions",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "push_subscriptions");
    }
}

