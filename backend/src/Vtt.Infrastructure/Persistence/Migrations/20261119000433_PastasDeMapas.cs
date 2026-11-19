using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PastasDeMapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "pasta_id",
                table: "mapas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pastas_de_mapas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    cor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, defaultValue: "#E2B23C"),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pastas_de_mapas", x => x.id);
                    table.ForeignKey(
                        name: "fk_pastas_de_mapas_mesas_mesa_id",
                        column: x => x.mesa_id,
                        principalTable: "mesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mapas_pasta_id",
                table: "mapas",
                column: "pasta_id");

            migrationBuilder.CreateIndex(
                name: "ix_pastas_de_mapas_mesa_id",
                table: "pastas_de_mapas",
                column: "mesa_id");

            migrationBuilder.AddForeignKey(
                name: "fk_mapas_pastas_de_mapas_pasta_id",
                table: "mapas",
                column: "pasta_id",
                principalTable: "pastas_de_mapas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Nome único por mesa sem diferenciar maiúsculas; o EF não modela índice sobre expressão.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_pastas_de_mapas_mesa_id_nome ON pastas_de_mapas (mesa_id, lower(nome));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_pastas_de_mapas_mesa_id_nome;");

            migrationBuilder.DropForeignKey(
                name: "fk_mapas_pastas_de_mapas_pasta_id",
                table: "mapas");

            migrationBuilder.DropTable(
                name: "pastas_de_mapas");

            migrationBuilder.DropIndex(
                name: "ix_mapas_pasta_id",
                table: "mapas");

            migrationBuilder.DropColumn(
                name: "pasta_id",
                table: "mapas");
        }
    }
}
