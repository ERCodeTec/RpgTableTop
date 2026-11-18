using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TamanhoDasCriaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lado_gargantuesco",
                table: "tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tamanho",
                table: "tokens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medio");

            migrationBuilder.AddColumn<int>(
                name: "lado_gargantuesco",
                table: "personagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tamanho",
                table: "personagens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medio");

            migrationBuilder.AddColumn<int>(
                name: "lado_gargantuesco",
                table: "modelos_de_token",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tamanho",
                table: "modelos_de_token",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medio");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tokens_lado_gargantuesco",
                table: "tokens",
                sql: "lado_gargantuesco IS NULL OR lado_gargantuesco BETWEEN 4 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personagens_lado_gargantuesco",
                table: "personagens",
                sql: "lado_gargantuesco IS NULL OR lado_gargantuesco BETWEEN 4 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_modelos_de_token_lado_gargantuesco",
                table: "modelos_de_token",
                sql: "lado_gargantuesco IS NULL OR lado_gargantuesco BETWEEN 4 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tokens_lado_gargantuesco",
                table: "tokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personagens_lado_gargantuesco",
                table: "personagens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_modelos_de_token_lado_gargantuesco",
                table: "modelos_de_token");

            migrationBuilder.DropColumn(
                name: "lado_gargantuesco",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "tamanho",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "lado_gargantuesco",
                table: "personagens");

            migrationBuilder.DropColumn(
                name: "tamanho",
                table: "personagens");

            migrationBuilder.DropColumn(
                name: "lado_gargantuesco",
                table: "modelos_de_token");

            migrationBuilder.DropColumn(
                name: "tamanho",
                table: "modelos_de_token");
        }
    }
}
