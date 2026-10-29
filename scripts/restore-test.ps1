<#
.SYNOPSIS
    Prueba de restauración de una copia de ControClient.

.DESCRIPTION
    Restaura el volcado de la copia y comprueba que las tablas principales tienen
    datos. Si el usuario configurado no puede crear bases, levanta un clúster
    PostgreSQL temporal (initdb + pg_ctl en un puerto libre) y restaura allí, de
    modo que la prueba no toca la base real ni necesita superusuario.

.PARAMETER Zip
    Copia a probar. Por defecto, la más reciente de BackupRoot.

.PARAMETER BackupRoot
    Carpeta de copias. Por defecto C:\organizaCLI\backups.

.PARAMETER AdminConnectionString
    Cadena con permisos CREATEDB. Si se omite, se usa la del fichero de secretos
    del propietario y, si no puede crear bases, se usa el clúster temporal.

.EXAMPLE
    ./restore-test.ps1
#>
param(
    [string]$Zip,
    [string]$BackupRoot = 'C:\organizaCLI\backups',
    [string]$AdminConnectionString,
    [string]$OwnerSecretsFile = 'C:\organizaCLI\secrets\contoclient-owner.txt'
)

$ErrorActionPreference = 'Stop'

function Get-AdminConnectionString {
    param([string]$Explicit, [string]$SecretsFile)

    if ($Explicit) { return $Explicit }

    if (Test-Path -LiteralPath $SecretsFile) {
        $line = Get-Content -LiteralPath $SecretsFile | Where-Object { $_ -match '^\s*Cadena:' } | Select-Object -First 1
        if ($line) { return ($line -replace '^\s*Cadena:\s*', '').Trim() }
    }

    return $null
}

function ConvertFrom-ConnectionString {
    param([string]$Cs)

    $parts = @{}
    foreach ($pair in ($Cs -split ';')) {
        if ($pair -match '^\s*([^=]+)=(.*)$') {
            $parts[$matches[1].Trim().ToLower()] = $matches[2].Trim()
        }
    }

    [pscustomobject]@{
        Host = if ($parts.ContainsKey('host')) { $parts['host'] } else { '127.0.0.1' }
        Port = if ($parts.ContainsKey('port')) { $parts['port'] } else { '5432' }
        User = if ($parts.ContainsKey('username')) { $parts['username'] } elseif ($parts.ContainsKey('user id')) { $parts['user id'] } else { $parts['user'] }
        Password = if ($parts.ContainsKey('password')) { $parts['password'] } else { '' }
    }
}

$bin = 'C:\Program Files\PostgreSQL\18\bin'
$psql = Join-Path $bin 'psql.exe'
$pgRestore = Join-Path $bin 'pg_restore.exe'
$initdb = Join-Path $bin 'initdb.exe'
$pgCtl = Join-Path $bin 'pg_ctl.exe'
$createdb = Join-Path $bin 'createdb.exe'

if (-not $Zip) {
    $Zip = Get-ChildItem -LiteralPath $BackupRoot -Filter 'contoclient-*.zip' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $Zip) { throw "No hay copias en $BackupRoot" }
}

if (-not (Test-Path -LiteralPath $Zip)) { throw "No existe la copia $Zip" }

$shaFile = "$Zip.sha256"
if (Test-Path -LiteralPath $shaFile) {
    $expected = (Get-Content -LiteralPath $shaFile -Raw).Trim().Split(' ')[0]
    $actual = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash
    if ($expected -ne $actual) { throw 'El SHA256 no coincide: la copia está dañada.' }
    Write-Host 'SHA256 correcto.'
}

