# ControClient

Aplicación interna de gestión de clientes IT y bóveda de credenciales para **Ideo Networks SL**.

Este repositorio implementa la especificación `docs/especificacion/ESPECIFICACION_v1.0.md` (v1.0.0, aprobada para desarrollo el 29/10/2026).

## Estado actual — Etapa 0 (cimientos)

| Pieza | Estado |
| --- | --- |
| Solución .NET 9 (Domain / Infrastructure / Web / Tests) | ✅ |
| PostgreSQL + EF Core con migración inicial | ✅ definida (requiere instancia) |
| Autenticación Microsoft Entra ID + MFA (web y API) | ✅ cableada; falta tenant real |
| Autorización por rol y alcance por cliente | ✅ base (política + matriz) |
| Auditoría funcional con redacción de secretos | ✅ |
| Endpoints de salud, sistema, clientes y auditoría | ✅ |
| Pruebas automatizadas | ✅ 18 |
| CI (build + test) | ✅ |

Pendiente de las siguientes etapas: concesiones por cliente y fichas dinámicas (1–2), documentación (3), bóveda (4), búsqueda y OpenAPI (5), Android (6), migración (7).

## Estructura

```
src/
  ControClient.Domain/          Entidades, reglas, autorización, redacción
  ControClient.Infrastructure/  EF Core, auditoría, registro de servicios
  ControClient.Web/             ASP.NET Core + Blazor Server + API
tests/
  ControClient.Tests/           xunit v3
docs/
  especificacion/               Especificación v1.0 y catálogo (anexo A)
  sql/                          Script DDL generado por EF Core
```

## Ejecutar en local (sin tenant ni base de datos)

El perfil `Development` usa autenticación simulada y base de datos en memoria:

```powershell
dotnet run --project src/ControClient.Web
```

- La autenticación de desarrollo **solo** se registra si `Authentication:Mode = Development` **y** el entorno es `Development`. En cualquier otro caso se exige Entra ID.
- La base en memoria no persiste: es para probar la interfaz y los endpoints.

Endpoints útiles:

| Endpoint | Requiere | Devuelve |
| --- | --- | --- |
| `GET /health` | nada | estado del servicio y de la base |
| `GET /api/v1/system/info` | sesión con MFA | versión, entorno, estado de bóveda |
| `GET /api/v1/clients` | sesión; alcance por rol | clientes visibles |
| `POST /api/v1/clients` | administrador o responsable | alta de cliente + auditoría |
| `GET /api/v1/audit/events` | administrador, auditor o custodio | últimos 50 eventos |

## Ejecutar con PostgreSQL y Entra ID

### 1. Base de datos y roles (una vez)

PostgreSQL 18 está instalado como servicio. Crea base y roles dedicados:

```powershell
# Edita antes las dos contraseñas de scripts/db-setup.sql
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h 127.0.0.1 -f scripts/db-setup.sql
```

Roles: `contoclient_owner` (migraciones, dueño del esquema) y `contoclient_app`
(solo DML). La aplicación nunca usa el superusuario.

### 2. Cadena de conexión y secretos

```powershell
dotnet user-secrets init --project src/ControClient.Web
dotnet user-secrets set "Database:Provider" "PostgreSQL" --project src/ControClient.Web
dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5432;Database=contoclient;Username=contoclient_app;Password=<app>" --project src/ControClient.Web
```

### 3. Aplicar el esquema (con el usuario dueño)

La fábrica de diseño lee `CONTOCLIENT_DB_OWNER` para no poner la contraseña en la línea de comandos:

```powershell
$env:CONTOCLIENT_DB_OWNER = "Host=127.0.0.1;Port=5432;Database=contoclient;Username=contoclient_owner;Password=<owner>"
dotnet ef database update -p src/ControClient.Infrastructure -s src/ControClient.Web
Remove-Item Env:CONTOCLIENT_DB_OWNER
```

### 4. Entra ID

```powershell
dotnet user-secrets set "AzureAd:TenantId" "<tenant>" --project src/ControClient.Web
dotnet user-secrets set "AzureAd:ClientId" "<app-id>" --project src/ControClient.Web
```

Registro de aplicación en Entra ID:
- Plataforma **Web** con redirección `https://<host>/signin-oidc`.
- Plataforma **Single-page/API** o exponer un ámbito para la APK (JWT Bearer).
- Política de acceso condicional que **exija MFA** (SEC-02).

En producción: `Authentication:Mode = Entra` y `Database:Provider = PostgreSQL`.

## Seguridad

- Ningún secreto en el repositorio. Cadenas de conexión y credenciales por user-secrets o variables de entorno.
- La auditoría redacta pares `clave=valor`, contraseñas y credenciales embebidas en URL; aun así, la regla es **no registrar secretos**.
- Los roles no se deducen del cliente: se validan en servidor. Un administrador **no** puede revelar secretos por su rol.
- La bóveda (etapa 4) se desella con DPAPI + MFA de administrador, con sobre de recuperación (SEC-11…SEC-14).

## Pruebas

```powershell
dotnet run --project tests/ControClient.Tests
```

Se usa **xunit v3** con la plataforma de pruebas en proceso.

## Convenciones

- Español para dominio y documentación; inglés para nombres técnicos de framework.
- `snake_case` en base de datos.
- Un cambio de comportamiento = una prueba que lo demuestra.
- Cada etapa cierra los criterios de aceptación (`AC-xx`) de la especificación antes de pasar a la siguiente.
