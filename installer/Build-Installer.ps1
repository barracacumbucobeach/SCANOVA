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
# da avaliação inicial do projeto cai para "AnyCPU" antes do pubxml ser importado, e os targets
# que geram resources.pri e os .xbf (páginas XAML compiladas) são pulados silenciosamente --
# publish "funciona" mas o app abre sem nenhuma janela (XamlParseException em runtime).
dotnet publish $appProject -c Release -p:PublishProfile=win-x64 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar SCANOVA.App." }

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
