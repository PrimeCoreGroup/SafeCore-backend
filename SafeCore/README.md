# SafeCore Backend

API ASP.NET Core para administrar dispositivos de seguridad estructural, recibir telemetría IoT, registrar riesgos y coordinar respuestas. La aplicación se organiza como un monolito modular: los contextos comparten el host y la base MySQL, mientras los controladores dependen de contratos de repositorio y EF Core queda en el adaptador de infraestructura.

## Contextos

- **IAM / IdentityAccess**: usuarios, autenticación JWT, roles y sesiones de acceso para dispositivos.
- **DeviceManagement**: inventario y credenciales de sensores y nodos Edge.
- **SensorDataIngestion**: lecturas escalares de gas y temperatura y vectores X/Y/Z de acelerómetros y giroscopios.
- **RiskDetection**: evaluación de telemetría e incidentes clasificados como fuego/calor, gas o humo combustible, impacto y fallo de sensor.
- **Emergency**: protocolos, respuestas y acciones de respuesta.
- **Notifications**: persistencia de notificaciones.
- **MonitoringConfiguration**: configuración y sincronización.

La interfaz `IRepository<TEntity>` es el puerto de persistencia y `EfRepository<TEntity>` su adaptador. `SafeCoreDbContext` registra relaciones, índices y reglas de integridad.

## Autenticación y autorización

No existe registro público. El primer administrador se crea con `BootstrapAdmin__Email` y `BootstrapAdmin__Password`; luego los administradores crean cuentas con `POST /api/users`. Los roles son `Administrator`, `Operator`, `Responder` y `Resident`. Los tokens de usuario duran 15 minutos y el login tiene bloqueo temporal por intentos fallidos y límite de solicitudes.

El alta de un dispositivo (`POST /api/devices`) genera un secreto aleatorio que solo se devuelve en esa respuesta. El cliente debe guardarlo en el almacén seguro del dispositivo y cambiarlo por un JWT mediante `POST /api/iam/auth/device-token`. El secreto se guarda como SHA-256 y se puede rotar con `POST /api/devices/{id}/rotate-secret`.

| Método y ruta | Acceso |
|---|---|
| `POST /api/iam/auth/login` | Público, usuario y contraseña |
| `POST /api/iam/auth/device-token` | Público, ID y secreto del dispositivo |
| `POST /api/iam/auth/change-password` | Usuario autenticado; se usa tras un restablecimiento administrativo |
| `GET /api/iam/auth/me` | Usuario autenticado |
| `/api/users` y sus rutas secundarias | Administrador |
| Lectura de inventario y telemetría | Administrador, operador o respondedor |
| `POST /api/SensorReadings` | JWT de dispositivo, administrador u operador asignado al nodo |
| `/api/properties`, `/api/zones` | Lectura según asignación; escritura solo administrador |
| `PUT /api/devices/{id}/zone` | Administrador u operador asignado al inmueble |
| `GET /api/settings` | Administrador u operador; actualización solo administrador |
| `GET /api/settings/thresholds` | Administrador, operador o respondedor; actualización solo administrador |
| `/api/analytics/*` | Administrador u operador; los datos se limitan a sus nodos asignados |
| Cambios de configuración, riesgos y respuestas | Según roles configurados en el controlador |
| `GET /health/live`, `GET /health/ready` | Público; readiness comprueba conexión a MySQL |

El JWT de dispositivo incluye su `device_id`; la ingesta rechaza lecturas que intenten usar el ID de otro sensor. No se devuelve el hash de credenciales ni `PasswordHash` en las APIs.

## Guía de integración para frontend

Esta sección describe los contratos HTTP implementados actualmente. Las rutas no distinguen mayúsculas y minúsculas. En producción usa la URL pública HTTPS de Render; localmente, la URL y puerto indicados por `dotnet run` o `launchSettings.json`. Swagger solo se habilita en `Development`.

### Formato común

- Enviar y recibir JSON con `Content-Type: application/json` y `Accept: application/json`.
- Las propiedades JSON usan `camelCase` (`deviceId`, `createdAt`).
- Las fechas deben enviarse en formato ISO 8601 con zona horaria, por ejemplo `2026-11-06T14:30:00Z`.
- Para rutas protegidas, enviar `Authorization: Bearer <accessToken>`.
- Las listas responden como un arreglo JSON directo, no como `{ items, total }`. Aceptan `?page=1&pageSize=100`; la página mínima es 1 y `pageSize` se limita a 1–250 (predeterminado 100). No se devuelve un total de registros.
- El patrón de éxito es `200` para lecturas/acciones que devuelven contenido, `201` para altas, y `204` para actualizaciones/bajas sin contenido.
- Los errores de validación normalmente responden `400` con `ValidationProblemDetails`; `401` indica token ausente/inválido o credenciales inválidas, `403` rol insuficiente, `404` recurso inexistente, `409` conflicto de integridad y `429` límite de login. No asumir un mensaje idéntico para todos los errores.

