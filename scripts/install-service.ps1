<#
.SYNOPSIS
    Publica ControClient e instala el servicio de Windows.

.DESCRIPTION
    Requiere una consola elevada (administrador). Publica en Release, crea el
    servicio con arranque automático, le pasa la configuración por variables de
    entorno y configura el reinicio ante fallos.

    La configuración de producción NO se lee de user-secrets: debe estar en
    appsettings.Production.json junto a la publicación o en variables de entorno
    del servicio (TenantId, ClientId, ClientSecret/ClientCertificates).

.PARAMETER InstallDir
    Carpeta destino. Por defecto C:\ProgramData\ControClient.

.PARAMETER ServiceName
    Nombre del servicio. Por defecto ControClient.

.PARAMETER DataRoot
    Carpeta de datos (documentos, claves, bóveda). Por defecto C:\ProgramData\ControClient\data.

.EXAMPLE
    ./install-service.ps1
#>
param(
    [string]$InstallDir = 'C:\ProgramData\ControClient',
    [string]$ServiceName = 'ControClient',
    [string]$DataRoot = 'C:\ProgramData\ControClient\data'
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Ejecuta este script en una consola elevada (administrador).'
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $projectRoot 'src\ControClient.Web'
$publishDir = Join-Path $InstallDir 'app'

Write-Host "Publicando en $publishDir…"
& dotnet publish $webProject -c Release -o $publishDir /p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw 'La publicación falló.' }

New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'documents') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'dpkeys') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'vault') | Out-Null

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "El servicio $ServiceName ya existe; se detiene y elimina."
    & sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 2
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

$dll = Join-Path $publishDir 'ControClient.Web.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "No se encontró $dll" }

$dotnet = (Get-Command dotnet).Source
$binPath = "`"$dotnet`" `"$dll`""

Write-Host "Creando servicio $ServiceName…"
& sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= 'ControClient - Gestión de clientes IT'
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el servicio.' }

& sc.exe description $ServiceName 'Gestión interna de clientes IT de Ideo Networks.' | Out-Null

# Directorio de trabajo y variables de entorno del servicio.
$reg = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
Set-ItemProperty -Path $reg -Name 'AppDirectory' -Value $publishDir
New-ItemProperty -Path $reg -Name 'Environment' -PropertyType MultiString -Force -Value @(
    'ASPNETCORE_ENVIRONMENT=Production',
    'ASPNETCORE_URLS=http://127.0.0.1:5080',
    "Storage__DocumentsRoot=$DataRoot\documents",
    "DataProtection__KeysPath=$DataRoot\dpkeys",
    "Vault__KeyFile=$DataRoot\vault\kek.bin"
) | Out-Null

# Reinicio ante fallo: 5 s, 30 s y 60 s; el contador se reinicia cada día.
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/30000/restart/60000 | Out-Null

& sc.exe start $ServiceName | Out-Null
Start-Sleep -Seconds 5

$status = (Get-Service -Name $ServiceName).Status
Write-Host "Estado del servicio: $status"
if ($status -ne 'Running') {
    Write-Host 'No arrancó. Revisa el visor de eventos y appsettings.Production.json.'
    exit 1
}

Write-Host ''
Write-Host 'Servicio instalado. Falta:'
Write-Host "  1. Crear $publishDir\appsettings.Production.json con AzureAd, Authentication y ConnectionStrings."
Write-Host '  2. Reiniciar el servicio tras crear la configuración.'
Write-Host '  3. Comprobar http://127.0.0.1:5080/health'
