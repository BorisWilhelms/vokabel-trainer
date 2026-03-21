using Microsoft.AspNetCore.Http.HttpResults;
using VokabelTrainer.Api.Components.Pages;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class ProgressEndpoints
{
    public static WebApplication MapProgressEndpoints(this WebApplication app)
    {
        app.MapGet("/progress/{listId:int}", async (int listId, ProgressService progressService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            var progress = await progressService.GetListProgressAsync(userId, listId);

            return new RazorComponentResult<Progress>(new
            {
                ProgressData = progress,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        return app;
    }
}
