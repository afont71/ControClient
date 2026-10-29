# Especificación de gestión de clientes IT y bóveda de credenciales

**Versión:** 1.0.0 · **Archivo:** `ESPECIFICACION_GESTION_CLIENTES_IT_v1.0.md`  
**Fecha:** 29/10/2026 · **Organización:** Ideo Networks SL  
**Sustituye a:** v0.6.12 (conservada como histórico) y v0.5.0 (intacta)  
**Estado:** **aprobada para desarrollo** el 29/10/2026. La producción queda supeditada a la puerta de AC-23 (expediente RGPD) y a la firma del revisor técnico.  
**Clasificación:** documentación interna. No contiene valores de contraseñas, tokens ni claves privadas.

---

## Índice

- [0. Resumen y alcance](#0-resumen-y-alcance)
- [1. Control documental](#1-control-documental)
- [2. Requisitos funcionales](#2-requisitos-funcionales)
- [3. Permisos y autorización](#3-permisos-y-autorización)
- [4. Catálogo contrastado](#4-catálogo-contrastado)
- [5. Diseño responsive web y Android](#5-diseño-responsive-web-y-android)
- [6. Arquitectura, identidad y protección](#6-arquitectura-identidad-y-protección)
- [7. Requisitos no funcionales](#7-requisitos-no-funcionales)
- [8. Cumplimiento y protección de datos](#8-cumplimiento-y-protección-de-datos)
- [9. Migración única](#9-migración-única)
- [10. Procedimiento de migración](#10-procedimiento-de-migración)
- [11. Secretos y documentos mixtos](#11-secretos-y-documentos-mixtos)
- [12. Rollback y recuperación](#12-rollback-y-recuperación)
- [13. Modelo de datos, trazabilidad y API](#13-modelo-de-datos-trazabilidad-y-api)
- [14. Criterios de aceptación](#14-criterios-de-aceptación)
- [15. Matriz de trazabilidad](#15-matriz-de-trazabilidad)
- [16. Plan de proyecto](#16-plan-de-proyecto)
- [17. Decisiones pendientes (ADR)](#17-decisiones-pendientes-adr)
- [18. Historial de versiones](#18-historial-de-versiones)
- [19. Evidencias y comprobaciones](#19-evidencias-y-comprobaciones)
- [Anexo A. Catálogo de clientes](#anexo-a-catálogo-de-clientes)
- [Anexo B. Identidad visual](#anexo-b-identidad-visual)

---

## 0. Resumen y alcance

### 0.1 Resumen ejecutivo

Ideo Networks SL gestiona la infraestructura, los servicios y los proyectos de 82 carpetas de cliente (37.677 archivos, 57,5 GB) repartidas en OneDrive, sin un inventario consultable, con credenciales dispersas en archivos y con el conocimiento técnico concentrado en personas. Esta especificación define la aplicación interna que sustituye esa situación: un servidor local propio, con base de datos, API, bóveda cifrada de credenciales, aplicación web y APK Android, autenticación corporativa Microsoft 365 y una migración única, auditable y reversible desde OneDrive.

El documento no desarrolla ni despliega la aplicación. Define qué debe construirse, con qué límites, cómo se verificará y qué decisiones siguen abiertas.

### 0.2 Alcance

| Incluido | Detalle |
| --- | --- |
| Inventario de clientes | Identidad, alias, sedes, contactos, estado comercial validado y responsables. |
| Fichas técnicas dinámicas | Plantillas versionadas con campos tipados, relaciones y adjuntos. |
| Bóveda de credenciales | Custodia cifrada, revelado y copia autorizados y auditados. |
| Documentación | Metadatos, versiones, procedencia y vista segura de documentos. |
| Plataformas | Aplicación web responsive y APK Android sobre la misma API. |
| Identidad | Microsoft Entra ID con MFA de Microsoft Authenticator. |
| Auditoría | Registro de consultas, revelados, copias, cambios y administración. |
| Migración | Una campaña única desde el catálogo y la carpeta de clientes. |

### 0.3 Fuera de alcance (v1)

- Sustituir a Microsoft 365, la gestión de tickets, la facturación o el ERP del cliente.
- Monitorización en tiempo real de equipos, agentes instalados o RMM.
- Ejecución remota, escritura o cambios automatizados en sistemas de clientes.
- Rotación automática de credenciales en sistemas externos (MIG-14 continúa aplicando).
- Sincronización permanente con OneDrive (MIG-01).
- Portal de autoservicio para clientes externos.
- Aplicación iOS en v1; se reevaluará tras la APK Android.
- Tema oscuro, multiidioma y firma electrónica de documentos.
- Gestión de identidades de clientes (alta de usuarios en sus sistemas).
- Custodia de claves privadas de clientes como servicio de confianza cualificado.

### 0.4 Supuestos y dependencias

1. Existe un tenant de Microsoft 365 con Entra ID y licencias que permiten MFA por Authenticator.
2. Hay un servidor local disponible, con virtualización o equipo dedicado, copia de seguridad y conexión estable.
3. Dirección designa un responsable de datos, un custodio de bóveda y un responsable de aceptación nombrados.
4. Los clientes aceptan que Ideo Networks custodie credenciales de sus sistemas, con contrato de encargado de tratamiento.
5. La carpeta de OneDrive se facilitará como copia o montaje de solo lectura para la campaña de migración.
6. El equipo de desarrollo es interno o de confianza, con acceso controlado al repositorio.
7. No se requiere acceso desde fuera de la Unión Europea ni transferencias internacionales de datos.

Si un supuesto deja de cumplirse, la especificación afectada se revisa y se registra en el historial.

### 0.5 Glosario

| Término | Definición |
| --- | --- |
| Carpeta | Directorio del origen (OneDrive). No implica un cliente distinto. |
| Cliente | Identidad empresarial canónica; puede agrupar varias carpetas. |
| Sede | Ubicación física o lógica de un cliente. |
| Ficha | Instancia de una plantilla versionada con campos tipados. |
| Plantilla | Definición de secciones, campos, tipos, validaciones y permisos. |
| Registro | Cualquier entidad persistida (cliente, sede, equipo, servicio, documento…). |
| Bóveda | Componente cifrado que custodia secretos y adjuntos sensibles. |
| Secreto | Contraseña, clave, token, cadena de conexión o certificado con clave. |
| Revelado | Acción explícita y auditada de mostrar un secreto. |
| Copia | Acción de llevar un secreto al portapapeles, auditada y temporal. |
| Referencia secreta | Campo de ficha que apunta a la bóveda, nunca al valor. |
| Manifiesto | Inventario inmutable de archivos de origen con hashes y estado. |
| Lote | Conjunto acotado de trabajo dentro de una campaña de migración. |
| Campaña | Ejecución completa y aprobada de la migración única. |
| Contenido no disponible | Marcador de OneDrive no hidratado; nunca «archivo vacío». |
| RTM | Matriz de trazabilidad requisito → prueba. |
| ADR | Registro de decisión de arquitectura. |

### 0.6 Criterios de éxito

1. El 100 % de las entradas del manifiesto tiene disposición final (AC-18).
2. Ningún secreto aparece en logs, búsquedas, exportaciones ni contexto de IA (AC-09).
3. Un usuario sin permiso no obtiene datos, nombres ni conteos de otro cliente (AC-05).
4. La migración se simula sin escrituras en producción y se revierte con ensayo previo (AC-13, AC-16).
5. La consulta de un cliente, su ficha y su documentación se resuelve en menos de un segundo en el percentil 95 (NFR-03).
6. El sistema funciona con copia de seguridad verificada y restauración probada (AC-20).

---

## 1. Control documental

### 1.1 Disponibilidad de la versión anterior

La conversación «Asignar carpeta de referencia», identificador `6ae30dfa-a218-83ed-99b4-c8df0c58c932`, menciona `ESPECIFICACION_GESTION_CLIENTES_IT_v0.4.md` (993 líneas), pero el archivo generado no se ha podido recuperar. No estaba entre los adjuntos accesibles ni se ha encontrado en el espacio de trabajo. Los enlaces históricos `sandbox:/mnt/data/...` no son archivos locales disponibles.

Por tanto, **esta entrega no afirma reproducir ni conservar literalmente las 993 líneas de v0.4**. Conserva los requisitos e historial recuperables, el desarrollo de v0.5 y las adiciones de v0.6. La v0.4 sigue siendo referencia para cualquier contenido no recuperado: nada de esta entrega pretende derogarlo por omisión.

Cuando esté disponible el original, realizar una comparación por secciones y requisitos, mantener íntegros sus anexos e historial, aplicar las adiciones de esta revisión y documentar cada sustitución explícita. No sobrescribir ni borrar ninguna versión anterior.

### 1.2 Fuentes utilizadas y límites

| Fuente | Disponibilidad y uso |
| --- | --- |
| Conversación de referencia | Mensajes recuperados; sustenta continuidad funcional e historial resumido. |
| `CATALOGO-CLIENTES.md` | Leído en v0.5; conservado como anexo A con SHA-256. |
| `catalogo-clientes.xlsx` | Leído en v0.5; cinco hojas contrastadas y recuentos verificados en v0.6. |
| [Sitio de Ideo Networks](https://ideonetworks.com/es) | Inspección visual, DOM, estilos calculados y CSS el 29/10/2026. |
| `C:\Users\koto\OneDrive - Ideo Networks SL\Clientes` | Ruta declarada. **No se ha abierto, enumerado, sincronizado ni modificado.** |
| `ESPECIFICACION_GESTION_CLIENTES_IT_v0.5.md` | Documento base de esta revisión; SHA-256 en §19. |

Los adjuntos aportan metadatos y descripciones. No prueban por sí solos la exactitud de una configuración, la vigencia de un acceso ni la presencia de una contraseña en un documento original. No se han extraído secretos de los archivos fuente.

### 1.3 Convenciones

- **Heredado:** requisito recuperado de versiones anteriores.
- **Nuevo v0.6:** requisito o decisión incorporada en esta revisión.
- **Observado:** dato contrastado en adjuntos o sitio, con procedencia.
- **Propuesto:** decisión de esta revisión pendiente de validación de producto.
- **Pendiente:** decisión abierta registrada en §17.
- Cada requisito tiene identificador estable (`BASE`, `UI`, `NFR`, `SEC`, `PRM`, `MIG`, `DAT`, `RGPD`) y una prueba asociada en §15.
- Los números de sección citados dentro del documento se refieren a esta versión 0.6.
- Las cifras de tamaño se expresan en MB decimales (10⁶ bytes) salvo indicación expresa; el manifiesto usará bytes.

**Formato de requisito.** Todo requisito se presenta en tabla con columnas `ID | Requisito | Prioridad | Estado | Verificación`. Prioridad: `P0` (bloqueante), `P1` (necesario para v1), `P2` (mejora posterior). Estado: `Heredado`, `Nuevo`, `Pendiente`. Verificación: identificador de criterio de aceptación o método documental.

### 1.4 Registro de cambios v0.5 → v0.6

| Sección v0.6 | Origen en v0.5 | Cambio |
| --- | --- | --- |
| 0 | — | **Nuevo.** Resumen ejecutivo, alcance, fuera de alcance, supuestos, glosario y criterios de éxito. |
| 1 | §1 | Añadidos registro de cambios, mapa de secciones y aprobación. |
| 2 | §2 | Requisitos BASE en tabla con prioridad, estado y verificación. |
| 3 | — | **Nuevo.** Roles y matriz de permisos (PRM-01…PRM-14). Resuelve el hueco de SEC-06. |
| 4 | §3 | Conciliación 106/126/94 explicada; nota del 0,1 MB; §3.2 y §3.3 conservadas. |
| 5 | §5 | Breakpoints de prueba alineados a 320 px (UI-10); «Documentación» unificado. |
| 6 | §6 | Bóveda renombrada a referencia funcional; nuevos §6.4 (claves) y §6.5 (observabilidad). |
| 6.6 | — | **Nuevo (v0.6.2).** Acceso remoto por OpenVPN en la Dream Machine y aislamiento de red (SEC-15…SEC-18). |
| 7 | — | **Nuevo.** Requisitos no funcionales NFR-01…NFR-16. |
| 8 | — | **Nuevo.** Cumplimiento y protección de datos (RGPD-01…RGPD-12). |
| 9 | §7 | MIG-01…MIG-10 conservados; protección del origen y roles sin cambios sustantivos. |
| 10 | §8 | Fases 0–8 conservadas con salidas y puertas explícitas. |
| 11 | §9 | MIG-11…MIG-14 conservados. |
| 12 | §10 | MIG-15 y MIG-16 conservados. |
| 13 | §11 | Modelo y trazabilidad conservados; §13.3 y §13.4 nuevos (API y errores/límites). |
| 14 | §12 | AC-01…AC-18 conservados; AC-19…AC-31 añadidos para cubrir requisitos nuevos. |
| 15 | — | **Nuevo.** Matriz de trazabilidad requisito → prueba. |
| 16 | §13 (parcial) | **Nuevo.** Fases de proyecto, hitos, equipo, estimación y riesgos. |
| 17 | §13 (parcial) | **Nuevo.** ADR-001…ADR-010 con opciones y recomendación. |
| 18 | §14 | Historial actualizado a 0.6.0. |
| 19 | §15 | Hashes de v0.5 y de los adjuntos; comprobaciones repetidas en v0.6. |
| Anexo A | Anexo A de v0.5 (líneas 548–724) | **Extraído** a fichero propio con hash; aquí solo referencia y resumen. |
| Anexo B | §4 | Identidad visual movida a anexo para que el cuerpo quede normativo. |

Ninguna sección de v0.5 se elimina sin sustitución equivalente; la correspondencia se detalla arriba.

### 1.5 Aprobación

| Rol | Nombre | Fecha | Estado |
| --- | --- | --- | --- |
| Autor de la especificación | Responsable del proyecto | 29/10/2026 | **Aprobada para desarrollo** |
| Revisor técnico | — | — | Pendiente (antes de producción) |
| Responsable de datos | Titular de la dirección del proyecto (nombre por confirmar) | 29/10/2026 | **Asignado** |
| Custodio de bóveda | Dirección (jefe del responsable del proyecto), custodio del sobre de recuperación | 29/10/2026 | **Asignado** |
| Responsable de aceptación | Titular de la dirección del proyecto (nombre por confirmar) | 29/10/2026 | **Aprobada para desarrollo** |

**Aprobación v1.0.0 (29/10/2026) y condiciones:**

1. La aprobación alcanza **diseño y desarrollo**, incluido el entorno de ensayo.
2. **No autoriza** la migración real ni la carga de datos de clientes: eso exige el plan aprobado de MIG-09 y la puerta de AC-23.
3. **Puertas antes de producción:** expediente RGPD (AC-23) y firma del revisor técnico.
4. Pendiente administrativo: completar los nombres en la columna «Nombre».
5. Cualquier cambio de alcance o reapertura de un ADR cerrado exige nueva versión.

Asignación acordada el 29/10/2026: una misma persona asume **responsable de datos** y **responsable de aceptación**; el **custodio de bóveda** recae en dirección (jefe del responsable), persona distinta y por tanto compatible con SEC-12.

Decisión asociada: **no habrá custodio externo** (29/10/2026). El custodio guarda el **sobre de recuperación**, no partes de la clave: el desellado diario lo realiza cualquier administrador con MFA (SEC-14) y el sobre solo se usa en desastre, con acta y doble control.

La aprobación de esta especificación no autoriza la migración real: la campaña exige el plan concreto y la simulación aprobada de MIG-08 y MIG-09.

---

## 2. Requisitos funcionales

### 2.1 Requisitos heredados

| ID | Requisito | Prioridad | Estado | Verificación |
| --- | --- | --- | --- | --- |
| BASE-01 | Servidor local centralizado con base de datos propia, API y bóveda cifrada. | P0 | Heredado | AC-01, AC-15 |
| BASE-02 | Aplicación web y APK Android distribuible, conectadas al mismo servidor. | P0 | Heredado | AC-03, AC-31 |
| BASE-03 | Inicio de sesión corporativo Microsoft 365 con Entra ID y MFA por Microsoft Authenticator. Sustituye el segundo factor por correo. | P0 | Heredado | AC-06 |
| BASE-04 | Gestión de usuarios y permisos por cliente, módulo y operación; consultas, altas, modificaciones y bajas según autorización. | P0 | Heredado | AC-05, AC-21 |
| BASE-05 | Bóveda integrada de credenciales recuperables por usuarios autorizados, vinculadas a clientes, equipos y servicios. | P0 | Heredado | AC-09, AC-10, AC-22 |
| BASE-06 | Fichas dinámicas con configuraciones técnicas, nuevos tipos de ficha y campos añadibles sin modificar código. | P0 | Heredado | AC-04 |
| BASE-07 | Dentro de cada cliente solo se muestran fichas con información autorizada; el usuario con permiso puede crear una ficha inexistente. | P0 | Heredado | AC-04, AC-05 |
| BASE-08 | Auditoría de consulta, revelado, copia y modificación de información sensible, además de administración y cambios de permisos. | P0 | Heredado | AC-09, AC-26 |
| BASE-09 | Entidades: clientes, sedes, equipos, servidores, redes, VPN, firewalls, Microsoft 365, licencias, servicios, proyectos, documentos e historial. | P0 | Heredado | AC-01, AC-18 |
| BASE-10 | Búsqueda global y por cliente; conservación de vínculos y procedencia de documentos originales. | P1 | Heredado | AC-28 |
| BASE-11 | Cliente y carpeta son entidades diferentes; proyectos internos y carpetas duplicadas requieren clasificación. | P0 | Heredado | AC-29 |
| BASE-12 | La actividad documental no determina el estado comercial del cliente. | P0 | Heredado | AC-29 |
| BASE-13 | Biometría Android opcional para desbloqueo local, manteniendo autenticación corporativa y permisos de servidor. | P2 | Heredado | AC-06 |
| BASE-14 | Evolución hacia consulta asistida por IA con permisos, fuentes y exclusión de secretos; ninguna modificación autónoma sin autorización expresa. | P2 | Heredado | AC-09 |

### 2.2 Modelo funcional de las fichas

Una ficha es una instancia de una plantilla versionada. Una plantilla define nombre, secciones, campos, tipos, obligatoriedad, validaciones, orden y permisos. Los valores secretos son referencias a la bóveda, nunca valores de texto del formulario general.

Tipos previstos: texto, texto largo, número, booleano, fecha, opción, opciones múltiples, dirección IP, URL, correo, relación a entidad, documento y referencia secreta. Cada cambio de esquema tendrá versión y estrategia para registros existentes. Desactivar un campo no elimina sus valores históricos; borrar una plantilla en uso exige tratamiento explícito de sus instancias.

Una ficha se considera con información cuando contiene al menos un dato de negocio autorizado, relación, documento o referencia secreta accesible. `0` y `false` son valores válidos. Un título automático, un identificador o un campo vacío no cuentan. No se filtrarán nombres, pestañas ni contadores de fichas prohibidas. «Añadir ficha» se presenta como acción independiente solo para usuarios con permiso.

Las operaciones de baja serán archivado lógico por defecto. La eliminación definitiva tendrá permiso separado, tratamiento de relaciones y auditoría. Los conflictos de edición se resolverán por control de versión; no se sobrescribirá silenciosamente una modificación concurrente.

### 2.3 Módulos y responsabilidades

| Módulo | Información y operaciones |
| --- | --- |
| Clientes | Identidad, alias, estado comercial validado, contactos, sedes y responsables. |
| Infraestructura | Equipos, servidores, redes, direccionamiento, conexiones y configuración versionada. |
| Servicios | Microsoft 365, dominios, correo, VPN, licencias, proveedores, vencimientos y relaciones. |
| Proyectos | Trabajo técnico, web, ERP, diseño y otras actividades del catálogo. |
| Documentación | Metadatos, versiones, adjuntos gestionados, clasificación y procedencia. |
| Bóveda | Credenciales, claves y adjuntos sensibles cifrados; revelado y copia autorizados. |
| Administración | Usuarios, permisos, plantillas, campos, auditoría y parámetros. |
| Migración | Inventario, mapeos, revisión, simulación, ejecución, conciliación y reversión. |

---

## 3. Permisos y autorización

**Nuevo v0.6.** Cierra el hueco detectado en SEC-06: v0.5 enumeraba acciones pero no definía roles ni matriz. Todo permiso se evalúa en servidor; la interfaz solo oculta lo que ya está denegado.

### 3.1 Principios

1. **Denegación por defecto:** sin concesión explícita no hay acceso, ni siquiera a metadatos o recuentos.
2. **Alcance por cliente y sede:** un permiso existe dentro de un ámbito concreto, no global.
3. **Separación de deberes:** administrar usuarios no concede revelar secretos; aprobar una migración no concede ejecutarla.
4. **Mínimo privilegio y caducidad:** los permisos de migración y soporte son temporales y se revocan al cerrar.
5. **Sin escalada por interfaz:** ocultar un botón no sustituye a la comprobación de autorización (AC-05).
6. **Auditoría de denegaciones:** los intentos denegados se registran sin el valor secreto (SEC-09).

### 3.2 Roles

| Rol | Finalidad | Alcance habitual |
| --- | --- | --- |
| Administrador del sistema | Configuración, usuarios, plantillas, parámetros. | Global, sin revelado por defecto. |
| Responsable de cliente | Relación con el cliente, datos comerciales y documentación. | Clientes asignados. |
| Técnico | Fichas técnicas, equipos, servicios y uso de la bóveda. | Clientes asignados. |
| Auditor / cumplimiento | Consulta de registros y auditoría, sin modificar. | Global o por cliente, solo lectura. |
| Consulta | Lectura de fichas y documentos autorizados. | Clientes asignados. |
| Custodio de bóveda | Clasificación, revisión y custodia de secretos. | Bóveda, sin administración general. |
| Operador de migración | Ejecución de lotes aprobados. | Campaña y ventana temporal. |
| Responsable de aceptación | Aprobación de corte, conciliación, cierre o rollback. | Campaña. |

Un usuario puede acumular varios roles; la unión nunca supera lo permitido por cada uno. Los roles de migración no forman parte del catálogo habitual del técnico (MIG-09).

### 3.3 Matriz rol × acción

Leyenda: **✓** permitido dentro del alcance; **◐** condicional (permiso específico o doble control); **—** denegado por defecto.

| Acción (ID) | Admin. sistema | Resp. cliente | Técnico | Auditor | Consulta | Custodio | Operador mig. | Resp. aceptación |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `clients.view` (PRM-01) | ✓ | ✓ | ✓ | ✓ | ✓ | ◐ | ◐ | ◐ |
| `clients.edit` (PRM-02) | ✓ | ✓ | — | — | — | — | — | — |
| `records.view` (PRM-03) | ✓ | ✓ | ✓ | ✓ | ✓ | ◐ | ◐ | ◐ |
| `records.edit` (PRM-04) | ✓ | ✓ | ✓ | — | — | — | — | — |
| `records.delete` (PRM-05) | ◐ | — | — | — | — | — | — | — |
| `vault.metadata.view` (PRM-06) | — | — | ✓ | ◐ | — | ✓ | — | — |
| `vault.reveal` (PRM-07) | — | — | ✓ | — | — | ✓ | — | — |
| `vault.copy` (PRM-08) | — | — | ◐ | — | — | ✓ | — | — |
| `vault.edit` (PRM-09) | — | — | ✓ | — | — | ✓ | — | — |
| `vault.export` (PRM-10) | — | — | — | — | — | ◐ | — | — |
| `docs.view` (PRM-11) | ✓ | ✓ | ✓ | ✓ | ✓ | ◐ | ◐ | ◐ |
| `docs.edit` (PRM-12) | ✓ | ✓ | ✓ | — | — | — | — | — |
| `admin.users` (PRM-13) | ✓ | — | — | — | — | — | — | — |
| `admin.templates` (PRM-14) | ✓ | ◐ | ◐ | — | — | — | — | — |
| `audit.view` | ◐ | — | — | ✓ | — | ◐ | — | ◐ |
| `audit.export` | ◐ | — | — | ◐ | — | — | — | — |
| `migration.run` | — | — | — | — | — | — | ✓ | — |
| `migration.approve` | — | — | — | — | — | — | — | ✓ |
| `settings.manage` | ✓ | — | — | — | — | — | — | — |

Decisiones asociadas:

- **PRM-07/PRM-08:** el revelado y la copia se conceden por cliente y por servicio, con caducidad opcional y auditoría separada. La copia exige un permiso distinto del revelado.
- **PRM-06:** el administrador del sistema no ve metadatos de bóveda salvo concesión expresa; así se evita que el rol técnico se convierta en acceso total.
- **PRM-10:** exportar secretos es excepcional, exige doble control (custodio + responsable de aceptación) y queda auditado.
- **`records.delete`:** eliminación definitiva solo con permiso específico; el archivado lógico lo realiza `records.edit`.
- **`vault.reveal` del custodio:** orientado a revisión, no a operación diaria; cada revelado requiere motivo.

### 3.4 Reglas de alcance y excepciones

1. Los permisos se conceden sobre **cliente y sede**; un permiso global exige rol global y justificación registrada.
2. Las relaciones cruzadas (un equipo compartido por dos clientes) no amplían acceso: el usuario ve solo la parte del cliente donde tiene permiso.
3. Los permisos temporales llevan fecha de inicio y fin; caducan automáticamente y su renovación se audita.
4. La delegación (un técnico concede a otro) requiere el permiso que se delega y queda registrada; no se delega `admin.users` ni `vault.export`.
5. La revocación impide nuevas consultas desde web y APK según la política de invalidación de sesión (SEC-09).
6. El acceso de emergencia (break-glass) se define en §6.4 con doble control y ventana temporal.
7. Cambiar de rol no altera la autoría histórica: la auditoría conserva el rol vigente en el momento de la acción.

### 3.5 Controles compensatorios en plantillas pequeñas

**Nuevo v0.6.9.** Con una a tres personas no es posible separar todos los deberes de §3.3. Se acepta la concentración de roles y se compensa con controles verificables, en lugar de simular una separación que no existe.

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| PRM-15 | **No autoaprobación:** nadie aprueba su propio alta, cambio de rol ni ampliación de alcance. Toda concesión exige una segunda persona; si no la hay, queda registrada con motivo y se revisa en la revisión externa mensual. | P0 | AC-21 |
| PRM-16 | **Revisión externa mensual de la auditoría:** revelados, copias, exportaciones y cambios de permisos, con acta firmada y hallazgos registrados. Si un mes no hay actividad relevante, se documenta igualmente la revisión. | P1 | AC-21 |

Además:

1. El doble control se limita a lo grave: exportar secretos (PRM-10) y abrir la bóveda tras un reinicio (SEC-12, SEC-14).
2. **No hay custodio externo** (decisión del 29/10/2026): el custodio interno guarda el sobre de recuperación. Se compensa con acta, doble control en su uso, rotación tras usarlo y revisión mensual de la auditoría (PRM-16).
3. El rol de custodio de bóveda se asigna a una persona distinta del responsable de aceptación (§1.5): en este caso, dirección.
4. La concentración de roles se documenta como riesgo aceptado, con fecha y responsable, en §16.4.
5. La revisión externa no necesita ser técnica: basta comprobar que los revelados y los cambios de permisos tienen sentido y que no hay accesos fuera de horario sin motivo.

---

## 4. Catálogo contrastado

### 4.1 Recuentos y conciliación

| Comprobación | Resultado verificado en v0.6 | Localizador |
| --- | ---: | --- |
| Carpetas catalogadas | 82 | `Resumen!A6:P87` |
| Archivos atribuidos a esas carpetas | 37.677 | Suma de `Resumen!G6:G87` = `G88` |
| Filas de inventario, sin cabecera | 37.682 | `Inventario!A2:E37683` |
| Entradas de raíz | 4 | Filtrar `Inventario` por `(raíz)` |
| Entrada de estructura interna | 1 | Filtrar `Inventario` por `index` |
| Documentos clave | 197 | `Documentos clave!A5:E201` |
| Carpetas con documentos clave | 43 | Valores distintos de `Documentos clave!A5:A201` |
| Documentos ya indexados en una BD anterior | 194 | Suma de `Resumen!O6:O87` |
| Carpetas con documentos indexados | 37 | Valores positivos de `Resumen!O6:O87` |
| Marcas en columna «Credenciales» | 106 | Suma de `Resumen!L6:L87` |
| Archivos con indicios de secreto en el nombre (criterio ampliado) | 126 | Reproducido en v0.6 |
| Tamaño total publicado | 57.524,5 MB (≈ 57,5 GB) | `Resumen!H88` y cabecera |
| Suma de tamaños por carpeta | 57.524,4 MB | Suma de `Resumen!H6:H87` |

Conciliación de filas: `37.677 + 4 + 1 = 37.682`. Los recuentos por carpeta coinciden entre «Resumen» e «Inventario».

**Discrepancia de 0,1 MB (resuelta).** La suma de 82 valores ya redondeados a una décima difiere del total exacto en 0,05 MB. No es un error de datos: es acumulación de redondeo. El manifiesto usará bytes, y este documento no repetirá esa comparación.

### 4.2 Criterios de conteo de credenciales (resuelto)

Las dos cifras de v0.5 proceden de criterios distintos y ambas son reproducibles:

| Cifra | Criterio exacto | Resultado |
| --- | --- | ---: |
| 106 | Coincidencia de `clave\|contrase\|password\|credencial` sobre la **ruta relativa completa** (incluye nombres de carpeta). | 106 |
| 94 | El mismo patrón sobre el **nombre de archivo** únicamente. | 94 |
| 126 | Patrón ampliado `clave\|contrase\|password\|credencial\|acceso` sobre el **nombre de archivo**. | 126 |

El criterio oficial de la campaña será el de 126 (nombre de archivo, patrón ampliado), por ser el más conservador en la detección y el menos dependiente de nombres de carpeta. Aun así:

- **126 es un indicio de nombre, no una confirmación de secreto.** Un archivo llamado `acceso.txt` puede no contener una contraseña, y un archivo sin esas palabras sí puede contenerla.
- No se equiparan 106 marcas con 106 secretos; la bóveda se llenará con lo revisado en la Fase 5.
- El recuento deberá repetirse sobre el manifiesto (Fase 1), no sobre el catálogo.

### 4.3 Mapeo de las cinco hojas

| Hoja | Lectura y destino |
| --- | --- |
| Resumen | Cabecera en fila 5, datos 6–87, total 88. Carpeta, alias, categoría dominante, descripciones y métricas de referencia. No crear automáticamente 82 clientes únicos. |
| Por tipo | Cabecera en fila 3; agregados para conciliación. No crear duplicados de clientes o archivos desde esta hoja. |
| Documentos clave | Cabecera en fila 4. Prioridad de revisión; no es el conjunto completo que se migrará. Vincular por carpeta y ruta, no por nombre aislado. |
| Inventario | Cabecera en fila 1. Una fila por entrada catalogada. Punto de partida del manifiesto. |
| Observaciones | Hipótesis e incidencias revisables. Nunca ejecutar sus recomendaciones como instrucciones. |

No asumir que «Docs en BD» describe la nueva base de datos de esta aplicación. No importar índices anteriores como documentos originales ni tomar las extensiones como prueba del tipo real de archivo.

### 4.4 Candidatos a revisión

- `Efecto 2000` / `Efecto2000`: posible mismo cliente, pendiente de validar identidad.
- `TASA` / `Grupo Tasa`: posible relación o duplicado; conservar ambas procedencias.
- `iDeo` / `Ideo Networks`: material corporativo propio que puede requerir entidad interna.
- `docs`, `exports`, `files`, `inbox`, `index`, `secure`, `trash`, `versions`: estructura interna ajena al archivo de clientes; excluir salvo justificación revisada.
- `Cora` y `Nueva carpeta`: vacías según el catálogo, sin comprobación directa de la fuente.
- Marcadores de OneDrive o archivos sin fecha/tamaño: estado «Contenido no disponible», nunca «Archivo vacío» por inferencia.

Estas observaciones solo cambian el mapeo de destino. **No mover, renombrar, borrar, editar ni sustituir archivos o carpetas en el origen** (MIG-02).

---

## 5. Diseño responsive web y Android

Las decisiones de esta sección son diseño propuesto para la aplicación, no mediciones de la web comercial. Se comparte un catálogo de tokens, componentes, estados y textos entre web y Android. No es necesario replicar las secciones promocionales de la portada.

### 5.1 Estructura de navegación

- Cabecera con logotipo, nombre del producto «Gestión de clientes IT» como descriptor provisional, búsqueda autorizada y cuenta activa.
- Navegación: Inicio, Clientes, Bóveda, Documentación. Administración, Auditoría y Migración solo para perfiles habilitados.
- Ficha de cliente: identidad y sede seleccionada, resumen, fichas con datos, documentación e historial. La bóveda aparece únicamente con acceso autorizado.
- Una acción primaria contextual: «Nuevo cliente», «Añadir ficha» o «Guardar». Las acciones de baja se separan de guardar.
- La búsqueda no devuelve resultados, sugerencias, totales ni fragmentos de clientes no autorizados; nunca busca por el valor de una contraseña.

### 5.2 Reglas por anchura

| Ancho útil | Navegación y composición | Formularios y tablas |
| --- | --- | --- |
| 320–767 CSS px / móvil compacto | Cabecera compacta, menú lateral temporal; una columna. Android puede usar barra inferior para Clientes, Buscar y Bóveda. | Una columna de campos. Filas se convierten en tarjetas con etiquetas; tablas técnicas extensas en región de scroll claramente identificada. |
| 768–1023 CSS px / tableta | Panel de navegación contraíble; contenido principal flexible. | Dos columnas si cada campo conserva ancho legible; detalle y lista se alternan si no caben. |
| Desde 1024 CSS px / escritorio | Navegación lateral persistente propuesta de 240 px, cabecera y contenido flexible. | Lista y detalle en paralelo cuando el ancho lo permita; formularios de dos columnas y tablas paginadas. |

En Android se evalúa el ancho disponible en dp y el escalado del sistema; no equiparar píxeles físicos con CSS px. Los puntos de corte son criterios del producto, ajustables tras pruebas. No detectar tamaño por marca de dispositivo ni por agente de usuario.

### 5.3 Tokens de aplicación propuestos

| Token | Decisión v0.6 |
| --- | --- |
| Tipos de letra | Montserrat 600 en títulos; Inter 400/600 en cuerpo y controles. |
| Título de pantalla | 24 px compacto, 28–32 px amplio; línea 1,2. Android: equivalentes en sp con escalado. |
| Cuerpo | 16 px / 24 px; metadatos 14 px / 21 px. No reducir toda la interfaz para encajar tablas. |
| Espaciado | Escala 4, 8, 12, 16, 24, 32 px; margen compacto 16 px, amplio 24–32 px. |
| Controles | Alto táctil mínimo de producto 44 CSS px en web; 48 dp en Android. |
| Bordes y radios | Radio 6 px en botones y campos, 8 px en paneles; líneas de separación discretas. |
| Anchura de lectura | Formularios con ancho máximo propuesto 1120 px; tablas pueden usar más espacio. |
| Tema | Claro en v1. Un eventual tema oscuro requerirá paleta propia validada. |
| Movimiento | Transiciones breves; sin animación decorativa continua; respetar movimiento reducido. |

### 5.4 Pantallas y estados

| Pantalla | Contenido y comportamiento exigidos |
| --- | --- |
| Acceso | Logotipo, descriptor y «Acceder con Microsoft 365». Errores comprensibles; MFA mediante flujo corporativo, sin formulario que recoja la contraseña de Microsoft. |
| Inicio | Clientes recientes autorizados, pendientes de revisión propios y avisos relevantes. No copiar cifras comerciales del sitio. |
| Clientes | Búsqueda, filtro por estado comercial validado y categoría, paginación, alta según permiso. Nombre, alias y sede relevantes. |
| Cliente | Resumen y fichas existentes; acción para añadir otras. Indicar procedencia y última validación sin confundir fecha del archivo con vigencia técnica. |
| Ficha / configuración | Campos tipados, relaciones, validaciones junto al campo y estado de guardado. Comparación de versiones sin valores secretos. |
| Bóveda | Lista de metadatos autorizados; secreto oculto, revelado/copia explícitos y temporales, indicación de permiso y auditoría. Referencia funcional en §6.3. |
| Documentación | Clasificación, versión, origen, estado de revisión y vista segura; documentos sensibles usan un circuito protegido. |
| Revisión de migración | Fuente redactada y propuesta comparables; conflictos, asociación a cliente, aprobar/rechazar y motivo. |
| Administración | Permisos, plantillas, campos y auditoría con el mismo sistema visual. |

Estados obligatorios: cargando, sin datos, sin resultados, sin permiso, sin conexión, sesión caducada, validación fallida, conflicto de edición, guardado correcto, fichero en cuarentena y migración fallida. «Sin datos» no revela la existencia de registros prohibidos. Ante pérdida de red no mostrar «Guardado» sin confirmación del servidor.

### 5.5 Accesibilidad y aceptación visual

Objetivo de producto: WCAG 2.2 AA para la web; flujos equivalentes accesibles mediante TalkBack en Android. Contraste de texto normal ≥ 4,5:1, texto grande ≥ 3:1 y componentes/foco ≥ 3:1 cuando corresponda. Navegación completa por teclado, foco visible y no tapado, etiquetas persistentes, errores asociados a sus campos y lectura en orden lógico. El color nunca será la única señal. Referencia: [WCAG 2.2](https://www.w3.org/TR/WCAG22/).

Comprobar reflujo a 320 CSS px, zoom y texto al 200 %, orientación horizontal y teclado virtual. Los contenidos largos deben ajustar línea o permitir desplazamiento local sin desplazar horizontalmente toda la página. Las excepciones de tablas complejas se documentan y ofrecen detalle por registro.

**UI-10 — Pruebas visuales.** Comparar tokens y activo de marca con el anexo B; verificar capturas de acceso, clientes, ficha, formulario, bóveda y error en **320**, 360, 390, 768, 1024 y 1440 CSS px (se añade 320 para cubrir el mínimo declarado). En Android verificar un teléfono compacto y una tableta, texto ampliado, teclado visible, rotación y TalkBack. Son pruebas futuras de la aplicación; esta revisión solo inspecciona la referencia corporativa.

**UI-11 — Nomenclatura.** Los módulos y la navegación usan los mismos nombres: «Documentación» (no «Documentos») y «Bóveda» (no «Password Safe» como producto).

---

## 6. Arquitectura, identidad y protección

### 6.1 Componentes

Arquitectura propuesta: interfaz web y APK Android → API autenticada del servidor local → base de datos relacional propia, servicio de bóveda y repositorio documental gestionado.

| Capa | Decisión v0.6 | Estado |
| --- | --- | --- |
| Base de datos | **PostgreSQL**, versión estable soportada en el momento de la implantación. | **Decidido (ADR-004)** |
| API | ASP.NET Core, servicio único versionado, autenticación Entra ID, autorización por recurso. | **Decidido (ADR-005)** |
| Bóveda | Servicio propio con cifrado por envoltura sobre biblioteca contrastada. | Pendiente (ADR-001) |
| Documentos | Sistema de ficheros cifrado con rutas derivadas de hash; metadatos, hash y clasificación en la BD. | **Decidido (ADR-003)** |
| Búsqueda | PostgreSQL FTS + `pg_trgm` detrás de la API; sin servicio adicional en v1. | **Decidido (ADR-007)** |
| Web | Blazor Server sobre ASP.NET Core, servida por el mismo servidor. | **Decidido (ADR-005)** |
| Android | Kotlin + Jetpack Compose, APK firmada distribuida de forma controlada. | **Decidido (ADR-006)**; distribución pendiente (ADR-008) |
| Acceso remoto | VPN OpenVPN en la UniFi Dream Machine, red aislada que solo alcanza el servidor en 443. | **Decidido (ADR-002)** |

Reglas:

- Los registros estructurados, permisos, relaciones y procedencia residen en la BD.
- Los documentos voluminosos podrán residir en almacenamiento propio cifrado, con identificadores y hashes registrados en la BD.
- **Nunca** un enlace a OneDrive será el único contenido de un documento declarado «migrado». Un archivo solo referenciado quedará identificado como tal y fuera de la cobertura de copia.
- Web y Android aplican las mismas reglas de negocio en servidor. Solo la API accede a BD y bóveda.
- Ninguna credencial de BD ni clave maestra estará incluida en la APK ni en el JavaScript del navegador.

### 6.2 Microsoft 365 y Android

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| SEC-01 | Identidad por tenant corporativo Entra ID e identificador estable de usuario, no solo por correo mutable. Validar emisor, audiencia, firma, caducidad y tenant. Denegación por defecto y autorización en cada operación y archivo. | P0 | AC-06 |
| SEC-02 | MFA con Microsoft Authenticator mediante políticas del tenant. La mera cuenta Microsoft 365 no acredita MFA exigido: verificar configuración efectiva, métodos, políticas y licencias antes de producción. No reintroducir correo como segundo factor. | P0 | AC-06 |
| SEC-03 | Bibliotecas mantenidas para OIDC y flujo de código de autorización con PKCE en clientes públicos, sin secretos embebidos. Registros de aplicación y redirecciones apropiados para web y Android. | P0 | AC-06 |
| SEC-04 | Biometría opcional para desbloquear material local protegido por Android Keystore; no sustituye a Entra, MFA ni sesión caducada. Borrado de datos temporales al cerrar sesión y bloqueo en segundo plano. Sin bóveda offline en v1. | P1 | AC-06 |

Referencias: [MSAL](https://learn.microsoft.com/en-us/entra/identity-platform/msal-authentication-flows) y [código de autorización](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-authorization-code-flow).

### 6.3 Bóveda y auditoría

**Aclaración v0.6 sobre «Password Safe».** En v0.5 el título de la especificación nombraba «bóveda Password Safe». Se aclara que **Password Safe se toma como referencia funcional de interfaz**, no como producto elegido: la bóveda debe ofrecer su modelo mental (árbol de grupos, lista, ficha de credencial, generador, revelado temporal, autobloqueo y copia con limpieza), pero implementado sobre la arquitectura de servidor de §6.1. El motor definitivo se decide en ADR-001.

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| SEC-05 | Cifrado autenticado de secretos y adjuntos sensibles con biblioteca o servicio de bóveda contrastado. Cifrado por envoltura: claves de datos separadas de la clave de protección, fuera de la BD y de sus copias ordinarias. Algoritmo, tamaños, nonces, custodia, rotación y recuperación se aprueban por diseño de amenazas y pruebas antes de producción. | P0 | AC-22 |
| SEC-06 | Permisos separados para ver metadatos, revelar, copiar, crear, editar, archivar y exportar secretos; matriz en §3.3. Ser administrador de usuarios no concede revelado. Acceso administrativo de emergencia con procedimiento y auditoría específicos. | P0 | AC-21 |
| SEC-07 | Valores secretos excluidos de logs, URLs, métricas, mensajes de error, historial ordinario, miniaturas, índice de búsqueda, exportaciones y contexto de IA. Versiones de secretos cifradas. No calcular hashes públicos de contraseñas de baja entropía. | P0 | AC-09 |
| SEC-08 | El servidor audita autorización y entrega de revelado/copia; no garantiza el uso posterior del portapapeles. La interfaz oculta el valor tras un plazo configurado, registra la acción y limpia el portapapeles cuando la plataforma lo permita sin borrar contenido ajeno. Android protege pantallas sensibles y la vista de aplicaciones recientes; documentar límites de captura del navegador. | P0 | AC-09, AC-26 |
| SEC-09 | Auditoría con actor, cliente, entidad, operación, resultado, fecha, correlación y motivo. Historial resistente a alteraciones, con control de acceso y copias independientes, sin payload secreto. Revocar cuenta o permiso impide nuevas consultas desde web y APK según política de invalidación definida y probada. | P0 | AC-26 |

**Funcionalidad de interfaz exigida a la bóveda (referencia tipo Password Safe):**

1. Árbol de grupos y lista filtrable por cliente, servicio, tipo y caducidad; búsqueda por metadatos, nunca por valor secreto.
2. Ficha con usuario, URL, notas no secretas, etiquetas, referencia al sistema y relación con cliente/equipo.
3. Generador de contraseñas con longitud y juego de caracteres configurables y política por defecto.
4. Revelado con cuenta atrás visible y ocultación automática; copia con aviso de limpieza.
5. Historial de versiones del secreto cifrado, sin exponer valores anteriores en la ficha.
6. Indicador de antigüedad y caducidad de la credencial, con lista de revisión (no rotación automática).
7. Autobloqueo por inactividad y bloqueo manual; cierre de sesión invalida el desbloqueo local.
8. Adjuntos sensibles opcionales (certificados, ficheros de configuración) con el mismo cifrado.
9. Ninguna búsqueda, ordenación ni exportación puede exponer longitudes, prefijos ni fragmentos del secreto.

### 6.4 Gestión de claves y recuperación

**Revisado v0.6.12.** Modelo decidido el 29/10/2026 (ADR-009): la clave maestra se guarda **cifrada por Windows (DPAPI)** y solo se libera a un **administrador autenticado con MFA**. Sustituye el reparto 3 de 5 de v0.6.1, que exigía dos personas en cada reinicio.

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| SEC-10 | Cifrado por envoltura: una clave de datos (DEK) por elemento o por grupo, envuelta con una clave maestra (KEK) que no reside en la BD, en sus copias ni en el repositorio de código. | P0 | AC-22 |
| SEC-11 | **Custodia de la KEK con DPAPI:** se guarda cifrada con la protección de datos de Windows, ligada al equipo y a la cuenta del servicio. El fichero copiado a otra máquina no es utilizable. La aplicación solo la descifra tras validar token de Entra, cumplimiento de MFA y rol de administrador de la aplicación, y nunca la escribe en logs, volcados, base de datos ni copias. | P0 | AC-22 |
| SEC-12 | **Sobre de recuperación:** en la constitución se genera una única copia sellada de la clave de recuperación, custodiada por el custodio de bóveda en caja fuerte o depósito mancomunado. Se usa solo si el servidor muere o se pierden las claves de Windows; su apertura exige acta, doble control y **rotación inmediata** de todas las claves. | P0 | AC-22 |
| SEC-13 | Rotación de KEK anual o ante sospecha, tras usar el sobre de recuperación o al cambiar de servidor, con reenvoltura de DEK sin reencriptar todos los secretos; rotación de DEK por elemento cuando el diseño lo permita. Toda rotación se audita y se ensaya. | P1 | AC-22 |
| SEC-14 | **Desellado por administrador:** cualquier usuario con rol de administrador de la aplicación y MFA válida puede desellar tras un reinicio. El evento se audita con actor, fecha, motivo y dirección IP; un técnico sin rol de administrador no puede desellar. | P0 | AC-22 |

**Modelo de desellado (ADR-009, cerrado).**

| Momento | Qué ocurre |
| --- | --- |
| Constitución (una vez) | Se genera la KEK, se sella con DPAPI, se crea el sobre de recuperación y se destruye cualquier copia en claro. |
| Arranque normal | La bóveda aparece sellada; un administrador entra con Entra + MFA y la aplicación libera la KEK del almacén de Windows. |
| Uso diario | Los técnicos revelan según su permiso; no desellan ni necesitan a nadie al lado. |
| Recuperación | Si el servidor muere: sobre sellado, acta, doble control y rotación de todas las claves al terminar. |
| Rotación | Anual o al cambiar de servidor; también tras usar el sobre. |

**Constitución de la bóveda (ceremonia única):**

1. Servidor de producción con TPM si existe (mejora prevista: sellado al TPM en lugar de solo DPAPI), aplicación instalada y bóveda vacía.
2. Un administrador genera la KEK; la aplicación la sella con DPAPI y muestra únicamente una huella de verificación (fingerprint), nunca la clave.
3. Se genera el sobre de recuperación, se precinta, se numera y se entrega con acta al custodio.
4. Prueba obligatoria en entorno aislado: restaurar copia de la BD, desellar con el sobre y comprobar que se descifra un secreto de prueba. Si falla, no se cargan secretos reales.
5. Acta de constitución con fecha, asistentes, huella de la clave y número del sobre.
6. Solo después se empiezan a cargar credenciales reales.

Reglas permanentes:

1. La KEK no se escribe nunca en texto claro ni se muestra en pantalla completa; solo su huella.
2. La copia de la BD no contiene claves recuperables; restaurar en otro servidor exige el sobre de recuperación.
3. El sobre de recuperación no se fotografía, no se copia y no sale del domicilio de custodia; su uso exige acta y dos personas.
4. Prueba anual documentada: restauración completa con el sobre en entorno aislado.
5. Al usar el sobre se genera un juego nuevo de claves y se destruye el anterior con acta.
6. La aplicación no expone la KEK por API y se prohíben los volcados de memoria en producción.
7. El proceso que descifra la KEK y el servicio de base de datos usan cuentas distintas.

**Riesgo residual aceptado (ADR-009, decisión del 29/10/2026):**

- **Un administrador solo puede desellar la bóveda.** No hay separación de deberes en el desellado; se compensa con limitación de administradores, auditoría de cada desellado, revisión mensual (PRM-16) y sobre de recuperación bajo doble control.
- **DPAPI protege contra robo de copias de la base de datos o del fichero de claves, no contra una imagen completa del disco** obtenida con acceso de administrador al servidor. El sellado al TPM elimina este riesgo y se adopta como mejora sin cambiar el diseño.
- Quien tenga a la vez el servidor y el sobre de recuperación compromete todos los secretos; por eso el sobre se custodia fuera del servidor y con doble control.

### 6.5 Observabilidad, registros y retención

**Nuevo v0.6.** v0.5 exigía auditoría pero no definía qué se registra, cuánto tiempo ni cómo se protege.

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| OBS-01 | Registro estructurado (JSON) con marca de tiempo, nivel, servicio, identificador de correlación y actor; sin secretos ni cuerpos completos de documentos. | P1 | AC-26 |
| OBS-02 | Auditoría funcional independiente del log técnico, en almacenamiento separado, con encadenamiento de integridad y acceso restringido a lectura. | P0 | AC-26 |
| OBS-03 | Métricas mínimas: latencia p50/p95 por endpoint, tasa de error, sesiones activas, revelados y copias por usuario, fallos de copia de seguridad, intentos de autorización denegados. | P1 | AC-26 |
| OBS-04 | Alertas: fallo de copia, error 5xx sostenido, picos de revelado o de copia, bloqueo de cuenta masivo, cambio de permisos fuera de horario. | P1 | AC-26 |
| OBS-05 | Retención propuesta: log técnico 30 días, métricas 90 días, auditoría 365 días en línea y 5 años en archivo frío. Plazos definitivos según §8 y validación jurídica. | P1 | AC-26 |

### 6.6 Acceso remoto y aislamiento de red

**Nuevo v0.6.2.** Decidido en ADR-002: el servidor no se expone a Internet; se llega por el servidor OpenVPN de la UniFi Dream Machine, que ya está configurado.

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| SEC-15 | Acceso remoto exclusivamente por el servidor OpenVPN de la Dream Machine, con una configuración `.ovpn` o certificado individual por dispositivo. Prohibido el perfil compartido, la cuenta genérica y la exposición directa de la API o de la base de datos a Internet. | P0 | AC-32 |
| SEC-16 | Red de VPN aislada: el firewall solo permite tráfico al servidor de la aplicación en el puerto 443 y bloquea el resto de la LAN, de otras VLAN y de la gestión del router. | P0 | AC-32 |
| SEC-17 | Configuración del túnel: TLS 1.2 mínimo (preferible 1.3), AES-256-GCM, `tls-crypt` o `tls-auth`, sin cifrados obsoletos; revocación por CRL al retirar un dispositivo. | P0 | AC-32 |
| SEC-18 | OpenVPN no aporta MFA y no se le atribuye: la identidad fuerte la exige Entra ID con Authenticator (SEC-02). El perfil VPN es un control de red, no de identidad; perder un dispositivo no debe permitir revelar secretos. | P0 | AC-06, AC-32 |
| SEC-19 | Certificado del servidor emitido por una CA pública mediante validación DNS-01, con renovación automática y alerta si falla. El nombre interno solo resuelve dentro de la VPN; la APK y el navegador no admiten excepciones ni certificados autofirmados en producción. | P0 | AC-33 |

Notas de operación:

1. Un perfil por persona y dispositivo; registro de entrega y retirada, coherente con §6.4.
2. La revocación se prueba: al retirar un perfil, la conexión debe fallar de inmediato.
3. El nombre interno del servidor se resuelve por DNS interno; la estrategia de certificado y nombre se decide en ADR-011.
4. La administración del router queda restringida y con firmware actualizado: es el único elemento expuesto de la infraestructura.
5. No se usa L2TP/PPTP aunque esté disponible, por considerarse obsoleto.
6. El certificado se renueva por DNS-01 y su caducidad se vigila con alerta (SEC-19, AC-33); una renovación fallida deja la aplicación inaccesible.

### 6.7 Distribución de la aplicación Android

**Nuevo v0.6.6.** Decidido en ADR-008: sin MDM, con APK firmada servida por HTTPS interno.

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| SEC-20 | APK firmada y distribuida por HTTPS interno (nunca por correo ni mensajería), con verificación de firma antes de instalar. La aplicación consulta `/api/v1/app/version` al arrancar y bloquea el uso si la versión instalada es inferior a la mínima obligatoria. | P0 | AC-31 |
| SEC-21 | La clave de firma se custodia fuera del repositorio, con copia sellada y procedimiento de sucesión documentado. Su pérdida impide publicar actualizaciones del mismo paquete y obliga a reinstalar en todos los dispositivos. | P1 | AC-31 |

Notas:

1. El bloqueo por versión mínima debe permitir un mensaje claro y una vía de actualización, no un error genérico.
2. La verificación de versión ocurre después de autenticar, para no revelar información a clientes no autorizados.
3. Si en el futuro se adopta Intune, SEC-20 y SEC-21 siguen vigentes; solo cambia el canal de distribución.

---

## 7. Requisitos no funcionales

**Nuevo v0.6.** Las cifras siguientes son propuestas de partida, marcadas como `Propuesto`, y se validan con dirección antes del desarrollo. Se medirán en el entorno de ensayo con un lote representativo.

| ID | Requisito | Objetivo propuesto | Verificación |
| --- | --- | --- | --- |
| NFR-01 | Disponibilidad en horario laboral (L-V, 08:00–19:00). | **99 % mensual** (ajustado el 29/10/2026 desde 99,5 % por servidor único con copias cada 4 h); mantenimientos planificados fuera de horario. | AC-19 |
| NFR-02 | RPO / RTO de la aplicación. | RPO ≤ 24 h (copia diaria cifrada) y RTO ≤ 8 h, con copia incremental cada 4 h. **RTO de bóveda:** tras un reinicio, un administrador con MFA la abre en minutos; si el servidor se pierde, el RTO depende de usar el sobre de recuperación (acta y doble control). | AC-20 |
| NFR-03 | Latencia de lectura. | Listados y búsqueda p95 < 500 ms; apertura de ficha < 800 ms; revelado de secreto < 300 ms con hasta 5.000 registros y 100.000 documentos indexados. | AC-19, AC-28 |
| NFR-04 | Volumen soportado. | Carga inicial 57,5 GB / 37.682 entradas; crecimiento esperado ≤ 20 % anual; soportar 200 GB sin cambio de arquitectura. | AC-19 |
| NFR-05 | Concurrencia. | **30 usuarios simultáneos** (ajustado el 29/10/2026 por margen de crecimiento), 5 escrituras por segundo en pico, sin degradación superior al 50 % del p95. | AC-19 |
| NFR-06 | Navegadores soportados. | Últimas dos versiones estables de Edge, Chrome, Firefox y Safari; sin soporte de Internet Explorer. | AC-03 |
| NFR-07 | Android. | Mínimo Android 10 (API 29); APK firmada, actualizaciones controladas; probada en un dispositivo compacto y una tableta. | AC-31 |
| NFR-08 | Localización. | Español (es-ES), fechas dd/mm/aaaa, zona Europe/Madrid, importes con coma decimal. Interfaz sin multiidioma en v1. | AC-03 |
| NFR-09 | Accesibilidad. | WCAG 2.2 AA en web; TalkBack equivalente en Android. | AC-30 |
| NFR-10 | Cifrado en tránsito. | TLS 1.2 mínimo (preferible 1.3) en toda comunicación; HSTS; sin cifrados obsoletos. | AC-19 |
| NFR-11 | Cifrado en reposo. | BD y almacén documental cifrados a nivel de volumen; secretos además cifrados a nivel de aplicación (§6.4). | AC-22 |
| NFR-12 | Copias de seguridad. | Estrategia 3-2-1: copia diaria completa cifrada, incremental cada 4 h, una réplica fuera del servidor; verificación mensual de restauración. | AC-20 |
| NFR-13 | Integridad documental. | SHA-256 registrado por documento importado; verificación periódica trimestral de una muestra del 5 %. | AC-18, AC-26 |
| NFR-14 | Recuperación de desastres. | Ensayo documentado semestral de restauración completa en entorno aislado. | AC-20 |
| NFR-15 | Mantenibilidad. | Cobertura de pruebas automatizadas ≥ 70 % en módulos de autorización, bóveda y migración; migraciones de esquema versionadas y reversibles. | AC-19 |
| NFR-16 | Capacidad de la bóveda. | 5.000 credenciales y 2.000 adjuntos sensibles sin degradación del p95 de revelado. | AC-19 |

Notas:

- RPO y RTO son propuestas; el valor definitivo se acuerda tras el primer ensayo de restauración (AC-20) y se registra en esta tabla.
- Ninguna cifra de NFR sustituye a la medición real: el criterio es la prueba en ensayo, no la estimación.
- NFR-01 y NFR-05 se ajustaron el 29/10/2026: disponibilidad al 99 % y 30 usuarios concurrentes.

---

## 8. Cumplimiento y protección de datos

**Nuevo v0.6.** v0.5 no trataba el marco jurídico, y la aplicación custodiará credenciales de sistemas de terceros, lo que eleva el riesgo. Los requisitos son de producto; su validación jurídica corresponde a asesoría especializada.

**Estado del expediente (29/10/2026).** El responsable de datos decide **posponer** la elaboración del expediente (contratos de encargo, registro de actividades, EIPD, subencargados, procedimientos y formación). Consecuencias registradas:

- AC-23 **no se considera superado** y sigue siendo puerta obligatoria antes de producción.
- Mientras RGPD-01 no esté firmado con un cliente, **no se cargan sus datos ni sus credenciales** en la aplicación (Fase 0 y Fase 7 quedan bloqueadas para ese cliente).
- La decisión y su riesgo asociado se registran en §16.4 y §17; no se interpreta como cumplimiento tácito.

### 8.1 Roles y base jurídica

| Situación | Rol de Ideo Networks | Implicación |
| --- | --- | --- |
| Datos de personas de los clientes (contactos técnicos, usuarios de sus sistemas) | **Encargado del tratamiento** de cada cliente | Contrato de encargo (art. 28 RGPD) por cliente, instrucciones documentadas, asistencia y devolución/supresión al terminar. |
| Datos propios (empleados, proveedores, candidatos) | **Responsable del tratamiento** | Registro de actividades, información, base jurídica y conservación propias. |
| Credenciales de sistemas de clientes | Datos del cliente tratados por cuenta ajena | Medidas reforzadas, minimización y prohibición de uso para fines distintos. |

**RGPD-01.** Formalizar contrato de encargado del tratamiento con cada cliente antes de cargar sus datos o credenciales, incluyendo objeto, duración, naturaleza, finalidad, tipos de datos, medidas de seguridad y subencargados.
**RGPD-02.** Mantener el registro de actividades de tratamiento (art. 30) actualizado por sistema y finalidad.
**RGPD-03.** Realizar una evaluación de impacto (EIPD, art. 35) antes de la puesta en producción de la bóveda, por el tratamiento a gran escala de credenciales de acceso; documentar medidas y riesgo residual.

### 8.2 Obligaciones y evidencias

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| RGPD-04 | Aplicar medidas del art. 32: cifrado en tránsito y reposo, control de acceso, minimización, confidencialidad, integridad, disponibilidad y resiliencia, con pruebas periódicas. | P0 | AC-23 |
| RGPD-05 | Autorizar por escrito cualquier subencargado (proveedor cloud, backup, correo) y exigirle garantías equivalentes; mantener lista pública de subencargados. | P0 | AC-23 |
| RGPD-06 | No realizar transferencias internacionales; si fueran inevitables, decisión de dirección con garantías adecuadas y registro. | P0 | AC-23 |
| RGPD-07 | Atender derechos de acceso, rectificación, supresión, limitación, portabilidad y oposición en el plazo legal, con procedimiento y registro. | P1 | AC-23 |
| RGPD-08 | Informar a los interesados cuando proceda (art. 13/14) mediante las cláusulas del cliente o aviso propio. | P1 | AC-23 |
| RGPD-09 | Formar al personal con acceso a datos y secretos; registrar la formación. | P1 | AC-23 |
| RGPD-10 | Evaluar la necesidad de Delegado de Protección de Datos o asesoría externa y documentar la decisión. | P1 | AC-23 |

### 8.3 Conservación y supresión

**RGPD-11.** Política de conservación **a dos niveles**, según la posición de Ideo Networks en cada tratamiento (decisión ADR-010). No se aplican los plazos propios a la documentación ajena.

**(a) Datos propios (Ideo Networks como responsable):**

| Tipo | Plazo | Nota |
| --- | --- | --- |
| Documentación contractual y mercantil | 6 años | Plazo mercantil orientativo (Código de Comercio); validar con asesoría. |
| Documentación fiscal y facturas emitidas | 4 años | Plazo fiscal orientativo; validar. |
| Documentación laboral | 4 años | Plazo laboral orientativo; validar. |
| Candidaturas | 1 año | Salvo consentimiento expreso. |

**(b) Datos de clientes (Ideo Networks como encargada):**

| Tipo | Plazo | Nota |
| --- | --- | --- |
| Documentación y datos del cliente | Mientras dure el contrato de encargo | El plazo lo fija el contrato con ese cliente (RGPD-01), no la ley fiscal de Ideo. |
| Credenciales activas | Mientras dure el servicio | Revisión anual de vigencia; baja al terminar. |
| Credenciales de servicios dados de baja | 90 días | Ventana de reversión; después, destrucción cifrada con evidencia. |
| Al terminar el encargo | Devolución o supresión según contrato | 90 días de cortesía para restaurar; si el cliente instruye por escrito otro plazo, prevalece su instrucción. |

**(c) Datos operativos de la aplicación:**

| Tipo | Plazo | Nota |
| --- | --- | --- |
| Logs técnicos | 30 días | Sin datos personales innecesarios. |
| Auditoría | 365 días en línea, 5 años en archivo | Trazabilidad de accesos; almacenamiento separado. |
| Staging, colas y temporales de migración | 30 días tras el cierre | Antes, si la ventana de rollback termina. |

Reglas comunes: revisión anual de la política, borrado verificable con registro y prohibición de conservar «por comodidad» más allá del plazo. Un dato sin plazo asignado se trata como pendiente de decisión y no se conserva indefinidamente.

**RGPD-12.** Al finalizar la relación, devolver o suprimir los datos del cliente según contrato, con certificado de destrucción de secretos y evidencia de borrado lógico; documentar la imposibilidad de borrado físico absoluto en copias de seguridad, indicando el plazo máximo de rotación.

### 8.4 Brechas e incidentes

1. Registro de incidentes con fecha, detección, alcance, datos afectados, medidas y responsable.
2. Notificación a la autoridad de control en 72 horas cuando exista riesgo (art. 33) y a los interesados cuando el riesgo sea alto (art. 34).
3. Notificación al cliente afectado sin dilación, conforme al contrato de encargo.
4. Preservación de evidencia y auditoría del incidente fuera del conjunto restaurado (coherente con §12).
5. Simulacro anual de brecha con credenciales ficticias y registro del resultado (AC-23).

---

## 9. Migración única

### 9.1 Principios obligatorios

| ID | Requisito |
| --- | --- |
| MIG-01 | Una incorporación inicial controlada, con lotes y reintentos auditables. No habrá sincronización permanente con OneDrive. |
| MIG-02 | El origen se trata exclusivamente como fuente de lectura. Ningún paso escribe dentro de él. |
| MIG-03 | No probar ni acceder automáticamente a la ruta declarada si no está expresamente proporcionada o montada en el entorno de migración. |
| MIG-04 | Inventariar antes de extraer; conservar una disposición explícita para cada entrada. |
| MIG-05 | Separar extracción, normalización, propuesta, revisión y publicación. Ningún dato ambiguo se convierte automáticamente en dato validado. |
| MIG-06 | Secretos y documentos que los contienen usan bóveda o almacén sensible cifrado desde su primera persistencia de trabajo. |
| MIG-07 | Cada dato publicado conserva sus fuentes y transformaciones, con versiones de reglas y decisiones humanas. |
| MIG-08 | Simulación previa sin escrituras en producción y ensayo de rollback verificable. |
| MIG-09 | Aprobación humana del plan concreto, excepciones, asociaciones y resultado de simulación antes del corte. |
| MIG-10 | Cierre formal, revocación del acceso de importación y operación posterior exclusivamente desde la aplicación. |

«Única» significa una campaña con una línea base aprobada. La recuperación de un fallo o la repetición idempotente de un lote pertenecen a la misma campaña y no generan una segunda carga. Cambiar la fuente o el alcance tras la aprobación invalida la simulación afectada y exige una nueva versión del plan.

### 9.2 Protección efectiva del origen

El ejecutor operará con acceso de lectura a una copia o montaje facilitado explícitamente por el responsable. Los permisos de escritura deben estar impedidos por la infraestructura; el importador no cambia ACL, atributos ni configuración de sincronización de la carpeta original. Temporales, resultados, manifiestos y logs se escriben fuera del árbol fuente.

No seguir enlaces simbólicos, junctions o rutas relativas que salgan de la raíz aprobada. Validar rutas canónicas y detectar ciclos. No ejecutar scripts, macros, instaladores ni accesos directos encontrados. Las exclusiones se registran, no se borran.

Los archivos «solo en línea» se marcan como no disponibles. El importador no fuerza su hidratación ni activa OneDrive: solicitar una exportación o copia disponible y verificable suministrada por el responsable. Si se requiere preservación estricta de metadatos de acceso, usar snapshot o montaje de lectura que evite actualizaciones de atime y efectos del cliente de sincronización.

Guardar evidencia anterior y posterior del conjunto disponible: rutas, bytes, fechas de modificación y hashes. Detectar cambios externos o de OneDrive durante la lectura; no atribuirlos automáticamente al importador. Si cambia un archivo entre inventario y extracción, ponerlo en conflicto y detener su promoción. La prueba de inmutabilidad comprende configuración de lectura y comparación, no solo una promesa del código.

### 9.3 Roles y puertas de aprobación

| Rol | Responsabilidad |
| --- | --- |
| Responsable de datos | Alcance, disponibilidad de fuentes, asociaciones carpeta-cliente y excepciones. |
| Revisor técnico | Valida equipos, servicios, configuración, fechas y contradicciones. |
| Custodio de bóveda | Revisa secretos y clasificación sensible sin divulgarlos en informes generales. |
| Operador de migración | Ejecuta el plan aprobado y controla lotes; no decide por sí solo fusiones ambiguas. |
| Responsable de aceptación | Autoriza corte, conciliación y cierre o rollback. |

La aprobación se liga a hash de manifiesto, versión del mapeo, reglas, esquema de destino y reporte de simulación. Cambiar cualquiera invalida la aprobación correspondiente. Los permisos de migración son temporales y no forman parte de los permisos habituales del técnico (§3.2).

---

## 10. Procedimiento de migración

### Fase 0 — Preparación

Definir campaña, propietario, alcance documental, política de retención y destino. Registrar el catálogo Excel con hash y generar una primera conciliación. Confirmar que la ruta o copia suministrada es la correcta sin explorar otras ubicaciones. Definir clasificación de documentos administrativos, históricos, audiovisuales, binarios e internos; ningún archivo se descarta por tamaño sin decisión registrada.

Evaluar capacidad con bytes reales: copia de trabajo cifrada, originales gestionados, derivados, índices, bóveda y copias de seguridad. Los 57,5 GB son una referencia redondeada, no una medición nueva ni una cifra suficiente para dimensionar todo el almacenamiento. Programar ventana de corte y condición de bloqueo de escrituras de destino.

**Salida:** alcance aprobado, roles, manifiesto preliminar, entorno aislado y plan de recuperación.

### Fase 1 — Inventariado reproducible

Registrar cada archivo de la raíz aprobada y conciliar con el Excel. Campos mínimos: `source_id`, `snapshot_id`, carpeta, ruta relativa exacta, nombre, tamaño en bytes, fecha y zona/precisión disponibles, extensión, tipo detectado, disponibilidad, hash SHA-256 cuando pueda leerse y estado de procesamiento.

Conservar también `source_catalog_id`, hoja y fila. La ruta exacta y el nombre pueden ser sensibles: permisos restringidos y redacción en reportes públicos. No inferir fecha horaria a partir de una fecha de día del Excel ni bytes exactos a partir de MB redondeados. Hash ausente significa no calculado, no archivo vacío.

Las entradas de contenedores comprimidos conservarán relación con su archivo padre; no inflar el recuento de archivos originales. Cada elemento interno tendrá localizador y estado propios. Límites de expansión, tamaño, profundidad y tiempo serán parámetros aprobados para impedir cargas descontroladas.

**Salida:** manifiesto inmutable versionado, diferencias frente al catálogo, exclusiones propuestas y mapa de carpetas.

### Fase 2 — Extracción local controlada

Usar detectores de tipo y parsers mantenidos en un entorno aislado, sin ejecutar contenido activo. Extraer texto y tablas de formatos soportados; OCR para escaneados con señal de confianza y ubicación. Los formatos no soportados se conservan como adjuntos o se envían a revisión, sin inventar su contenido.

Rutas de tratamiento: documentos ofimáticos/PDF; texto y configuraciones; hojas con celdas; imágenes/OCR; archivos comprimidos; audiovisuales; binarios/instaladores; cifrados o protegidos. No romper cifrado ni adivinar contraseñas de documentos. Registrar «Requiere acceso autorizado» cuando corresponda.

El clasificador puede proponer cliente, sede, equipo, red, servicio, licencia, proyecto, documento o secreto. No convierte nombres de archivos en hechos técnicos. Todas las salidas de extracción se consideran potencialmente sensibles hasta su clasificación; persistirlas cifradas, incluso OCR y temporales. No enviar documentos ni secretos a servicios externos de IA. Un uso futuro de IA exige flujo aprobado, redacción previa y evaluación de destino.

**Salida:** hechos candidatos con localizadores de fuente y material sensible aislado.

### Fase 3 — Normalización

Crear reglas versionadas para nombres, alias, fechas, dominios, URLs, IP/subred, MAC, números de serie, unidades, fabricante y tipo de servicio. Conservar valor original protegido y valor propuesto; nunca perder ceros significativos, mayúsculas relevantes o espacios de un secreto. Las contraseñas no se recortan, transliteran ni «corrigen».

La fecha de archivo no equivale a fecha de instalación o validez del dato. Un dato ausente se conserva como ausente, no como cero. Validar estructura y rangos sin conectarse a los sistemas de clientes para comprobar accesos.

Mapear cada carpeta a cliente canónico, entidad interna, proyecto o exclusión con motivo. Permitir varias carpetas por cliente y, excepcionalmente, contenidos de una carpeta asociados a varias entidades mediante reglas explícitas y revisadas.

**Salida:** candidatos normalizados y tabla de mapeos aprobable.

### Fase 4 — Deduplicación y conflictos

Separar tres problemas: duplicado de archivo, duplicado de entidad y versiones contradictorias de un dato.

- Archivos iguales: tamaño y hash de contenido, manteniendo todas sus rutas de procedencia. El almacenamiento compartido no concede acceso cruzado entre clientes.
- Clientes: identidad empresarial o confirmación humana; el nombre parecido solo genera candidato. No fusionar automáticamente los pares de §4.4.
- Equipos: número de serie, cliente y contexto; una IP privada puede repetirse en distintos clientes o redes y no es clave global.
- Servicios: clave compuesta revisada de cliente, tipo y dominio/instancia. Conservar múltiples proveedores o entornos.
- Credenciales: contexto de cuenta, sistema y cliente; no fusionar solo por usuario o valor secreto. Si se requiere comparar valores, hacerlo dentro del componente protegido, sin exportar huellas reutilizables.
- Configuraciones: conservar variantes, fecha, fuente y decisión de vigencia; el archivo más reciente no gana automáticamente.

Una fusión conserva identificadores fuente, alias, relaciones, motivo, autor y evidencia suficiente para deshacerla en destino. Los conflictos no resueltos impiden publicar los registros afectados.

### Fase 5 — Revisión humana

La cola muestra dato candidato, fuente autorizada, reglas aplicadas, validaciones y destino propuesto. El material sensible se revisa mediante acceso de bóveda, con valores ocultos por defecto y auditoría separada.

Revisión obligatoria del 100 % de asociaciones carpeta-cliente, fusiones, secretos candidatos, conflictos, cambios en registros existentes y exclusiones. Los lotes de metadatos de bajo riesgo pueden aprobarse conjuntamente después de revisar su resumen y muestras; registrar cobertura exacta, sin presentar un muestreo como revisión de todos los contenidos.

Decisiones: aprobar, corregir, rechazar, excluir con motivo o dejar pendiente. Una corrección no altera el original. Registrar actor, fecha, versión, motivo y alcance. Un dato extraído con baja confianza no se publica como validado por defecto.

### Fase 6 — Simulación

Ejecutar el mismo plan, mapeos y transformaciones sobre un entorno de ensayo aislado. No escribir en la BD, bóveda o repositorio documental de producción. Los secretos reales, si son imprescindibles para el ensayo, permanecen en una bóveda de ensayo aislada y cifrada con acceso y retención limitados; usar datos sintéticos cuando baste para probar el comportamiento.

Producir informe de altas, relaciones, fusiones, actualizaciones previstas, exclusiones, errores y volumen; diferencias por carpeta, cliente, tipo y lote. Probar permisos, trazabilidad, visualización de fichas, manejo de secretos y restauración. Medir rendimiento con un lote representativo antes de estimar duración global.

Reejecutar el mismo lote debe producir cero duplicados y cero cambios imprevistos. Simular interrupción tras cada etapa de escritura y recuperación. Simular también cambios de fuente o de reglas para comprobar que se invalida la aprobación.

**Puerta de paso:** errores bloqueantes resueltos; excepciones enumeradas y aprobadas; pruebas de recuperación y seguridad superadas; reporte y plan firmados lógicamente por responsables.

### Fase 7 — Importación y corte

1. Confirmar identidad de manifiesto, versión de reglas y aprobaciones; comprobar que no hay deriva de fuente.
2. Detener escrituras de usuarios en el destino para el alcance del corte. Crear copia coherente verificable de BD, bóveda, claves recuperables y repositorio documental.
3. Cargar en área de staging, manteniendo estado no visible. Crear entidades, documentos, referencias y secretos con clave de idempotencia y diario de operaciones.
4. Validar integridad referencial, permisos y existencia de objetos/secretos antes de promover un lote.
5. Hacer visible el conjunto solo cuando todos los componentes estén confirmados. No exponer un registro cuyo secreto o documento no haya terminado de persistirse.
6. Conciliar, ejecutar pruebas funcionales y registrar aceptación. Solo entonces habilitar escrituras de usuarios.

BD y bóveda pueden no compartir transacción. Usar estados de preparación/confirmación, registro duradero de operaciones y compensaciones idempotentes. Una transacción SQL aislada no basta para revertir objetos externos. Si falla un paso, impedir visibilidad parcial, conservar evidencia y reanudar o revertir de forma controlada.

### Fase 8 — Conciliación y cierre

Cada entrada de origen tendrá un único resultado final de cobertura: `importada`, `duplicada_vinculada`, `excluida_aprobada`, `pendiente` o `fallida`. La suma coincide con el alcance del manifiesto. Los elementos dentro de archivos comprimidos se concilian por separado con su padre.

Las entidades resultantes pueden ser muchas por archivo, y varios archivos pueden sustentar una entidad. Por ello, no comparar «número de filas de BD» con «número de archivos» como prueba de éxito. Conciliar también relaciones, fuentes, documentos almacenados, referencias de bóveda y permisos.

No cerrar como migración completa con pendientes o fallos dentro del alcance comprometido. Si se acepta un alcance reducido, enumerar exclusiones y datos no trasladados, con aprobación y denominador revisado; el informe no afirmará que se han migrado todos los archivos.

Tras la aceptación: emitir acta de cierre, fijar fuente histórica, desactivar el importador y retirar sus credenciales y permisos. Operación posterior desde la aplicación. El origen permanece intacto. Eliminar solo temporales de trabajo según política aprobada y tras vencer la ventana de rollback; documentar tratamiento de claves y copias cifradas, sin prometer borrado físico absoluto de soportes o copias de seguridad.

---

## 11. Secretos y documentos mixtos durante la migración

| ID | Requisito | Prioridad | Verificación |
| --- | --- | --- | --- |
| MIG-11 | Detectar candidatos a contraseña, token, clave privada, cadena de conexión, certificado con clave, semilla MFA y configuración sensible. El nombre de archivo solo prioriza la revisión: no afirmar que los 126 archivos contienen secretos hasta inspección autorizada. | P0 | AC-09, AC-10 |
| MIG-12 | Si un documento combina instrucciones y secretos, guardar el original en almacenamiento sensible cifrado y crear, cuando proceda, una copia redactada para consulta general. Ambas conservan relación, hashes y procedencia. El original no se edita. Miniaturas, OCR, índices y previsualizaciones usan solo la versión autorizada y redactada. | P0 | AC-10 |
| MIG-13 | Cifrar antes de persistir datos extraídos; staging, colas, temporales, volcados y reportes sin secretos en claro. Reducir su vida en memoria y evitar volcados de proceso. Los informes muestran identificadores de bóveda y estado de validación, nunca el valor. | P0 | AC-09 |
| MIG-14 | Importar no significa verificar que una contraseña funciona ni rotarla. No iniciar sesión automáticamente en sistemas de clientes. Si se detecta exposición previa, registrar una tarea separada de revisión/rotación sin modificar el origen ni servicios externos durante la campaña. | P0 | AC-09 |

El rollback de datos no puede deshacer un secreto ya visto o copiado ni una rotación externa. Por diseño la migración no hace rotaciones; cualquier acción externa pertenece a un procedimiento distinto.

---

## 12. Rollback y recuperación

### 12.1 Disparadores y preparación

Revertir o detener la publicación ante asignación al cliente incorrecto, secreto expuesto en un canal no autorizado, pérdida de integridad, permisos incorrectos, divergencia no explicada o fallo irrecuperable de la promoción. El responsable de aceptación decide el rollback con el operador; cualquier operador puede suspender inmediatamente la exposición ante un incidente.

Antes del corte, probar una restauración coherente de BD, objetos, bóveda y claves en entorno aislado. Documentar duración medida, responsable, pasos y verificaciones. RTO y ventana de rollback se acuerdan tras el ensayo (NFR-02, NFR-14); no se inventan tiempos garantizados. El punto objetivo durante la ventana sin escrituras es el estado inmediatamente anterior a la importación.

### 12.2 Procedimiento verificable

1. Bloquear nuevas escrituras y detener trabajos pendientes del lote o campaña. Marcar sus datos como no visibles y conservar el registro de incidente.
2. Identificar operaciones por `migration_run_id` y `batch_id`, incluyendo objetos y versiones de secretos. No borrar por nombre de cliente ni por coincidencia de ruta.
3. Restaurar la copia coherente previa o ejecutar compensaciones verificadas en orden inverso de dependencias. Retirar solo objetos creados por esa campaña y sin referencias ajenas; restaurar valores previos de registros actualizados.
4. Comprobar integridad referencial, permisos, lecturas de prueba, hashes documentales y referencias de bóveda. Verificar ausencia de objetos visibles huérfanos.
5. Mantener la auditoría de la ejecución fallida y del rollback fuera del conjunto restaurado. No «limpiar» el historial para aparentar que no ocurrió.
6. Registrar el resultado, comparar el destino con la línea base previa y confirmar que la fuente sigue intacta.

**MIG-15.** Si ya se permitieron cambios de usuarios tras el corte, no restaurar indiscriminadamente toda la BD. Preservar los cambios posteriores, calcular dependencias y aplicar un plan de compensación revisado; si no puede conservarse el trabajo posterior, detener y elevar la decisión al responsable. Propuesta: completar aceptación y ensayo operativo antes de levantar el bloqueo.

**MIG-16.** Después de un rollback, una nueva ejecución requiere resolver la causa, repetir la simulación afectada y aprobar el plan. Mantener el vínculo entre ejecución fallida, reversión y reintento.

---

## 13. Modelo de datos, trazabilidad y API

### 13.1 Modelo lógico

Modelo compatible con los requisitos recuperados; no se afirma que reproduzca las tablas exactas de v0.4.

| Entidad | Contenido mínimo |
| --- | --- |
| `clients`, `client_aliases`, `sites` | Identidad canónica, alias y sedes; estado comercial separado de actividad documental. |
| `users`, `roles`, `client_permissions` | Identidad Entra, estado, acciones y alcance; denegación por defecto. |
| `assets`, `networks`, `services`, `licenses`, `projects` | Entidades técnicas y relaciones con cliente/sede. |
| `form_templates`, `field_definitions`, `record_instances` | Plantillas versionadas, campos tipados y valores no secretos. |
| `vault_items`, `vault_versions` | Metadatos mínimos y referencias a material cifrado; política de acceso separada. |
| `documents`, `document_versions`, `document_objects` | Contenido propio o referencia explícita, hash, clasificación y versión. |
| `source_catalogs`, `source_snapshots`, `source_files` | Huella de catálogo, manifiesto y archivo con ruta, bytes, fechas y disponibilidad. |
| `source_links` | Relación muchos-a-muchos entre entidad/campo de destino y fuente, con localizador. |
| `extraction_candidates` | Candidato, extractor/versión, regla, validación, confianza y ubicación protegida. |
| `mapping_decisions`, `duplicate_groups`, `review_decisions` | Mapeos, candidatos a duplicado, conflictos, aprobaciones y motivos. |
| `migration_runs`, `migration_batches`, `migration_operations` | Plan, estado, idempotencia, checkpoints, compensación y resultado. |
| `audit_events` | Registro independiente de acciones, actor, fecha, cliente y resultado sin secretos. |

### 13.2 Trazabilidad a nivel de campo

Para cada valor importado: identificador de fuente, hash del archivo leído, ruta relativa protegida, hoja/celda o página/sección/línea cuando exista, valor original en almacén adecuado, transformación aplicada, versión de extractor/regla, decisión humana, fecha de validación, ejecución y entidad/campo destino.

Para OCR indicar página y región, y distinguir reconocimiento de validación. Si un formato no permite localizador exacto, declarar la granularidad real; no inventar celdas o líneas. Los valores secretos originales nunca se guardan en `source_links`: solo referencia protegida.

| ID | Requisito | Verificación |
| --- | --- | --- |
| DAT-01 | Conservar múltiples fuentes y explicitar cuál sustenta el dato vigente. Editar tras la migración crea una nueva versión de aplicación; no reescribe la fuente ni oculta el origen importado. | AC-12 |
| DAT-02 | Clave de idempotencia basada en campaña, snapshot, archivo/localizador, tipo de entidad y versión de mapeo; ledger con restricciones únicas. La reanudación del mismo plan no duplica altas; un cambio de plan abre revisión explícita. | AC-14 |

### 13.3 Contratos de API

**Nuevo v0.6.** v0.5 describía la API en prosa; aquí se fija el contrato mínimo antes de estimar el desarrollo. El contrato definitivo se publicará como OpenAPI 3.1 y será la fuente de los clientes web y Android.

| Recurso | Operaciones | Notas |
| --- | --- | --- |
| `/api/v1/clients` | `GET`, `POST` | Filtro por alcance del usuario aplicado antes de responder; paginación por cursor. |
| `/api/v1/clients/{id}` | `GET`, `PATCH` | `ETag`/`If-Match` para concurrencia; `409` en conflicto. |
| `/api/v1/clients/{id}/sites` | `GET`, `POST` | — |
| `/api/v1/records` | `GET`, `POST` | Fichas tipadas por plantilla; validación en servidor. |
| `/api/v1/records/{id}` | `GET`, `PATCH`, `DELETE` | `DELETE` = archivado lógico salvo permiso de eliminación. |
| `/api/v1/templates` | `GET`, `POST`, `PATCH` | Solo administración de plantillas. |
| `/api/v1/vault/items` | `GET`, `POST` | Solo metadatos en `GET`; nunca el secreto. |
| `/api/v1/vault/items/{id}/reveal` | `POST` | `POST` con motivo; `Cache-Control: no-store`; auditoría obligatoria. |
| `/api/v1/vault/items/{id}/copy` | `POST` | Permiso distinto del revelado; devuelve confirmación, no el valor en la respuesta. |
| `/api/v1/documents` | `GET`, `POST` | Metadatos; descarga mediante token efímero. |
| `/api/v1/search` | `GET` | Solo sobre índices autorizados; excluye secretos. |
| `/api/v1/audit/events` | `GET` | Solo roles con permiso; exportación auditada. |
| `/api/v1/app/version` | `GET` | Versión mínima y URL de descarga; requiere sesión autenticada. |
| `/api/v1/migrations/{id}` | `GET`, `POST` | Crear plan, consultar inventario, simular, aprobar, ejecutar, revertir, cerrar. |

Reglas transversales:

1. Autenticación obligatoria en todos los recursos salvo la raíz de salud (`/health`), que no revela datos.
2. Autorización y filtrado de alcance en servidor, antes de la serialización.
3. Las respuestas sensibles no se cachean (`no-store`) ni se registran en logs con cuerpo.
4. Los secretos nunca viajan en rutas, parámetros de URL ni cabeceras personalizadas.
5. Toda escritura acepta `Idempotency-Key` y devuelve un identificador de operación consultable.
6. Versionado por prefijo `/v1`; cambios incompatibles crean `/v2`, sin romper clientes instalados.

### 13.4 Errores, límites e idempotencia

| Aspecto | Regla |
| --- | --- |
| Formato de error | `{ "error": { "code": "...", "message": "...", "correlationId": "..." } }`, sin datos internos ni valores secretos. |
| Códigos | `400` validación, `401` sin sesión, `403` sin permiso, `404` inexistente o fuera de alcance, `409` conflicto de versión, `422` regla de negocio, `429` límite, `500` error interno. |
| Fuera de alcance | Un recurso no autorizado responde `404` para no revelar su existencia. |
| Paginación | Cursor opaco; tamaño máximo 200 elementos; nunca «total» de registros no autorizados. |
| Límite de peticiones | 600 peticiones/minuto por usuario; 30 revelados/minuto; 10 exportaciones/hora. Respuesta `429` con `Retry-After`. |
| Idempotencia | Clave por operación y ventana de 24 h; repetir no duplica ni reejecuta efectos. |
| Concurrencia | `ETag` obligatorio en `PATCH`; sin coincidencia, `409` y ninguna escritura. |

---

## 14. Criterios de aceptación

| ID | Prueba | Resultado exigido |
| --- | --- | --- |
| AC-01 | Continuidad documental | Requisitos recuperados y catálogo conservados; al aportar v0.4, comparación completa sin omisiones no justificadas. |
| AC-02 | Marca y nomenclatura | Activo original con proporción 2,5:1; azul de logo diferenciado del primario; Montserrat e Inter verificables; módulos con los nombres de UI-11. |
| AC-03 | Responsive | Flujos de §5 completos en 320, 360, 390, 768, 1024 y 1440 CSS px, sin pérdida de acciones ni datos; estados vacíos y errores incluidos. |
| AC-04 | Fichas dinámicas | Crear un tipo nuevo y un campo sin código; mostrar solo fichas con datos autorizados; `0` y `false` cuentan. |
| AC-05 | Permisos | Un usuario sin acceso no obtiene registros, nombres, conteos, adjuntos ni secretos de otro cliente por UI o API. |
| AC-06 | Autenticación | Web y APK exigen identidad corporativa y MFA conforme a la política efectiva; biometría no evita sesión ni permisos. |
| AC-07 | Solo lectura | Intentos de escritura del proceso sobre la fuente denegados; sin temporales en origen; manifiestos anterior y posterior conciliados. |
| AC-08 | Inventario | Las 37.682 entradas del Excel se concilian como 37.677 + 4 + 1; diferencias con el snapshot futuro explicadas. |
| AC-09 | Secretos | Ningún secreto de prueba aparece en logs, exportaciones, búsqueda, OCR público, historial general ni contexto de IA. |
| AC-10 | Clasificación | Documentos mixtos protegidos; solo derivados redactados accesibles al usuario sin permiso de bóveda. |
| AC-11 | Duplicados | Pares ambiguos requieren revisión; archivos iguales conservan todas sus fuentes; no fusionar por IP privada global. |
| AC-12 | Revisión | Aprobaciones vinculadas a versión/hash; cambios de plan o fuente invalidan la aprobación; conflictos bloquean la promoción. |
| AC-13 | Simulación | Cero escrituras en producción; resultado conciliable por cliente y lote; exclusiones justificadas. |
| AC-14 | Idempotencia | Repetir un lote aprobado no duplica entidades, documentos, relaciones ni versiones de secretos. |
| AC-15 | Fallo parcial | Una caída entre BD, bóveda y objetos no deja datos visibles incompletos; reanudación o compensación verificable. |
| AC-16 | Rollback | Restauración de BD, objetos y bóveda al punto aprobado, auditoría preservada y fuente intacta. |
| AC-17 | Cambios posteriores | El procedimiento no destruye trabajo de usuarios tras el corte; detecta dependencias y exige plan revisado. |
| AC-18 | Cierre | Cada entrada tiene disposición final; permisos de importación retirados; no existe sincronización recurrente. |
| AC-19 | Rendimiento y capacidad | Con 5.000 registros, 100.000 documentos y **30 usuarios concurrentes**, p95 de listado < 500 ms, ficha < 800 ms y revelado < 300 ms; sin errores 5xx; NFR-03, NFR-04, NFR-05, NFR-15 y NFR-16 medidos y registrados. |
| AC-20 | Copias y continuidad | Restauración completa desde copia en entorno aislado dentro del RTO acordado, con RPO medido, evidencia de cifrado y verificación mensual documentada; NFR-02, NFR-12 y NFR-14. |
| AC-21 | Matriz de permisos | Cada fila de §3.3 probada con un usuario por rol: lo permitido funciona y lo denegado no filtra datos, nombres, conteos ni secretos; los permisos temporales caducan; se demuestra que nadie aprueba su propio permiso ni su cambio de rol (PRM-15) y que existe acta de revisión externa (PRM-16). |
| AC-22 | Claves y recuperación | No existe material de clave en BD, copias ni repositorio; la bóveda nace sellada tras un reinicio y solo la abre un administrador con Entra + MFA, quedando auditado; un técnico sin rol de administrador no puede desellar; el fichero de clave copiado a otro equipo no es utilizable; el sobre de recuperación restaura en entorno aislado y fuerza rotación completa; la rotación de KEK no reencripta secretos. |
| AC-23 | Cumplimiento RGPD | Existen contrato de encargo por cliente, registro de actividades, EIPD aprobada, lista de subencargados, procedimiento de derechos y simulacro de brecha con registro; RGPD-01…RGPD-12 verificados documentalmente. |
| AC-24 | Trazabilidad documental | La matriz de §15 cubre el 100 % de los requisitos de esta versión y cada fila apunta a un criterio existente. |
| AC-25 | Contrato de API | OpenAPI 3.1 publicado; errores, paginación, límites, `ETag` e idempotencia probados con pruebas automatizadas; sin secretos en URL ni en logs. |
| AC-26 | Observabilidad y auditoría | Logs estructurados sin secretos; auditoría independiente con integridad verificable; métricas y alertas operativas; retención aplicada; una acción sensible genera evento completo con actor, cliente, resultado y correlación. |
| AC-27 | Activos de marca y licencias | SVG original y proporción conservados; fuentes distribuidas con licencia documentada (Inter y Montserrat, SIL OFL); icono APK aprobado visualmente. |
| AC-28 | Búsqueda | Búsqueda global y por cliente con resultados en p95 < 500 ms sobre 100.000 documentos, sin devolver clientes ni secretos no autorizados, con resaltado y filtros. |
| AC-29 | Cliente, carpeta y estado | Varias carpetas pueden asociarse a un cliente; el estado comercial validado no se deriva de fechas de archivo; «contenido no disponible» se distingue de «vacío». |
| AC-30 | Accesibilidad | WCAG 2.2 AA verificado en web y flujos equivalentes con TalkBack en Android: teclado, foco, contraste, etiquetas, errores asociados, zoom y texto al 200 %. |
| AC-31 | Android | APK firmada verifica su firma, instala y opera en Android 10 o superior, en teléfono compacto y tableta; la app bloquea el uso por debajo de la versión mínima y ofrece actualización; sin secretos en almacenamiento no protegido. |
| AC-32 | Aislamiento de red | Desde un dispositivo conectado por OpenVPN: responde el servidor en 443 y **no** responde ningún otro recurso de la LAN, de otras VLAN ni la gestión del router; retirado el perfil, la conexión falla; no existe perfil compartido entre usuarios; el túnel usa las versiones y cifrados de SEC-17. |
| AC-33 | Certificado y nombres | El certificado es válido para el nombre interno, emitido por CA pública, con renovación automática probada y alerta ante fallo; el nombre no resuelve fuera de la VPN; ni la APK ni el navegador aceptan excepciones TLS. |

Estas pruebas definen trabajo de desarrollo y aceptación. No se presentan como ejecutadas sobre una aplicación que todavía no existe.

---

## 15. Matriz de trazabilidad

**Nuevo v0.6.** Cada requisito de esta versión tiene al menos una prueba. Las filas agrupadas comparten criterio y método.

| Requisito(s) | Ámbito | Criterio | Método |
| --- | --- | --- | --- |
| BASE-01, BASE-09 | Servidor, BD y entidades | AC-01, AC-18 | Revisión funcional + manifiesto |
| BASE-02 | Web y APK sobre la misma API | AC-03, AC-31 | Prueba manual en dispositivos |
| BASE-03, BASE-13, SEC-01…SEC-04 | Identidad, MFA, biometría | AC-06 | Prueba de flujo con tenant real de ensayo |
| BASE-04, PRM-01…PRM-16 | Autorización y roles | AC-05, AC-21 | Matriz de pruebas por rol y recurso |
| PRM-15, PRM-16 | No autoaprobación y revisión externa | AC-21 | Intento de autoaprobación + acta mensual |
| BASE-05, SEC-05, SEC-10…SEC-14 | Bóveda y claves | AC-09, AC-10, AC-22 | Ensayo de cifrado, revelado y recuperación |
| BASE-06, BASE-07 | Fichas dinámicas | AC-04, AC-05 | Crear tipo y campo sin código |
| BASE-08, SEC-07, SEC-08, SEC-09, OBS-01…OBS-05 | Auditoría y observabilidad | AC-09, AC-26 | Revisión de eventos y logs |
| BASE-10 | Búsqueda y procedencia | AC-28 | Prueba de carga y permisos |
| BASE-11, BASE-12 | Cliente vs carpeta vs estado | AC-29 | Casos con varias carpetas por cliente |
| BASE-14 | Evolución IA | AC-09 | Revisión de exclusión de secretos |
| SEC-06 | Permisos de bóveda | AC-21 | Prueba de revelado y copia por rol |
| NFR-01, NFR-03, NFR-04, NFR-05, NFR-15, NFR-16 | Rendimiento, capacidad, mantenibilidad | AC-19 | Prueba de carga y cobertura |
| NFR-02, NFR-12, NFR-14 | Copia y continuidad | AC-20 | Restauración cronometrada |
| NFR-06, NFR-08 | Navegadores e idioma | AC-03 | Matriz de navegadores |
| NFR-07 | Android mínimo y distribución | AC-31 | Instalación en dispositivos de prueba |
| NFR-09 | Accesibilidad | AC-30 | Auditoría WCAG y TalkBack |
| NFR-10 | TLS | AC-19 | Escaneo de configuración |
| NFR-11 | Cifrado en reposo | AC-22 | Verificación de volúmenes y bóveda |
| NFR-13 | Integridad documental | AC-18, AC-26 | Muestra de hashes trimestral |
| RGPD-01…RGPD-12 | Cumplimiento | AC-23 | Expediente documental y simulacro |
| MIG-01 | Campaña única | AC-18 | Registro de campañas y permisos |
| MIG-02, MIG-03 | Origen inmutable | AC-07 | Intento de escritura + comparación |
| MIG-04 | Inventario previo | AC-08 | Conciliación del manifiesto |
| MIG-05, MIG-07, MIG-09 | Separación y aprobación | AC-12 | Expediente de aprobaciones |
| MIG-06 | Cifrado desde el primer paso | AC-10 | Revisión de staging |
| MIG-08 | Simulación sin escrituras | AC-13 | Ensayo aislado |
| MIG-10 | Cierre y revocación | AC-18 | Acta y permisos retirados |
| MIG-11, MIG-12, MIG-13, MIG-14 | Secretos en migración | AC-09, AC-10 | Revisión de candidatos y redacción |
| MIG-15, MIG-16 | Rollback con cambios y reintento | AC-16, AC-17 | Ensayo de interrupción y reintento |
| DAT-01 | Fuentes múltiples y versión | AC-12 | Inspección de trazabilidad |
| DAT-02 | Idempotencia | AC-14 | Repetición de lote |
| UI-01…UI-09 | Identidad visual | AC-02, AC-27 | Comparación con anexo B |
| UI-10 | Pruebas visuales | AC-03, AC-30 | Capturas y medición |
| UI-11 | Nomenclatura | AC-02 | Revisión de textos |
| SEC-15, SEC-16, SEC-17, SEC-18, SEC-19 | Acceso remoto, aislamiento y certificado | AC-32, AC-33, AC-06 | Pruebas de red desde dispositivo VPN |
| SEC-20, SEC-21 | Distribución y firma de la APK | AC-31 | Instalación, bloqueo por versión y custodia de firma |

---

## 16. Plan de proyecto

**Nuevo v0.6.** v0.5 enumeraba tareas en §13 sin calendario. El plan es orientativo y se reestima tras ADR-001, ADR-002 y ADR-005.

### 16.1 Fases y entregables

| Fase | Duración propuesta | Entregables | Dependencias |
| --- | --- | --- | --- |
| A. Cierre de decisiones | 2 semanas | ADR-001 a ADR-010 resueltos; v1.0 de especificación aprobada | Dirección y responsables nombrados |
| B. Base técnica | 4 semanas | Repositorio, CI, BD, autenticación Entra, auditoría, entorno de ensayo | Fase A |
| C. Núcleo funcional | 6 semanas | Clientes, sedes, fichas dinámicas, permisos, búsqueda básica | Fase B |
| D. Bóveda | 4 semanas | Cifrado, revelado, copia, generador, autobloqueo, claves y recuperación | Fase B; ADR-001 |
| E. Documentación | 3 semanas | Carga, versiones, hash, vista segura, documentos mixtos | Fase C |
| F. Web y APK | 6 semanas | Interfaz web responsive y APK firmada sobre la misma API | Fases C y D |
| G. Migración en seco | 3 semanas | Manifiesto, simulación, conciliación, ensayo de rollback con muestra real | Fases E y F; datos facilitados |
| H. Campaña real y cierre | 2 semanas | Corte, conciliación, acta de cierre y retirada de permisos | Fase G aprobada |

Total orientativo: **30 semanas** con solapamiento parcial de F con D y E.

### 16.2 Hitos

| Hito | Contenido | Criterio de paso |
| --- | --- | --- |
| M1 | Decisiones cerradas y especificación v1.0 | §17 sin ADR bloqueantes abiertos |
| M2 | Autenticación y auditoría funcionando | AC-06, AC-26 |
| M3 | Clientes y fichas con permisos | AC-04, AC-05, AC-21 |
| M4 | Bóveda operable y recuperable | AC-22, con constitución, sobre de recuperación y prueba de restauración |
| M5 | Aplicación completa en ensayo | AC-03, AC-19, AC-30, AC-31 |
| M6 | Migración simulada y aprobada | AC-08, AC-12, AC-13, AC-16 |

### 16.3 Equipo y estimación

| Perfil | Dedicación propuesta |
| --- | --- |
| Desarrollo backend/API y BD | 1 persona a tiempo completo |
| Desarrollo web | 1 persona a tiempo completo |
| Desarrollo Android | 0,5 persona |
| Seguridad y bóveda (revisión) | Externo, por hitos M2 y M4 |
| Protección de datos (RGPD) | Asesoría externa, por hitos M1 y M4 |
| Responsable de datos y aceptación | Interno, parcial |

La estimación no incluye la campaña real, cuyo esfuerzo depende del volumen de revisión humana (Fase 5) y no de la programación.

### 16.4 Riesgos

| Riesgo | Impacto | Mitigación |
| --- | --- | --- |
| Pérdida del servidor o de las claves de Windows | Imposibilidad de desellar | Sobre de recuperación sellado (SEC-12), prueba anual y rotación tras su uso |
| Imagen completa del disco por un administrador | Compromiso de la bóveda (DPAPI no protege del volcado completo) | Riesgo aceptado en v1; mitigación prevista: sellado al TPM sin cambio de diseño; limitar administradores y auditar accesos |
| Un administrador desella solo | Sin separación de deberes en el desellado | Riesgo aceptado el 29/10/2026; auditoría de cada desellado, revisión mensual (PRM-16) y pocos administradores |
| Perfil VPN comprometido o dispositivo perdido | Acceso a la red interna | Aislamiento SEC-16, perfiles individuales y revocación por CRL (SEC-17), MFA de Entra en la aplicación (SEC-18) |
| Concentración de roles en plantilla de 1–3 personas | Sin separación de deberes efectiva | **Riesgo aceptado el 29/10/2026** con controles compensatorios de §3.5: no autoaprobación, revisión externa mensual y doble control limitado |
| Expediente RGPD sin iniciar | Bloquea producción y expone a sanción; impide cargar datos de clientes | **Riesgo abierto el 29/10/2026:** AC-23 es puerta obligatoria; RGPD-01 se firma antes de cargar datos de cada cliente |
| Fuga de credenciales por el origen (OneDrive) | Alto, reputacional y legal | Origen inmutable, revisión de accesos, rotación posterior fuera de campaña (MIG-14) |
| Complejidad de la migración en revisión humana | Retraso del corte | Lotes, muestreo justificado, cola de revisión priorizada |
| Ambigüedad de entidades (duplicados) | Datos incorrectos | Revisión obligatoria del 100 % de asociaciones (Fase 5) |
| Dependencia de una sola persona | Bloqueo operativo | Documentación, reparto de claves, procedimientos escritos |
| Alcance creciente (ERP, tickets, RMM) | Desviación del plan | §0.3 fuera de alcance explícito |
| Cumplimiento RGPD incompleto | Sanción y bloqueo del proyecto | AC-23 como puerta antes de producción |
| Disponibilidad del servidor local | Interrupción del servicio | NFR-01, copias, RTO acordado |

---

## 17. Decisiones pendientes (ADR)

Cada ADR se cierra en la Fase A. Formato: contexto, opciones, recomendación, criterio y estado.

| ADR | Decisión | Opciones | Recomendación | Estado |
| --- | --- | --- | --- | --- |
| ADR-001 | Motor de bóveda | (a) Servicio propio con cifrado por envoltura sobre libsodium; (b) Vaultwarden/Bitwarden autohospedado; (c) HashiCorp Vault / OpenBao (KV v2 + Transit); (d) Passbolt. | **Cerrada: (a).** Bóveda integrada en la aplicación con libsodium (XChaCha20-Poly1305) y cifrado por envoltura; sin servicio externo de claves. Se descartan (b) y (d) por duplicar permisos y usuarios, y (c) se reevaluará como mejora (ver ADR-009). | **Cerrada 29/10/2026** |
| ADR-002 | Acceso remoto al servidor local | (a) VPN FortiGate; (b) WireGuard propio; (c) Tailscale; (d) Cloudflare Tunnel; (e) OpenVPN en la UniFi Dream Machine. | **Cerrada: (e).** OpenVPN ya configurado en la Dream Machine, con perfil individual por dispositivo y red de VPN aislada (§6.6). Descartado (d) porque el tráfico se descifra en el borde de un tercero (RGPD-06); (b) y (c) quedan como alternativas si el router cambia. | **Cerrada 29/10/2026** |
| ADR-003 | Almacenamiento documental | (a) Sistema de archivos cifrado con rutas por hash; (b) BLOB en PostgreSQL; (c) almacenamiento de objetos compatible S3 local. | **Cerrada: (a).** Ficheros en volumen cifrado con rutas derivadas del hash; metadatos, hash y clasificación en la BD. Se reevalúa (c) si el volumen supera 200 GB o si NFR-04 lo exige. | **Cerrada 29/10/2026** |
| ADR-004 | Base de datos | PostgreSQL frente a alternativas | **Cerrada: PostgreSQL**, versión estable soportada, por madurez, FTS integrada, transacciones, JSONB para fichas y respaldo consolidado. | **Cerrada 29/10/2026** |
| ADR-005 | Framework web | (a) ASP.NET Core + Blazor; (b) React/Next; (c) Vue; (d) Flutter Web. | **Cerrada: (a), Blazor Server.** Un solo lenguaje en servidor y web, reutiliza el entorno .NET existente y evita duplicar la API para el navegador. La misma API la consume la APK. (d) descartado para pantallas densas de datos. | **Cerrada 29/10/2026** |
| ADR-006 | Framework Android | (a) Kotlin + Compose; (b) Flutter; (c) React Native. | **Cerrada: (a).** Integración directa con Android Keystore, biometría, pantallas seguras y captura bloqueada; un solo código nativo y sin puente intermedio para el material cifrado. | **Cerrada 29/10/2026** |
| ADR-007 | Búsqueda | PostgreSQL FTS + trigram, Meilisearch, Elasticsearch | **Cerrada: PostgreSQL FTS + `pg_trgm`** en v1, tras la API. Se revisará en la Fase E si no alcanza el p95 de NFR-03 o si se indexa texto completo de los 100.000 documentos. | **Cerrada 29/10/2026** |
| ADR-008 | Distribución APK | (a) Managed Google Play/Intune; (b) APK directa con control de versión; (c) envío por correo. | **Cerrada: (b).** APK firmada servida por HTTPS interno, verificación de firma, bloqueo por versión mínima y custodia de la clave de firma (§6.7). Se descarta (a) por no disponer de MDM y (c) por falta de integridad. | **Cerrada 29/10/2026** |
| ADR-009 | Custodia de claves | (1) Clave en la app protegida por el sistema; (2) Reparto 3 de 5 reconstruido al arrancar; (3) OpenBao/KMS local; (4) HSM USB. | **Cerrada: (1), con DPAPI + MFA.** La KEK se guarda cifrada por Windows y se libera solo a un administrador con Entra + MFA. Un sobre de recuperación sellado cubre la pérdida del servidor y obliga a rotar al usarse. Se descarta el reparto 3 de 5 por exigir dos personas en cada reinicio. TPM como mejora sin cambio de diseño. | **Cerrada 29/10/2026** |
| ADR-010 | Retención documental | (a) Plazos propios aplicados a todo; (b) política a dos niveles; (c) dejar todo al contrato. | **Cerrada: (b).** Datos propios con plazos legales orientativos; datos de clientes según contrato de encargo y 90 días de cortesía; datos operativos con sus propios plazos (§8.3). Queda pendiente la validación jurídica de las cifras, no del diseño. | **Cerrada 29/10/2026** |
| ADR-011 | Certificado y nombre interno del servidor | (a) Nombre público con DNS interno y certificado público (ACME DNS-01); (b) CA propia instalada en cada dispositivo; (c) certificado autofirmado con excepción manual. | **Cerrada: (a).** `gestion.ideonetworks.com` (o el nombre que se acuerde) resuelve solo dentro de la VPN y el certificado se renueva por DNS-01. La APK no instala CA propia. (b) queda como alternativa si cambia el dominio; (c) descartado en producción. | **Cerrada 29/10/2026** |

Pendientes explícitos adicionales: v0.4 original; activos de marca y licencias distribuibles; icono APK; tenant y política MFA; versiones Android compatibles; plazo de ocultación de secretos y de sesión; alcance de documentos voluminosos; retención de staging, copias y auditoría; responsables nominales; RTO y ventana de rollback definitivos.

Estado del expediente RGPD (§8): **pospuesto por decisión del responsable de datos el 29/10/2026**; AC-23 sigue bloqueando la producción y RGPD-01 debe firmarse antes de cargar datos de un cliente.

Estado de responsables (§1.5): responsable de datos, de aceptación y custodio de bóveda asignados el 29/10/2026 (el custodio es dirección, sin apoyo externo).

---

## 18. Historial de versiones

Historial recuperado de los mensajes; no sustituye las notas literales de los Markdown anteriores no disponibles.

| Versión | Cambios |
| --- | --- |
| 0.1.0 | Documento maestro inicial; 82 carpetas, catálogo, módulos, propuesta PostgreSQL, API, operaciones, seguridad y fases. Se informó de 614 líneas. |
| 0.2.0 | Incorporación del Excel y sus cinco hojas, conciliación del inventario, procedencia y validaciones. Se informó de 687 líneas. |
| 0.3.0 | Bóveda de credenciales integrada, servidor local, web/APK, permisos granulares, campos personalizados y auditoría. Se informó de 914 líneas. |
| 0.4.0 | Microsoft 365 / Entra ID con Authenticator; fichas dinámicas con configuraciones, nuevos tipos y campos y visibilidad solo con información autorizada. Se informó de 993 líneas. |
| 0.5.0 | Identidad visual verificable; diseño responsive web/Android; migración única con origen inmutable, inventario, extracción, normalización, deduplicación, revisión, bóveda, trazabilidad, simulación, corte y rollback; contraste de adjuntos y criterios de aceptación. Consolidación literal pendiente de v0.4. |
| **0.6.0** | **Alcance, fuera de alcance, supuestos, glosario y criterios de éxito; requisitos no funcionales con objetivos medibles; roles y matriz de permisos; gestión de claves y recuperación; cumplimiento RGPD; observabilidad y retención; contrato de API y errores/límites; matriz de trazabilidad completa y 13 criterios de aceptación nuevos; plan de proyecto con hitos y riesgos; ADR-001…ADR-010; aclaración de «Password Safe» como referencia funcional; conciliación 106/126/94 y del redondeo de 0,1 MB; breakpoints de prueba alineados a 320 px; anexo de catálogo extraído a fichero propio con hash.** |
| **0.6.1** | **Cierre de ADR-001 y ADR-009 el 29/10/2026: bóveda integrada con libsodium y cifrado por envoltura, sin servicio externo de claves; clave maestra sin persistencia, en cinco partes con umbral 3 de 5 y reconstrucción en memoria al arrancar. Se reescriben SEC-11, SEC-12 y SEC-14, el procedimiento de §6.4, el RTO de bóveda en NFR-02, los riesgos de §16.4 y AC-22. Riesgo residual documentado: extracción de la KEK desde memoria por un administrador del servidor.** |
| **0.6.2** | **Cierre de ADR-002 el 29/10/2026: acceso remoto por el servidor OpenVPN de la UniFi Dream Machine, con perfil individual por dispositivo y red de VPN aislada. Nuevo §6.6 (SEC-15…SEC-18), criterio AC-32, fila de riesgos de perfil comprometido y ADR-011 (certificado y nombre interno). Se descarta Cloudflare Tunnel por descifrado en el borde.** |
| **0.6.3** | **Cierre de ADR-011 el 29/10/2026: certificado de CA pública con validación DNS-01 y renovación automática, nombre interno que solo resuelve dentro de la VPN. Nuevo SEC-19 y criterio AC-33; actualizada la matriz de trazabilidad y el recuento de requisitos.** |
| **0.6.4** | **Cierre de ADR-003, ADR-004 y ADR-007 el 29/10/2026: PostgreSQL como base de datos, documentos en sistema de ficheros cifrado con rutas por hash y búsqueda con PostgreSQL FTS + `pg_trgm` detrás de la API. Actualizada la tabla de componentes de §6.1 con el estado decidido.** |
| **0.6.5** | **Cierre de ADR-005 y ADR-006 el 29/10/2026: ASP.NET Core + Blazor Server en web/API y Kotlin + Jetpack Compose en Android. Actualizada la tabla de componentes de §6.1; la distribución de la APK sigue pendiente (ADR-008).** |
| **0.6.6** | **Cierre de ADR-008 el 29/10/2026: APK directa por HTTPS interno, sin MDM, con verificación de firma, bloqueo por versión mínima y custodia de la clave de firma. Nuevo §6.7 (SEC-20, SEC-21), ampliado AC-31 y añadido el endpoint `/api/v1/app/version`.** |
| **0.6.7** | **Cierre de ADR-010 el 29/10/2026: política de conservación a dos niveles en §8.3 — datos propios con plazos legales, datos de clientes según contrato de encargo y 90 días de cortesía, datos operativos con plazos propios. Queda pendiente la validación jurídica de las cifras, no del diseño.** |
| **0.6.8** | **Validación de NFR el 29/10/2026: NFR-01 baja a 99 % de disponibilidad y NFR-05 sube a 30 usuarios concurrentes; AC-19 actualizado. El resto de NFR se mantiene.** |
| **0.6.9** | **Ajuste de la matriz de permisos a una plantilla de 1–3 personas: nuevo §3.5 con PRM-15 (no autoaprobación) y PRM-16 (revisión externa mensual de auditoría), doble control limitado, parte de la clave en custodia externa y riesgo de concentración de roles aceptado y registrado en §16.4. Ampliado AC-21.** |
| **0.6.10** | **Expediente RGPD pospuesto por decisión del responsable de datos el 29/10/2026. Se registra como riesgo abierto en §16.4 y §17: AC-23 sigue bloqueando la producción y RGPD-01 debe firmarse antes de cargar datos de un cliente. No se interpreta como cumplimiento tácito.** |
| **0.6.11** | **Custodio de bóveda designado el 29/10/2026 en dirección, sin apoyo externo. Nuevo reparto de partes en §6.4 (nadie reúne tres; reservas bajo doble control), ampliado AC-22 y riesgo asociado registrado en §16.4. Queda una única condición de producción: el expediente RGPD.** |
| **0.6.12** | **Cambio del modelo de claves (ADR-009 revisada) el 29/10/2026: se abandona el reparto 3 de 5 y se adopta KEK cifrada con DPAPI, liberada a un administrador autenticado con Entra + MFA, más un sobre de recuperación sellado que obliga a rotación al usarse. Reescritos §6.4 (SEC-11, SEC-12, SEC-14), AC-22, NFR-02 y los riesgos de §16.4; TPM queda como mejora sin cambio de diseño.** |
| **1.0.0** | **Aprobación para desarrollo el 29/10/2026. Contenido idéntico a 0.6.12 más el registro de aprobación y sus condiciones: alcanza diseño y ensayo, no la migración real ni la carga de datos; producción supeditada a AC-23 (RGPD) y a la firma del revisor técnico. Se conservan v0.5 y v0.6.12 como histórico.** |

### 18.1 Sustituciones y precisiones explícitas

- El segundo factor por correo fue sustituido por Microsoft Authenticator en v0.4; no se reactiva.
- «82 clientes» se interpreta como 82 carpetas catalogadas, no 82 identidades empresariales confirmadas.
- «Estado» del catálogo se conserva como actividad documental, no estado comercial.
- Las recomendaciones históricas de mover carpetas o modificar archivos con credenciales quedan sin efecto para esta campaña: origen inmutable.
- Se distingue referencia documental de copia importada a almacenamiento propio.
- Biometría Android es desbloqueo local complementario; no sustituye políticas de identidad y autorización.
- La implementación criptográfica continúa pendiente; el documento define requisitos sin afirmar una solución validada.
- **«Password Safe» designa en v0.6 una referencia funcional de interfaz, no un producto seleccionado.**
- La cifra oficial de indicios de secreto en nombres es 126 (patrón ampliado sobre nombre de archivo), sin equipararla a secretos confirmados.

---

## 19. Evidencias y comprobaciones

| Evidencia | Resultado |
| --- | --- |
| Lectura de los dos adjuntos | Completada sin modificación. |
| Recuento de «Resumen» frente a «Inventario» | Coinciden las 82 carpetas y todos sus recuentos. |
| Conciliación de filas | 37.677 + 4 + 1 = 37.682. |
| Discrepancias de v0.5 | 106/126/94 explicadas por criterio de conteo; 0,1 MB explicado por redondeo acumulado. |
| Documentos clave | 197 en 43 carpetas. |
| Documentos indexados en BD anterior | 194 en 37 carpetas. |
| Requisitos con identificador | 113 (BASE 14, PRM 16, SEC 21, OBS 5, NFR 16, RGPD 12, MIG 16, DAT 2, UI 11, sin contar criterios de aceptación). |
| Criterios de aceptación | 33 (AC-01…AC-33). |
| Cobertura de trazabilidad | 100 % de los requisitos listados en §15. |
| Decisiones de arquitectura cerradas | ADR-001, ADR-002 y ADR-009 cerradas el 29/10/2026 por el responsable de datos y de aceptación. |
| Inspección del sitio | Portada de escritorio y móvil a 390 × 844; estilos calculados, reglas de contenedor, fuentes y SVG. |
| Fuente local de clientes | No accedida. No se ha ejecutado migración ni validación de credenciales. |
| v0.4 | Referenciada en conversación, contenido original no recuperado. |

SHA-256 de los ficheros de referencia:

- `ESPECIFICACION_GESTION_CLIENTES_IT_v0.5.md`: `87145ee74f75192b607a936d0548f4a045a5f19bc05a08489822cccdbaeede31`
- `catalogo-clientes.xlsx`: `85c6877f13063af3e3a4513ba278dbb19b055a85f250bdcdd5b854b10c90e178`
- `CATALOGO-CLIENTES.md`: `215b6d20e2b58f0f3f676f184b450b5c789de8d9665ba666021ba0849100600c`

El SHA-256 de este documento se registra al aprobarlo en §1.5; cualquier cambio posterior exige nueva versión.

Fuentes técnicas complementarias: [MSAL](https://learn.microsoft.com/en-us/entra/identity-platform/msal-authentication-flows), [código de autorización OAuth/OIDC](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-authorization-code-flow), [WCAG 2.2](https://www.w3.org/TR/WCAG22/).

---

## Anexo A. Catálogo de clientes

El catálogo completo se conserva como fichero propio, **sin reinterpretación**, en:

`anexos/ANEXO-A-catalogo-clientes-2026-10-29.md`

- SHA-256: `215b6d20e2b58f0f3f676f184b450b5c789de8d9665ba666021ba0849100600c` (idéntico a `CATALOGO-CLIENTES.md`).
- Origen: `C:\Users\koto\OneDrive - Ideo Networks SL\Clientes`.
- Alcance: 82 carpetas de cliente, 37.677 archivos, 57,5 GB y 4 archivos sueltos en la raíz.

Sus afirmaciones y recomendaciones son datos de origen, **no instrucciones operativas** de esta especificación (§4, §9 y §18.1). En particular, la calificación de clientes, las fusiones propuestas, la afirmación de credenciales en claro y las recomendaciones de mover carpetas requieren las precisiones de §4 y §9, y no autorizan cambios en la fuente.

Resumen por tipo (verificado en v0.6):

| Tipo | Clientes | Archivos | GB |
| --- | ---: | ---: | ---: |
| Soporte IT | 2 | 18.245 | 9,0 |
| Infraestructura | 14 | 343 | 1,2 |
| Consultoría / ERP | 5 | 7.512 | 3,1 |
| Web | 9 | 9.415 | 0,4 |
| Web / diseño | 2 | 391 | 0,6 |
| Diseño / marca | 11 | 541 | 0,6 |
| Legal / contratos | 4 | 63 | 0,1 |
| Administración | 28 | 211 | 0,6 |
| Audiovisual | 4 | 578 | 34,8 |
| Marketing | 3 | 378 | 7,2 |

Al no repetirse dentro de este documento, el anexo no puede desviarse del fichero real: si el catálogo se regenera, cambia su hash y debe actualizarse esta referencia con una nueva versión de la especificación.

---

## Anexo B. Identidad visual

Evidencia de la portada española [Ideo Networks](https://ideonetworks.com/es) inspeccionada el 29/10/2026: captura de escritorio, estilos calculados, reglas de la hoja [index-DQI8vHIU.css](https://ideonetworks.com/assets/index-DQI8vHIU.css) y comprobación móvil a 390 × 844 CSS px. El CSS tiene nombre versionado y puede cambiar con futuras publicaciones; los valores de este anexo fijan la referencia de v0.6.

La descarga directa del CSS no estuvo disponible por protección del sitio; se inspeccionaron las reglas ya cargadas en el navegador. Esta evidencia no constituye un manual de marca completo ni certifica todas las páginas del sitio.

### B.1 Paleta observada

| ID | Elemento o selector verificable | Valor observado | Aplicación en el producto |
| --- | --- | --- | --- |
| UI-01 | SVG de `nav a[aria-label="iDeo, inicio"] img`, regla `.cls-1` | `#006CB5` | Mantener el color del logotipo original. |
| UI-02 | Botón principal de navegación; `--primary`/`--brand` | `hsl(203 100% 35%)`; calculado `rgb(0,110,179)` ≈ `#006EB3` | Acciones primarias, selección y foco con contraste comprobado. |
| UI-03 | Fondo del `body`; `--background` | `hsl(180 20% 98%)`; calculado `#F9FBFB` | Fondo general claro. |
| UI-04 | `--surface`, `--card` | `hsl(0 0% 100%)` / `#FFFFFF` | Formularios y superficies de trabajo. |
| UI-05 | `body`; `--text` | `hsl(216 16% 14%)`; calculado `#1E2329` | Texto principal. |
| UI-06 | `h1#home-title`; `--heading` | `hsl(210 20% 8%)`; calculado `#101418` | Títulos. |
| UI-07 | `--text-muted`, enlace de contacto del pie | `hsl(215 14% 34%)`; calculado `#4B5563` | Texto secundario, conservando legibilidad. |
| UI-08 | Bordes calculados; `--border` | `hsl(214 32% 91%)`; calculado `#E1E7EF` | Separación de secciones y tablas; no usarlo solo para identificar un control. |
| UI-09 | Sección `aria-labelledby="necesidad-title"`; `--muted` | `hsl(214 32% 96%)`; calculado `#F2F4F8` | Fondo secundario. |

Los hexadecimales calculados son conversiones RGB de 8 bits. La implementación conservará la representación HSL de referencia cuando proceda. **El azul del logotipo y el azul de interacción no son idénticos** y no se normalizarán a un único tono sin decisión de marca.

Se observaron variables de familias `--area-mantenia: 203 100% 35%`, `--area-connect: 192 95% 32%`, `--area-shield: 156 73% 31%`, `--area-forge: 258 70% 55%` y `--area-automate: 330 72% 45%`. Son valores HSL. La aplicación usa la paleta principal anterior; las familias no se asignarán automáticamente a estados de seguridad ni a clientes.

Los colores semánticos de éxito, aviso y error se definirán como tokens funcionales del producto y pasarán contraste. Su presencia en el CSS del sitio no demuestra que todas sus combinaciones sean accesibles. Cada estado llevará texto e icono, además de color.

### B.2 Tipografía observada

| Elemento | Evidencia | Criterio |
| --- | --- | --- |
| Cuerpo y controles | `"Inter Variable", Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`; cuerpo 16 px, peso 400, interlínea 24 px | Usar Inter para datos, navegación y formularios. |
| Títulos | `Montserrat, system-ui, ...`; H1 peso 600 | Usar Montserrat 600 para títulos y jerarquía. |
| Escala de portada | H1 con `clamp(2.3rem,4.5vw,4rem)` y línea 1,07; 57,63 px en escritorio observado y 36,8 px en móvil | Referencia de marca; adaptar tamaño al trabajo de gestión, según §5. |
| Botón superior | Inter 14 px, peso 600, interlínea 21 px | Conservar la familia y peso; ampliar área táctil en producto. |
| Recursos | Reglas `@font-face` locales: Inter variable 100–900 y Montserrat 600/700 | Distribuir fuentes con web/APK, documentar licencias (ambas SIL OFL) y evitar dependencia de una CDN externa para operar. |

Ejemplos de recursos observados: [Inter Latin](https://ideonetworks.com/assets/inter-latin-wght-normal-Dx4kXJAl.woff2) y [Montserrat Latin 600](https://ideonetworks.com/assets/montserrat-latin-600-normal-UVxSCcoG.woff2). Estos enlaces identifican la evidencia, no sustituyen la gestión de activos y licencias (AC-27).

### B.3 Composición y marca observadas

La portada presenta logotipo azul a la izquierda, navegación horizontal en escritorio, fondo claro, títulos de gran jerarquía y acciones azules. El bloque inicial distribuye texto y diagrama de servicios en dos columnas; en móvil se apilan y aparece el control de menú. Se observan líneas finas, rombos en el diagrama y secciones con numeración. Los elementos decorativos se reservarán para identidad y orientación, sin saturar las pantallas de datos. Fuente: [portada inspeccionada](https://ideonetworks.com/es).

Reglas de `.container-custom`: ancho máximo 1440 px; márgenes automáticos; relleno horizontal 24 px base, 32 px desde 768, 48 px desde 1024 y 64 px desde 1280. La navegación tiene altura 72 px. Se observaron botones rectangulares con radio 6 px y una llamada principal tipo cápsula. Estas magnitudes son del sitio, no obligaciones de densidad para la aplicación.

El SVG original tiene `viewBox="0 0 240 96"`, proporción 2,5:1, y en la cabecera altura 40 px. Mantener trazados, proporción, denominación «iDeo» y punto de marca. No reemplazarlo por texto escrito con Montserrat, deformarlo ni inventar un nuevo símbolo. La adaptación al icono Android utilizará el activo original dentro de una máscara con margen seguro; la composición requiere aprobación visual porque no se ha localizado una norma oficial de icono de aplicación.
