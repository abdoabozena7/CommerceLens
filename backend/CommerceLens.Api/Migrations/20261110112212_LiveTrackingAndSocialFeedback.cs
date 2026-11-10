using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceLens.Api.Migrations
{
    /// <inheritdoc />
    public partial class LiveTrackingAndSocialFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventKey",
                table: "StoreEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SocialImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalRecords = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedComments = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateComments = table.Column<int>(type: "INTEGER", nullable: false),
                    IgnoredPosts = table.Column<int>(type: "INTEGER", nullable: false),
                    FixtureComments = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialComments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordId = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    CommentId = table.Column<string>(type: "TEXT", nullable: true),
                    SourceMode = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PostUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ParentPostUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PublishedAtRaw = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceKind = table.Column<string>(type: "TEXT", nullable: false),
                    Truncated = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialComments_SocialImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "SocialImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreEvents_EventKey",
                table: "StoreEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialComments_ImportBatchId",
                table: "SocialComments",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialComments_SourceMode_RecordId",
                table: "SocialComments",
                columns: new[] { "SourceMode", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialImportBatches_Sha256",
                table: "SocialImportBatches",
                column: "Sha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialComments");

            migrationBuilder.DropTable(
                name: "SocialImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_StoreEvents_EventKey",
                table: "StoreEvents");

            migrationBuilder.DropColumn(
                name: "EventKey",
                table: "StoreEvents");
        }
    }
}
