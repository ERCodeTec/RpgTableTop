using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImagensEEstruturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "imagem_id",
                table: "tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "fundo_escala",
                table: "mapas",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "fundo_offset_x",
                table: "mapas",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "fundo_offset_y",
                table: "mapas",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<Guid>(
                name: "imagem_fundo_id",
                table: "mapas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "imagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enviada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_original = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    largura = table.Column<int>(type: "integer", nullable: false),
                    altura = table.Column<int>(type: "integer", nullable: false),
                    caminho_armazenado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imagens", x => x.id);
                    table.ForeignKey(
                        name: "fk_imagens_mesas_mesa_id",
                        column: x => x.mesa_id,
                        principalTable: "mesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_imagens_usuarios_enviada_por_usuario_id",
                        column: x => x.enviada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estruturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    largura = table.Column<int>(type: "integer", nullable: false),
                    profundidade = table.Column<int>(type: "integer", nullable: false),
                    altura = table.Column<double>(type: "double precision", nullable: false),
                    rotacao_graus = table.Column<int>(type: "integer", nullable: false),
                    cor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    textura_imagem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bloqueia_movimento = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    porta_aberta = table.Column<bool>(type: "boolean", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estruturas", x => x.id);
                    table.CheckConstraint("ck_estruturas_altura", "altura >= 0.1 AND altura <= 3");
                    table.CheckConstraint("ck_estruturas_area", "x >= 0 AND y >= 0 AND largura >= 1 AND profundidade >= 1");
                    table.CheckConstraint("ck_estruturas_rotacao", "rotacao_graus IN (0, 90, 180, 270)");
                    table.ForeignKey(
                        name: "fk_estruturas_imagens_textura_imagem_id",
                        column: x => x.textura_imagem_id,
                        principalTable: "imagens",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_estruturas_mapas_mapa_id",
                        column: x => x.mapa_id,
                        principalTable: "mapas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tokens_imagem_id",
                table: "tokens",
                column: "imagem_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapas_imagem_fundo_id",
                table: "mapas",
                column: "imagem_fundo_id");

            migrationBuilder.CreateIndex(
                name: "ix_estruturas_mapa_id",
                table: "estruturas",
                column: "mapa_id");

            migrationBuilder.CreateIndex(
                name: "ix_estruturas_textura_imagem_id",
                table: "estruturas",
                column: "textura_imagem_id");

            migrationBuilder.CreateIndex(
                name: "ix_imagens_enviada_por_usuario_id",
                table: "imagens",
                column: "enviada_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_imagens_mesa_id",
                table: "imagens",
                column: "mesa_id");

            migrationBuilder.AddForeignKey(
                name: "fk_mapas_imagens_imagem_fundo_id",
                table: "mapas",
                column: "imagem_fundo_id",
                principalTable: "imagens",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_tokens_imagens_imagem_id",
                table: "tokens",
                column: "imagem_id",
                principalTable: "imagens",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_mapas_imagens_imagem_fundo_id",
                table: "mapas");

            migrationBuilder.DropForeignKey(
                name: "fk_tokens_imagens_imagem_id",
                table: "tokens");

            migrationBuilder.DropTable(
                name: "estruturas");

            migrationBuilder.DropTable(
                name: "imagens");

            migrationBuilder.DropIndex(
                name: "ix_tokens_imagem_id",
                table: "tokens");

            migrationBuilder.DropIndex(
                name: "ix_mapas_imagem_fundo_id",
                table: "mapas");

            migrationBuilder.DropColumn(
                name: "imagem_id",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "fundo_escala",
                table: "mapas");

            migrationBuilder.DropColumn(
                name: "fundo_offset_x",
                table: "mapas");

            migrationBuilder.DropColumn(
                name: "fundo_offset_y",
                table: "mapas");

            migrationBuilder.DropColumn(
                name: "imagem_fundo_id",
                table: "mapas");
        }
    }
}