### Roles

| Rol | Uso habitual |
|---|---|
| `Administrator` | Gestión completa y administración de usuarios. |
| `Operator` | Operación de sensores, riesgos, protocolos, respuestas y notificaciones. |
| `Responder` | Consulta operativa y registro de acciones de respuesta. |
| `Resident` | Consulta sus propias notificaciones. |
| `Device` | Token de sensor; solo habilita la política de ingreso de lecturas. |

Los endpoints de login y canje de secreto de dispositivo son públicos. El resto requiere JWT salvo los health checks. La política de ingreso de lecturas acepta `Device`, `Administrator` u `Operator`.

### IAM y autenticación

#### `POST /api/iam/auth/login` — público

Inicia sesión. El login permite hasta 10 solicitudes por minuto por IP; cinco fallos consecutivos pueden bloquear la cuenta durante 15 minutos.

```json
{
  "email": "operador@example.com",
  "password": "ContraseñaDePrueba-2026!"
}
```

`200 OK` devuelve:

```json
{
  "token": {
    "accessToken": "<jwt>",
    "tokenType": "Bearer",
    "expiresInSeconds": 900
  },
  "user": {
    "id": "00000000-0000-0000-0000-000000000001",
    "email": "operador@example.com",
    "firstName": "Ana",
    "lastName": "Pérez",
    "roles": ["Operator"]
  }
}
```

`401` se devuelve si el email/contraseña no son válidos o el usuario no está activo. No hay endpoint de registro público; un administrador crea usuarios.

#### `GET /api/iam/auth/me` — autenticado

Devuelve el usuario asociado al JWT vigente (`200`, mismo objeto `user` del login). Usarlo al cargar la sesión o para recuperar los roles. Token expirado/inválido: `401`.

#### `POST /api/iam/auth/device-token` — público

Intercambia el secreto de aprovisionamiento por un JWT de dispositivo. El secreto se entrega una sola vez al crear o rotar el dispositivo; guardarlo en el almacén seguro del dispositivo.

```json
{
  "deviceId": "00000000-0000-0000-0000-000000000001",
  "secret": "<secreto entregado al aprovisionar el dispositivo>"
}
```

`200 OK` devuelve `TokenResponse`: `accessToken`, `tokenType` y `expiresInSeconds`. Credenciales incorrectas: `401`.

### Usuarios — prefijo `/api/users` — solo `Administrator`

| Método y ruta | Resultado |
|---|---|
| `GET /api/users?page=1&pageSize=100` | `200`: arreglo de usuarios, ordenado por email. |
| `GET /api/users/{id}` | `200`: usuario; `404`: no existe. `id` es GUID. |
| `POST /api/users` | `201`: crea usuario y devuelve usuario con su rol. |
| `PUT /api/users/{id}` | `204`; `404`; `409` si el administrador intenta deshabilitarse a sí mismo. Actualiza nombre, apellido y estado; no cambia email, contraseña ni rol. |
| `DELETE /api/users/{id}` | `204`; `404`; `409` si es el último administrador, si se intenta borrar la propia cuenta o si tiene notificaciones asociadas. En esos casos, deshabilitar la cuenta con PUT. |
| `GET /api/users/{id}/assignments` | `200`: asignaciones de inmueble y zona. |
| `PUT /api/users/{id}/assignments` | Reemplaza la lista completa de asignaciones. |
| `POST /api/users/{id}/reset-password` | Restablece contraseña y fuerza cambio en siguiente login. |

Alta (`POST`): `email` válido, contraseña de 12–128 caracteres que incluya mayúscula, minúscula, número y carácter no alfanumérico, nombres de hasta 100 caracteres y `role` exactamente uno de `Administrator`, `Operator`, `Responder`, `Resident`.

```json
{
  "email": "operador@example.com",
  "password": "ContraseñaDePrueba-2026!",
  "firstName": "Ana",
  "lastName": "Pérez",
  "role": "Operator"
}
```

Actualización (`PUT`):

```json
{
  "firstName": "Ana",
  "lastName": "Pérez García",
  "status": "Active"
}
```

