using System.Text;
using Finanzas.Api.Health;
using Finanzas.Api.Middlewares;
using Finanzas.Api.Security;
using Finanzas.Infrastructure;
using Finanzas.Infrastructure.Integrations.Erp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddFinanceInfrastructure();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => value.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Configura Cors__AllowedOrigins__0 fuera de Development.");
}

builder.Services.AddCors(options => options.AddPolicy("FinanceFrontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Issuer)
    || string.IsNullOrWhiteSpace(jwt.Audience)
    || string.IsNullOrWhiteSpace(jwt.Key))
{
    throw new InvalidOperationException("Configura Jwt__Issuer, Jwt__Audience y Jwt__Key.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.Zero
    });

builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build());
builder.Services.AddScoped<IAuthorizationHandler, ErpPermissionAuthorizationHandler>();

var erpBaseUrl = builder.Configuration["Erp:BaseUrl"]?.Trim().TrimEnd('/');
var validErpUrl = Uri.TryCreate(
    string.IsNullOrWhiteSpace(erpBaseUrl) ? null : $"{erpBaseUrl}/",
    UriKind.Absolute,
    out var erpBaseUri);
var internalApiKey = builder.Configuration["Erp:InternalApiKey"];
var internalApiKeyHeader = builder.Configuration["Erp:InternalApiKeyHeader"] ?? "X-Internal-Api-Key";

builder.Services.AddHttpClient(ErpHttpClientNames.Permissions, client =>
{
    if (validErpUrl) client.BaseAddress = erpBaseUri;
    client.Timeout = Timeout.InfiniteTimeSpan;
}).AddStandardResilienceHandler(ConfigureErpResilience);
builder.Services.AddHttpClient(ErpHttpClientNames.InternalCatalogs, client =>
{
    if (validErpUrl) client.BaseAddress = erpBaseUri;
    client.Timeout = Timeout.InfiniteTimeSpan;
    if (!string.IsNullOrWhiteSpace(internalApiKey))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation(internalApiKeyHeader, internalApiKey);
    }
}).AddStandardResilienceHandler(ConfigureErpResilience);

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<FinanceDatabaseHealthCheck>("finance-db", tags: ["ready"]);

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});

var app = builder.Build();

if (!validErpUrl)
{
    app.Logger.LogWarning("Erp__BaseUrl no esta configurada; permisos y catalogos externos fallaran de forma cerrada.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseHttpsRedirection());
app.UseCors("FinanceFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
app.MapControllers();

static void ConfigureErpResilience(HttpStandardResilienceOptions options)
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
    options.Retry.MaxRetryAttempts = 2;
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
}

app.Run();

public partial class Program;
