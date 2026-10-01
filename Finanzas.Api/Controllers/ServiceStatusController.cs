using Finanzas.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finanzas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/finanzas/service-status")]
public sealed class ServiceStatusController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<object>> Get() => Ok(ApiResponse<object>.Ok(new
    {
        service = "Finanzas.Api",
        status = "ready-for-migration",
        timestampUtc = DateTimeOffset.UtcNow
    }));
}

