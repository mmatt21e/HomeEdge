using System.Security.Claims;
using HomeStock.Application.Abstractions;

namespace HomeStock.Web.Infrastructure;

/// <summary>Resolves the acting user from the current HTTP context for history/audit stamping.</summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => User?.Identity?.Name;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