`PUT /api/users/{id}/assignments` recibe `{ "assignments": [{ "propertyId": "...", "zoneId": "..." }] }`; `zoneId` puede ser `null` para asignar un inmueble entero. Un usuario `Resident` debe tener zona. `POST /api/users/{id}/reset-password` recibe `{ "newPassword": "..." }`; el usuario debe iniciar sesión y llamar `POST /api/iam/auth/change-password` con `currentPassword` y `newPassword`. La respuesta de login y `GET /api/iam/auth/me` incluyen `mustChangePassword`. El token anterior se invalida al cambiar la contraseña y la app debe iniciar sesión otra vez. La regla de contraseña requiere 12 caracteres, mayúscula, minúscula, número y símbolo. El backend devuelve `409` al intentar crear una cuarta cuenta Administrator.

`status` admite `Active` o `Disabled`. El objeto de respuesta contiene `id`, `email`, `firstName`, `lastName`, `status`, `roles` y `createdAt`; nunca contiene hashes de contraseña.

### Dispositivos — prefijo `/api/devices`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/devices?page=1&pageSize=100` | Administrator, Operator, Responder | `200`: arreglo, ordenado por número de serie. |
| `GET /api/devices/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `POST /api/devices` | Administrator, Operator | `201`: dispositivo aprovisionado y secreto en claro (solo esta respuesta). `409` si el serial ya existe. `createdByUserId` sale del JWT. |
| `PUT /api/devices/{id}` | Administrator | `204`; `404`; `409` serial duplicado. |
| `DELETE /api/devices/{id}` | Administrator | `204`; `404`; `409` si tiene lecturas, componentes o comandos de prueba registrados. |
| `POST /api/devices/{id}/rotate-secret` | Administrator | `200`: nuevo secreto en claro; `404`. El secreto anterior deja de servir. |
| `PUT /api/devices/{id}/zone` | Administrator, Operator asignado al inmueble | Asigna o quita la zona con `{ "zoneId": "..." }` o `{ "zoneId": null }`. |

`POST` y `PUT` reciben:

```json
{
  "serialNumber": "SC-LAB-001",
  "name": "Sensor laboratorio",
  "category": "ESP32",
  "connectivityStatus": "Online"
}
```

`serialNumber` y `name` hasta 100 caracteres; `category` hasta 50. `connectivityStatus`: `Online`, `Offline`, `Degraded` o `Maintenance`. La respuesta incluye `zoneId`, `zoneName`, `propertyId`, `propertyName`, `createdByUserId`, `createdByName` y `lastSeenAt`, además de los campos básicos. Al crear/rotar, el formato es `{ "device": { ... }, "deviceSecret": "..." }`. No se puede volver a consultar el secreto.

Componentes y conexión:

| Método y ruta | Acceso |
|---|---|
| `GET /api/devices/{id}/components` y `GET /api/devices/{id}/components/{componentId}` | Administrator, Operator o Responder con acceso al nodo |
| `POST /api/devices/{id}/components` | Administrator u Operator con acceso al nodo |
| `PUT /api/devices/{id}/components/{componentId}` | Administrator u Operator con acceso al nodo |
| `DELETE /api/devices/{id}/components/{componentId}` | Administrator u Operator con acceso; `409` si hay lecturas/acciones/pruebas vinculadas |
| `PUT /api/devices/{id}/manifest` | JWT del mismo dispositivo |
| `POST /api/devices/{id}/health` | JWT del mismo dispositivo |
| `POST /api/devices/{id}/components/{componentId}/test` | Administrator u Operator con acceso; responde `202` y `commandId` |
| `GET /api/devices/{id}/commands/{commandId}` | Administrator u Operator con acceso |

Los componentes tienen `kind` (`Sensor`/`Actuator`), `type`, `label`, `channel`, `status` (`Ok`, `Fault`, `Disconnected`, `Maintenance`, `Disabled`), `lastSeenAt`, `installedAt` y `replacedAt`. El manifiesto contiene `components` con `kind`, `type`, `label`, `channel` e `installedAt`; reemplaza la declaración anterior y deshabilita los componentes omitidos. El reporte de salud acepta `{ "components": [{ "componentId": "...", "status": "Ok" }] }`.

Las pruebas se encolan como comandos: el firmware debe consultar `GET /api/devices/{id}/commands/pending` usando su token, ejecutar la prueba y enviar `POST /api/devices/{id}/commands/{commandId}/result` con `{ "status": "Completed", "result": "..." }` o `Failed`. Si no hay respuesta en dos minutos, el comando vence y el componente pasa a `Fault`. Aún no existe transporte MQTT ni entrega push directa hacia el ESP32.

### Lecturas IoT — prefijo `/api/SensorReadings`

Las rutas son insensibles a mayúsculas; también puede usarse `/api/sensorreadings`.

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/SensorReadings?page=1&pageSize=100&from=...&to=...` | Administrator, Operator, Responder | `200`: arreglo paginado de lecturas de nodos asignados; filtros opcionales de fecha. |
| `GET /api/SensorReadings/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `POST /api/SensorReadings` | JWT Device, Administrator, Operator | `201`: lectura guardada. Para JWT de dispositivo, `deviceId` debe coincidir con el `device_id` del token; si no, `403`. |
| `PUT /api/SensorReadings/{id}` | Administrator, Operator | `204`; `404` lectura inexistente. |
| `DELETE /api/SensorReadings/{id}` | Administrator | `204` o `404`. |

Cuerpo para MQ-2 (ADC 12-bit; `digitalAlarm: true` indica salida activa LOW):

```json
{
  "deviceId": "00000000-0000-0000-0000-000000000001",
  "sensorType": "Gas",
  "measuredValue": 1840,
  "digitalAlarm": false,
  "unit": "esp32-adc-counts",
  "capturedAt": "2026-11-06T14:30:00Z"
}
```

Cuerpo para DS18B20: `sensorType: "Temperature"`, `measuredValue` en °C, `unit: "C"`; no enviar `digitalAlarm` ni ejes. Para reportar sensor DS18B20 desconectado, se admite `measuredValue: -127`.

```json
{
  "deviceId": "00000000-0000-0000-0000-000000000001",
  "sensorType": "Temperature",
  "measuredValue": 24.6,
  "unit": "C",
  "capturedAt": "2026-11-06T14:30:00Z"
}
```

Cuerpo para MPU6050 (`Accelerometer` o `Gyroscope`): enviar los tres ejes signed 16-bit, sin valor escalar ni alarma digital.

```json
{
  "deviceId": "00000000-0000-0000-0000-000000000001",
  "sensorType": "Accelerometer",
  "axisX": 160,
  "axisY": -80,
  "axisZ": 16300,
  "unit": "mpu6050-counts",
  "capturedAt": "2026-11-06T14:30:00Z"
}
```

Tipos exactos: `Gas`, `Temperature`, `Accelerometer`, `Gyroscope`. `capturedAt` no puede estar más de cinco minutos en el futuro. `Gas` requiere `digitalAlarm`; ADC entre 0–4095. `componentId` opcional identifica el sensor físico del nodo. El JSON de respuesta contiene `id`, los campos de lectura, `capturedAt` y `createdAt`. Al crear una lectura, se ejecuta la evaluación automática de riesgos.

### Situaciones de riesgo — ruta `/api/RiskSituations`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/RiskSituations?page=1&pageSize=100&from=...&to=...` | Administrator, Operator, Responder | `200`: arreglo paginado limitado a nodos asignados; fechas opcionales. |
| `GET /api/RiskSituations/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `POST /api/RiskSituations` | Administrator, Operator | `201`. Crea riesgo manual. |
| `PUT /api/RiskSituations/{id}` | Administrator, Operator | `204` o `404`. Actualiza tipo, estado, severidad y fecha detectada. |
| `DELETE /api/RiskSituations/{id}` | Administrator | `204`; `404`; `409` si hay una respuesta de emergencia vinculada. |

El POST/PUT recibe `riskType` (`Fire`, `GasOrSmoke`, `Earthquake`, `SensorFault`), `status` (`Open`, `Acknowledged`, `Resolved`), `severity` (`Low`, `Medium`, `High`, `Critical`) y `detectedAt` ISO 8601. El alta manual se marca con `origin: "Manual"`; los riesgos calculados de sensores incluyen `deviceId`, `origin: "Sensor"`, `componentId` si se identificó el sensor y pueden incluir `alertUntil`. Al pasar a `Resolved`, `resolvedAt` registra la hora UTC.

```json
{
  "riskType": "Fire",
  "status": "Open",
  "severity": "High",
  "detectedAt": "2026-11-06T14:30:00Z"
}
```

Los riesgos automáticos usan los umbrales de `SafetyThresholds` (por defecto, temperatura ≥40 °C, subida de gas >100 cuentas ADC, aceleración Δ magnitud >200 cuentas; fallo del DS18B20 a -127 °C). Se deduplican mientras estén activos y algunas lecturas pueden resolverlos automáticamente. El backend persiste la alerta: no acciona directamente buzzer, LED, relé ni envía notificaciones externas.

### Emergencias — protocolos, respuestas y acciones

#### Protocolos — `/api/emergency/protocols`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/emergency/protocols?page=1&pageSize=100` | Administrator, Operator, Responder | `200`: arreglo ordenado por nombre. |
| `GET /api/emergency/protocols/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `POST /api/emergency/protocols` | Administrator, Operator | `201`. |
| `PUT /api/emergency/protocols/{id}` | Administrator, Operator | `204` o `404`. |
| `DELETE /api/emergency/protocols/{id}` | Administrator | `204`; `404`; `409` si una respuesta lo usa. |

POST/PUT: `{ "name": "Evacuación", "emergencyType": "Fire", "isActive": true }`. `name` máximo 100 caracteres, `emergencyType` máximo 50. Una respuesta solo puede referir un protocolo activo.

#### Respuestas — `/api/EmergencyResponses`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/EmergencyResponses?page=1&pageSize=100` | Administrator, Operator, Responder | `200`: arreglo. |
| `GET /api/EmergencyResponses/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `POST /api/EmergencyResponses` | Administrator, Operator, Responder | `201`; `400` si protocolo no existe/no está activo o riesgo no existe. |
| `PUT /api/EmergencyResponses/{id}` | Administrator, Operator | `204`; `404`; `400` referencias inválidas. |
| `DELETE /api/EmergencyResponses/{id}` | Administrator | `204`; `404`; `409` si contiene acciones registradas. |

POST/PUT: `protocolId` GUID, `emergencyType` (máximo 50), `riskSituationId` GUID y `status` uno de `Open`, `InProgress`, `Resolved`, `Cancelled`.

```json
{
  "protocolId": "00000000-0000-0000-0000-000000000010",
  "emergencyType": "Fire",
  "riskSituationId": "00000000-0000-0000-0000-000000000020",
  "status": "Open"
}
```

#### Acciones — `/api/emergency/actions`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/emergency/actions?page=1&pageSize=100` | Administrator, Operator, Responder | `200`: arreglo. |
| `GET /api/emergency/actions/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `POST /api/emergency/actions` | Administrator, Operator, Responder | `201`; `400` si `emergencyResponseId` no existe. |
| `PUT /api/emergency/actions/{id}` | Administrator, Operator, Responder | `204`; `404`; `400` si respuesta no existe. |
| `DELETE /api/emergency/actions/{id}` | Administrator | `204` o `404`. |

POST/PUT: `emergencyResponseId` GUID, `actuatorType` (máximo 50), `instruction` (máximo 1000) y `status` uno de `Pending`, `Dispatched`, `Completed`, `Failed`, `Cancelled`.

```json
{
  "emergencyResponseId": "00000000-0000-0000-0000-000000000030",
  "actuatorType": "Valve",
  "instruction": "Cerrar válvula de gas según protocolo local",
  "status": "Pending"
}
```

Las acciones son registros de coordinación; no implican que el backend envíe un comando físico al ESP32.

### Notificaciones — ruta `/api/Notifications`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/Notifications?page=1&pageSize=100` | Administrator, Operator, Responder | `200`: arreglo. |
| `GET /api/Notifications/{id}` | Administrator, Operator, Responder | `200` o `404`. |
| `GET /api/Notifications/mine?page=1&pageSize=100` | Resident | `200`: solo notificaciones del usuario del token. |
| `POST /api/Notifications` | Administrator, Operator, Responder | `201`; `400` si el destinatario no existe. |
| `PUT /api/Notifications/{id}` | Administrator, Operator, Responder | `204` o `404`. |
| `DELETE /api/Notifications/{id}` | Administrator | `204` o `404`. |

