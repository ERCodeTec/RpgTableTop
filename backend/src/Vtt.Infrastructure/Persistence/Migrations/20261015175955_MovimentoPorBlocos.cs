using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovimentoPorBlocos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "custo_pendente",
                table: "tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "deslocamento",
                table: "tokens",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            // Os tokens que já existem começam a rodada com o movimento inteiro (o deslocamento padrão).
            migrationBuilder.AddColumn<int>(
                name: "movimento_restante",
                table: "tokens",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "origem_pendente_x",
                table: "tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origem_pendente_y",
                table: "tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deslocamento",
                table: "personagens",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tokens_movimento",
                table: "tokens",
                sql: "deslocamento BETWEEN 1 AND 30 AND movimento_restante BETWEEN 0 AND deslocamento");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personagens_deslocamento",
                table: "personagens",
                sql: "deslocamento BETWEEN 1 AND 30");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tokens_movimento",
                table: "tokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personagens_deslocamento",
                table: "personagens");

            migrationBuilder.DropColumn(
                name: "custo_pendente",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "deslocamento",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "movimento_restante",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "origem_pendente_x",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "origem_pendente_y",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "deslocamento",
                table: "personagens");
        }
    }
}
