using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NevoaDeGuerra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "visao",
                table: "tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "visao",
                table: "personagens",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            // Os tokens de jogador espelham a visão do personagem, que acabou de nascer com o padrão.
            migrationBuilder.Sql("""
                UPDATE tokens t SET visao = p.visao
                FROM personagens p
                WHERE t.personagem_id = p.id AND t.dono_usuario_id IS NOT NULL;
                """);

            migrationBuilder.AddColumn<string>(
                name: "nevoa",
                table: "mapas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Desligada");

            migrationBuilder.CreateTable(
                name: "memorias_de_nevoa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    celulas = table.Column<byte[]>(type: "bytea", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memorias_de_nevoa", x => x.id);
                    table.ForeignKey(
                        name: "fk_memorias_de_nevoa_mapas_mapa_id",
                        column: x => x.mapa_id,
                        principalTable: "mapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_memorias_de_nevoa_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_tokens_visao",
                table: "tokens",
                sql: "visao BETWEEN 0 AND 60");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personagens_visao",
                table: "personagens",
                sql: "visao BETWEEN 0 AND 60");

            migrationBuilder.CreateIndex(
                name: "ix_memorias_de_nevoa_mapa_id_tipo_usuario_id",
                table: "memorias_de_nevoa",
                columns: new[] { "mapa_id", "tipo", "usuario_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_memorias_de_nevoa_usuario_id",
                table: "memorias_de_nevoa",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "memorias_de_nevoa");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tokens_visao",
                table: "tokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personagens_visao",
                table: "personagens");

            migrationBuilder.DropColumn(
                name: "visao",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "visao",
                table: "personagens");

            migrationBuilder.DropColumn(
                name: "nevoa",
                table: "mapas");
        }
    }
}