POST/PUT recibe `recipientUserId` GUID, `title` (máximo 200), `body` (máximo 4000), `channel` (`Email`, `Sms`, `Push`) y `status` (`Pending`, `Delivered`, `Failed`). Este contexto persiste notificaciones, pero no implementa entrega real por correo, SMS o push.

### Ajustes y umbrales — `/api/settings`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/settings` | Administrator, Operator | Devuelve ajustes generales y umbrales actuales. |
| `PUT /api/settings` | Administrator | Actualiza nombre, zona horaria y opciones. |
| `GET /api/settings/thresholds` | Administrator, Operator, Responder | Devuelve los umbrales guardados en MySQL. |
| `PUT /api/settings/thresholds` | Administrator | Actualiza los umbrales con validación de rangos. |

`SystemSettingsDto`: `systemName`, `timeZone` (identificador IANA, por ejemplo `America/Lima`), `criticalAlertsEnabled`, `notifyResponders`, `manualConfirmation`. Umbrales: `temperatureCelsius` (>−55 y ≤125), `gasAdcRiseCounts` (>0 y ≤4095), `accelerationMagnitudeDeltaCounts` (>0 y ≤56755) e `impactAlertMilliseconds` (100–10000). Los cambios de umbral aplican en la siguiente lectura sin redeploy.

