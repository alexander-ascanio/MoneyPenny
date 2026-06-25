using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    /// <inheritdoc />
    public partial class AddCommentImageTextCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comment_image_text_cache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    TicketActionId = table.Column<int>(type: "integer", nullable: false),
                    ImageSource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    VisionModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_image_text_cache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comment_image_text_cache_ImageSource",
                table: "comment_image_text_cache",
                column: "ImageSource",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comment_image_text_cache_TicketActionId",
                table: "comment_image_text_cache",
                column: "TicketActionId");

            migrationBuilder.CreateIndex(
                name: "IX_comment_image_text_cache_TicketId",
                table: "comment_image_text_cache",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_image_text_cache");
        }
    }
}
