using System.Security.Claims;

namespace VokabelTrainer.Api.Endpoints;

public static class HttpContextExtensions
{
    public static int GetUserId(this HttpContext ctx) =>
        int.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