`criticalAlertsEnabled` y `notifyResponders` controlan la creación de notificaciones internas pendientes para responders; no desactivan el registro de riesgos ni las alarmas locales del ESP32. `manualConfirmation` exige crear la respuesta en `Open` y pasarla a `InProgress` antes de resolverla. Las notificaciones se persisten, pero no hay entrega externa de correo, SMS o push.

Ejemplo de `PUT /api/settings`:

```json
{
  "systemName": "SafeCore Edificio Central",
  "timeZone": "America/Lima",
  "criticalAlertsEnabled": true,
  "notifyResponders": true,
  "manualConfirmation": false
}
```

### Inmuebles, zonas y asignaciones

Admin ve todos los inmuebles y nodos. Operator ve las zonas a las que se le asignó; Responder ve los nodos del inmueble asignado; Resident solo ve sus zonas asignadas. El administrador crea, modifica y elimina inmuebles/zonas. El operador puede registrar dispositivos y asignarlos a zonas de sus inmuebles.

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/properties?page=1&pageSize=100`, `GET /api/properties/{id}` | JWT; restringido por asignación | Lista o detalle visible. |
| `POST /api/properties`, `PUT /api/properties/{id}`, `DELETE /api/properties/{id}` | Administrator | CRUD; `409` si el inmueble tiene zonas o usuarios asignados. |
| `GET /api/properties/{id}/zones?page=1&pageSize=100` | JWT; restringido por asignación | Zonas visibles. |
| `POST /api/properties/{id}/zones` | Administrator | Crea una zona. |
| `GET /api/zones/{id}` | JWT; restringido por asignación | Detalle de zona. |
| `PUT /api/zones/{id}`, `DELETE /api/zones/{id}` | Administrator | Modifica/elimina; `409` si hay nodos o usuarios asignados. |

Property recibe `{ "name": "Edificio Central", "address": "Av. Ejemplo 100" }`; Zone recibe `{ "name": "Piso 3 - Cocina" }`. `PUT /api/users/{id}/assignments` asigna `{ "assignments": [{ "propertyId": "...", "zoneId": "..." }] }`; se reemplaza la lista completa. `zoneId: null` asigna el inmueble completo; los Resident deben tener zona.

### Analítica — `/api/analytics` — Administrator y Operator

Las rutas `summary` y `trends` aceptan `from`/`to` ISO 8601 opcionales; por defecto usan los últimos 30 días y el máximo es 366 días. Las consultas se restringen a nodos asignados.

- `GET /api/analytics/summary`: total de alertas, conteos por tipo y severidad, porcentaje resuelto, promedio entre detección y resolución en minutos, disponibilidad calculada a partir del historial de conectividad, rango UTC y zona horaria configurada.
- `GET /api/analytics/trends?interval=day|week|month`: lecturas de alertas registradas/atendidas por periodo, agrupadas en la zona horaria configurada.
- `GET /api/analytics/reports`: catálogo JSON de resumen de alertas, estado de dispositivos y actividad operativa. PDF/CSV queda pendiente porque el requisito no define el formato.

El backend registra cambios de conectividad y `lastSeenAt`; nodos sin comunicación durante cinco minutos pasan a `Offline`. `resolvedAt` se completa al resolver un riesgo, tanto manual como automáticamente.

### Configuración de monitoreo — ruta `/api/SystemConfigurations`

| Método y ruta | Acceso | Resultado |
|---|---|---|
| `GET /api/SystemConfigurations?page=1&pageSize=100` | Administrator, Operator | `200`: arreglo. |
| `GET /api/SystemConfigurations/{id}` | Administrator, Operator | `200` o `404`. |
| `POST /api/SystemConfigurations` | Administrator | `201`. |
| `PUT /api/SystemConfigurations/{id}` | Administrator | `204` o `404`. |
| `DELETE /api/SystemConfigurations/{id}` | Administrator | `204` o `404`. |

POST/PUT recibe `version` entero ≥1, `syncStatus` (`Pending`, `Synced`, `Failed`) y `lastSyncedAt` (ISO 8601 o `null`).

### Health checks — públicos

| Método y ruta | Significado |
|---|---|
| `GET /health/live` | `200` con `{ "status": "live" }` si el proceso HTTP responde. |
| `GET /health/ready` | `200` con `{ "status": "ready" }` si MySQL acepta conexión; `503` si no está disponible. |

### Secuencia sugerida para integrar el frontend

1. Iniciar sesión con `POST /api/iam/auth/login`; guardar `token.accessToken` en almacenamiento seguro y conservar `user.roles` para mostrar/ocultar funciones según rol.
2. En cada llamada protegida, enviar `Authorization: Bearer <accessToken>`. Ante `401`, solicitar inicio de sesión de nuevo; ante `403`, mostrar acceso denegado sin reintentos automáticos.
3. Cargar datos paginados solicitando `page` y `pageSize`; la respuesta es un arreglo. Para avanzar, solicitar la página siguiente; si llega menos elementos que `pageSize`, probablemente es la última página.
4. Para formularios, enviar JSON con los valores permitidos exactamente como se indican. Mostrar los errores `400` junto al campo correspondiente cuando la respuesta incluya `errors`.
5. Para sensores, el cliente de aplicación normalmente consulta lecturas y riesgos; el dispositivo IoT envía lecturas usando su propio JWT. No guardar `deviceSecret` en el frontend web.

## Telemetría

`POST /api/SensorReadings` acepta `SensorType` con uno de estos valores: `Gas`, `Temperature`, `Accelerometer` o `Gyroscope`. Gas y temperatura requieren `MeasuredValue`; acelerómetro y giroscopio requieren los tres ejes `AxisX`, `AxisY` y `AxisZ`. `CapturedAt` debe incluir zona horaria y no puede estar más de cinco minutos en el futuro.

La evaluación automática replica los parámetros del firmware compartido: DS18B20 a **40 °C o más** crea un riesgo `Fire`; `MeasuredValue` del MQ-2 representa cuentas ADC del ESP32 entre 0 y 4095, y `DigitalAlarm: true` significa que `MQ_DO` está en LOW. El backend crea `GasOrSmoke` cuando la entrada digital está activa o el ADC sube más de **100 cuentas** respecto de la lectura anterior del mismo sensor. El error DS18B20 `-127 °C` se registra como `SensorFault`.

El criterio de impacto usa la magnitud `sqrt(x² + y² + z²)` de las cuentas crudas del acelerómetro MPU6050 y detecta un evento cuando el cambio absoluto supera **200 cuentas**. `AlertUntil` conserva la ventana de **1500 ms** del firmware; nuevos impactos durante esa ventana la extienden. Los riesgos se deduplican mientras siguen activos y se resuelven al recibir una lectura que indique que la condición terminó. Los valores pueden configurarse en `SafetyThresholds`.

Ejemplo de movimiento:

```json
{
  "deviceId": "GUID-DEL-DISPOSITIVO",
  "sensorType": "Accelerometer",
  "axisX": 160,
  "axisY": -80,
  "axisZ": 16300,
  "unit": "mpu6050-counts",
  "capturedAt": "2026-11-05T20:45:00Z"
}
```

Ejemplo de lectura MQ-2 con la salida digital activa en LOW:

```json
{
  "deviceId": "GUID-DEL-DISPOSITIVO",
  "sensorType": "Gas",
  "measuredValue": 1840,
  "digitalAlarm": true,
  "unit": "esp32-adc-counts",
  "capturedAt": "2026-11-05T20:45:00Z"
}
```

Para temperatura, usa `sensorType: "Temperature"`, `unit: "C"` y `measuredValue` en grados Celsius. El ejemplo de firmware solo lee acelerómetro, no giroscopio; aunque el endpoint admite `Gyroscope`, el código actual de detección de sismo usa lecturas `Accelerometer`.

Las listas aceptan `page` y `pageSize` (máximo 250) para acotar las consultas. Las lecturas se guardan en UTC y cuentan con índice por dispositivo y fecha.

## Configuración local

Se requiere SDK .NET 10. Las credenciales se leen desde User Secrets o variables de entorno; no se guardan en `appsettings*.json`.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=safecore;User ID=safecore;Password=CAMBIAR;SslMode=Required;" --project SafeCore/SafeCore.csproj
dotnet user-secrets set "Jwt:SigningKey" "REEMPLAZAR_POR_UN_SECRETO_ALEATORIO_DE_AL_MENOS_32_BYTES" --project SafeCore/SafeCore.csproj
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project SafeCore/SafeCore.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "Use-una-contraseña-larga-y-única-2026!" --project SafeCore/SafeCore.csproj
dotnet tool restore
dotnet ef database update --project SafeCore/SafeCore.csproj --startup-project SafeCore/SafeCore.csproj
dotnet run --project SafeCore/SafeCore.csproj
```

