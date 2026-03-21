namespace VokabelTrainer.Api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Shared.Dtos.Progress;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProgressController(ProgressService progressService) : ControllerBase
{
    [HttpGet("list/{listId}")]
    public async Task<ActionResult<ListProgressDto>> GetListProgress(int listId)
    {
        var result = await progressService.GetListProgressAsync(GetUserId(), listId);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException());
}
