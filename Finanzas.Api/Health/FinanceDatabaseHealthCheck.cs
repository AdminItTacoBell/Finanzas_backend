using Finanzas.Application.Abstractions.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Finanzas.Api.Health;

public sealed class FinanceDatabaseHealthCheck : IHealthCheck
{
    private readonly IFinanceDbConnectionFactory _connectionFactory;

    public FinanceDatabaseHealthCheck(IFinanceDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("FinanceDb disponible.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("FinanceDb no disponible.", exception);
        }
    }
}

