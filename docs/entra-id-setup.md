# Conectar Microsoft Entra ID

Esta guía deja la aplicación con login real (Microsoft 365 + MFA) en lugar del modo
de desarrollo. Necesitas ser administrador del tenant.

## 1. Registro de aplicación (web)

En **Entra ID → Registros de aplicaciones → Nuevo registro**:

| Campo | Valor |
| --- | --- |
| Nombre | `ControClient` |
| Tipos de cuenta | Solo cuentas de esta organización |
| URI de redirección | Plataforma **Web** → `https://<host>/signin-oidc` |

Anota:

- **Id. de aplicación (cliente)**: será `AzureAd:ClientId`.
- **Id. de directorio (inquilino)**: será `AzureAd:TenantId`.

En **Certificados y secretos** no hace falta secreto: el flujo es de código de
autorización con PKCE y cookies, sin secreto de cliente.

En **Autenticación → Configuración de plataforma → Web**, añade también
`https://<host>/signout-callback-oidc`.

## 2. API para la APK Android

La aplicación Android necesita un token de acceso para la misma API:

1. **Exponer una API** en el mismo registro: URI de id. de aplicación
   `api://<client-id>` y un ámbito `access_as_user`.
2. Añadir la plataforma **Android** con el nombre de paquete de la APK y
   la huella SHA-1 de la clave de firma.
3. Conceder consentimiento de administrador para el ámbito.

## 3. MFA obligatoria

La aplicación **no** sustituye a la política de acceso condicional:

1. **Protección → Acceso condicional → Nueva directiva**.
2. Usuarios: grupo del personal técnico.
3. Aplicaciones: `ControClient`.
4. Controles: **Requerir autenticación multifactor**.
5. Habilitar la directiva y comprobar el registro de inicios de sesión.

La MFA se comprueba además en el arranque del servidor mediante el claim `amr`
(`Authentication:RequireMfaClaim`). Si el tenant no emite ese claim, desactívalo,
pero mantén la política de acceso condicional.

## 4. Configuración local

```powershell
dotnet user-secrets set "Authentication:Mode" "Entra" --project src/ControClient.Web
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>" --project src/ControClient.Web
dotnet user-secrets set "AzureAd:ClientId" "<client-id>" --project src/ControClient.Web
```

Y el primer administrador, para que el primer inicio de sesión no entre como lectura:

```powershell
dotnet user-secrets set "Authentication:BootstrapAdminObjectId" "<object-id del usuario>" --project src/ControClient.Web
```

El `object-id` se obtiene en **Entra ID → Usuarios → (usuario) → Identificadores de objeto**.

## 5. Certificado y nombre

Ver ADR-011 de la especificación: nombre interno con DNS dentro de la VPN y
certificado de CA pública con validación DNS-01.

## 6. Verificación

1. Arranca con `Authentication:Mode=Entra`.
2. Abre `https://<host>/` y comprueba que redirige a Microsoft.
3. Inicia sesión con la cuenta del primer administrador: debe entrar como
   `Administrator` (revisar en `/api/v1/system/info`).
4. Comprueba que un usuario sin rol entra como `ReadOnly` y no ve clientes.
5. Revisa `/api/v1/audit/events` para ver los accesos.

## Errores frecuentes

| Síntoma | Causa |
| --- | --- |
| `AADSTS50011` | URI de redirección no registrada exactamente (incluye esquema y puerto) |
| `AADSTS700054` | Falta consentimiento del ámbito de la API |
| Entra como `ReadOnly` | Falta `Authentication:BootstrapAdminObjectId` o el `oid` no coincide |
| Bucle de login | Cookies bloqueadas por `SameSite`/HTTPS: usa HTTPS también en pruebas |
| Arranque falla con «Configuración incompleta» | El servidor no arranca a propósito si `TenantId` o `ClientId` siguen en `PENDIENTE` |
