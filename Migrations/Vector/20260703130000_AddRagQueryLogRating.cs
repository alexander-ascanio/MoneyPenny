using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    /// <inheritdoc />
    public partial class AddRagQueryLogRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "Rating",
                table: "rag_query_logs",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "rag_query_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RatedByUserId",
                table: "rag_query_logs",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseType",
                table: "rag_query_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_rag_query_logs_TicketId_ResponseType",
                table: "rag_query_logs",
                columns: new[] { "TicketId", "ResponseType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rag_query_logs_TicketId_ResponseType",
                table: "rag_query_logs");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "rag_query_logs");

            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "rag_query_logs");

            migrationBuilder.DropColumn(
                name: "RatedByUserId",
                table: "rag_query_logs");

            migrationBuilder.DropColumn(
                name: "ResponseType",
                table: "rag_query_logs");
        }
    }
}
