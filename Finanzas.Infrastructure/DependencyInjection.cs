using Finanzas.Application.Abstractions.Data;
using Finanzas.Infrastructure.Integrations.Erp;
using Finanzas.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Finanzas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinanceInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFinanceDbConnectionFactory, SqlFinanceDbConnectionFactory>();
        services.AddScoped<ErpInternalApiClient>();
        return services;
    }
}

