using System.Net;
using System.Net.Http.Json;
using Finanzas.Application.Common;
using Microsoft.Extensions.Logging;

namespace Finanzas.Infrastructure.Integrations.Erp;

/// <summary>
/// Transporte base para contratos internos de Administracion. Los adaptadores
/// financieros concretos deben envolverlo; nunca se conecta al RDS del ERP.
/// </summary>
public sealed class ErpInternalApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ErpInternalApiClient> _logger;

    public ErpInternalApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<ErpInternalApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _httpClientFactory
                .CreateClient(ErpHttpClientNames.InternalCatalogs)
                .GetAsync(relativePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ERP respondio HTTP {StatusCode} para {Path}.",
                    (int)response.StatusCode,
                    relativePath);
                throw new ExternalServiceUnavailableException(
                    "Administracion no esta disponible temporalmente.");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ErpEnvelope<T>>(
                cancellationToken: cancellationToken);
            return envelope?.Data;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExternalServiceUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No se pudo consultar {Path} en el ERP.", relativePath);
            throw new ExternalServiceUnavailableException(
                "Administracion no esta disponible temporalmente.", exception);
        }
    }

    private sealed record ErpEnvelope<TData>(bool Success, string Message, TData? Data);
}

