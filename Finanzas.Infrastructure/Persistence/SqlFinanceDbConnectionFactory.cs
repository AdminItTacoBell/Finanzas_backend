using System.Data.Common;
using Finanzas.Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Finanzas.Infrastructure.Persistence;

public sealed class SqlFinanceDbConnectionFactory : IFinanceDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqlFinanceDbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DbConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("FinanceDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configura ConnectionStrings__FinanceDb para utilizar el RDS exclusivo de Finanzas.");
        }

        return new SqlConnection(connectionString);
    }
}

