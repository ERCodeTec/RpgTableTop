using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HpEVisibilidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tokens_mapa_id_x_y",
                table: "tokens");

            migrationBuilder.AddColumn<string>(
                name: "exibicao_hp",
                table: "tokens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Barra");

            // Padrão por tipo: personagens mostram o HP completo ao grupo; inimigos e NPCs, só a barra.
            migrationBuilder.Sql("UPDATE tokens SET exibicao_hp = 'Completo' WHERE tipo = 'Personagem';");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_mapa_id_x_y",
                table: "tokens",
                columns: new[] { "mapa_id", "x", "y" },
                unique: true,
                filter: "visivel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tokens_mapa_id_x_y",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "exibicao_hp",
                table: "tokens");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_mapa_id_x_y",
                table: "tokens",
                columns: new[] { "mapa_id", "x", "y" },
                unique: true);
        }
    }
}
