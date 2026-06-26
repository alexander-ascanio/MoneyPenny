using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    /// <inheritdoc />
    public partial class AddDocumentChunkSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "document_chunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketActionId",
                table: "document_chunks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_Source",
                table: "document_chunks",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_TicketId_Source",
                table: "document_chunks",
                columns: new[] { "TicketId", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_document_chunks_Source",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_TicketId_Source",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "TicketActionId",
                table: "document_chunks");
        }
    }
}
