using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Finanzas.Infrastructure.Integrations.Erp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace Finanzas.Api.Security;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class ErpPermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ErpPermissionAuthorizationHandler> _logger;

    public ErpPermissionAuthorizationHandler(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        ILogger<ErpPermissionAuthorizationHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.FindAll("permission").Any(claim =>
            string.Equals(claim.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(userId)
            || !AuthenticationHeaderValue.TryParse(authorization, out var bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(bearer.Parameter))
        {
            return;
        }

        var cacheKey = $"erp-permissions:{userId}";
        if (!_cache.TryGetValue(cacheKey, out HashSet<string>? permissions))
        {
            permissions = await LoadPermissionsAsync(bearer.Parameter);
            if (permissions is null)
            {
                return;
            }

            _cache.Set(cacheKey, permissions, CacheDuration);
        }

        if (permissions is not null && permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<HashSet<string>?> LoadPermissionsAsync(string token)
    {
        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClientFactory
                .CreateClient(ErpHttpClientNames.Permissions)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ERP rechazo la consulta de permisos con HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<SessionEnvelope>(
                cancellationToken: cancellationToken);
            return envelope?.Data?.Permisos?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No fue posible validar permisos contra el ERP.");
            return null;
        }
    }

    private sealed record SessionEnvelope(SessionData? Data);
    private sealed record SessionData(IReadOnlyCollection<string>? Permisos);
}
