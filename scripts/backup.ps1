<#
.SYNOPSIS
    Copia de seguridad de ControClient: base de datos, documentos, claves y bóveda.

.DESCRIPTION
    Genera un ZIP con:
      - volcado de PostgreSQL en formato custom (pg_dump -Fc)
      - documentos del almacén
      - claves de protección de datos (dpkeys)
      - clave maestra de la bóveda (kek.bin)
    y un fichero .sha256 para verificar la integridad.

    No incluye secretos de configuración (van en user-secrets o en el gestor).
    El ZIP contiene material sensible: guárdalo cifrado en destino externo.

.PARAMETER BackupRoot
    Carpeta donde se dejan las copias. Por defecto C:\organizaCLI\backups.

.PARAMETER DataRoot
    Carpeta de datos de la aplicación. Por defecto C:\organizaCLI\contoclient-data.

.PARAMETER RetentionDays
    Días que se conservan las copias. Por defecto 30.

.EXAMPLE
    ./backup.ps1
    ./backup.ps1 -BackupRoot D:\copias -RetentionDays 90
#>
param(
    [string]$BackupRoot = 'C:\organizaCLI\backups',
    [string]$DataRoot = 'C:\organizaCLI\contoclient-data',
    [int]$RetentionDays = 30,
    [string]$ConnectionString,
    [string]$OwnerSecretsFile = 'C:\organizaCLI\secrets\contoclient-owner.txt'
)

$ErrorActionPreference = 'Stop'

function Get-ConnectionString {
    param([string]$Explicit, [string]$SecretsFile)

    if ($Explicit) { return $Explicit }

    if (Test-Path -LiteralPath $SecretsFile) {
        $line = Get-Content -LiteralPath $SecretsFile | Where-Object { $_ -match '^\s*Cadena:' } | Select-Object -First 1
        if ($line) { return ($line -replace '^\s*Cadena:\s*', '').Trim() }
    }

    throw "No hay cadena de conexión. Pasa -ConnectionString o revisa $SecretsFile"
}

function ConvertFrom-ConnectionString {
    param([string]$Cs)

    $parts = @{}
    foreach ($pair in ($Cs -split ';')) {
        if ($pair -match '^\s*([^=]+)=(.*)$') {
            $parts[$matches[1].Trim().ToLower()] = $matches[2].Trim()
        }
    }

    $hostName = if ($parts.ContainsKey('host')) { $parts['host'] } else { '127.0.0.1' }
    $database = if ($parts.ContainsKey('database')) { $parts['database'] } else { 'contoclient' }
    $user = if ($parts.ContainsKey('username')) { $parts['username'] } elseif ($parts.ContainsKey('user id')) { $parts['user id'] } elseif ($parts.ContainsKey('user')) { $parts['user'] } else { $null }
    $password = if ($parts.ContainsKey('password')) { $parts['password'] } else { '' }
    $port = if ($parts.ContainsKey('port')) { $parts['port'] } else { '5432' }

    if (-not $user) { throw "La cadena de conexión no incluye usuario." }
    if (-not $database) { throw "La cadena de conexión no incluye base de datos." }

    [pscustomobject]@{ Host = $hostName; Port = $port; Database = $database; User = $user; Password = $password }
}

$pgDump = 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe'
if (-not (Test-Path -LiteralPath $pgDump)) {
    throw "No se encuentra pg_dump en $pgDump"
}

$db = ConvertFrom-ConnectionString -Cs (Get-ConnectionString -Explicit $ConnectionString -SecretsFile $OwnerSecretsFile)

New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$work = Join-Path $env:TEMP "contoclient-backup-$stamp"
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    Write-Host "Volcando base de datos $($db.Database)…"
    $dumpFile = Join-Path $work 'database.dump'
    $env:PGPASSWORD = $db.Password
    try {
        & $pgDump -h $db.Host -p $db.Port -U $db.User -d $db.Database -Fc -f $dumpFile
        if ($LASTEXITCODE -ne 0) { throw "pg_dump terminó con código $LASTEXITCODE" }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }

    foreach ($name in 'documents', 'dpkeys') {
        $src = Join-Path $DataRoot $name
        if (Test-Path -LiteralPath $src) {
            Write-Host "Copiando $name…"
            Copy-Item -LiteralPath $src -Destination (Join-Path $work $name) -Recurse
        }
        else {
            Write-Warning "No existe $src; se omite."
        }
    }

    $kek = Join-Path $DataRoot 'vault\kek.bin'
    if (Test-Path -LiteralPath $kek) {
        Write-Host 'Copiando clave de la bóveda…'
        New-Item -ItemType Directory -Force -Path (Join-Path $work 'vault') | Out-Null
        Copy-Item -LiteralPath $kek -Destination (Join-Path $work 'vault\kek.bin')
    }
    else {
        Write-Warning "No existe $kek; la bóveda no se podrá recuperar desde esta copia."
    }

    $manifest = Join-Path $work 'MANIFEST.txt'
    @(
        "ControClient backup"
        "Fecha: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
        "Base de datos: $($db.Database) en $($db.Host):$($db.Port)"
        "Origen de datos: $DataRoot"
        "Contenido: database.dump, documents/, dpkeys/, vault/kek.bin"
        "AVISO: contiene material sensible. Guárdalo cifrado y con acceso restringido."
    ) | Set-Content -LiteralPath $manifest -Encoding utf8

    $zip = Join-Path $BackupRoot "contoclient-$stamp.zip"
    Write-Host "Comprimiendo en $zip…"
    Compress-Archive -Path (Join-Path $work '*') -DestinationPath $zip -CompressionLevel Optimal

    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    "$hash  $(Split-Path -Leaf $zip)" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii

    # Comprobación de que el volcado es legible, sin restaurar todavía.
    $pgRestore = 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe'
    & $pgRestore -l $dumpFile | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "El volcado no es legible por pg_restore." }

    Write-Host ''
    Write-Host "Copia creada: $zip"
    Write-Host "Tamaño: $([math]::Round((Get-Item $zip).Length / 1MB, 2)) MB"
    Write-Host "SHA256: $hash"
}
finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}

$limit = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem -LiteralPath $BackupRoot -Filter 'contoclient-*.zip' |
    Where-Object { $_.LastWriteTime -lt $limit } |
    ForEach-Object {
        Write-Host "Borrando copia antigua $($_.Name)"
        Remove-Item -LiteralPath $_.FullName -Force
        Remove-Item -LiteralPath "$($_.FullName).sha256" -Force -ErrorAction SilentlyContinue
    }
