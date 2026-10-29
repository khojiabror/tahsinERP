using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tahsinERP.ControlPanel.Data.Migrations
{
    /// <inheritdoc />
    public partial class SalesBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "DemoRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "DemoRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "web");

            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "DemoRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "DemoRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BotChats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    StateData = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Company = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartParam = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotChats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BotGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsMember = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BotSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SupportChatId = table.Column<long>(type: "bigint", nullable: true),
                    WebhookSecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebhookSetAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BotThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DemoRequestId = table.Column<int>(type: "int", nullable: true),
                    Company = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GroupChatId = table.Column<long>(type: "bigint", nullable: true),
                    GroupMessageId = table.Column<long>(type: "bigint", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivityUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotThreads_BotChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "BotChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BotThreads_DemoRequests_DemoRequestId",
                        column: x => x.DemoRequestId,
                        principalTable: "DemoRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BotThreadMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThreadId = table.Column<int>(type: "int", nullable: false),
                    FromStaff = table.Column<bool>(type: "bit", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Media = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    GroupChatId = table.Column<long>(type: "bigint", nullable: true),
                    GroupMessageId = table.Column<long>(type: "bigint", nullable: true),
                    UserMessageId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotThreadMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotThreadMessages_BotThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "BotThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BotSettings",
                columns: new[] { "Id", "SupportChatId", "WebhookSecret", "WebhookSetAtUtc" },
                values: new object[] { 1, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_TelegramChatId",
                table: "DemoRequests",
                column: "TelegramChatId");

            migrationBuilder.CreateIndex(
                name: "IX_BotChats_LastSeenUtc",
                table: "BotChats",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BotThreadMessages_GroupChatId_GroupMessageId",
                table: "BotThreadMessages",
                columns: new[] { "GroupChatId", "GroupMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_BotThreadMessages_ThreadId",
                table: "BotThreadMessages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_BotThreads_ChatId_Status",
                table: "BotThreads",
                columns: new[] { "ChatId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BotThreads_DemoRequestId",
                table: "BotThreads",
                column: "DemoRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BotThreads_Status_Id",
                table: "BotThreads",
                columns: new[] { "Status", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotGroups");

            migrationBuilder.DropTable(
                name: "BotSettings");

            migrationBuilder.DropTable(
                name: "BotThreadMessages");

            migrationBuilder.DropTable(
                name: "BotThreads");

            migrationBuilder.DropTable(
                name: "BotChats");

            migrationBuilder.DropIndex(
                name: "IX_DemoRequests_TelegramChatId",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "DemoRequests");
        }
    }
}
