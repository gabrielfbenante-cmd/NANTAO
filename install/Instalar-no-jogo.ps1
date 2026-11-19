# Instala os mods NANTAO (NantaoAgents, ChineloComCriativo, CarrinhoCriativo)
# no perfil do Thunderstore Mod Manager ou do r2modman.
#
# Uso: clique duas vezes em "Instalar.bat" (ou rode este script no PowerShell).
# Opcional: -Perfil "NomeDoPerfil"  (padrao: Default)
param([string]$Perfil = "Default")
$ErrorActionPreference = "Stop"

function Sair($codigo) { Write-Host ""; Read-Host "Aperte Enter para fechar"; exit $codigo }

$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$origem = Join-Path $aqui "BepInEx"
if (-not (Test-Path $origem)) {
    Write-Host "Nao achei a pasta 'BepInEx' ao lado deste script. Extraia o .zip inteiro antes de rodar." -ForegroundColor Red
    Sair 1
}

if (Get-Process -Name "Lethal Company" -ErrorAction SilentlyContinue) {
    Write-Host "Feche o Lethal Company antes de instalar." -ForegroundColor Red
    Sair 1
}

$candidatos = @(
    (Join-Path $env:APPDATA "Thunderstore Mod Manager\DataFolder\LethalCompany\profiles\$Perfil"),
    (Join-Path $env:APPDATA "r2modmanPlus-local\LethalCompany\profiles\$Perfil")
)
$destino = $candidatos | Where-Object { Test-Path (Join-Path $_ "BepInEx") } | Select-Object -First 1
if (-not $destino) {
    Write-Host "Nao achei o perfil '$Perfil' do Lethal Company. Procurei em:" -ForegroundColor Red
    $candidatos | ForEach-Object { Write-Host "  $_" }
    Write-Host "Abra o Thunderstore Mod Manager / r2modman, selecione o perfil e clique 'Start modded' uma vez."
    Sair 1
}
$bep = Join-Path $destino "BepInEx"
Write-Host "Instalando em: $destino" -ForegroundColor Cyan

# Versao antiga (NeonRun) foi substituida pelo NantaoAgents.
Get-ChildItem (Join-Path $bep "plugins") -Recurse -Filter "NeonRun.dll" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Aviso: achei a versao antiga em $($_.FullName). Desinstale o 'NeonRun' no gerenciador." -ForegroundColor Yellow
}

$backup = Join-Path $bep ("backup-nantao-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$copiados = 0
Get-ChildItem $origem -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($origem.Length + 1)
    $alvo = Join-Path $bep $rel
    if (Test-Path $alvo) {
        $b = Join-Path $backup $rel
        New-Item -ItemType Directory -Force -Path (Split-Path $b) | Out-Null
        Copy-Item $alvo $b -Force
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $alvo) | Out-Null
    Copy-Item $_.FullName $alvo -Force
    Write-Host "  + BepInEx\$rel"
    $copiados++
}
if (Test-Path $backup) { Write-Host "Versoes anteriores guardadas em: $backup" }

# O chinelo e o carrinho precisam disso (senao o BepInEx encerra os plugins).
$cfg = Join-Path $bep "config\BepInEx.cfg"
if (Test-Path $cfg) {
    $texto = Get-Content $cfg -Raw
    if ($texto -match "HideManagerGameObject\s*=\s*false") {
        $texto = $texto -replace "HideManagerGameObject\s*=\s*false", "HideManagerGameObject = true"
        Set-Content -Path $cfg -Value $texto -NoNewline
        Write-Host "BepInEx.cfg: HideManagerGameObject = true" -ForegroundColor Green
    }
} else {
    Write-Host "Aviso: BepInEx.cfg ainda nao existe. Abra o jogo uma vez, feche e rode este instalador de novo." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Pronto! $copiados arquivo(s) instalado(s). Abra o jogo pelo gerenciador (Start modded)." -ForegroundColor Green
Sair 0
