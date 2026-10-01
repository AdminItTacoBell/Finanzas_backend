namespace Finanzas.Domain.Common;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected init; } = default!;
}

