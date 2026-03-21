using Microsoft.AspNetCore.Http.HttpResults;
using VokabelTrainer.Api.Components.Pages;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class DashboardEndpoints
{
    public static WebApplication MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/", async (VocabularyListService listService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            var lists = await listService.GetAllForUserAsync(userId);

            return new RazorComponentResult<Dashboard>(new
            {
                Lists = lists,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        return app;
    }
}
