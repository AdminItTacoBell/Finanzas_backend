namespace Finanzas.Application.Common;

public sealed class ExternalServiceUnavailableException : Exception
{
    public ExternalServiceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

