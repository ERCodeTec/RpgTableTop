using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FotoDoPersonagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "imagem_id",
                table: "personagens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_personagens_imagem_id",
                table: "personagens",
                column: "imagem_id");

            migrationBuilder.AddForeignKey(
                name: "fk_personagens_imagens_imagem_id",
                table: "personagens",
                column: "imagem_id",
                principalTable: "imagens",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_personagens_imagens_imagem_id",
                table: "personagens");

            migrationBuilder.DropIndex(
                name: "ix_personagens_imagem_id",
                table: "personagens");

            migrationBuilder.DropColumn(
                name: "imagem_id",
                table: "personagens");
        }
    }
}
