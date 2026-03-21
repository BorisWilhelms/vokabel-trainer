namespace VokabelTrainer.Api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Shared.Dtos.Lists;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VocabularyListsController(VocabularyListService listService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VocabularyListSummaryDto>>> GetAll()
    {
        return Ok(await listService.GetAllForUserAsync(GetUserId()));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VocabularyListDto>> GetById(int id)
    {
        var result = await listService.GetByIdAsync(id, GetUserId());
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateVocabularyListRequest request)
    {
        var id = await listService.CreateAsync(GetUserId(), request);
        return Ok(id);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateVocabularyListRequest request)
    {
        var success = await listService.UpdateAsync(id, GetUserId(), request);
        if (!success)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await listService.DeleteAsync(id, GetUserId());
        if (!success)
            return NotFound();
        return Ok();
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException());
}
