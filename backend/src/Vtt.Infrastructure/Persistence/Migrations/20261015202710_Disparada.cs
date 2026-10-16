using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Disparada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tokens_movimento",
                table: "tokens");

            migrationBuilder.AddColumn<bool>(
                name: "disparada",
                table: "tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tokens_movimento",
                table: "tokens",
                sql: "deslocamento BETWEEN 1 AND 30 AND movimento_restante BETWEEN 0 AND CASE WHEN disparada THEN deslocamento * 2 ELSE deslocamento END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tokens_movimento",
                table: "tokens");

            // Sem a Disparada, o movimento que passou do deslocamento não cabe na regra antiga.
            migrationBuilder.Sql("UPDATE tokens SET movimento_restante = LEAST(movimento_restante, deslocamento)");

            migrationBuilder.DropColumn(
                name: "disparada",
                table: "tokens");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tokens_movimento",
                table: "tokens",
                sql: "deslocamento BETWEEN 1 AND 30 AND movimento_restante BETWEEN 0 AND deslocamento");
        }
    }
}
