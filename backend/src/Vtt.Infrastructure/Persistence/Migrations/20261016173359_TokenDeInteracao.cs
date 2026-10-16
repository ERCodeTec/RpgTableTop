using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TokenDeInteracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_modelos_de_token_tipo",
                table: "modelos_de_token");

            migrationBuilder.AddColumn<Guid>(
                name: "conteudo_imagem_id",
                table: "tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                table: "tokens",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "revelado",
                table: "tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "conteudo_imagem_id",
                table: "modelos_de_token",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descricao",
                table: "modelos_de_token",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tokens_conteudo_imagem_id",
                table: "tokens",
                column: "conteudo_imagem_id");

            migrationBuilder.CreateIndex(
                name: "ix_modelos_de_token_conteudo_imagem_id",
                table: "modelos_de_token",
                column: "conteudo_imagem_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_modelos_de_token_tipo",
                table: "modelos_de_token",
                sql: "tipo IN ('Inimigo', 'Npc', 'Interacao')");

            migrationBuilder.AddForeignKey(
                name: "fk_modelos_de_token_imagens_conteudo_imagem_id",
                table: "modelos_de_token",
                column: "conteudo_imagem_id",
                principalTable: "imagens",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_tokens_imagens_conteudo_imagem_id",
                table: "tokens",
                column: "conteudo_imagem_id",
                principalTable: "imagens",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_modelos_de_token_imagens_conteudo_imagem_id",
                table: "modelos_de_token");

            migrationBuilder.DropForeignKey(
                name: "fk_tokens_imagens_conteudo_imagem_id",
                table: "tokens");

            migrationBuilder.DropIndex(
                name: "ix_tokens_conteudo_imagem_id",
                table: "tokens");

            migrationBuilder.DropIndex(
                name: "ix_modelos_de_token_conteudo_imagem_id",
                table: "modelos_de_token");

            migrationBuilder.DropCheckConstraint(
                name: "ck_modelos_de_token_tipo",
                table: "modelos_de_token");

            migrationBuilder.DropColumn(
                name: "conteudo_imagem_id",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "descricao",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "revelado",
                table: "tokens");

            migrationBuilder.DropColumn(
                name: "conteudo_imagem_id",
                table: "modelos_de_token");

            migrationBuilder.DropColumn(
                name: "descricao",
                table: "modelos_de_token");

            migrationBuilder.AddCheckConstraint(
                name: "ck_modelos_de_token_tipo",
                table: "modelos_de_token",
                sql: "tipo IN ('Inimigo', 'Npc')");
        }
    }
}
