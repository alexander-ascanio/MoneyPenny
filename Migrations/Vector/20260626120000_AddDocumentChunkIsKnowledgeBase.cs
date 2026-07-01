using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    /// <inheritdoc />
    public partial class AddDocumentChunkIsKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsKnowledgeBase",
                table: "document_chunks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsKnowledgeBase",
                table: "document_chunks");
        }
    }
}
