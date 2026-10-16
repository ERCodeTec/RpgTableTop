using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MesasEConvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mesas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mesas", x => x.id);
                    table.ForeignKey(
                        name: "fk_mesas_usuarios_criada_por_usuario_id",
                        column: x => x.criada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "convites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usos_maximos = table.Column<int>(type: "integer", nullable: true),
                    usos_atuais = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convites", x => x.id);
                    table.CheckConstraint("ck_convites_usos", "usos_atuais >= 0 AND (usos_maximos IS NULL OR usos_atuais <= usos_maximos)");
                    table.ForeignKey(
                        name: "fk_convites_mesas_mesa_id",
                        column: x => x.mesa_id,
                        principalTable: "mesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mesa_membros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    personagem_ativo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entrou_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mesa_membros", x => x.id);
                    table.ForeignKey(
                        name: "fk_mesa_membros_mesas_mesa_id",
                        column: x => x.mesa_id,
                        principalTable: "mesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mesa_membros_personagens_personagem_ativo_id",
                        column: x => x.personagem_ativo_id,
                        principalTable: "personagens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_mesa_membros_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_convites_codigo",
                table: "convites",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_convites_mesa_id",
                table: "convites",
                column: "mesa_id");

            migrationBuilder.CreateIndex(
                name: "ix_mesa_membros_mesa_id_usuario_id",
                table: "mesa_membros",
                columns: new[] { "mesa_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mesa_membros_personagem_ativo_id",
                table: "mesa_membros",
                column: "personagem_ativo_id");

            migrationBuilder.CreateIndex(
                name: "ix_mesa_membros_usuario_id",
                table: "mesa_membros",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_mesas_criada_por_usuario_id",
                table: "mesas",
                column: "criada_por_usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "convites");

            migrationBuilder.DropTable(
                name: "mesa_membros");

            migrationBuilder.DropTable(
                name: "mesas");
        }
    }
}
