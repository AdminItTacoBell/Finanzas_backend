using System.Data.Common;

namespace Finanzas.Application.Abstractions.Data;

public interface IFinanceDbConnectionFactory
{
    DbConnection CreateConnection();
}