Swagger solo se habilita en `Development`. Se crea el administrador al arrancar si aún no existe; quitar la contraseña bootstrap de los secretos una vez desplegado el primer administrador.

## Despliegue con Docker Compose

La imagen usa .NET 10, se ejecuta como usuario no root y escucha en el puerto 8080. La base de datos MySQL se administra por separado. Para el primer despliegue, configura secretos mediante el entorno seguro del host (o un `.env` local no versionado; `.env` está excluido del contexto Docker):

```dotenv
SAFECORE_CONNECTION_STRING=Server=HOST;Port=3306;Database=BASE;User ID=USUARIO;Password=SECRETO;SslMode=Required;
SAFECORE_JWT_SIGNING_KEY=SECRETO_ALEATORIO_DE_AL_MENOS_32_BYTES
SAFECORE_BOOTSTRAP_ADMIN_EMAIL=admin@example.com
SAFECORE_BOOTSTRAP_ADMIN_PASSWORD=CONTRASEÑA_LARGA_Y_UNICA
SAFECORE_TRUSTED_PROXY_IP=IP_DEL_PROXY_QUE_CONECTA_AL_CONTENEDOR
```

Después ejecuta:

```powershell
docker compose build safecore
docker compose --profile maintenance run --rm migrate
docker compose up -d safecore
```

