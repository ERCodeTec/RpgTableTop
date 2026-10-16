#!/usr/bin/env bash
# Backup da Mesa Virtual: dump do PostgreSQL e o volume de imagens, com data e hora no nome.
#
#   scripts/backup.sh [pasta-de-destino]     (padrão: ./backups)
#
# Roda com os containers no ar (docker compose up -d). Não interrompe a mesa.
# Para restaurar: scripts/restaurar.sh <arquivo.dump> <arquivo.tar.gz>
set -euo pipefail

cd "$(dirname "$0")/.."
destino="${1:-backups}"
carimbo="$(date +%Y-%m-%d_%H%M%S)"
banco="$destino/vtt-banco-$carimbo.dump"
imagens="$destino/vtt-uploads-$carimbo.tar.gz"
mkdir -p "$destino"

echo "Banco de dados -> $banco"
# Formato "custom" do pg_dump: compactado e restaurável com pg_restore, inclusive por partes.
docker compose exec -T postgres pg_dump --username=vtt --dbname=vtt --format=custom > "$banco"

echo "Imagens enviadas -> $imagens"
docker compose exec -T api tar -czf - -C /app uploads > "$imagens"

# Um arquivo vazio significa que o comando falhou sem escrever nada: melhor avisar agora.
for arquivo in "$banco" "$imagens"; do
  if [ ! -s "$arquivo" ]; then
    echo "ERRO: $arquivo ficou vazio. O backup NÃO foi concluído." >&2
    exit 1
  fi
done

echo "Backup concluído:"
ls -lh "$banco" "$imagens"
echo "Guarde os dois arquivos juntos, fora desta máquina."
