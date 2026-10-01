# Infraestructura AWS de Finanzas

Esta fase crea el contenedor independiente sin mover endpoints ni datos. No debe existir una regla publica de ALB hacia Finanzas hasta completar y probar el primer corte vertical.

## Topologia por ambiente

Crear recursos separados en las VPC de Desarrollo y Produccion:

- ECR: `bellconnect-finanzas`.
- ECS cluster: `bellconnect-finanzas-dev` / `bellconnect-finanzas-prod`.
- ECS service y task definition propios.
- Target group propio, puerto 8080, health check `/health/ready`.
- Container health check `/health/live`.
- CloudWatch Log Group `/ecs/bellconnect-finanzas-{env}`.
- Task role y execution role propios.
- Security group del task exclusivo.
- RDS SQL Server de Finanzas y usuario SQL exclusivo.

El cluster es una frontera operativa, no una frontera de red. La red se define en el ECS service mediante VPC, subredes y security groups.

## Reglas de red minimas

| Origen | Destino | Puerto | Uso |
|---|---|---:|---|
| ALB SG | Finanzas task SG | 8080 | Trafico HTTP interno |
| Finanzas task SG | Finance RDS SG | 1433 | Datos financieros |
| Finanzas task SG | ERP API/ALB | 443 | Catalogos y permisos por API |

No autorizar al task SG de Finanzas contra el RDS del ERP. No crear FK, linked servers ni consultas cruzadas entre bases.

## Orden seguro de creacion

1. Crear ECR, log group, roles y security groups.
2. Crear el cluster ECS de cada ambiente.
3. Registrar la task definition con `desiredCount = 0` mientras no exista/configure `FinanceDb`.
4. En Desarrollo, conectar exclusivamente el RDS financiero ya existente.
5. Crear el RDS de Produccion con Multi-AZ, cifrado, backups y proteccion contra eliminacion antes de subir el servicio.
6. Crear target group y servicio; usar deployment circuit breaker con rollback.
7. Validar `/health/live`, `/health/ready`, logs y autenticacion.
8. Mantener sin trafico publico hasta migrar un endpoint completo.

## Configuracion del contenedor

Variables no sensibles:

- `ASPNETCORE_ENVIRONMENT`
- `Cors__AllowedOrigins__0`
- `Erp__BaseUrl`
- `Erp__InternalApiKeyHeader`
- `Jwt__Issuer`
- `Jwt__Audience`

Valores sensibles inyectados por ECS desde SSM o Secrets Manager:

- `ConnectionStrings__FinanceDb`
- `Jwt__Key`
- `Erp__InternalApiKey`

La task role debe usarse para AWS (S3, SQS u otros) cuando se migren esas funciones; no agregar access key y secret key al archivo de configuracion.

## Disponibilidad e independencia

- `/health/live` comprueba solamente que el proceso esta vivo.
- `/health/ready` comprueba solamente el RDS financiero.
- ERP, RRHH, Marketing, SMTP, S3 y GP no participan en health checks.
- Si Administración no responde, solo fallan con 503 las operaciones que necesiten sus datos; Finanzas y el ERP principal siguen arrancados.
- Si Finanzas cae, el ALB deja de enviarle trafico. El ERP principal no comparte proceso, task, target group ni conexion de base y no debe caer.

En Produccion usar al menos dos tasks distribuidos entre dos zonas de disponibilidad. En Desarrollo una task es suficiente.

## Enrutamiento incremental

No crear todavía una regla general `/api/finanzas/*`, porque desviaria endpoints que continuan en el ERP. Para cada corte se agrega una regla exacta o un conjunto acotado, por ejemplo:

```text
/api/finanzas/liquidaciones/viaticos-bodega/* -> Finanzas target group
resto de /api/finanzas/*                     -> ERP target group
```

La reversión inmediata del código consiste en devolver esa regla al target group del ERP. La reversión de datos exige el procedimiento de corte y reconciliación descrito en `MIGRACION_FINANZAS_A_MICROSERVICIO.md`.

