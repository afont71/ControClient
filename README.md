# ControClient

Aplicación interna de gestión de clientes IT y bóveda de credenciales para **Ideo Networks SL**.

Este repositorio implementa la especificación `docs/especificacion/ESPECIFICACION_v1.0.md` (v1.0.0, aprobada para desarrollo el 29/10/2026).

## Estado actual — Etapa 0 (cimientos)

| Pieza | Estado |
| --- | --- |
| Solución .NET 9 (Domain / Infrastructure / Web / Tests) | ✅ |
| PostgreSQL + EF Core con migraciones aplicadas | ✅ |
| Autenticación Microsoft Entra ID + MFA (web y API) | ✅ cableada; falta tenant real |
| Concesiones por cliente (ver/editar/revelar) | ✅ |
| Pantallas de clientes (lista, alta, ficha, permisos) | ✅ |
| Plantillas versionadas y fichas dinámicas con validación | ✅ |
| Pantallas de fichas (alta, detalle, edición con versión) | ✅ |
| Documentos con hash SHA-256, versiones y descarga auditada | ✅ |
| Subida desde la ficha del cliente con clasificación | ✅ |
| Auditoría funcional con redacción de secretos | ✅ |
| Endpoints de salud, sistema, clientes, usuarios, plantillas, fichas, documentos y auditoría | ✅ |
| Pruebas automatizadas | ✅ 37 |
| CI (build + test) | ✅ |

Pendiente: bóveda (4), búsqueda y OpenAPI (5), Android (6), migración (7).

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
| `GET /api/v1/clients?query=&page=` | sesión; alcance por rol | clientes visibles, paginados |
| `GET /api/v1/clients/{id}` | ver el cliente | detalle; **404** si está fuera de alcance |
| `POST /api/v1/clients` | administrador o responsable | alta + auditoría |
| `PATCH /api/v1/clients/{id}` | editar el cliente | nombre y estado comercial |
| `GET /api/v1/clients/{id}/grants` | editar el cliente | concesiones vigentes |
| `POST /api/v1/clients/{id}/grants` | administrador | crea o actualiza concesión |
| `DELETE /api/v1/clients/{id}/grants/{grantId}` | administrador | revoca concesión |
| `GET /api/v1/users` | administrador | usuarios para conceder permisos |
| `PATCH /api/v1/users/{id}/role` | administrador | cambia rol (no el propio) |
| `GET /api/v1/templates` | sesión | plantillas activas (administrador ve todas) |
| `POST /api/v1/templates` | administrador | nueva versión de plantilla |
| `GET /api/v1/clients/{id}/records` | ver el cliente | fichas del cliente |
| `POST /api/v1/clients/{id}/records` | editar el cliente | alta de ficha validada |
| `GET /api/v1/records/{id}` | ver su cliente | ficha + esquema de la plantilla |
| `PATCH /api/v1/records/{id}` | editar su cliente | actualización con versión (**409** si cambió) |
| `GET /api/v1/clients/{id}/documents` | ver el cliente | documentos y versión actual |
| `POST /api/v1/clients/{id}/documents` | editar el cliente | subida `multipart/form-data` (`file`, `name`, `classification`) |
| `GET /api/v1/documents/{id}` | ver su cliente | documento con todas las versiones |
| `POST /api/v1/documents/{id}/versions` | editar su cliente | nueva versión (`file`) |
| `GET /api/v1/documents/{id}/content?version=` | ver su cliente | descarga desde el almacén, auditada |
| `GET /api/v1/audit/events` | administrador, auditor o custodio | últimos 50 eventos |

Acciones de una concesión (`actions`, flags): `1` ver, `2` editar, `4` revelar secretos. Se combinan sumando: `3` = ver y editar.

### Documentos

- El contenido se guarda en disco, fuera de la base: `Storage:DocumentsRoot` (por defecto `storage/documents` junto a la aplicación).
- Ruta derivada del SHA-256; contenido idéntico no se duplica.
- Límite por defecto 50 MB (`Documents:MaxBytes`) y bloqueo de ejecutables (`.exe`, `.dll`, `.ps1`, `.msi`, `.iso`…).
- `scanStatus` empieza como `NotScanned`: **no hay antivirus conectado** en esta etapa; no lo interpretes como limpio.
- Cada descarga queda auditada con actor, cliente, documento y versión.

Las plantillas iniciales (`servidor`, `vpn`, `correo`) se siembran al arrancar si no hay ninguna y pueden desactivarse con `Seeding:DefaultTemplates=false`.

### Simular usuarios en desarrollo

Con `Authentication:Mode = Development`, cualquier petición puede cambiar de usuario con cabeceras:

```powershell
# Técnico simulado
$h = @{ 'X-Dev-ObjectId' = 'tech-0001'; 'X-Dev-Role' = 'Technician'; 'X-Dev-Name' = 'Técnico de prueba' }
Invoke-WebRequest http://127.0.0.1:5080/api/v1/clients -Headers $h
```

El primer acceso crea el usuario como **ReadOnly**; el rol se cambia con `PATCH /api/v1/users/{id}/role`. Fuera de Development este mecanismo no existe.

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
