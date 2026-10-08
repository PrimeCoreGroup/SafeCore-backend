# SafeCore API

API construida en .NET 9.0 para la gestión del sistema autónomo de seguridad estructural SafeCore, estructurada bajo principios de Domain-Driven Design (DDD) y separada en Bounded Contexts.

## Estructura del Proyecto

El proyecto está dividido en los siguientes módulos lógicos:

*   **SafeCore.Api**: Host principal, configuración global, inyección de dependencias y configuración de Swagger.
*   **SafeCore.Shared**: Kernel de dominio (clases base `Entity`), contratos de respuestas y `SafeCoreDbContext`.
*   **SafeCore.IdentityAccess**: Gestión de usuarios y acceso a la plataforma.
*   **SafeCore.DeviceManagement**: Registro y estado de dispositivos IoT (sensores y actuadores).
*   **SafeCore.SensorDataIngestion**: Recepción y persistencia de métricas enviadas por los dispositivos Edge.
*   **SafeCore.RiskDetection**: Evaluación de situaciones de riesgo detectadas.
*   **SafeCore.Emergency**: Gestión de protocolos, respuestas a emergencias generadas y acciones a tomar.
*   **SafeCore.Notifications**: Gestión de canales de alerta y notificación de eventos críticos.
*   **SafeCore.MonitoringConfiguration**: Parámetros de monitoreo y sincronización de estado de la red.

## Ejecución y Pruebas

El proyecto está configurado para desplegar la documentación interactiva de la API de forma automática al iniciar en modo Desarrollo.

1. Configurar los datos de acceso a la base de datos MySQL en `appsettings.json` o `appsettings.Development.json`.
2. Compilar y ejecutar el proyecto desde Rider. La base de datos y todas las tablas se generarán automáticamente gracias a `dbContext.Database.EnsureCreated()`.
3. El explorador web abrirá automáticamente la dirección `http://localhost:5000` mostrando la interfaz de Swagger UI.

## Endpoints Disponibles para Frontend

Todas las rutas base cuentan con operaciones CRUD (GET, POST, PUT, DELETE). Las peticiones POST y PUT requieren el envío de un payload en formato JSON.

### 1. Usuarios (`/api/Users`)
Permite registrar y gestionar a los usuarios, administradores y residentes del sistema.
*   `Email` (string)
*   `FirstName` (string)
*   `LastName` (string)
*   `PasswordHash` (string)
*   `Status` (string)

### 2. Dispositivos (`/api/Devices`)
Administración del ciclo de vida de los nodos Edge, sensores y actuadores.
*   `SerialNumber` (string)
*   `Name` (string)
*   `Category` (string)
*   `ConnectivityStatus` (string)

### 3. Ingesta de Sensores (`/api/SensorReadings`)
Recepción de las métricas enviadas por los sensores de movimiento, humo y temperatura.
*   `DeviceId` (Guid)
*   `SensorType` (string)
*   `MeasuredValue` (decimal)
*   `Unit` (string)
*   `CapturedAt` (datetime)

### 4. Detección de Riesgos (`/api/RiskSituations`)
Alertas o posibles amenazas evaluadas por el sistema.
*   `RiskType` (string)
*   `Status` (string)
*   `Severity` (string)
*   `DetectedAt` (datetime)

### 5. Respuestas a Emergencias (`/api/EmergencyResponses`)
Protocolos activos desplegados como respuesta a un evento de riesgo confirmado.
*   `ProtocolId` (Guid)
*   `EmergencyType` (string)
*   `RiskSituationId` (Guid)
*   `Status` (string)

### 6. Notificaciones (`/api/Notifications`)
Registro de alertas despachadas hacia residentes o servicios de emergencia.
*   `RecipientUserId` (Guid)
*   `Title` (string)
*   `Body` (string)
*   `Channel` (string)
*   `Status` (string)

### 7. Configuración del Sistema (`/api/SystemConfigurations`)
Monitoreo y versionado de las configuraciones distribuidas a los nodos Edge.
*   `Version` (int)
*   `SyncStatus` (string)
*   `LastSyncedAt` (datetime)

### Validaciones a considerar en el Frontend
*   Al crear una entidad mediante `POST`, el backend generará automáticamente el `Id` (Guid) y la fecha de creación (`CreatedAt`), retornando el objeto completo con estado HTTP 201 Created.
*   Las actualizaciones mediante `PUT` requieren el `Id` en la URL de la petición (`/api/[controller]/{id}`) y deben devolver HTTP 204 No Content.
*   En el caso de entidades relacionadas, como `SensorReadings`, el frontend debe asegurar enviar un `DeviceId` válido que exista previamente en la tabla `Devices`.