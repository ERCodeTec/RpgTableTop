using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapasETokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mapas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    largura = table.Column<int>(type: "integer", nullable: false),
                    altura = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cor_fundo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapas", x => x.id);
                    table.CheckConstraint("ck_mapas_altura", "altura BETWEEN 5 AND 100");
                    table.CheckConstraint("ck_mapas_largura", "largura BETWEEN 5 AND 100");
                    table.ForeignKey(
                        name: "fk_mapas_mesas_mesa_id",
                        column: x => x.mesa_id,
                        principalTable: "mesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    hp_atual = table.Column<int>(type: "integer", nullable: false),
                    hp_maximo = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    cor_borda = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    icone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    personagem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dono_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    visivel = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokens", x => x.id);
                    table.CheckConstraint("ck_tokens_hp", "hp_maximo BETWEEN 1 AND 999 AND hp_atual BETWEEN 0 AND hp_maximo");
                    table.CheckConstraint("ck_tokens_posicao", "x >= 0 AND y >= 0");
                    table.ForeignKey(
                        name: "fk_tokens_mapas_mapa_id",
                        column: x => x.mapa_id,
                        principalTable: "mapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tokens_personagens_personagem_id",
                        column: x => x.personagem_id,
                        principalTable: "personagens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tokens_usuarios_dono_usuario_id",
                        column: x => x.dono_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mapas_mesa_id",
                table: "mapas",
                column: "mesa_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapas_mesa_id_ativo",
                table: "mapas",
                column: "mesa_id",
                unique: true,
                filter: "ativo");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_dono_usuario_id",
                table: "tokens",
                column: "dono_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_mapa_id_dono_usuario_id",
                table: "tokens",
                columns: new[] { "mapa_id", "dono_usuario_id" },
                unique: true,
                filter: "dono_usuario_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tokens_mapa_id_x_y",
                table: "tokens",
                columns: new[] { "mapa_id", "x", "y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_personagem_id",
                table: "tokens",
                column: "personagem_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tokens");

            migrationBuilder.DropTable(
                name: "mapas");
        }
    }
}
