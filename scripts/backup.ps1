# Backup da Mesa Virtual no Windows: dump do PostgreSQL e o volume de imagens, com data e hora no nome.
#
#   .\scripts\backup.ps1 [-Destino backups]
#
# Roda com os containers no ar (docker compose up -d). Não interrompe a mesa.
# Os arquivos são gerados dentro dos containers e copiados para fora: o redirecionamento ">" do
# PowerShell corrompe arquivos binários em versões anteriores à 7.4.
param([string]$Destino = "backups")

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
New-Item -ItemType Directory -Force -Path $Destino | Out-Null

$carimbo = Get-Date -Format "yyyy-MM-dd_HHmmss"
$banco = Join-Path $Destino "vtt-banco-$carimbo.dump"
$imagens = Join-Path $Destino "vtt-uploads-$carimbo.tar.gz"

function Executar {
    & docker compose @args
    if ($LASTEXITCODE -ne 0) { throw "Falhou: docker compose $args" }
}

Write-Host "Banco de dados -> $banco"
Executar exec -T postgres pg_dump --username=vtt --dbname=vtt --format=custom --file=/tmp/vtt-backup.dump
Executar cp postgres:/tmp/vtt-backup.dump $banco
Executar exec -T postgres rm /tmp/vtt-backup.dump

Write-Host "Imagens enviadas -> $imagens"
Executar exec -T api tar -czf /tmp/vtt-uploads.tar.gz -C /app uploads
Executar cp api:/tmp/vtt-uploads.tar.gz $imagens
Executar exec -T api rm /tmp/vtt-uploads.tar.gz

foreach ($arquivo in @($banco, $imagens)) {
    if ((Get-Item $arquivo).Length -eq 0) { throw "$arquivo ficou vazio. O backup NAO foi concluido." }
}

Write-Host "Backup concluido:"
Get-Item $banco, $imagens | Format-Table Name, Length
Write-Host "Guarde os dois arquivos juntos, fora desta maquina."
