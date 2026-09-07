#Requires -Version 5.1
<#
    .SYNOPSIS
    Publica o SCANOVA.App e gera o instalador MSI (Fase 11, seção 91/118), com o checksum
    SHA-256 do arquivo final ao lado dele (seção 118 — "instalador + checksum").

    Só roda no Windows (precisa do Windows App SDK / compilador XAML para publicar
    SCANOVA.App, e do WiX Toolset para o instalador — nenhum dos dois roda em Linux/macOS,
    ver docs/BUILD.md).

    .PARAMETER ProductVersion
    Versão do instalador (major.minor.build — sem o 4º componente, exigência do formato MSI).

    .EXAMPLE
    .\installer\Build-Installer.ps1 -ProductVersion 1.0.0
#>
param(
    [string]$ProductVersion = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src\SCANOVA.App"
$installerProject = Join-Path $repoRoot "installer\SCANOVA.Installer\SCANOVA.Installer.wixproj"
$publishDir = Join-Path $appProject "bin\publish\win-x64"

Write-Host "==> Publicando SCANOVA.App (win-x64, autocontido)..." -ForegroundColor Cyan
# -p:Platform=x64 explícito na linha de comando é necessário mesmo com <Platform>x64</Platform>
# já dentro do win-x64.pubxml -- bug conhecido do WinUI 3/Windows App SDK: sem isso, o Platform
# da avaliação inicial do projeto cai para "AnyCPU" antes do pubxml ser importado. Corrige a
# avaliação, mas sozinho NÃO basta -- ver o passo seguinte.
dotnet publish $appProject -c Release -p:PublishProfile=win-x64 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar SCANOVA.App." }

Write-Host "==> Copiando resources.pri e .xbf para a pasta publicada..." -ForegroundColor Cyan
# Bug conhecido e ainda sem correção definitiva do próprio Windows App SDK, para apps WinUI 3
# "Unpackaged": `dotnet publish` gera resources.pri e as páginas .xbf normalmente na pasta de
# BUILD (bin\x64\Release\...), mas não os copia para a pasta de PUBLISH. Sem isso, SCANOVA.App
# abre sem nenhuma janela (XamlParseException em MainWindow.InitializeComponent()). Workaround
# oficial confirmado em github.com/microsoft/WindowsAppSDK issues #3451/#4603 ("copy the .pri
# file from the release folder to publish"). Busca dinamicamente em vez de fixar o caminho
# exato de bin\, pra não quebrar se a estrutura de pastas do SDK mudar de novo.
$appBin = Join-Path $appProject "bin"
$sourcePri = Get-ChildItem -Path $appBin -Filter "resources.pri" -Recurse |
    Where-Object { $_.FullName -notlike "*\publish\*" } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $sourcePri) { throw "resources.pri não foi encontrado na pasta de build (fora de publish\) -- não há o que copiar." }
Copy-Item $sourcePri.FullName $publishDir -Force
Write-Host "Copiado: $($sourcePri.FullName) -> $publishDir"

$sourceRoot = $sourcePri.DirectoryName
$xbfFiles = Get-ChildItem -Path $sourceRoot -Filter "*.xbf" -Recurse
foreach ($xbf in $xbfFiles) {
    $relative = $xbf.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $dest = Join-Path $publishDir $relative
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Force -Path $destDir | Out-Null }
    Copy-Item $xbf.FullName $dest -Force
}
Write-Host "Copiados $($xbfFiles.Count) arquivo(s) .xbf de $sourceRoot -> $publishDir"

Write-Host "==> Compilando o instalador (WiX)..." -ForegroundColor Cyan
dotnet build $installerProject -c Release -p:AppPublishDir="$publishDir\" -p:ProductVersion=$ProductVersion
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }

$msi = Get-ChildItem -Path (Join-Path $repoRoot "installer\SCANOVA.Installer\bin") -Filter "SCANOVA-Setup.msi" -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $msi) { throw "Não encontrei o SCANOVA-Setup.msi gerado — verifique a saída acima." }

$checksumPath = "$($msi.FullName).sha256"
$hash = Get-FileHash -Path $msi.FullName -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $($msi.Name)" | Out-File -FilePath $checksumPath -Encoding ascii -NoNewline

Write-Host ""
Write-Host "Instalador gerado: $($msi.FullName)" -ForegroundColor Green
Write-Host "Checksum (SHA-256): $checksumPath" -ForegroundColor Green
