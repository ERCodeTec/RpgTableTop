using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Iniciativa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "jogou_na_rodada",
                table: "tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "iniciativa",
                table: "mesas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Inativa");

            migrationBuilder.AddColumn<int>(
                name: "rodada",
                table: "mesas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "vez_do_token_id",
                table: "mesas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "valores_de_iniciativa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grupo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    valor = table.Column<int>(type: "integer", nullable: false),
                    informado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_valores_de_iniciativa", x => x.id);
                    table.CheckConstraint("ck_valores_de_iniciativa_dono", "(usuario_id IS NULL) <> (grupo IS NULL)");
                    table.CheckConstraint("ck_valores_de_iniciativa_valor", "valor BETWEEN -10 AND 99");
                    table.ForeignKey(
                        name: "fk_valores_de_iniciativa_mesas_mesa_id",
                        column: x => x.mesa_id,
                        principalTable: "mesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_valores_de_iniciativa_mesa_id_grupo",
                table: "valores_de_iniciativa",
                columns: new[] { "mesa_id", "grupo" },
                unique: true,
                filter: "grupo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_valores_de_iniciativa_mesa_id_usuario_id",
                table: "valores_de_iniciativa",
                columns: new[] { "mesa_id", "usuario_id" },
                unique: true,
                filter: "usuario_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "valores_de_iniciativa");

            migrationBuilder.DropColumn(
                name: "jogou_na_rodada",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "iniciativa",
                table: "mesas");

            migrationBuilder.DropColumn(
                name: "rodada",
                table: "mesas");

            migrationBuilder.DropColumn(
                name: "vez_do_token_id",
                table: "mesas");
        }
    }
}
