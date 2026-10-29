# Producción: puesta en marcha y operación

Checklist para pasar de la máquina de desarrollo a un servidor que se pueda usar
con datos reales. Nada de esto es opcional si hay información de clientes dentro.

## 1. Servicio de Windows

En una consola **elevada**:

```powershell
cd C:\organizaCLI\ControClient
.\scripts\install-service.ps1
```

Publica en `C:\ProgramData\ControClient\app`, crea el servicio `ControClient` con
arranque automático, le pasa la configuración por variables de entorno y configura
reinicio ante fallos.

Antes de arrancarlo, crear `C:\ProgramData\ControClient\app\appsettings.Production.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant>",
    "ClientId": "<client-id>",
    "ClientSecret": "<secreto>",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "Authentication": {
    "Mode": "Entra",
    "RequireMfaClaim": true,
    "RequirePreRegistration": true,
    "BootstrapAdminObjectId": "<oid del primer administrador>"
  },
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=contoclient;Username=contoclient_app;Password=<clave>"
  },
  "Database": { "Provider": "PostgreSQL" },
  "Documents": { "MaxBytes": 52428800 }
}
```

El fichero contiene secretos: restringe la ACL a la cuenta del servicio y a
administradores. Alternativa mejor: variables de entorno del servicio
(`AzureAd__ClientSecret`), que no quedan en disco.

Comprobar: `Get-Service ControClient`, `Invoke-WebRequest http://127.0.0.1:5080/health`.

## 2. Datos y permisos

| Ruta | Contenido | Permisos |
| --- | --- | --- |
| `C:\ProgramData\ControClient\data\documents` | Documentos de clientes | servicio + administradores |
| `C:\ProgramData\ControClient\data\dpkeys` | Claves de sesión | **crítico**: incluir en copias |
| `C:\ProgramData\ControClient\data\vault\kek.bin` | Clave maestra de la bóveda | **crítico** |

Sin `dpkeys`, las sesiones dejan de valer (no se pierden datos). Sin `kek.bin`,
**la bóveda es irrecuperable**.

## 3. Copias de seguridad

```powershell
.\scripts\backup.ps1 -BackupRoot D:\copias -RetentionDays 90
```

Genera un ZIP con volcado de PostgreSQL, documentos, `dpkeys` y `kek.bin`, más su
SHA256. **El ZIP contiene material sensible**: guárdalo cifrado y fuera del servidor.

Programar a diario con el Programador de tareas (cuenta con acceso a la carpeta):

```powershell
schtasks /Create /TN "ControClient backup" /SC DAILY /ST 03:00 /RU SYSTEM /RL HIGHEST ^
  /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\organizaCLI\ControClient\scripts\backup.ps1 -BackupRoot D:\copias"
```

## 4. Prueba de restauración

Una copia que no se ha restaurado nunca no es una copia. Al menos una vez al mes:

```powershell
.\scripts\restore-test.ps1
```

Restaura la última copia en un clúster temporal, comprueba que hay usuarios,
clientes y fichas, y lo elimina. No toca la base real ni necesita superusuario.

## 5. Red y certificados

- Publicar **solo por HTTPS** detrás de un proxy inverso (IIS, nginx o Caddy) con
  certificado de la CA interna (ADR-011). El puerto 5080 queda en `127.0.0.1`.
- Cortafuegos: 443 accesible solo desde la LAN/VPN; 5432 solo desde el propio servidor.
- Registrar en Entra la URI real (`https://<host>/signin-oidc`) además de las de
  localhost para pruebas.
- Renovar el certificado antes de que caduque (está en el calendario de vencimientos…
  del propio servidor).

## 6. Endurecimiento pendiente antes de datos reales

| Tema | Estado |
| --- | --- |
| Rotar la contraseña de PostgreSQL compartida en el chat | **pendiente** |
| `RequireMfaClaim`: el tenant no emite `amr`; decidir política o claims opcionales | **pendiente** |
| Expediente RGPD (encargado de tratamiento, base jurídica, plazos) | **pendiente** |
| Cifrado del disco del servidor (BitLocker) | recomendado |
| Copia fuera del sitio (otra ubicación física) | recomendado |
| Monitorización de `/health` y del espacio en disco | recomendado |

## 7. Actualizaciones

```powershell
git pull
.\scripts\install-service.ps1    # republica y reinicia el servicio
```

Las migraciones de EF se aplican al arrancar en Development; en producción hay que
aplicarlas de forma controlada antes de arrancar la nueva versión:

```powershell
dotnet ef database update -p src/ControClient.Infrastructure -s src/ControClient.Web
```

Haz copia antes de cada actualización con migración.
