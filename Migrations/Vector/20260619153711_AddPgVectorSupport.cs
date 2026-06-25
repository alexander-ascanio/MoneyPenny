using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    /// <inheritdoc />
    public partial class AddPgVectorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.Sql("""
                DELETE FROM ticket_embeddings
                WHERE "Vector" IS NULL
                   OR array_length("Vector", 1) IS NULL
                   OR array_length("Vector", 1) <> 1536;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE ticket_embeddings
                ALTER COLUMN "Vector" TYPE vector(1536)
                USING "Vector"::vector(1536);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_ticket_embeddings_Vector_hnsw"
                ON ticket_embeddings USING hnsw ("Vector" vector_cosine_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_ticket_embeddings_Vector_hnsw";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE ticket_embeddings
                ALTER COLUMN "Vector" TYPE real[]
                USING "Vector"::real[];
                """);

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
