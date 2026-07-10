using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260710190000_AddRagQueryLogTeamSupportActionId")]
    public partial class AddRagQueryLogTeamSupportActionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamSupportActionId",
                table: "rag_query_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamSupportActionId",
                table: "rag_query_logs");
        }
    }
}
