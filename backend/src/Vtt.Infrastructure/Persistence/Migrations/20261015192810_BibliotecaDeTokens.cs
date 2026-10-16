using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BibliotecaDeTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "mesa_id",
                table: "imagens",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "origem_imagem_id",
                table: "imagens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                table: "imagens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categorias_de_modelo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias_de_modelo", x => x.id);
                    table.ForeignKey(
                        name: "fk_categorias_de_modelo_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modelos_de_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    hp_maximo = table.Column<int>(type: "integer", nullable: false),
                    icone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    imagem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cor_borda = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    deslocamento = table.Column<int>(type: "integer", nullable: false),
                    visao = table.Column<int>(type: "integer", nullable: false),
                    exibicao_hp = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos_de_token", x => x.id);
                    table.CheckConstraint("ck_modelos_de_token_deslocamento", "deslocamento BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_modelos_de_token_hp_maximo", "hp_maximo BETWEEN 1 AND 999");
                    table.CheckConstraint("ck_modelos_de_token_tipo", "tipo IN ('Inimigo', 'Npc')");
                    table.CheckConstraint("ck_modelos_de_token_visao", "visao BETWEEN 0 AND 60");
                    table.ForeignKey(
                        name: "fk_modelos_de_token_categorias_de_modelo_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categorias_de_modelo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_modelos_de_token_imagens_imagem_id",
                        column: x => x.imagem_id,
                        principalTable: "imagens",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_modelos_de_token_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_imagens_mesa_id_origem_imagem_id",
                table: "imagens",
                columns: new[] { "mesa_id", "origem_imagem_id" },
                unique: true,
                filter: "origem_imagem_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_imagens_usuario_id",
                table: "imagens",
                column: "usuario_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_imagens_escopo",
                table: "imagens",
                sql: "(mesa_id IS NULL) <> (usuario_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_categorias_de_modelo_usuario_id",
                table: "categorias_de_modelo",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_modelos_de_token_categoria_id",
                table: "modelos_de_token",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_modelos_de_token_imagem_id",
                table: "modelos_de_token",
                column: "imagem_id");

            migrationBuilder.CreateIndex(
                name: "ix_modelos_de_token_usuario_id",
                table: "modelos_de_token",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_imagens_usuarios_usuario_id",
                table: "imagens",
                column: "usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // Nome único por usuário sem diferenciar maiúsculas; o EF não modela índice sobre expressão.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_categorias_de_modelo_usuario_id_nome ON categorias_de_modelo (usuario_id, lower(nome));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_categorias_de_modelo_usuario_id_nome;");

            migrationBuilder.DropForeignKey(
                name: "fk_imagens_usuarios_usuario_id",
                table: "imagens");

            migrationBuilder.DropTable(
                name: "modelos_de_token");

            migrationBuilder.DropTable(
                name: "categorias_de_modelo");

            migrationBuilder.DropIndex(
                name: "ix_imagens_mesa_id_origem_imagem_id",
                table: "imagens");

            migrationBuilder.DropIndex(
                name: "ix_imagens_usuario_id",
                table: "imagens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_imagens_escopo",
                table: "imagens");

            migrationBuilder.DropColumn(
                name: "origem_imagem_id",
                table: "imagens");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                table: "imagens");

            // As imagens pessoais não têm mesa; sem esta migration, não cabem na tabela (os arquivos ficam no disco).
            migrationBuilder.Sql("DELETE FROM imagens WHERE mesa_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "mesa_id",
                table: "imagens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