El perfil `maintenance` ejecuta `dotnet SafeCore.dll --migrate` como tarea separada antes de levantar la API. Las migraciones no se ejecutan automáticamente al iniciar cada réplica. Compose enlaza el puerto al loopback (`127.0.0.1:8080`) por defecto; publica la API mediante un proxy o balanceador con TLS y configura sus límites, certificados y cabeceras de proxy. Configura `SAFECORE_TRUSTED_PROXY_IP` con la dirección interna real del proxy; la aplicación solo acepta `X-Forwarded-*` de proxies de confianza. Para una plataforma que enruta al contenedor directamente, configura `API_BIND_ADDRESS` según el aislamiento de red de esa plataforma.

Las migraciones iniciales están pensadas para una **base vacía**, como indicaste para este despliegue. Antes de publicar, ejecuta la migración y confirma que el endpoint readiness responda. Antes de cambios futuros de esquema, toma un respaldo verificable y ensaya la migración en una copia.

## Despliegue en Render

El archivo `render.yaml` prepara un Blueprint de Render que construye `SafeCore/Dockerfile`, aplica las migraciones en el paso pre-deploy y espera a que `/health/ready` confirme la conexión a MySQL antes de enrutar tráfico. El servicio escucha internamente en el puerto 8080, igual que la imagen Docker. El auto-deploy queda desactivado hasta que se configuren y validen los secretos.

