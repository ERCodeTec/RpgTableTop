# Restaura um backup feito por scripts\backup.ps1 (ou backup.sh).
#
#   .\scripts\restaurar.ps1 -Banco <vtt-banco-....dump> -Imagens <vtt-uploads-....tar.gz> [-Sim]
#
# SUBSTITUI o banco e as imagens atuais pelos do backup. A API fica fora do ar durante a
# restauracao (alguns segundos) e volta sozinha no fim. Use -Sim para nao pedir confirmacao.
param(
    [Parameter(Mandatory = $true)][string]$Banco,
    [Parameter(Mandatory = $true)][string]$Imagens,
    [switch]$Sim
)

$ErrorActionPreference = "Stop"
$Banco = (Resolve-Path $Banco).Path
$Imagens = (Resolve-Path $Imagens).Path
Set-Location (Join-Path $PSScriptRoot "..")

if (-not $Sim) {
    Write-Host "Isto vai SUBSTITUIR o banco de dados e as imagens atuais por:"
    Write-Host "  $Banco"
    Write-Host "  $Imagens"
    if ((Read-Host "Digite 'restaurar' para continuar") -ne "restaurar") { Write-Host "Cancelado."; exit 1 }
}

function Executar {
    & docker compose @args
    if ($LASTEXITCODE -ne 0) { throw "Falhou: docker compose $args" }
}

Write-Host "Restaurando o banco de dados..."
Executar cp $Banco postgres:/tmp/vtt-restaurar.dump
Executar stop api
Executar exec -T postgres pg_restore --username=vtt --dbname=vtt --clean --if-exists --no-owner /tmp/vtt-restaurar.dump
Executar exec -T postgres rm /tmp/vtt-restaurar.dump

Write-Host "Restaurando as imagens..."
# A API precisa estar no ar para receber o arquivo; ela so le as imagens sob demanda.
Executar start api
Executar cp $Imagens api:/tmp/vtt-uploads.tar.gz
Executar exec -T api sh -c "find /app/uploads -mindepth 1 -delete && tar -xzf /tmp/vtt-uploads.tar.gz -C /app && rm /tmp/vtt-uploads.tar.gz"
Executar restart api

Write-Host "Restauracao concluida. Confira o endereco /health da aplicacao."
