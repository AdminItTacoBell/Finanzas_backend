# Finanzas Backend

Base del microservicio financiero independiente de BellConnect. En esta etapa no contiene controladores de negocio ni migraciones de base; por lo tanto no reemplaza todavía ningún endpoint del ERP.

## Estructura

- `Finanzas.Api`: HTTP, JWT, autorización, middleware y health checks.
- `Finanzas.Application`: contratos y casos de uso.
- `Finanzas.Domain`: modelo financiero puro.
- `Finanzas.Infrastructure`: SQL Server/Dapper e integraciones por API.
- `Finanzas.Tests`: pruebas automatizadas.
- `infrastructure`: plantilla de task definition ECS.
- `docs`: guía para preparar AWS sin afectar el ERP.

## Principios ya aplicados

- Un único connection string: `FinanceDb`.
- No existe conexión al RDS del ERP.
- Administración se consume por HTTP.
- Sus llamadas HTTP tienen timeout, reintentos limitados y circuit breaker.
- JWT compatible con el ERP actual.
- Permisos validados contra `api/auth/me` con cache corto y fallo cerrado.
- Liveness independiente; readiness depende solo de FinanceDb.
- Configuración sensible fuera del repositorio.

## Configuración local

Desde esta carpeta:

```powershell
dotnet user-secrets set "ConnectionStrings:FinanceDb" "CONEXION_RDS_FINANZAS_DEV" --project .\Finanzas.Api\Finanzas.Api.csproj
dotnet user-secrets set "Jwt:Key" "MISMA_LLAVE_JWT_DEL_ERP_DEV" --project .\Finanzas.Api\Finanzas.Api.csproj
dotnet user-secrets set "Erp:InternalApiKey" "MISMA_API_KEY_INTERNA_DEL_ERP_DEV" --project .\Finanzas.Api\Finanzas.Api.csproj
dotnet run --project .\Finanzas.Api\Finanzas.Api.csproj --launch-profile https
```

Verificaciones:

```text
GET https://localhost:7103/health/live
GET https://localhost:7103/health/ready
GET https://localhost:7103/api/finanzas/service-status  (requiere JWT)
```

## Imagen Docker en ECR

El workflow `.github/workflows/finanzas-backend-ecr.yml` valida, construye y publica la imagen:

- `developer` publica en el repositorio indicado por `ECR_REPOSITORY_DEV`.
- `main` publica en el repositorio indicado por `ECR_REPOSITORY_PROD`.
- Tambien puede ejecutarse manualmente seleccionando `development` o `production`.

Variables requeridas en GitHub Actions:

- `AWS_REGION`
- `ECR_REPOSITORY_DEV=finanzas-backend-dev` para Desarrollo.
- `ECR_REPOSITORY_PROD=finanzas-backend-prod` para Produccion.

Variables opcionales:

- `ECS_CLUSTER_DEV` y `ECS_SERVICE_DEV`.
- `ECS_CLUSTER_PROD` y `ECS_SERVICE_PROD`.

Si las variables ECS del ambiente todavia no existen, el workflow publica la imagen en ECR y omite el redespliegue. No se guardan credenciales de AWS, RDS ni JWT en el repositorio.

## Siguiente corte

Antes de copiar código financiero se debe inventariar el flujo completo elegido. La recomendación sigue siendo comenzar por Liquidaciones como un corte vertical: tablas, procedimientos, API, jobs, archivos, correo, tiempo real, pruebas y reconciliación. No copiar el esquema `finanzas` completo sin clasificar sus dependencias administrativas.
# Finanzas_backend
