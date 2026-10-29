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

**Sí hace falta credencial de cliente.** Blazor Server es un cliente confidencial:
el canje del código en el servidor exige secreto o certificado. En
**Certificados y secretos → Nuevo secreto de cliente** crea uno (6–12 meses) y
copia el **Valor** en ese momento; será `AzureAd:ClientSecret`. En producción es
preferible un **certificado** (`AzureAd:ClientCertificates`), porque el secreto
caduca y hay que rotarlo.

En **Autenticación → Configuración de plataforma → Web**, añade también
`https://<host>/signout-callback-oidc`.

### Desarrollo con localhost

`http://localhost` es una excepción admitida por Microsoft y **el puerto no se
compara**, así que registra las rutas sin puerto y sirve para cualquier puerto:

```
http://localhost/signin-oidc
http://localhost/signout-callback-oidc
```

No registres varios localhost que solo cambien el puerto: Entra elegiría uno al
azar. Arranca con `dotnet run --project src/ControClient.Web --urls http://localhost:5080`.
Si el portal rechaza HTTP en la plataforma Web, usa `dotnet dev-certs https --trust`
y registra `https://localhost/signin-oidc`. No uses la plataforma «Móvil y
escritorio» para este caso: convertiría la aplicación en cliente público.

Cuando pases a producción, registra el host interno por HTTPS y elimina las dos
rutas de localhost (o mantén un registro aparte `ControClient-Dev`).

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
dotnet user-secrets set "AzureAd:ClientSecret" "<valor-del-secreto>" --project src/ControClient.Web
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
| `AADSTS50011` | URI de redirección no registrada exactamente |
| `AADSTS700054` | La app pide `id_token`: debe pedir `code` (la aplicación ya lo fuerza) |
| `message.State is null` | Cookie de correlación perdida: usa HTTPS o `response_mode=query` (ya configurado en Development) |
| Entra como `ReadOnly` | Falta `Authentication:BootstrapAdminObjectId` o el `oid` no coincide |
| Bucle de login | Cookies bloqueadas por `SameSite`/HTTPS: usa HTTPS también en pruebas |
| Arranque falla con «Configuración incompleta» | El servidor no arranca a propósito si faltan `TenantId`, `ClientId` o la credencial |

## Identificadores y MFA: comprobación real

En **Development** existe `GET /api/v1/system/claims`, que devuelve los tipos de claim
con valores enmascarados. Sirve para diagnosticar sin exponer datos.

Lecciones verificadas contra el tenant real:

1. **El identificador estable es `objectidentifier` (GUID de 36).**
   `nameidentifier` es el claim `sub` (43 caracteres, identificador por aplicación) y
   **no** debe usarse como clave: la aplicación ya prioriza el `oid`.
2. **`BootstrapAdminObjectId` es el GUID del usuario**, no el `sub` ni el id de la
   aplicación. Solo se aplica al crear la fila; si el usuario ya existe como
   `ReadOnly`, hay que cambiar el rol en la base de datos o borrar la fila.
3. **Si el tenant no emite `amr` ni `acrs`**, la MFA no se puede comprobar en el
   token: la garantía es el acceso condicional. En ese caso
   `Authentication:RequireMfaClaim` debe ser `false`; en producción, valora añadir
   claims opcionales o aceptar la política como control único y documentarlo.