Al crear el Blueprint, Render solicitará `ConnectionStrings__DefaultConnection`, `BootstrapAdmin__Email` y `BootstrapAdmin__Password`; generará `Jwt__SigningKey`. Configura el connection string de Aiven completo con `SslMode=Required`. Esos valores se administran en Render y no se guardan en este repositorio. La clave JWT generada debe permanecer estable entre despliegues.

La configuración usa el plan Starter para evitar que la API de sensores se suspenda por inactividad y para habilitar el paso pre-deploy de migraciones. Si se elige otro plan, confirma que permita ese paso. Render termina TLS en su proxy y reenvía `X-Forwarded-*`; la imagen solo debe exponerse a través de ese proxy. El health check de readiness requiere que Aiven esté accesible.

En Aiven, comprueba que el servicio permita conexiones desde los rangos de salida de la región de Render elegida. Obtén los rangos en el panel de Render del servicio (Connect → Outbound) y añádelos al IP filter de Aiven. Confirma la región de Aiven antes de crear el servicio Render; la región de Render no se puede cambiar después de creado.

Después de configurar las variables, ejecuta el primer deploy manualmente desde Render. Comprueba que el pre-deploy de migraciones finalice, que el health check pase y que `GET /health/ready` devuelva `200`. Swagger permanece habilitado solo en `Development`; en producción valida la API con un cliente HTTP autenticado.

Este cambio agrega migraciones de esquema. Como el servicio Render actual no tiene Pre-Deploy Command configurado, antes de desplegar el nuevo commit respalda Aiven y ejecuta desde la raíz del repositorio en PowerShell `dotnet ef database update --project .\SafeCore\SafeCore.csproj --startup-project .\SafeCore\SafeCore.csproj`. `SafeCoreDbContextFactory` usa `ConnectionStrings:DefaultConnection` desde User Secrets o variables de entorno; ya no apunta a un servidor local fijo. Luego actualiza Render. Si habilitas Pre-Deploy más adelante, el comando del contenedor es `dotnet SafeCore.dll --migrate`.

## Estado de seguridad funcional

El backend evalúa las condiciones y persiste incidentes con los umbrales compartidos desde el firmware. El buzzer, LEDs y relé permanecen controlados localmente por el ESP32: el backend no envía comandos a actuadores ni sustituye las protecciones locales. Tampoco incluye envío real de SMS/correo/push. El MQ-2 entrega cuentas ADC y no identifica por sí solo qué gas está presente; sus límites requieren calibración del sensor instalado. Valida los umbrales y el comportamiento del relé con hardware real antes de confiar en el sistema para proteger vidas.
