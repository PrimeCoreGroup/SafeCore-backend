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
| `GET /api/iam/auth/me` | Usuario autenticado |
| `/api/users` | Administrador |
| Lectura de inventario y telemetría | Administrador, operador o respondedor |
| `POST /api/SensorReadings` | JWT de dispositivo, administrador u operador |
| Cambios de configuración, riesgos y respuestas | Según roles configurados en el controlador |
| `GET /health/live`, `GET /health/ready` | Público; readiness comprueba conexión a MySQL |

El JWT de dispositivo incluye su `device_id`; la ingesta rechaza lecturas que intenten usar el ID de otro sensor. No se devuelve el hash de credenciales ni `PasswordHash` en las APIs.

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

## Estado de seguridad funcional

El backend evalúa las condiciones y persiste incidentes con los umbrales compartidos desde el firmware. El buzzer, LEDs y relé permanecen controlados localmente por el ESP32: el backend no envía comandos a actuadores ni sustituye las protecciones locales. Tampoco incluye envío real de SMS/correo/push. El MQ-2 entrega cuentas ADC y no identifica por sí solo qué gas está presente; sus límites requieren calibración del sensor instalado. Valida los umbrales y el comportamiento del relé con hardware real antes de confiar en el sistema para proteger vidas.
