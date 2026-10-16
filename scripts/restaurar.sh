#!/usr/bin/env bash
# Restaura um backup feito por scripts/backup.sh.
#
#   scripts/restaurar.sh <vtt-banco-....dump> <vtt-uploads-....tar.gz> [--sim]
#
# SUBSTITUI o banco e as imagens atuais pelos do backup. A API fica fora do ar durante a
# restauração (alguns segundos) e volta sozinha no fim. Use --sim para não pedir confirmação.
set -euo pipefail

if [ "$#" -lt 2 ]; then
  sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
fi

banco="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
imagens="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
for arquivo in "$banco" "$imagens"; do
  [ -s "$arquivo" ] || { echo "ERRO: arquivo não encontrado ou vazio: $arquivo" >&2; exit 1; }
done

cd "$(dirname "$0")/.."

if [ "${3:-}" != "--sim" ]; then
  echo "Isto vai SUBSTITUIR o banco de dados e as imagens atuais por:"
  echo "  $banco"
  echo "  $imagens"
  read -r -p "Digite 'restaurar' para continuar: " resposta
  [ "$resposta" = "restaurar" ] || { echo "Cancelado."; exit 1; }
fi

echo "Parando a API..."
docker compose stop api

echo "Restaurando o banco de dados..."
# --clean --if-exists apaga cada tabela antes de recriá-la; --no-owner evita depender do usuário de origem.
docker compose exec -T postgres pg_restore --username=vtt --dbname=vtt --clean --if-exists --no-owner < "$banco"

echo "Restaurando as imagens..."
# Um container temporário da API, só com o volume montado, troca o conteúdo da pasta de uploads.
docker compose run --rm --no-deps -T --entrypoint sh api \
  -c 'find /app/uploads -mindepth 1 -delete && tar -xzf - -C /app' < "$imagens"

echo "Subindo a API..."
docker compose start api
echo "Restauração concluída. Confira em http://localhost:${API_PORT:-8080}/health"