$work = Join-Path $env:TEMP ('contoclient-restore-' + (Get-Date -Format 'yyyyMMddHHmmss'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

$tempClusterStarted = $false
$pgdata = Join-Path $work 'pgdata'
$port = 55432
$scratch = 'restoretest'

try {
    Write-Host "Extrayendo $([System.IO.Path]::GetFileName($Zip))…"
    Expand-Archive -LiteralPath $Zip -DestinationPath $work -Force
    $dbfile = Join-Path $work 'database.dump'
    if (-not (Test-Path -LiteralPath $dbfile)) { throw 'La copia no contiene database.dump' }

    $targetHost = '127.0.0.1'
    $targetPort = '5432'
    $targetUser = $null
    $useTempCluster = $true

    $cs = Get-AdminConnectionString -Explicit $AdminConnectionString -SecretsFile $OwnerSecretsFile
    if ($cs) {
        $db = ConvertFrom-ConnectionString -Cs $cs
        $env:PGPASSWORD = $db.Password
        Write-Host 'Comprobando si el usuario puede crear bases…'
        $probeLog = Join-Path $work 'probe.log'
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $psql -h $db.Host -p $db.Port -U $db.User -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $scratch" *> $probeLog
        $probeCode = $LASTEXITCODE
        $ErrorActionPreference = $previous

        if ($probeCode -eq 0) {
            $useTempCluster = $false
            $targetHost = $db.Host
            $targetPort = $db.Port
            $targetUser = $db.User
            Write-Host "Restaurando en $scratch sobre el servidor existente…"
        }
        else {
            Write-Host 'Sin permiso CREATEDB: se usará un clúster temporal.'
        }
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }

    if ($useTempCluster) {
        Write-Host 'Creando clúster temporal…'
        & $initdb -D $pgdata -U postgres --auth=trust --encoding=UTF8 --locale=C *> (Join-Path $work 'initdb.log')
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar el clúster temporal.' }

        # Arranque desacoplado: pg_ctl -w se queda colgado cuando el postmaster
        # hereda las tuberías de la sesión, así que se lanza y se sondea con psql.
        $postgresExe = Join-Path $bin 'postgres.exe'
        $pgProc = Start-Process -FilePath $postgresExe `
            -ArgumentList @('-D', $pgdata, '-p', "$port", '-c', 'listen_addresses=127.0.0.1') `
            -RedirectStandardOutput (Join-Path $work 'pg.out') `
            -RedirectStandardError (Join-Path $work 'pg.err') `
            -WindowStyle Hidden -PassThru

        $tempClusterStarted = $true
        $targetHost = '127.0.0.1'
        $targetPort = "$port"
        $targetUser = 'postgres'

        $ready = $false
        for ($i = 0; $i -lt 60; $i++) {
            & $psql -h $targetHost -p $targetPort -U $targetUser -d postgres -w -At -c 'select 1' *> (Join-Path $work 'wait.log')
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
            Start-Sleep -Milliseconds 500
        }

        if (-not $ready) { throw 'El clúster temporal no respondió a tiempo.' }

        & $psql -h $targetHost -p $targetPort -U $targetUser -d postgres -w -v ON_ERROR_STOP=1 -c "CREATE DATABASE $scratch" *> (Join-Path $work 'createdb.log')
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la base temporal.' }
    }
    Write-Host 'Restaurando…'
    & $pgRestore -h $targetHost -p $targetPort -U $targetUser -d $scratch --no-owner --no-privileges $dbfile
    # pg_restore puede devolver 1 por avisos no fatales; los datos se comprueban abajo.

    $checks = @{}
    foreach ($table in 'users', 'clients', 'form_templates', 'record_instances', 'audit_events', 'groups') {
        $value = & $psql -h $targetHost -p $targetPort -U $targetUser -d $scratch -At -c "SELECT count(*) FROM $table"
        if ($LASTEXITCODE -ne 0) { throw "Falta la tabla $table en la restauración." }
        $checks[$table] = [int]$value
    }

    Write-Host ''
    Write-Host 'Restauración verificada. Filas por tabla:'
    $checks.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0,-16} {1}" -f $_.Key, $_.Value) }

    if ($checks['users'] -eq 0) { throw 'La restauración no tiene usuarios: la copia no es válida.' }
    if ($checks['clients'] -eq 0) { throw 'La restauración no tiene clientes: la copia no es válida.' }

    Write-Host ''
    Write-Host 'La copia se puede restaurar y contiene datos.'
}
finally {
    if ($tempClusterStarted) {
        if ($pgProc -and -not $pgProc.HasExited) {
            Stop-Process -Id $pgProc.Id -Force -ErrorAction SilentlyContinue
            $pgProc.WaitForExit(10000) | Out-Null
        }
        Write-Host 'Clúster temporal detenido.'
    }
    elseif ($targetUser) {
        # Solo si se restauró sobre el servidor real con permiso CREATEDB.
        $env:PGPASSWORD = ''
        & $psql -h $targetHost -p $targetPort -U $targetUser -d postgres -v ON_ERROR_STOP=0 -c "DROP DATABASE IF EXISTS $scratch" | Out-Null
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
