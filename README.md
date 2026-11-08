# SafeCore Backend

API ASP.NET Core para administrar sensores IoT, telemetría de gas, temperatura y movimiento, riesgos y coordinación de emergencias.

## Documentación para integración frontend

La referencia detallada de endpoints está en [SafeCore/README.md — Guía de integración para frontend](SafeCore/README.md#guía-de-integración-para-frontend). Incluye rutas, roles, parámetros, cuerpos JSON de ejemplo, respuestas y códigos de error.

La API desplegada en Render usa producción y no expone Swagger. Para probarla, usa Postman u otro cliente HTTP. Los health checks públicos son `/health/live` y `/health/ready`.

## Contextos del backend

- IAM y gestión de usuarios
- Inventario de dispositivos IoT
- Ingesta de lecturas de sensores
- Detección de riesgos
- Protocolos, respuestas y acciones de emergencia
- Notificaciones
- Configuración de monitoreo

Consulta [SafeCore/README.md](SafeCore/README.md) para arquitectura, configuración local, telemetría, despliegue en Render y notas de seguridad funcional.
